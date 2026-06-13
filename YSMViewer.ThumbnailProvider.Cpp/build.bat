@echo off
chcp 65001 >nul
setlocal EnableExtensions EnableDelayedExpansion

echo ==============================================
echo   YSMViewer Thumbnail Provider C++ Build
echo ==============================================
echo.
echo Make sure to run this from a Visual Studio
echo Developer Command Prompt (x64 Native Tools).
echo.

set "OUTDIR=%~dp0build"
set "CS_PUBLISH_DIR=%~dp0..\YSMViewer.ThumbnailProvider\bin\Release\net10.0-windows\win-x64\publish"
set "CS_DLL=%CS_PUBLISH_DIR%\YSMViewer.ThumbnailProvider.dll"

if not exist "%OUTDIR%" mkdir "%OUTDIR%"

set "CS_FOUND=0"
if exist "%CS_DLL%" (
    set "CS_FOUND=1"
) else (
    :: Search alternative publish locations
    for /r "%~dp0..\YSMViewer.ThumbnailProvider\bin\Release" %%f in (YSMViewer.ThumbnailProvider.dll) do (
        if exist "%%f" (
            set "CS_DLL=%%f"
            set "CS_FOUND=1"
            goto :cs_found
        )
    )
)

:cs_found
if "!CS_FOUND!" == "1" (
    echo Copying C# DLL ^(AOT native^): !CS_DLL!
    copy /Y "!CS_DLL!" "%OUTDIR%\YSMViewer.ThumbnailProvider.dll" >nul
) else (
    echo [WARNING] C# DLL not found
    echo [HINT] Build and publish the C# project first with:
    echo   dotnet publish YSMViewer.ThumbnailProvider -c Release
    echo.
    echo Continuing without C# DLL ^(thumbnail provider will fail at runtime^).
)

echo.
echo Compiling C++ COM server...

cl.exe /nologo /EHsc /std:c++17 /LD /Fe:"%OUTDIR%\YSMViewer.ThumbnailProvider.Cpp.dll" ^
    /Fo:"%OUTDIR%\\" ^
    YsmThumbnailProvider.cpp DllMain.cpp ^
    /link /DEF:YsmThumbnailProvider.def ^
    shlwapi.lib gdi32.lib ole32.lib advapi32.lib user32.lib

if %errorLevel% equ 0 (
    echo.
    echo [OK] Build successful!
    echo Output: %OUTDIR%\YSMViewer.ThumbnailProvider.Cpp.dll
    if exist "%OUTDIR%\YSMViewer.ThumbnailProvider.dll" (
        echo C# AOT DLL: %OUTDIR%\YSMViewer.ThumbnailProvider.dll
    )
    echo.
    echo Both DLLs are native (no .NET runtime required).
    echo Run install.ps1 (as admin) to register the COM DLL.
) else (
    echo.
    echo [ERROR] Build failed!
    exit /b 1
)

pause
