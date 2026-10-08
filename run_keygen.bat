@echo off
title Restaurant POS - Developer Key Generator
chcp 65001 >nul
set "BASE=%~dp0"
cd /d "%BASE%tools\RestaurantPOS.KeyGen\bin\Release\net10.0-windows"
if not exist "RestaurantPOS.KeyGen.exe" (
    echo [ERROR] RestaurantPOS.KeyGen.exe not found. Building first...
    cd /d "%BASE%tools\RestaurantPOS.KeyGen"
    dotnet build -c Release
    cd /d "%BASE%tools\RestaurantPOS.KeyGen\bin\Release\net10.0-windows"
)
start "" "RestaurantPOS.KeyGen.exe"
exit /b 0
