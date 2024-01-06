dotnet publish -c Release -o bin/publish \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:TrimMode=partial \
  -p:EnableCompressionInSingleFile=true \
  -p:InvariantGlobalization=true \
  -p:DebugType=embedded
