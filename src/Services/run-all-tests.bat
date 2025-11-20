@echo off
setlocal enabledelayedexpansion

echo ====================================
echo Running Tests for All Services
echo ====================================
echo.

REM Identity Service Tests - Green (#00FF00)
echo [Identity Service Tests]
wt.exe --tabColor "#00FF00" --title "Identity Tests" cmd.exe /k "cd /d %~dp0Identity\Identity.API.Tests && dotnet test"
timeout /t 2 /nobreak >nul

REM Tenant Service Tests - Yellow (#FFFF00)
echo [Tenant Service Tests]
wt.exe --tabColor "#FFFF00" --title "Tenant Tests" cmd.exe /k "cd /d %~dp0Tenant\Tenant.API.Tests && dotnet test"
timeout /t 2 /nobreak >nul

REM Notification Service Tests - Cyan-ish (#0077ffff)
echo [Notification Service Tests]
wt.exe --tabColor "#0077ffff" --title "Notification Tests" cmd.exe /k "cd /d %~dp0Notification\Notification.API.Tests && dotnet test"
timeout /t 2 /nobreak >nul

REM FileManager Service Tests - Magenta-ish (#6200ffff)
echo [FileManager Service Tests]
wt.exe --tabColor "#6200ffff" --title "FileManager Tests" cmd.exe /k "cd /d %~dp0FileManager\FileManager.API.Tests && dotnet test"

echo.
echo ====================================
echo All test suites are running in separate Windows Terminal tabs...
echo ====================================
