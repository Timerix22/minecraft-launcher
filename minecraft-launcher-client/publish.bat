set /p version=<launcher_version.txt
dotnet publish -c Release -o bin\publish -p:Version=%version%
del /f bin\publish\minecraft-launcher.exe.config
