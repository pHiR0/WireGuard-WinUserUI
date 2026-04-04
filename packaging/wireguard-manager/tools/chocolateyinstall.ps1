$ErrorActionPreference = 'Stop'

$packageArgs = @{
  packageName   = $env:ChocolateyPackageName
  fileType      = 'msi'
  url64bit      = 'https://github.com/pHiR0/WireGuard-WinUserUI/releases/download/v26.04.04.0310/WireGuard-WinUserUI-26.04.04.0310-x64.msi'  # URL del release; la versión del paquete Chocolatey (26.4.4.310) difiere por normalización NuGet
  checksum64    = 'ba0ffc7ceb015b9a220e7aa756f47659ef3f4d9ec3069a2ff7fe12d63df1e41f'
  checksumType64= 'sha256'
  silentArgs    = '/quiet /norestart'
  validExitCodes= @(0, 3010)
}

Install-ChocolateyPackage @packageArgs
