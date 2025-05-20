# TDAUnlocker
This tool will unlock the developer console in DOOM: The Dark Ages to allow access to restricted commands.

### Installation
This tool **requires** .NET 6.0 to be installed in order to function.  
You can download the .NET Installer from [this link](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-6.0.36-windows-x64-installer).

To install TDAUnlocker, download `TDAUnlocker.zip` from the releases page or this [link](https://github.com/bowsr/TDAUnlocker/releases/latest/download/TDAUnlocker.zip).  
Extract the contents of the zip file to any folder you'd like, then run the exe.

### Usage
While DOOM: The Dark Ages is open, click the "Unlock Console Commands" button to unlock the console.  
Alternatively, you can enable the auto unlock option to have TDAUnlocker automatically unlock the console every time you open the game.

If you kept the "Replace god with noclip" box checked, the `god` command's function will be replaced with noclip's function. Just type `god` into the console to access noclip.  
If you still need godmode after replacing the `god` command, you can set the cvar `g_permaGodMode` to `1`.

### Credits
- [LiveSplit](https://github.com/LiveSplit/LiveSplit) - for their memory code
- rumii - for originally finding how to unlock the console
- Micrologist - for their original lua script that replaced the god command with noclip in Cheat Engine
