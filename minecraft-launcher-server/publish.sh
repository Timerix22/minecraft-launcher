#!/bin/bash
dotnet publish -c release -o bin/publish \
  --self-contained \
  --use-current-runtime \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:TrimMode=partial \
  -p:EnableCompressionInSingleFile=true
