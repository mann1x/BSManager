# BSManager


BSManager is a small portable utility to switch automatically on and off the Vive Base Stations with the VR Headset.

It does support all Pimax models and the Vive Pro headsets.

Currently supports only Base Stations v1.


## **USE AT YOUR OWN RISK**


## Installation

It's a portable application. The only software pre-requisite is the Desktop Runtime for .NET Core 5.0.9 (https://versionsof.net/core/5.0/5.0.9/)

Move it into a permanent directory, you can create a shortcut to launch it or use the drop down menu option to create it on the desktop.

If you wish in the dropdown menu you can also select "Run at Startup" and it will be run at every current user logon.

To control the Base Stations you need a BLE adapter and pair the Base Stations in Windows (their name starts with "HTC BS "). The HeadSet Bluetooth adapter can't used.


## Usage

There's no main window, only the dropdown menu accessible clicking with right mouse button on the system tray icon.

You can check the BS discovered, the HMD status, configure the startup, the Steam VR DB and send a sleep command.

At startup if the HMD is found active the software will send the base stations a wakeup command.

Base stations are discovered via BLE at boot. Discovery will run for 20 cycles before giving up. In case the BS are found in the Steam DB discovery will search till the same number is found via BLE.

**Please use the Issues tab on GitHub if you find issues or have a request**

Almost all excellent BLEConsole from SeNSSoFT is included for easy troubleshooting and upgrade/evolutions (https://github.com/sensboston/BLEConsole).


## Compilation

You can compile with Visual Studio 2019 and .NET Core 5.