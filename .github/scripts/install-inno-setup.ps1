# Instala a versão fixada do Inno Setup e devolve o caminho do compilador.
#
# Fixada, com hash, em vez de usar a da imagem do runner: aquela muda sem aviso e pode sair da
# imagem, e o mesmo commit deve gerar o mesmo instalador. Mesmo critério do modelo do Whisper:
# um arquivo trocado no servidor falha aqui.
#
# Ao atualizar a versão, releia a licença e atualize o design da spec 0009.
#
# Uso: install-inno-setup.ps1 -Destination <pasta>   (imprime o caminho do ISCC.exe)
param(
    [Parameter(Mandatory)] [string] $Destination
)

$ErrorActionPreference = 'Stop'

$url = 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_1/innosetup-7.1.1-x64.exe'
$expected = '8a5a6711c3766a6e5fb74f280ba0e3551e719ce94538d901fdbd1f8f9920b3f0'
$installer = Join-Path ([System.IO.Path]::GetTempPath()) 'innosetup.exe'

Invoke-WebRequest -Uri $url -OutFile $installer -MaximumRetryCount 3

$actual = (Get-FileHash -Algorithm SHA256 $installer).Hash.ToLowerInvariant()
if ($actual -ne $expected) {
    Remove-Item $installer
    throw "Hash inesperado no instalador do Inno Setup: $actual"
}

$process = Start-Process -FilePath $installer -Wait -PassThru -ArgumentList @(
    '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', '/NOICONS', "/DIR=`"$Destination`"")
if ($process.ExitCode -ne 0) {
    throw "O instalador do Inno Setup saiu com o código $($process.ExitCode)."
}

Join-Path $Destination 'ISCC.exe'
