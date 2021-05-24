#!/bin/sh
cd "$(dirname "$0")/src" && dotnet build && ../BizHawk/EmuHawkMono.sh --mono-no-redirect --open-ext-tool-dll=CrystalAiCtrl
