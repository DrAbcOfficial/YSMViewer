<#
.SYNOPSIS
    Unregisters and removes the YSMViewer Thumbnail Provider COM server.
.DESCRIPTION
    Unregisters the COM server and removes the DLLs from
    %APPDATA%\YSMViewer\Thumbnail. Administrator privileges are required.
#>

$ErrorActionPreference = 'Stop'

$dllDir = "$env:APPDATA\YSMViewer\Thumbnail"
$winDll = Join-Path $dllDir "YSMViewer.ThumbnailProvider.Win.dll"
$csDll = Join-Path $dllDir "YSMViewer.ThumbnailProvider.dll"

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "   YSMViewer Thumbnail Provider - Uninstall" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

# Admin check
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal $identity
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "[ERROR] Administrator privileges required!" -ForegroundColor Red
    Write-Host "[ERROR] 需要管理员权限！" -ForegroundColor Red
    Write-Host "Right-click this script and select 'Run with PowerShell' (as Admin)." -ForegroundColor Yellow
    Write-Host "右键点击此脚本，选择'使用 PowerShell 运行(管理员)'。" -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

# Check if installed
if (-not (Test-Path $winDll) -and -not (Test-Path $csDll)) {
    Write-Host "[WARN] No DLLs found in $dllDir" -ForegroundColor Yellow
    Write-Host "[WARN] Provider may not be installed." -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 0
}

# Unregister COM server
Write-Host "Unregistering COM server..." -ForegroundColor Yellow
if (Test-Path $winDll) {
    $regsvr = "$env:SystemRoot\System32\regsvr32.exe"
    $proc = Start-Process -FilePath $regsvr -ArgumentList @('/u', $winDll) -Wait -NoNewWindow -PassThru
    if ($proc.ExitCode -eq 0) {
        Write-Host "[OK] Unregistration successful! / 卸载成功！" -ForegroundColor Green
    } else {
        Write-Host "[ERROR] Unregistration failed with exit code: $($proc.ExitCode)" -ForegroundColor Red
        Write-Host "[ERROR] 卸载失败，退出代码: $($proc.ExitCode)" -ForegroundColor Red
    }
} else {
    Write-Host "[WARN] C++ DLL not found, skipping unregistration" -ForegroundColor Yellow
}

# Clean up files
Write-Host "Cleaning up $dllDir ..." -ForegroundColor Yellow
if (Test-Path $csDll) { Remove-Item -Path $csDll -Force }
if (Test-Path $winDll) { Remove-Item -Path $winDll -Force }
if (Test-Path $dllDir) { Remove-Item -Path $dllDir -Recurse -Force }

Write-Host "[OK] Cleanup complete." -ForegroundColor Green
Write-Host ""
Read-Host "Press Enter to exit"
