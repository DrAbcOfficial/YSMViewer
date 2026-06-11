@echo off
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

set "DLL_PATH=%~dp0build\YSMViewer.ThumbnailProvider.Cpp.dll"

if not exist "%DLL_PATH%" (
    echo [ERROR] DLL not found.
    pause
    exit /b 1
)

regsvr32 /s /u "%DLL_PATH%"

if %errorLevel% equ 0 (
    echo [OK] Unregistration successful! / 卸载成功！
) else (
    echo [ERROR] Unregistration failed code: %errorLevel%
)
pause
