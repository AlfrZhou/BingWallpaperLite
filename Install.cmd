@echo off
set /p "BWL_VERSION="<"%~dp0Version.txt"
"%~dp0dist\BingWallpaperLite-Setup-%BWL_VERSION%.exe" %*
if errorlevel 1 pause
