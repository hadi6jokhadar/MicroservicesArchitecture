@echo off
echo ========================================
echo AI.API - Development Instance
echo ========================================
echo Environment: Development
echo Ports: 5008 (HTTP)
echo Database: ai
echo ========================================
echo.

call .\venv\Scripts\activate.bat
uvicorn main:app --reload --port 5008

pause
