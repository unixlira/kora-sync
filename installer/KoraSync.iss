; Instalador do KoraSync (Inno Setup) — gera um único setup.exe.
; Pré-requisito: publicar o app primeiro (ver README.md na raiz do repo):
;   dotnet publish src/KazakoraAgent.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
; Isso cria bin\Release\net8.0-windows\win-x64\publish\KazakoraAgent.App.exe
; (um único .exe, sem depender de .NET instalado na máquina de destino).

#define MyAppName "KoraSync"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Kazakora"
#define MyAppURL "https://devlira.com.br"
#define MyAppExeName "KazakoraAgent.App.exe"

[Setup]
AppId={{5A3E9B1C-6F2D-4B8A-9C1E-2D7F8A4B6E10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=dist
OutputBaseFilename=KoraSyncSetup
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
; Instala só pro usuário atual — não precisa admin (mesma filosofia da
; chave de "iniciar com o Windows" do app, que também é por usuário).
PrivilegesRequired=lowest
; Ícone do instalador em si (fundo branco quadrado) — deliberadamente
; diferente do ícone do app instalado (Assets\app.ico, fundo redondo),
; que continua vindo embutido no .exe publicado via ApplicationIcon.
SetupIconFile=..\assets\logo\kora_sync_installer.ico

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar um atalho na Área de Trabalho"; GroupDescription: "Atalhos adicionais:"
Name: "startupicon"; Description: "Iniciar o KoraSync junto com o Windows"; GroupDescription: "Inicialização:"; Flags: unchecked
; Não marcado por padrão de propósito: o próprio app já tem essa opção em
; Configurações (grava na mesma chave de Run do usuário) — ativar os dois
; ao mesmo tempo abriria duas instâncias no login. Use só um dos dois.

[Files]
Source: "..\src\KazakoraAgent.App\bin\Release\net8.0-windows\win-x64\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir o {#MyAppName} agora"; Flags: nowait postinstall skipifsilent
