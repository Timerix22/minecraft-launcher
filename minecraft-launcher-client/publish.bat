dotnet publish -c Release -o bin\publish ^
  --self-contained ^
  -r win-x64 ^
  -p:PublishSingleFile=true ^
  -p:PublishTrimmed=true ^
  -p:TrimMode=partial ^
  -p:EnableCompressionInSingleFile=true ^
  -p:OptimizationPreference=Size ^
  -p:InvariantGlobalization=true ^
  -p:DebugType=none
