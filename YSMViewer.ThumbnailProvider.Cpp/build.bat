@echo off
echo ==============================================
echo   YSMViewer Thumbnail Provider C++ Build
echo ==============================================
echo.
echo Make sure to run this from a Visual Studio
echo Developer Command Prompt (x64 Native Tools).
echo.

:: Copy C# DLL to output directory first
set "OUTDIR=%~dp0build"
set "CS_DLL=%~dp0..\YSMViewer.ThumbnailProvider\bin\Release\net10.0-windows\win-x64\publish\YSMViewer.ThumbnailProvider.dll"

if not exist "%OUTDIR%" mkdir "%OUTDIR%"

if exist "%CS_DLL%" (
    echo Copying C# DLL: %CS_DLL%
    copy /Y "%CS_DLL%" "%OUTDIR%\YSMViewer.ThumbnailProvider.dll" >nul
) else (
    echo [WARNING] C# DLL not found. Build it first with:
    echo   dotnet publish YSMViewer.ThumbnailProvider -c Release -r win-x64
)

echo.
echo Compiling...

cl.exe /nologo /EHsc /std:c++17 /LD /Fe:"%OUTDIR%\YSMViewer.ThumbnailProvider.Cpp.dll" ^
    /Fo:"%OUTDIR%\\" ^
    YsmThumbnailProvider.cpp DllMain.cpp ^
    /link /DEF:YsmThumbnailProvider.def ^
    shlwapi.lib gdi32.lib ole32.lib advapi32.lib user32.lib

if %errorLevel% equ 0 (
    echo.
    echo [OK] Build successful!
    echo Output: %OUTDIR%\YSMViewer.ThumbnailProvider.Cpp.dll
    echo.
    echo C# DLL and C++ DLL are both in the build\ directory.
    echo Run install.bat (as admin) to register the COM DLL.
) else (
    echo.
    echo [ERROR] Build failed!
)

pause
