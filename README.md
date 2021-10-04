# BSManager


BSManager is a small portable utility to switch automatically on and off the Vive Base Stations with the VR Headset.

It does support all Pimax models and the Vive Pro headsets.

Base Stations v1 and v2 are both supported.


## **USE AT YOUR OWN RISK**


## Installation

It's a portable application; the only software pre-requisite is the Desktop Runtime for .NET Core 3.1 (https://versionsof.net/core/3.1/3.1.19/) but it should be self-contained.

Move it into a permanent directory, you can create a shortcut to launch it or use the drop-down menu option to create it on the desktop.

If you wish in the drop-down menu you can also select "Run at Startup" and it will be run at every current user logon.

To control the Base Stations you need a BLE adapter. The HeadSet Bluetooth adapter, if any, can't be used.

In theory there's no need to pair the Base Stations in Windows but if they are not seen try it (their name starts with "HTC BS " or "LHB-").

If you get many errors or can't see the Base Stations they are too far from to the USB dongle/antenna, shorten the distance.


## Usage

There's no main window, only the drop-down menu accessible clicking with right mouse button on the system tray icon.


![Tray Menu Picture](https://github.com/mann1x/BSManager/raw/master/BSManager/BSManager_tray.png)


You can check the BS discovered, the HMD status, configure the startup, the Steam VR DB and enable logging (it will create a BSManager.log file).

At startup if the HMD is found active the software will send the base stations a wake-up command or a sleep command if the HMD is off (this only for v1).

Base stations are not discovered, the App will listen to BLE Advertisement messages.

The 2nd number is "Discovered:" after the / is the count of Base Stations found in the SteamVR database.

The icon on the left of the Base Station will change upon a correct wake-up or sleep command is received. After the name the pending action, if any, is displayed.

Only for the BS v2 if the last issued command is Sleep and the Base Stations are still On after 5 minutes the command will be re-sent.

Only for the BS v1 the Stations will be briefly powered on, about 30 seconds, to set the Sleep mode (they don't report the power state).

BSManager can automatically start & kill Pimax Runtime & close selected SteamVR components (which reduces risk of SteamVR crashes).

Manage Runtime is an option that can be enabled in the System Tray Icon drop-down menu; it can be enabled if the Pimax Runtime is not in the default directory running only once BSManager with Admin privileges.

With Manage Runtime enabled Pitool is automatically open and closed. The process will start about 15 seconds after the HMD has changed state to allow reboots (HMD reboot, Pimax service restart or manual on/off) without disruptions.

There's also support for customizable lists of processes to close when the Headset is powered off. There's a graceful and an immediate killing list. It's better if possible to use the graceful list. Unfortunately this will not work for processes that doesn't directly close like Pitool which are asking for user input to quit the application.

The default list for processes to be killed gracefully is: "vrmonitor", "vrdashboard", "ReviveOverlay", "vrmonitor" (twice in the list to repeat attempts to close). To customize the graceful list use a "BSManager.grace.txt" file in the same directory of BSManager.exe, one process per line (will replace the default).

The immediate killing list (SIGTERM) is empty by default and can be customized using a "BSManager.kill.txt" file in the same directory of BSManager.exe, one process per line

**Please use the Issues tab on GitHub if you find issues or have a request**


## Credits

- Thanks to:
    - The excellent BLEConsole from SeNSSoFT [https://github.com/sensboston/BLEConsole]
    - LightHouseController from Alex Flynn [https://bitbucket.org/Flynny75/lighthousecontroller/src/master/]
    - SparkerInVR's great support in testing [https://www.twitch.tv/sparkerinvr]


## Compilation

You can compile with Visual Studio 2019 and .NET Core 3.1.


## Changelog:

- v2.4.1
    - Fix: Bug in Run at Startup (watch out the AutoUpdater is impacted as well, you may need to update manually!)
- v2.4.0
    - New: Toast for BS commands progress (so you know if it's actually doing something), can be disabled from Help and Info drop-down menu
    - New: Notifications and BLE errors improvements
    - New: Run at Startup executable path will be replaced with the current if different from registry (avoid startup of an old version)
    - New: Moved from .NET 5 to .NET Core 3.1 (less memory requirements, more stable development environment); .NET install now self-contained (bigger file size)
    - New: Improved routine to kill processes 
    - Fix: Support for HTC manufactured Base Stations v2
    - Fix: Added support for Pimax LightHouses DB
    - Fix: Bug in Run at Startup
- v2.3.0
    - New: Option to automatically start & kill Pimax Runtime & close SteamVR components (reduces risk of SteamVR crashes)
    - New: Manage Runtime is an option that can be enabled in the System Tray Icon drop-down menu
    - New: Manage Runtime can be enabled if the Pimax Runtime is not in the default directory running only once BSManager with Admin privileges
    - New: Delayed Base Stations control; rebooting HMD or PiService will not trigger a Base Stations power off/on cycle
    - New: Customizable list for processes to be killed gracefully, default: "vrmonitor", "vrdashboard", "ReviveOverlay", "vrmonitor" (twice in the list to repeat) 
    - New: Graceful list can be customized using a "BSManager.grace.txt" file in the same directory of BSManager.exe, one process per line (will replace the default)
    - New: Immediate killing list (SIGTERM) can be customized using a "BSManager.kill.txt" file in the same directory of BSManager.exe, one process per line
    - Fix: Improved log files readability
- v2.2.1
    - New: Unified BLE workflow for BS v1 and v2, no functional changes
- v2.1.0
    - New: Background thread that will start and stop the BLE Advertisement watcher on demand
    - New: Reduced CPU usage from 0.01-0.02% to almost 0% in idle
    - New: Optimized memory usage, should be stable at about 65MB (Garbage collector every 10 minutes)
- v2.0.0
    - New: Support for Base Stations v2
    - New: Removed discovery to use BLE Advertisement messages instead
    - New: Icon to display BS status, action pending
    - New: Switch to enable/disable Debug Log
    - Fix: Many fixes and code improvements
- v1.2.5
    - Fix: Tentative fix for BS v2, wrong characteristic
    - Fix: Improved reliability of SteamVR DB parsing
    - Fix: Improved BLE Discovery
- v1.2.4
    - Fix: Bug in BS v2 discovery
- v1.2.3
    - New: Implemented AutoUpdater
- v1.2.1
    - New: Added blind experimental Base Stations v2 support
- v1.2.0
    - New: Added mutex for only one instance active
    - Fix: Fixed critical bug, mistake on BT devicelist trim
    - New: Cycling through command retries and looping the whole command sequence
    - New: Added error and exception management
    - New: Exceptions are now saved in a log file
    - New: Implemented tooltip balloons to display error messages
    - New: Tray icon will display HeadSet status via a green dot, means active
    - Fix: Fixed BT commands loop with close (open should disconnected previously connected device but seems not doing it)
    - Fix: Other small fixes    
- v1.0.1
    - New: Introduced a wait of 2.5 seconds between BLE discovery cycles to wait for BLE adapter to be available at system startup 
- v1.0.0
    - Initial release
