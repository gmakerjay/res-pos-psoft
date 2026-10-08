@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

set "ROOT=%~dp0"
set "SERVER_DIR=%ROOT%web_server\RestaurantPOS.Server"
set "WPF_DIR=%ROOT%client_pc\RestaurantPOS.Wpf\bin\Release\net10.0-windows"
set "WPF_EXE=%WPF_DIR%\Psoft-RES Online.exe"
set "WEB_DIR=%ROOT%web_server\RestaurantPOS.Web"
set "CLIENT_RELEASE_DIR=%ROOT%build_output\client_pc"
set "CLIENT_RELEASE_EXE=%CLIENT_RELEASE_DIR%\Psoft-RES Online.exe"

cls
echo ======================================================================
echo           RESTAURANT POS SYSTEM - QUICK LAUNCHER (v1.0.0)
echo ======================================================================
echo.
echo   [1] Run All Components (Server + Web App + Windows POS)
echo   [2] Launch Windows Desktop POS (Classic XP Client)
echo   [3] Start Central Server (ASP.NET Core Port 5000)
echo   [4] Start Web App Dev Server (Vite Port 5173)
echo   [5] Build Entire Solution (RestaurantPOS.slnx)
echo   [6] Launch Published Client POS (build_output)
echo   [7] Launch Developer KeyGen Tool (Private Key Generator)
echo   [0] Exit
echo.
echo ======================================================================
set /p opt="Select an option (0-7): "

if "%opt%"=="1" goto run_all
if "%opt%"=="2" goto run_pos
if "%opt%"=="3" goto run_server
if "%opt%"=="4" goto run_web
if "%opt%"=="5" goto run_build
if "%opt%"=="6" goto run_release
if "%opt%"=="7" goto run_keygen
if "%opt%"=="0" goto end
goto end

:run_all
echo.
echo [1/3] Starting Central Server (Port 5000)...
start "RestaurantPOS Server" cmd /k "cd /d "%SERVER_DIR%" && dotnet run"
timeout /t 3 >nul

echo [2/3] Starting Web App (Port 5173)...
start "RestaurantPOS Web" cmd /k "cd /d "%WEB_DIR%" && npm run dev"
timeout /t 2 >nul

echo [3/3] Launching Windows Desktop POS...
if exist "%WPF_EXE%" (
    cd /d "%WPF_DIR%"
    start "" "%WPF_EXE%"
) else (
    echo [ERROR] %WPF_EXE% not found. Building first...
    dotnet build "%ROOT%RestaurantPOS.slnx"
    cd /d "%WPF_DIR%"
    start "" "%WPF_EXE%"
)
goto end

:run_pos
echo.
echo [*] Launching Windows Desktop POS...
if exist "%WPF_EXE%" (
    cd /d "%WPF_DIR%"
    start "" "%WPF_EXE%"
) else (
    echo [INFO] Building WPF Application...
    dotnet build "%ROOT%client_pc\RestaurantPOS.Wpf\RestaurantPOS.Wpf.csproj"
    cd /d "%WPF_DIR%"
    start "" "%WPF_EXE%"
)
goto end

:run_server
echo.
echo [*] Starting Central Server on http://localhost:5000 ...
cd /d "%SERVER_DIR%"
dotnet run
goto end

:run_web
echo.
echo [*] Starting Web App Dev Server on http://localhost:5173 ...
cd /d "%WEB_DIR%"
npm run dev
goto end

:run_build
echo.
echo [*] Building RestaurantPOS.slnx...
dotnet build "%ROOT%RestaurantPOS.slnx"
echo.
pause
goto end

:run_release
echo.
echo [*] Launching Published Client from build_output\client_pc...
if exist "%CLIENT_RELEASE_EXE%" (
    cd /d "%CLIENT_RELEASE_DIR%"
    start "" "%CLIENT_RELEASE_EXE%"
) else (
    echo [ERROR] %CLIENT_RELEASE_EXE% not found.
    pause
)
goto end

:run_keygen
echo.
echo [*] Launching Developer KeyGen Tool...
call "%ROOT%run_keygen.bat"
goto end

:end
exit /b 0
