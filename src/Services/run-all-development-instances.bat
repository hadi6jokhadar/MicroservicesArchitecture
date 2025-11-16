@echo off
setlocal enabledelayedexpansion

REM Redis Service - Gray (#646464ff)
wt.exe --tabColor "#646464ff" --title "Redis" cmd.exe /k "cd /d "%~dp0..\.." && redis-server.lnk"
timeout /t 4 /nobreak >nul

REM Tenant Service - Yellow (#FFFF00)
wt.exe --tabColor "#FFFF00" --title "Tenant Service" cmd.exe /k "cd /d "%~dp0Tenant\Tenant.API" && run-development-instance.bat"
timeout /t 4 /nobreak >nul

REM Identity Service - run-tenant-instance.bat - Green (#00FF00)
wt.exe --tabColor "#00FF00" --title "Identity - Tenant Instance" cmd.exe /k "cd /d "%~dp0Identity\Identity.API" && run-tenant-instance.bat"
timeout /t 4 /nobreak >nul

REM Identity Service - run-development-instance.bat - Green (#00FF00)
wt.exe --tabColor "#00FF00" --title "Identity - Dev Instance" cmd.exe /k "cd /d "%~dp0Identity\Identity.API" && run-development-instance.bat"
timeout /t 4 /nobreak >nul

REM Notification Service - Cyan-ish
wt.exe --tabColor "#0077ffff" --title "Notification Service" cmd.exe /k "cd /d "%~dp0Notification\Notification.API" && run-development-instance.bat"
timeout /t 4 /nobreak >nul

REM FileManager Service - Magenta-ish
wt.exe --tabColor "#6200ffff" --title "FileManager Service" cmd.exe /k "cd /d "%~dp0FileManager\FileManager.API" && run-development-instance.bat"

echo All development instances are starting...