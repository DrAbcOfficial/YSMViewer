@echo off
chcp 65001 >nul
echo ==============================================
echo   YSMViewer Thumbnail Provider - Uninstall
echo ==============================================
echo.

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Administrator privileges required!
    pause
    exit /b 1
)

set "DLL_DIR=%APPDATA%\YSMViewer\Thumbnail"
set "DLL_PATH=%DLL_DIR%\YSMViewer.ThumbnailProvider.Cpp.dll"

if not exist "%DLL_PATH%" (
    echo [WARN] DLL not found at %DLL_PATH%
    echo [WARN] Provider may not be installed.
    goto cleanup
)

echo Unregistering from %DLL_DIR% ...
regsvr32 /s /u "%DLL_PATH%"

if %errorLevel% equ 0 (
    echo [OK] Unregistration successful! / 卸载成功！
) else (
    echo [ERROR] Unregistration failed code: %errorLevel%
)

:cleanup
REM Clean up installed files
if exist "%DLL_DIR%" (
    echo Removing %DLL_DIR% ...
    rd /s /q "%DLL_DIR%"
)
pause
