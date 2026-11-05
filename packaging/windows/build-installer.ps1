# Gera o instalador do Windows a partir da saída do dotnet publish.
#
# Uso: build-installer.ps1 -Compiler <ISCC.exe> -Source <pasta do publish> -Output <pasta>
#                          -Version <versão> -NumericVersion <X.Y.Z> [-Fast]
#
# -Fast não comprime: serve para o instalador de versão antiga do teste, que só precisa existir.
param(
    [Parameter(Mandatory)] [string] $Compiler,
    [Parameter(Mandatory)] [string] $Source,
    [Parameter(Mandatory)] [string] $Output,
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $NumericVersion,
    [switch] $Fast
)

$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Force $Output | Out-Null

$arguments = @(
    "/DAppVersion=$Version",
    "/DAppNumericVersion=$NumericVersion",
    "/DSourceDir=$((Resolve-Path $Source).Path)",
    "/DCompression=$(if ($Fast) { 'none' } else { 'lzma2' })",
    "/O$((Resolve-Path $Output).Path)",
    "/FVibePlayer-$Version-win-x64-setup",
    (Join-Path $PSScriptRoot 'VibePlayer.iss'))

& $Compiler @arguments
if ($LASTEXITCODE -ne 0) {
    throw "O ISCC saiu com o código $LASTEXITCODE."
}
