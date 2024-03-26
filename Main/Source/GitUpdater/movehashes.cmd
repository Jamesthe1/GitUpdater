@echo off
set /p libgithash=<hash
rename "%~1\libgit2-%libgithash%.so" git2-%libgithash%
robocopy "%~1 " "..\..\Natives" git2-%libgithash%.dll git2-%libgithash%