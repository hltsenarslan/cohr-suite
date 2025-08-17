#!/usr/bin/env bash
set -e
dotnet clean
dotnet restore
dotnet build -c Release
dotnet test  -c Release