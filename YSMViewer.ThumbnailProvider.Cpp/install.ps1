<#
.SYNOPSIS
    Installs and registers the YSMViewer Thumbnail Provider COM server.
.DESCRIPTION
    Copies the thumbnail provider DLLs to %APPDATA%\YSMViewer\Thumbnail
    and registers the COM server. Administrator privileges are required.
#>

$ErrorActionPreference = 'Stop'

$srcDir = Split-Path -Parent $PSCommandPath
$dstDir = "$env:APPDATA\YSMViewer\Thumbnail"

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "   YSMViewer Thumbnail Provider - Install" -ForegroundColor Cyan
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

# Required DLLs
$dlls = @("YSMViewer.ThumbnailProvider.dll", "YSMViewer.ThumbnailProvider.Cpp.dll")
$missing = $dlls | Where-Object { -not (Test-Path (Join-Path $srcDir $_)) }
if ($missing) {
    foreach ($dll in $missing) {
        Write-Host "[ERROR] $dll not found next to install.ps1" -ForegroundColor Red
    }
    Write-Host "[HINT] Ensure both DLLs are in the same folder as this script." -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

# Create destination
if (-not (Test-Path $dstDir)) {
    New-Item -ItemType Directory -Path $dstDir -Force | Out-Null
}

# Copy DLLs
Write-Host "Copying to $dstDir ..." -ForegroundColor Yellow
foreach ($dll in $dlls) {
    Copy-Item -Path (Join-Path $srcDir $dll) -Destination (Join-Path $dstDir $dll) -Force
    Write-Host "  [OK] $dll" -ForegroundColor Green
}

# Register COM server
Write-Host "Registering from $dstDir ..." -ForegroundColor Yellow
$cppDll = Join-Path $dstDir "YSMViewer.ThumbnailProvider.Cpp.dll"
$regsvr = "$env:SystemRoot\System32\regsvr32.exe"
$proc = Start-Process -FilePath $regsvr -ArgumentList @($cppDll) -Wait -NoNewWindow -PassThru
if ($proc.ExitCode -eq 0) {
    Write-Host "[OK] Registration successful! / 注册成功！" -ForegroundColor Green
    Write-Host "NOTE: Restart Explorer or log out for changes to take effect." -ForegroundColor Cyan
    Write-Host "提示：重启资源管理器或注销后生效。" -ForegroundColor Cyan
} else {
    Write-Host "[ERROR] Registration failed with exit code: $($proc.ExitCode)" -ForegroundColor Red
    Write-Host "[ERROR] 注册失败，退出代码: $($proc.ExitCode)" -ForegroundColor Red
}

Write-Host ""
Read-Host "Press Enter to exit"
