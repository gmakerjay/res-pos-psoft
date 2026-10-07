@echo off
title Restaurant POS - Build Windows Desktop Client
chcp 65001 >nul
set "BASE=%~dp0"
set "OUT=%BASE%..\build_output\client_pc"

echo ======================================================================
echo    RESTAURANT POS - BUILD WINDOWS CLIENT (RELEASE)
echo ======================================================================
echo.

echo [*] Compiling RestaurantPOS.Wpf (Release)...
cd /d "%BASE%RestaurantPOS.Wpf"
dotnet build RestaurantPOS.Wpf.csproj -c Release
if %errorlevel% neq 0 (
    echo [ERROR] Build failed.
    pause
    exit /b 1
)

echo.
echo [*] Copying Release artifacts to %OUT% ...
if not exist "%OUT%" mkdir "%OUT%"
xcopy /E /I /Y "%BASE%RestaurantPOS.Wpf\bin\Release\net10.0-windows\*" "%OUT%\"

echo.
echo ======================================================================
echo [SUCCESS] Windows Desktop POS built and copied to:
echo %OUT%
echo ======================================================================
pause
exit /b 0
