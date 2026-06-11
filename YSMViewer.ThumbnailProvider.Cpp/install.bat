@echo off
echo ==============================================
echo   YSMViewer Thumbnail Provider - Install
echo ==============================================
echo.

REM Check admin
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Administrator privileges required!
    echo Right-click this file -> Run as administrator.
    echo [ERROR] 需要管理员权限！
    pause
    exit /b 1
)

set "DLL_PATH=%~dp0build\YSMViewer.ThumbnailProvider.Cpp.dll"

if not exist "%DLL_PATH%" (
    echo [ERROR] YSMViewer.ThumbnailProvider.Cpp.dll not found.
    echo Run build.bat first.
    echo [ERROR] 未找到 DLL，请先运行 build.bat
    pause
    exit /b 1
)

echo DLL: %DLL_PATH%
echo.

regsvr32 /s "%DLL_PATH%"

if %errorLevel% equ 0 (
    echo [OK] Registration successful! / 注册成功！
    echo NOTE: Restart Explorer or log out for changes.
    echo 提示：重启资源管理器或注销后生效。
) else (
    echo [ERROR] Registration failed code: %errorLevel%
    echo [ERROR] 注册失败，错误: %errorLevel%
)
echo.
pause
