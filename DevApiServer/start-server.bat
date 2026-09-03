@echo off
title Ferris Wheel Dev API Server

cd /d "%~dp0"

echo.
echo  Starting Ferris Wheel Dev API Server...
echo  Port  : 5002
echo  API   : http://localhost:5002/api/v1
echo  Test  : http://localhost:5002/index.html
echo.
echo  Press Ctrl+C to stop.
echo.

npm start

pause

