@echo off
set /p build_hash=<hash
dotnet build /p:BuildHash=%build_hash%