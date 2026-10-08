@echo off
title Psoft-RES Online - Windows Desktop Client
chcp 65001 >nul
set "BASE=%~dp0"
set "BIN_REL=%BASE%RestaurantPOS.Wpf\bin\Release\net10.0-windows\Psoft-RES Online.exe"
set "BIN_DBG=%BASE%RestaurantPOS.Wpf\bin\Debug\net10.0-windows\Psoft-RES Online.exe"

echo ======================================================================
echo    PSOFT-RES ONLINE - WINDOWS DESKTOP CLIENT (Classic XP Theme)
echo ======================================================================
echo.

if exist "%BIN_REL%" (
    echo [*] Starting Psoft-RES Online.exe (Release)...
    start "" "%BIN_REL%"
    exit /b 0
)

if exist "%BIN_DBG%" (
    echo [*] Starting Psoft-RES Online.exe (Debug)...
    start "" "%BIN_DBG%"
    exit /b 0
)

echo [*] Binary not found. Building Release first...
dotnet build "%BASE%RestaurantPOS.Client.slnx" -c Release
if exist "%BIN_REL%" (
    start "" "%BIN_REL%"
) else (
    echo [ERROR] Build failed. Please check errors above.
    pause
)
exit /b 0
