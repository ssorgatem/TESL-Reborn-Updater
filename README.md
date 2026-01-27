# TESL-Reborn-Updater
Simple helper to update the TESL-reborn plugin before game launch

#Installation

Download the latest release [here](https://github.com/ssorgatem/TESL-Reborn-Updater/releases)

Put it in the game folder, next to "The Elder Scrolls Legends.exe".

Run it there or add it to the Steam launch options like this:

**Linux:**

`./TESLRebornUpdater.exe; WINEDLLOVERRIDES="winhttp=n,b" %command%`

**Windows:**

`powershell -WindowStyle Hidden -Command "Start-Process TESLRebornUpdater.exe -Wait -WindowStyle Hidden; & '%command%'"`


I can only test on Manjaro and SteamOS.

Thanks to Solo_mag for testing on their Windows system and for finding the magic powershell invocation
