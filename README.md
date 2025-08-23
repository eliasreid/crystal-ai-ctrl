# crystal-ai-ctrl

Plugin for BizHawk's GBHawk emulator to control the actions of NPC trainers in battles.

Interfaces with this webserver and browser client (https://github.com/eliasreid/pokemon-enemy-ctrl-web) to allow remote control of enemy trainers.

## Setup

Run `downloadEmulator.sh` from project root directory to download and extract compatible version of the emulator to the correct location.

Download and install Visual Studio and make sure .NET desktop developement option is checked: https://visualstudio.microsoft.com/downloads/

Run the emulator with the external tool (`./BizHawk/EmuHawk --open-ext-tool-dll=CrystalAiCtrl`) and a compatible ROM, then click the `Connect` button to start a session. See https://github.com/eliasreid/pokemon-enemy-ctrl-web repo for instructions on setting up the server.

