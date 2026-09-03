@echo off
title Stop Ferris Wheel Dev API Server

echo.
echo  Stopping Ferris Wheel Dev API Server (port 5002)...
echo.

:: Find and kill the node process listening on port 5002
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":5002 " ^| findstr "LISTENING"') do (
    echo  Killing PID %%a
    taskkill /PID %%a /F >nul 2>&1
)

echo  Done.
echo.
pause

