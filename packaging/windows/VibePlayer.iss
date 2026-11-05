; Instalador do VibePlayer para Windows (spec 0009). Compile com build-installer.ps1, que passa a
; versão e a pasta do publish: sem elas o compilador falha, de propósito, para nenhuma versão ficar
; escrita aqui.
;
; A instalação é só para o usuário atual, porque os dados também são: o desinstalador alcança apenas
; a pasta de dados de quem o rodou, e oferecer os dois modos deixaria duas cópias convivendo.

#ifndef AppVersion
  #error Informe /DAppVersion
#endif
#ifndef AppNumericVersion
  #error Informe /DAppNumericVersion
#endif
#ifndef SourceDir
  #error Informe /DSourceDir
#endif
#ifndef Compression
  #define Compression "lzma2"
#endif

; Permanente: é o que faz uma versão nova reconhecer a anterior. Trocar cria uma segunda instalação.
#define AppGuid "F80C533D-B789-4A17-8444-56057846BE4C"

[Setup]
AppId={{{#AppGuid}}
AppName=VibePlayer
AppVersion={#AppVersion}
AppVerName=VibePlayer {#AppVersion}
AppPublisher=Daniel Vieira Fernandes
AppPublisherURL=https://github.com/DanielVieiraFernandes/VibePlayer
AppCopyright=Copyright © 2026 Daniel Vieira Fernandes
VersionInfoVersion={#AppNumericVersion}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
; Com PrivilegesRequired=lowest, {autopf} é %LOCALAPPDATA%\Programs.
DefaultDirName={autopf}\VibePlayer
; Garante uma subpasta própria mesmo que o usuário escolha outro lugar.
AppendDefaultDirName=yes
DisableProgramGroupPage=yes
LicenseFile={#SourceDir}\LICENSE
UninstallDisplayName=VibePlayer
UninstallDisplayIcon={app}\VibePlayer.exe
WizardStyle=modern
Compression={#Compression}
SolidCompression=yes
; O app não tem mutex; quem o encontra aberto é o Restart Manager do Windows. O processo de
; transcrição não tem janela, mas sai sozinho quando o app fecha a entrada padrão dele.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[InstallDelete]
; Ao atualizar, um plugin do VLC retirado da lista de permissão ficaria no disco e seria carregado.
; Só as pastas do próprio app: a pasta de instalação pode ter sido escolhida pelo usuário.
Type: filesandordirs; Name: "{app}\libvlc"
Type: filesandordirs; Name: "{app}\runtimes"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\VibePlayer"; Filename: "{app}\VibePlayer.exe"
Name: "{autodesktop}\VibePlayer"; Filename: "{app}\VibePlayer.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\VibePlayer.exe"; Description: "{cm:LaunchProgram,VibePlayer}"; Flags: nowait postinstall skipifsilent

[CustomMessages]
brazilianportuguese.NewerVersionInstalled=O VibePlayer %1 já está instalado, e este instalador é da versão {#AppVersion}.%n%nInstalar uma versão mais antiga por cima pode deixar seus dados inacessíveis, porque o banco de dados já foi atualizado. Baixe a versão mais recente, ou desinstale a atual antes.
brazilianportuguese.DeleteUserData=Apagar também os dados do VibePlayer?%n%n%1%n%nIsso remove a biblioteca, o progresso das aulas, as transcrições, os backups e os modelos baixados, e não pode ser desfeito. Seus vídeos não são tocados.%n%nSe pretende instalar o VibePlayer de novo, responda Não: tudo estará lá.

[Code]
const
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{' + '{#AppGuid}' + '}_is1';

{ Tira o sufixo de pré-lançamento, que StrToVersion não entende. }
function NumericPart(Version: String): String;
var
  Dash: Integer;
begin
  Dash := Pos('-', Version);
  if Dash > 0 then
    Result := Copy(Version, 1, Dash - 1)
  else
    Result := Version;
end;

{ Recusa versão mais antiga: o banco já migrado não abre com segurança no app anterior. A mesma
  versão pode ser reinstalada, para reparar. Só compara números: pré-lançamentos não são publicados
  para usuários, e sair de um deles para a release de mesmo número deve funcionar. }
function InitializeSetup(): Boolean;
var
  Installed: String;
  InstalledVersion, ThisVersion: Int64;
begin
  Result := True;
  if RegQueryStringValue(HKCU, UninstallKey, 'DisplayVersion', Installed) and
     StrToVersion(NumericPart(Installed), InstalledVersion) and
     StrToVersion('{#AppNumericVersion}', ThisVersion) and
     (ComparePackedVersion(InstalledVersion, ThisVersion) > 0) then
  begin
    SuppressibleMsgBox(FmtMessage(CustomMessage('NewerVersionInstalled'), [Installed]), mbError, MB_OK, IDOK);
    Result := False;
  end;
end;

{ Só quem está na frente da tela decide apagar os dados: em modo silencioso, um script de
  implantação não respondeu a nada. Ao atualizar, o Inno instala por cima sem passar por aqui. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent then
  begin
    DataDir := ExpandConstant('{userappdata}\VibePlayer');
    if DirExists(DataDir) and
       (MsgBox(FmtMessage(CustomMessage('DeleteUserData'), [DataDir]), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES) then
      DelTree(DataDir, True, True, True);
  end;
end;
