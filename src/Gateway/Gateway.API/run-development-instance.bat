@echo off
echo ========================================
echo Gateway API - YARP Reverse Proxy
echo ========================================
echo Environment: Development
echo Port: 5000 (HTTP)
echo Proxies: Identity(5001), Tenant(5002), Notification(5004)
echo          FileManager(5005), Translation(5006), Category(5007)
echo          AI(5008), Nasheed(5009)
echo ========================================
echo.

set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://localhost:5000
dotnet run --no-launch-profile

pause
