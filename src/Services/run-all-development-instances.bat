@echo off
setlocal enabledelayedexpansion

REM Tenant Service - Yellow (#FFFF00)
wt.exe --tabColor "#FFFF00" cmd.exe /k "cd /d "%~dp0Tenant\Tenant.API" && run-development-instance.bat"

REM Identity Service - run-tenant-instance.bat - Green (#00FF00)
wt.exe --tabColor "#00FF00" cmd.exe /k "cd /d "%~dp0Identity\Identity.API" && run-tenant-instance.bat"

REM Identity Service - run-development-instance.bat - Green (#00FF00)
wt.exe --tabColor "#00FF00" cmd.exe /k "cd /d "%~dp0Identity\Identity.API" && run-development-instance.bat"

REM Notification Service - Cyan (#00FFFF)
wt.exe --tabColor "#00FFFF" cmd.exe /k "cd /d "%~dp0Notification\Notification.API" && run-development-instance.bat"

echo All development instances are starting...
