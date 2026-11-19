@echo off
if not exist "%~dp0artifacts\win-x64\ReferenceOverlayViewer.exe" (
  echo Build the app first: powershell -File scripts\build.ps1 -Publish
  pause
  exit /b 1
)
start "" "%~dp0artifacts\win-x64\ReferenceOverlayViewer.exe" %*
