# TESL-Reborn-Updater
Simple helper to update the TESL-rehborn plugin before game launch

#Installation

Put it in the game folder, next to "The Elder Scrolls Legends.exe".
Run it there or add it to the Steam launch options like this:

Linux:

./TESLRebornUpdater.exe; WINEDLLOVERRIDES="winhttp=n,b" %command%

Windows:

updater.bat %command%

(you can omit the WINEDLLOVERRIDES="winhttp=n,b" part on Windows).

I can only test on Manjaro and SteamOS.

Thanks to Solo_mag for testing on their Windows system
