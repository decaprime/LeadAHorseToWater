dotnet build
xcopy .\bin\Debug\netstandard2.1\LeadAHorseToWater.dll L:\SteamLibrary\steamapps\common\VRisingDedicatedServer\BepInEx\plugins /y

pushd L:\SteamLibrary\steamapps\common\VRisingDedicatedServer\
start_server_example.bat
popd