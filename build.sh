mcs -target:exe -out:TESLRebornUpdater.exe \
    -reference:"System.dll" \
    -reference:"System.Net.Http.dll" \
    -reference:"System.IO.Compression.dll" \
    -reference:"System.IO.Compression.FileSystem.dll" \
    -optimize \
    TESLRebornUpdater.cs
apack TESLRebornUpdater.zip TESLRebornUpdater.exe updater.bat
