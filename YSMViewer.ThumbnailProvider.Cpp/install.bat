@echo off
chcp 65001 >nul
echo ==============================================
echo   YSMViewer Thumbnail Provider - Install
echo ==============================================
echo.

REM Check admin
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Administrator privileges required!
    echo Right-click this file -^> Run as administrator.
    echo [ERROR] 需要管理员权限！
    pause
    exit /b 1
)

set "SRC_DIR=%~dp0"
set "DST_DIR=%APPDATA%\YSMViewer\Thumbnail"

REM Check source DLLs exist
if not exist "%SRC_DIR%YSMViewer.ThumbnailProvider.dll" (
    echo [ERROR] YSMViewer.ThumbnailProvider.dll not found in %SRC_DIR%
    pause
    exit /b 1
)
if not exist "%SRC_DIR%YSMViewer.ThumbnailProvider.Cpp.dll" (
    echo [ERROR] YSMViewer.ThumbnailProvider.Cpp.dll not found in %SRC_DIR%
    pause
    exit /b 1
)

REM Create destination
if not exist "%DST_DIR%" mkdir "%DST_DIR%"

REM Copy DLLs
echo Copying to %DST_DIR% ...
copy /y "%SRC_DIR%YSMViewer.ThumbnailProvider.dll" "%DST_DIR%\" >nul || (
    echo [ERROR] Failed to copy YSMViewer.ThumbnailProvider.dll
    pause
    exit /b 1
)
copy /y "%SRC_DIR%YSMViewer.ThumbnailProvider.Cpp.dll" "%DST_DIR%\" >nul || (
    echo [ERROR] Failed to copy YSMViewer.ThumbnailProvider.Cpp.dll
    pause
    exit /b 1
)

REM Register
echo Registering from %DST_DIR% ...
regsvr32 /s "%DST_DIR%\YSMViewer.ThumbnailProvider.Cpp.dll"

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
