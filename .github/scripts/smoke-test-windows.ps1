# Testa o instalador do Windows de ponta a ponta, em silêncio. Só roda em runner descartável: instala
# no perfil do usuário e cria dados de verdade.
#
# Para não depender de uma release anterior, gera um instalador da versão 0.0.1 com os mesmos
# arquivos. Assim, a atualização e a recusa de versão antiga são testadas em toda execução, e não só
# depois que já houver um instalador publicado.
#
# Uso: smoke-test-windows.ps1 -Compiler <ISCC.exe> -Publish <pasta> -Installer <setup.exe>
#                             -Version <versão> -Model <modelo> -Fixture <áudio> -Logs <pasta>
param(
    [Parameter(Mandatory)] [string] $Compiler,
    [Parameter(Mandatory)] [string] $Publish,
    [Parameter(Mandatory)] [string] $Installer,
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $Model,
    [Parameter(Mandatory)] [string] $Fixture,
    [Parameter(Mandatory)] [string] $Logs
)

$ErrorActionPreference = 'Stop'

$appDir = Join-Path $env:LOCALAPPDATA 'Programs\VibePlayer'
$exe = Join-Path $appDir 'VibePlayer.exe'
$dataDir = Join-Path $env:APPDATA 'VibePlayer'
$marker = Join-Path $dataDir 'marcador-da-ci.txt'
$staleFile = Join-Path $appDir 'libvlc\arquivo-da-versao-antiga.txt'
$shortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\VibePlayer.lnk'
$oldVersion = '0.0.1'

New-Item -ItemType Directory -Force $Logs | Out-Null

function Assert-That([bool] $condition, [string] $message) {
    if (-not $condition) {
        throw $message
    }
}

function Invoke-Setup([string] $path, [string] $logName) {
    $log = Join-Path $Logs $logName
    $process = Start-Process -FilePath $path -Wait -PassThru -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CLOSEAPPLICATIONS', "/LOG=`"$log`"")
    return $process.ExitCode
}

function Get-InstalledVersion {
    $keys = Get-ChildItem 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall' -ErrorAction SilentlyContinue
    foreach ($key in $keys) {
        if ((Get-ItemProperty $key.PSPath).DisplayName -eq 'VibePlayer') {
            return (Get-ItemProperty $key.PSPath).DisplayVersion
        }
    }

    return $null
}

function Start-App {
    $process = Start-Process -FilePath $exe -PassThru
    Start-Sleep -Seconds 15
    Assert-That (-not $process.HasExited) "O app fechou sozinho com o código $($process.ExitCode)."
    return $process
}

Write-Host "Gerando o instalador da versão $oldVersion"
$oldOutput = Join-Path ([System.IO.Path]::GetTempPath()) 'instalador-antigo'
& (Join-Path $PSScriptRoot '..\..\packaging\windows\build-installer.ps1') `
    -Compiler $Compiler -Source $Publish -Output $oldOutput -Version $oldVersion -NumericVersion $oldVersion -Fast | Out-Null
$oldInstaller = Join-Path $oldOutput "VibePlayer-$oldVersion-win-x64-setup.exe"

Write-Host 'Instalando a versão antiga'
Assert-That ((Invoke-Setup $oldInstaller 'instalar-antigo.log') -eq 0) 'A instalação da versão antiga falhou.'
Assert-That ((Get-InstalledVersion) -eq $oldVersion) 'A versão antiga não ficou registrada.'
Assert-That (Test-Path $shortcut) 'O atalho do menu Iniciar não foi criado.'

Write-Host 'Abrindo a janela da versão antiga'
$running = Start-App

Set-Content -Path $staleFile -Value 'sobra da versão antiga'
Set-Content -Path $marker -Value 'dados do usuário'

Write-Host 'Atualizando com o app aberto'
Assert-That ((Invoke-Setup $Installer 'atualizar.log') -eq 0) 'A atualização falhou.'
Assert-That ($running.WaitForExit(30000)) 'A atualização não fechou o app aberto.'
Assert-That ((Get-InstalledVersion) -eq $Version) "A versão registrada não é $Version."
Assert-That (-not (Test-Path $staleFile)) 'Sobrou arquivo da versão antiga em libvlc.'
Assert-That (Test-Path $marker) 'A atualização mexeu na pasta de dados.'

Write-Host 'Conferindo os avisos de licença'
foreach ($notice in 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'licenses\LGPL-2.1.txt') {
    Assert-That (Test-Path (Join-Path $appDir $notice)) "Faltou $notice na instalação."
}

Write-Host 'Transcrevendo com o executável instalado'
& (Join-Path $PSScriptRoot 'smoke-transcription.ps1') -Executable $exe -Fixture $Fixture -Model $Model

Write-Host 'Abrindo a janela da versão nova'
$running = Start-App
$running.Kill($true)
$running.WaitForExit()

Write-Host 'Tentando instalar a versão antiga por cima'
Assert-That ((Invoke-Setup $oldInstaller 'recusar-antigo.log') -ne 0) 'A versão antiga foi aceita por cima da nova.'
Assert-That ((Get-InstalledVersion) -eq $Version) 'A tentativa recusada mudou a versão instalada.'

Write-Host 'Desinstalando em silêncio'
$uninstaller = Join-Path $appDir 'unins000.exe'
Start-Process -FilePath $uninstaller -Wait -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')

# O desinstalador se copia para a pasta temporária para conseguir apagar a própria pasta, então
# o processo aguardado acima pode terminar antes do trabalho.
$deadline = [DateTime]::UtcNow.AddSeconds(60)
while ((Test-Path $exe) -and [DateTime]::UtcNow -lt $deadline) {
    Start-Sleep -Seconds 1
}

Assert-That (-not (Test-Path $exe)) 'A desinstalação não removeu o app.'
Assert-That ($null -eq (Get-InstalledVersion)) 'A entrada em Aplicativos instalados continuou lá.'
Assert-That (-not (Test-Path $shortcut)) 'A desinstalação não removeu o atalho.'
Assert-That (Test-Path $marker) 'A desinstalação silenciosa apagou a pasta de dados.'

Write-Host 'Instalador conferido.'
