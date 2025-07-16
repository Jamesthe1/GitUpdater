@echo off

if "%~1" == "help" (
    echo %~0 game_version
    exit 0
)

rem Spaces are apparently needed between the path and end quote. Why Microsoft
echo Moving assemblies...
robocopy "bin\%~1 " "..\..\..\%~1\Assemblies " GitUpdater.dll LibGit2Sharp.dll /mov
if %errorlevel% gtr 4 exit %errorlevel%
echo Moving Linux natives...
robocopy "bin\%~1\lib\linux-x64 " "..\..\Natives " *.so /mov
if %errorlevel% gtr 4 exit %errorlevel%
echo Moving Windows natives...
robocopy "bin\%~1\lib\win32\x64 " "..\..\Natives " *.dll /mov
if %errorlevel% gtr 4 exit %errorlevel%
echo Cleaning up...
rmdir bin /S /Q
echo Done.