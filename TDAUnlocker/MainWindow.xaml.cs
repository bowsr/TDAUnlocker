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

        DeepPointer ConsoleLockDP, MetricsDP, GodCmdDP, NoclipCmdDP, CmdArrayDP;

        int FailedHookProcID = 0;
        bool CanSwapGodNoclipFuncs = false;
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

            if(CanSwapGodNoclipFuncs && (bool)SwapGodNoclipCheckbox.IsChecked) SwapGodAndNoclipFuncs();
        }

        /// <summary>
        /// Swaps the god func pointer with the noclip func pointer in the cmd array.
        /// Thanks to Micrologist for the Cheat Engine lua script that this was ported from.
        /// </summary>
        private void SwapGodAndNoclipFuncs() {
            HideErrorMessage();
            UnlockButton.Content = "Swapping god and noclip...";

            IntPtr godCmdPtr = IntPtr.Zero;
            IntPtr noclipCmdPtr = IntPtr.Zero;
            IntPtr cmdArrayPtr = IntPtr.Zero;
            GodCmdDP?.DerefOffsets(TDAGameProcess, out godCmdPtr);
            NoclipCmdDP?.DerefOffsets(TDAGameProcess, out noclipCmdPtr);
            CmdArrayDP?.DerefOffsets(TDAGameProcess, out cmdArrayPtr);

            if(godCmdPtr == IntPtr.Zero || noclipCmdPtr == IntPtr.Zero || cmdArrayPtr == IntPtr.Zero) {
                SetErrorMessage("Failed to deref cmd offsets.");
                return;
            }

            IntPtr addr, val;
            for(long l = cmdArrayPtr.ToInt64() - 1000; l <= cmdArrayPtr.ToInt64() + 1000; l += 8) {
                addr = new IntPtr(l);
                val = TDAGameProcess.ReadPointer(addr);
                if(val == godCmdPtr) {
                    TDAGameProcess.VirtualProtect(addr, 8, MemPageProtect.PAGE_EXECUTE_READWRITE);
                    TDAGameProcess.WriteValue(addr, noclipCmdPtr);
                    UnlockButton.Content = "Unlocked & swapped noclip";
                    return;
                }
            }

            SetErrorMessage("Couldn't find god cmd ptr.");
            UnlockButton.Content = "Console Unlocked";
        }

        /// <summary>
        /// Scans for various required signatures.
        /// Thanks to rumii and Micrologist for the Cheat Engine lua scripts this was ported from.
        /// </summary>
        private void SigScan() {
            SigScanTarget consoleLocked   = new("084C8B0EBA01");
            SigScanTarget consoleUnlocked = new("084C8B0EBA00");
            SigScanTarget perfMetrics     = new("2569204650530000252E32666D7300004672616D65203A202575");
            SigScanTarget godCmd          = new("40534883EC208B41??488BDA83F8FF7523488BCAE8");
            SigScanTarget noclipCmd       = new("48895C24??574883EC20488B02488BCA488BDAFF90????????488BC8");
            SigScanTarget cmdArray        = new("6964436C69656E7447616D654D73675F5265737061776E506C6179657220");

            SignatureScanner scanner = new(TDAGameProcess, TDAGameProcess.MainModule.BaseAddress, TDAGameProcess.MainModule.ModuleMemorySize);

            // Scanning for console restriction address
            var scannedPtr = scanner.Scan(consoleLocked);
            if(scannedPtr == IntPtr.Zero) {
                scannedPtr = scanner.Scan(consoleUnlocked);
                if(scannedPtr == IntPtr.Zero) {
                    SetErrorMessage("Couldn't find memory address to unlock console.");
                    FailedHookProcID = TDAGameProcess.Id;
                    return;
                }
                IsConsoleUnlocked = true;
            }

            ConsoleLockDP = CreateDeepPointer(scannedPtr, 0x5);

            // Scanning for performance metrics string
            scannedPtr = scanner.Scan(perfMetrics);
            if(scannedPtr == IntPtr.Zero) {
                if(!IsConsoleUnlocked) SetErrorMessage("Couldn't find metrics.");
                return;
            }

            MetricsDP = CreateDeepPointer(scannedPtr);

            CanSwapGodNoclipFuncs = true;
            // Scanning for god function
            scannedPtr = scanner.Scan(godCmd);
            if(scannedPtr == IntPtr.Zero) {
                CanSwapGodNoclipFuncs = false;
                SetErrorMessage("Couldn't find god function.");
                return;
            }

            GodCmdDP = CreateDeepPointer(scannedPtr);

            // Scanning for noclip function
            scannedPtr = scanner.Scan(noclipCmd);
            if(scannedPtr == IntPtr.Zero) {
                CanSwapGodNoclipFuncs = false;
                SetErrorMessage("Couldn't find noclip function.");
                return;
            }

            NoclipCmdDP = CreateDeepPointer(scannedPtr);

            // Scanning for cmd array
            scannedPtr = scanner.Scan(cmdArray);
            if(scannedPtr == IntPtr.Zero) {
                CanSwapGodNoclipFuncs = false;
                SetErrorMessage("Couldn't find cmd array.");
                return;
            }

            CmdArrayDP = CreateDeepPointer(scannedPtr);
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

        private DeepPointer CreateDeepPointer(IntPtr ptr, int offset = 0x0) => new((int) (ptr.ToInt64() - TDAGameProcess.MainModule.BaseAddress.ToInt64()) + offset);

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