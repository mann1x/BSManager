# BSManager


BSManager is a small portable utility to switch automatically on and off the Vive Base Stations with the VR Headset.

It does support all Pimax models and the Vive Pro headsets.

Base Stations v1 and v2 are both supported.


## **USE AT YOUR OWN RISK**


## Installation

It's a portable application. The only software pre-requisite is the Desktop Runtime for .NET Core 5.0.9 (https://versionsof.net/core/5.0/5.0.9/)

Move it into a permanent directory, you can create a shortcut to launch it or use the drop down menu option to create it on the desktop.

If you wish in the dropdown menu you can also select "Run at Startup" and it will be run at every current user logon.

To control the Base Stations you need a BLE adapter. The HeadSet Bluetooth adapter, if any, can't used.

In theory there's no need to pair the Base Stations in Windows but if they are not seen try it (their name starts with "HTC BS " or "LHB-").

If you get many errors or can't see the Base Stations they are too far from to the USB dongle/antenna, shorten the distance.


## Usage

There's no main window, only the dropdown menu accessible clicking with right mouse button on the system tray icon.


![Tray Menu Picture](https://github.com/mann1x/BSManager/raw/master/BSManager/BSManager_tray.png)


You can check the BS discovered, the HMD status, configure the startup, the Steam VR DB and enable logging (it will create a BSManager.log file).

At startup if the HMD is found active the software will send the base stations a wakeup command or a sleep command if the HMD is off (this only for v1).

Base stations are not discovered, the App will listen to BLE Advertisment messages.

The 2nd number is "Discovered:" after the / is the count of Base Stations found in the SteamVR database.

The icon on the left of the Base Station will change upon a correct Wakeup or Sleep command is received. After the name the pending action, if any, is displayed.

Only for the BS v2 if the last issued command is Sleep and the Base Stations are still On after 5 minutes the command will be re-sent.

**Please use the Issues tab on GitHub if you find issues or have a request**


## Credits

    - The excellent BLEConsole from SeNSSoFT (https://github.com/sensboston/BLEConsole).
    - LightHouseController from Alex Flynn (https://bitbucket.org/Flynny75/lighthousecontroller/src/master/).
    - SparkerInVR's great support in testing (https://www.twitch.tv/sparkerinvr).


## Compilation

You can compile with Visual Studio 2019 and .NET 5.


## Changelog:

- v2.0.0
    - New: Support for Base Stations v2
    - New: Removed discovery to use BLE Advertisement messages instead
    - New: Icon to display BD status, action pending
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
