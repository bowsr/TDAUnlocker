using LiveSplit;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Timer = System.Windows.Threading.DispatcherTimer;

namespace TDAUnlocker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static readonly Brush BG_GRAY = new SolidColorBrush(Color.FromRgb(60, 60, 60));

        DeepPointer ConsoleLockDP, MetricsDP;

        int FailedHookProcID = 0;
        bool IsHooked = false;
        bool IsConsoleUnlocked = false;
        Process TDAGameProcess = new();

        Timer ProcessTimer;

        public MainWindow()
        {
            InitializeComponent();
            UnlockButton.IsEnabled = false;

            ProcessTimer = new();
            ProcessTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            ProcessTimer.Tick += ProcessTimer_Tick;

            ProcessTimer.Start();
        }

        private void ProcessTimer_Tick(object? sender, EventArgs e) {
            if(IsHooked) {
                if(TDAGameProcess.HasExited) {
                    IsHooked = false;
                    IsConsoleUnlocked = false;
                    TDAGameProcess = new();
                    FailedHookProcID = 0;
                    UnlockButton.IsEnabled = false;
                    return;
                }
                if(!IsConsoleUnlocked && (bool)AutoUnlockCheckbox.IsChecked) {
                    UnlockButton.IsEnabled = false;
                    UnlockConsole();
                    return;
                }
                UnlockButton.IsEnabled = !IsConsoleUnlocked;
            } else {
                HookGameProcess();
                UnlockButton.Content = (IsHooked) ? "Unlock Console Commands" : "Game Not Detected";
                UnlockButton.IsEnabled = IsHooked && !(bool)AutoUnlockCheckbox.IsChecked;
            }
        }

        private void HookGameProcess() {
            List<Process> procList = Process.GetProcesses().ToList().FindAll(x => x.ProcessName.Contains("DOOMTheDarkAges"));

            if(procList.Count == 0) return;
            if(procList.Count > 1) {
                SetErrorMessage("Found too many game instances");
                return;
            }
            if(procList[0].HasExited) return;

            if(procList[0].Id == FailedHookProcID) {
                SetErrorMessage("Failed to hook into the game.");
                return;
            }
            TDAGameProcess = procList[0];
            FailedHookProcID = 0;
            IsHooked = true;
        }

        private void UnlockConsole() {
            HideErrorMessage();
            UnlockButton.Content = "Unlocking Console...";

            SigScan();

            if(FailedHookProcID != 0) return;

            IntPtr consolePtr = IntPtr.Zero;
            IntPtr metricsPtr = IntPtr.Zero;
            ConsoleLockDP?.DerefOffsets(TDAGameProcess, out consolePtr);
            MetricsDP?.DerefOffsets(TDAGameProcess, out metricsPtr);

            if(consolePtr != IntPtr.Zero) {
                TDAGameProcess.WriteBytes(consolePtr, new byte[1] { 0 });
            }
            if(metricsPtr != IntPtr.Zero) {
                TDAGameProcess.VirtualProtect(metricsPtr, 1024, MemPageProtect.PAGE_READWRITE);
                TDAGameProcess.WriteBytes(metricsPtr, ToByteArray("%i FPS (T)", 20));
            }

            IsConsoleUnlocked = true;
            UnlockButton.Content = "Console Unlocked";
        }

        private void SigScan() {
            SigScanTarget consoleLocked   = new("084C8B0EBA01");
            SigScanTarget consoleUnlocked = new("084C8B0EBA00");
            SigScanTarget perfMetrics     = new("2569204650530000252E32666D7300004672616D65203A202575");

            SignatureScanner scanner = new(TDAGameProcess, TDAGameProcess.MainModule.BaseAddress, TDAGameProcess.MainModule.ModuleMemorySize);

            var scannedPtr = scanner.Scan(consoleLocked);
            if(scannedPtr == IntPtr.Zero) {
                scannedPtr = scanner.Scan(consoleUnlocked);
                if(scannedPtr == IntPtr.Zero) {
                    SetErrorMessage("Couldn't find memory address to unlock console.");
                    FailedHookProcID = TDAGameProcess.Id;
                    return;
                }
            }

            ConsoleLockDP = new((int)(scannedPtr.ToInt64() - TDAGameProcess.MainModule.BaseAddress.ToInt64()) + 0x5);

            scannedPtr = scanner.Scan(perfMetrics);
            if(scannedPtr == IntPtr.Zero) {
                SetErrorMessage("Couldn't find metrics.");
                return;
            }

            MetricsDP = new((int)(scannedPtr.ToInt64() - TDAGameProcess.MainModule.BaseAddress.ToInt64()));
        }

        private void SetErrorMessage(string msg) {
            ErrorLabel.Content = msg;
            ErrorLabel.Foreground = Brushes.Red;
        }
        private void HideErrorMessage() => ErrorLabel.Foreground = BG_GRAY;

        #region UI Events
        private void UnlockButton_Click(object sender, RoutedEventArgs e) {
            ProcessTimer.Stop();
            UnlockButton.IsEnabled = false;
            UnlockConsole();
            ProcessTimer.Start();
        }
        #endregion Events

        private static byte[] ToByteArray(string text, int length) {
            byte[] output = new byte[length];
            byte[] textArray = Encoding.ASCII.GetBytes(text);
            for(int i = 0; i < length; i++) {
                if(i >= textArray.Length)
                    break;
                output[i] = textArray[i];
            }
            return output;
        }
    }
}