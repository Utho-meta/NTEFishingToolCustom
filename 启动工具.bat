@echo off
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~dp0NTEFishingToolCustom.exe' -WorkingDirectory '%~dp0' -Verb RunAs"
