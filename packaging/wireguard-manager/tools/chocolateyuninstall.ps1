$ErrorActionPreference = 'Stop'

$packageArgs = @{
  packageName   = $env:ChocolateyPackageName
  softwareName  = 'WireGuard Manager*'
  fileType      = 'msi'
  validExitCodes= @(0, 3010)
}

[array]$key = Get-UninstallRegistryKey -SoftwareName $packageArgs['softwareName']

if ($key.Count -eq 1) {
  $key | ForEach-Object {
    # Para MSI: PSChildName es el GUID del producto ({XXXX-...})
    # msiexec /x {GUID} /qn /norestart
    $packageArgs['silentArgs'] = "$($_.PSChildName) /qn /norestart"
    Uninstall-ChocolateyPackage @packageArgs
  }
} elseif ($key.Count -eq 0) {
  Write-Warning "$($packageArgs['packageName']) has already been uninstalled by other means."
} elseif ($key.Count -gt 1) {
  Write-Warning "$($key.Count) matches found!"
  Write-Warning "To prevent accidental removal, uninstall manually."
}
