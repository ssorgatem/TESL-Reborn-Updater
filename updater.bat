@echo off
>nul 2>&1 "%__CD__%if exist TESLRebornUpdater.exe start /b /wait "" "TESLRebornUpdater.exe""
start "" /b "The Elder Scrolls Legends.exe" %*
