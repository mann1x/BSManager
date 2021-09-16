# BSManager


BSManager is a small portable utility to switch automatically on and off the Vive Base Stations with the VR Headset.

It does support all Pimax models and the Vive Pro headsets.

Currently supports only Base Stations v1. Blind experimental Support for Base Stations v2.


## **USE AT YOUR OWN RISK**


## Installation

It's a portable application. The only software pre-requisite is the Desktop Runtime for .NET Core 5.0.9 (https://versionsof.net/core/5.0/5.0.9/)

Move it into a permanent directory, you can create a shortcut to launch it or use the drop down menu option to create it on the desktop.

If you wish in the dropdown menu you can also select "Run at Startup" and it will be run at every current user logon.

To control the Base Stations you need a BLE adapter and pair the Base Stations in Windows (their name starts with "HTC BS "). The HeadSet Bluetooth adapter can't used.


## Usage

There's no main window, only the dropdown menu accessible clicking with right mouse button on the system tray icon.


![Tray Menu Picture](https://github.com/mann1x/BSManager/raw/master/BSManager/BSManager_tray.png)


You can check the BS discovered, the HMD status, configure the startup, the Steam VR DB and send a sleep command.

At startup if the HMD is found active the software will send the base stations a wakeup command.

Base stations are discovered via BLE at boot. Discovery will run for 20 cycles before giving up. In case the BS are found in the Steam DB discovery will search till the same number is found via BLE.

**Please use the Issues tab on GitHub if you find issues or have a request**

Almost all excellent BLEConsole from SeNSSoFT is included for easy troubleshooting and upgrade/evolutions (https://github.com/sensboston/BLEConsole).


## Compilation

You can compile with Visual Studio 2019 and .NET 5.

## Changelog:

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
