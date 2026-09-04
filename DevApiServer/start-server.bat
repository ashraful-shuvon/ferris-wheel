@echo off
title Ferris Wheel Dev API Server

cd /d "%~dp0"

echo.
echo  Starting Ferris Wheel Dev API Server...
echo  (macOS: double-click start-server.command, or just press Play in Unity)
echo  Port  : 5002
echo  API   : http://127.0.0.1:5002/api/v1
echo  Test  : http://127.0.0.1:5002/index.html
echo.
echo  Press Ctrl+C to stop.
echo.

npm start

pause

