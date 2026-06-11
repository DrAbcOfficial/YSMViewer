#Requires -RunAsAdministrator
param([switch]$Unregister)

$dllPath = Join-Path $PSScriptRoot "..\bin\Release\net10.0-windows\win-x64\publish\YSMViewer.ThumbnailProvider.comhost.dll"

if (-not (Test-Path $dllPath)) {
    $dllPath = Join-Path $PSScriptRoot "..\bin\Debug\net10.0-windows\win-x64\YSMViewer.ThumbnailProvider.comhost.dll"
}

if (-not (Test-Path $dllPath)) {
    Write-Error "comhost.dll not found. Build the project first: dotnet publish YSMViewer.ThumbnailProvider -c Release"
    exit 1
}

$clsid = "{F4E2C1A8-7B3D-4E5F-9A1C-2D8E6F0B4A3C}"
$progId = "YSMViewer.ThumbnailProvider"

if ($Unregister) {
    Write-Host "Unregistering .ysm thumbnail provider..."

    Remove-Item -Path "HKCR:\CLSID\$clsid" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "HKCR:\$progId" -Recurse -Force -ErrorAction SilentlyContinue

    $ysmKey = Get-Item -Path "HKCR:\.ysm" -ErrorAction SilentlyContinue
    if ($ysmKey) {
        $shellexKey = Get-Item -Path "HKCR:\.ysm\ShellEx" -ErrorAction SilentlyContinue
        if ($shellexKey) {
            Remove-Item -Path "HKCR:\.ysm\ShellEx\{E357FCCD-A995-4576-B01F-234630154E96}" -Force -ErrorAction SilentlyContinue
        }
    }

    Write-Host "Unregistered."
} else {
    Write-Host "Registering .ysm thumbnail provider..."

    New-Item -Path "HKCR:\CLSID\$clsid" -Force | Out-Null
    Set-ItemProperty -Path "HKCR:\CLSID\$clsid" -Name "(Default)" -Value $progId
    New-Item -Path "HKCR:\CLSID\$clsid\InprocServer32" -Force | Out-Null
    Set-ItemProperty -Path "HKCR:\CLSID\$clsid\InprocServer32" -Name "(Default)" -Value $dllPath
    Set-ItemProperty -Path "HKCR:\CLSID\$clsid\InprocServer32" -Name "ThreadingModel" -Value "Both"

    New-Item -Path "HKCR:\$progId" -Force | Out-Null
    New-Item -Path "HKCR:\$progId\CLSID" -Force | Out-Null
    Set-ItemProperty -Path "HKCR:\$progId\CLSID" -Name "(Default)" -Value $clsid

    New-Item -Path "HKCR:\.ysm" -Force | Out-Null
    New-Item -Path "HKCR:\.ysm\ShellEx" -Force | Out-Null
    New-Item -Path "HKCR:\.ysm\ShellEx\{E357FCCD-A995-4576-B01F-234630154E96}" -Force | Out-Null
    Set-ItemProperty -Path "HKCR:\.ysm\ShellEx\{E357FCCD-A995-4576-B01F-234630154E96}" -Name "(Default)" -Value $clsid

    Write-Host "Registered. Restart explorer.exe to apply."
}

Write-Host "Done."
