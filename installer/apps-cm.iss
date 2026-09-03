; Script do Inno Setup para o apps-cm. Compilado pelo Release.ps1 via ISCC.exe, apontando direto
; para a pasta publish\ que o "dotnet publish" já gera — não empacota nada além disso.
;
; AppId é fixo para sempre: é o que faz o Inno reconhecer uma instalação de versão mais nova como
; upgrade da mesma instalação (em vez de criar uma segunda entrada em Adicionar/Remover Programas).
; Não gerar um novo GUID depois — se precisar recriar o instalador do zero, reusar este mesmo valor.
#define MyAppName "apps-cm"
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

[Setup]
AppId={{BA5E16B3-1382-41EC-8849-595BF4194E70}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Time interno
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
OutputDir=..
OutputBaseFilename=apps-cm-setup
SetupIconFile=..\src\AppsCm.Shell\favicon.ico
UninstallDisplayIcon={app}\AppsCm.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; TODO: SignTool=... / SignedUninstaller=yes quando houver certificado de assinatura de código.

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\AppsCm.exe"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\AppsCm.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos adicionais"; Flags: unchecked

[Run]
Filename: "{app}\AppsCm.exe"; Description: "Abrir apps-cm"; Flags: nowait postinstall skipifsilent

; {app} não guarda dados do usuário (logs e config ficam em %LocalAppData%\apps-cm\, fora daqui);
; seguro limpar tudo no desinstalar, inclusive arquivos que o Updater tenha copiado depois da
; instalação original e que o instalador em si nunca rastreou.
[UninstallDelete]
Type: filesandordirs; Name: "{app}"
