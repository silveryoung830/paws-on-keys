[CmdletBinding()]
param([switch]$Test)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$frameworkBase = Join-Path $env:WINDIR 'Microsoft.NET'
$framework = Join-Path $frameworkBase 'Framework64\v4.0.30319'
if (!(Test-Path -LiteralPath (Join-Path $framework 'csc.exe'))) {
    $framework = Join-Path $frameworkBase 'Framework\v4.0.30319'
}
$csc = Join-Path $framework 'csc.exe'
if (!(Test-Path -LiteralPath $csc)) { throw 'Windows with .NET Framework 4.8 is required.' }
$dest = Join-Path $root 'build'
[IO.Directory]::CreateDirectory($dest) | Out-Null
$options = @('/nologo', '/langversion:5', '/codepage:65001', '/warn:4', '/warnaserror+', '/optimize+', '/platform:anycpu')
$refs = @('System.dll', 'System.Core.dll', 'System.Xml.dll', 'System.Xaml.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll',
    'WPF\PresentationCore.dll', 'WPF\PresentationFramework.dll', 'WPF\WindowsBase.dll')
foreach ($assembly in $refs) { $options += '/reference:' + (Join-Path $framework $assembly) }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | Sort-Object Name | ForEach-Object { $_.FullName })
$app = Join-Path $dest 'PawsOnKeys.exe'
$iconFile = Join-Path $root 'src\paws.ico'
$manifest = Join-Path $root 'src\app.manifest'
& $csc @options '/target:winexe' '/main:PawsOnKeys.Program' "/win32manifest:$manifest" "/win32icon:$iconFile" "/out:$app" @sources
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }
Write-Host "Built $app"
if ($Test) {
    $exe = Join-Path $dest 'SmokeTests.exe'
    & $csc @options '/target:exe' '/main:PawsOnKeys.SmokeTests' "/out:$exe" @sources (Join-Path $root 'tests\SmokeTests.cs')
    if ($LASTEXITCODE -ne 0) { throw "Test build failed ($LASTEXITCODE)." }
    & $exe
    if ($LASTEXITCODE -ne 0) { throw "Smoke tests failed ($LASTEXITCODE)." }
}
