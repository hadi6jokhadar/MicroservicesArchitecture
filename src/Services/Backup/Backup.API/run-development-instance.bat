@echo off
echo ========================================
echo Backup API - Development Instance
echo ========================================
echo Environment: Development
echo Ports: 5010 (HTTP) / 5110 (HTTPS)
echo Database: backup (global, single-database — not multi-tenant)
echo ========================================
echo.

set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://localhost:5010;https://localhost:5110
dotnet run --no-launch-profile

pause
