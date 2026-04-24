$ErrorActionPreference = 'Stop'

$packageArgs = @{
  packageName   = $env:ChocolateyPackageName
  fileType      = 'msi'
  url64bit      = "https://github.com/pHiR0/WireGuard-WinUserUI/releases/download/v$($env:ChocolateyPackageVersion)/WireGuard-WinUserUI-$($env:ChocolateyPackageVersion)-x64.msi" 
  checksum64    = 'f404acf06a59f19a6a64f501242100ac0f9772491e4c9678236727de294f8c5d'
  checksumType64= 'sha256'
  silentArgs    = '/quiet /norestart'
  validExitCodes= @(0, 3010)
}

Install-ChocolateyPackage @packageArgs
