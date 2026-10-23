; Installateur Windows de Misty (Inno Setup 6.3+).
; Ne pas compiler à la main : lancer installer\build.ps1, qui publie l'app puis appelle ISCC avec la bonne version.
;
; Structure installée (compatible avec la mise à jour automatique, voir UpdateService) :
;   %LOCALAPPDATA%\Programs\Misty\Misty-<version>\Misty.exe
; Pas de droits administrateur nécessaires. Les données (versions, mondes, profils) restent dans %LOCALAPPDATA%\Misty.

#ifndef Version
  #error "Version manquante : lancer installer\build.ps1"
#endif
#ifndef Source
  #define Source "..\publish\Misty-" + Version
#endif
#ifndef Sortie
  #define Sortie "..\publish"
#endif

#define Dossier "Misty-" + Version

[Setup]
; Ne jamais changer cet ID : c'est lui qui relie les versions entre elles (et UpdateService.CleDesinstallation)
AppId={{8F4C2A61-3B7E-4D19-9A55-6C1E0F2B7D43}
AppName=Misty
AppVersion={#Version}
AppVerName=Misty {#Version}
AppPublisher=xTheoxreborn
AppPublisherURL=https://github.com/xTheoxreborn/Misty
AppSupportURL=https://github.com/xTheoxreborn/Misty/issues
AppUpdatesURL=https://github.com/xTheoxreborn/Misty/releases
VersionInfoVersion={#Version}
DefaultDirName={localappdata}\Programs\Misty
DisableDirPage=auto
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#Sortie}
OutputBaseFilename=Misty-Setup-{#Version}
SetupIconFile=..\src\Misty\Assets\icone.ico
UninstallDisplayIcon={app}\{#Dossier}\Misty.exe
UninstallDisplayName=Misty
UninstallFilesDir={app}\uninstall
LicenseFile=..\LICENSE.txt
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
fr.DotNetTitre=Installation de .NET 10
fr.DotNetEchec=L'installation du runtime .NET 10 (Windows Desktop) a échoué.%n%nInstalle-le depuis https://dotnet.microsoft.com/download/dotnet/10.0 puis relance l'installation de Misty.
fr.SupprimerDonnees=Supprimer aussi les données de Misty (versions de Minecraft, profils, mondes, packs de textures) ?%n%nChoisis « Non » pour les garder en cas de réinstallation.
fr.Lancer=Lancer Misty
en.DotNetTitre=Installing .NET 10
en.DotNetEchec=Installing the .NET 10 runtime (Windows Desktop) failed.%n%nInstall it from https://dotnet.microsoft.com/download/dotnet/10.0 then run the Misty setup again.
en.SupprimerDonnees=Also delete Misty's data (Minecraft versions, profiles, worlds, texture packs)?%n%nChoose "No" to keep them for a later reinstall.
en.Lancer=Launch Misty

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[InstallDelete]
; Versions précédentes (installées par le setup ou par la mise à jour automatique)
Type: filesandordirs; Name: "{app}\Misty-*"

[Files]
Source: "{#Source}\*"; DestDir: "{app}\{#Dossier}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Misty"; Filename: "{app}\{#Dossier}\Misty.exe"; WorkingDir: "{app}\{#Dossier}"
Name: "{autodesktop}\Misty"; Filename: "{app}\{#Dossier}\Misty.exe"; WorkingDir: "{app}\{#Dossier}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#Dossier}\Misty.exe"; Description: "{cm:Lancer}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Les mises à jour automatiques créent des dossiers Misty-<version> inconnus de l'installateur
Type: filesandordirs; Name: "{app}"

[Code]
var
  PageDotNet: TDownloadWizardPage;

{ Le runtime Windows Desktop 10 est-il présent ? (Misty est publié en "framework-dependent") }
function DotNetInstalle: Boolean;
var
  Recherche: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), Recherche);
  if Result then
    FindClose(Recherche);
end;

procedure InitializeWizard;
begin
  PageDotNet := CreateDownloadPage(CustomMessage('DotNetTitre'), SetupMessage(msgPreparingDesc), nil);
end;

{ Juste avant l'installation : téléchargement + installation silencieuse de .NET si besoin }
function NextButtonClick(CurPageID: Integer): Boolean;
var
  Code: Integer;
begin
  Result := True;
  if (CurPageID <> wpReady) or DotNetInstalle then
    Exit;

  PageDotNet.Clear;
  PageDotNet.Add('https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe', 'windowsdesktop-runtime-win-x64.exe', '');
  PageDotNet.Show;
  try
    try
      PageDotNet.Download;
      { ShellExec : l'installateur de .NET demande lui-même l'élévation (UAC) }
      if not ShellExec('', ExpandConstant('{tmp}\windowsdesktop-runtime-win-x64.exe'), '/install /quiet /norestart', '',
                       SW_SHOW, ewWaitUntilTerminated, Code)
         or ((Code <> 0) and (Code <> 1638) and (Code <> 3010)) then
      begin
        SuppressibleMsgBox(CustomMessage('DotNetEchec'), mbError, MB_OK, IDOK);
        Result := False;
      end;
    except
      if not PageDotNet.AbortedByUser then
        SuppressibleMsgBox(AddPeriod(GetExceptionMessage) + #13#10#13#10 + CustomMessage('DotNetEchec'), mbCriticalError, MB_OK, IDOK);
      Result := False;
    end;
  finally
    PageDotNet.Hide;
  end;
end;

{ Désinstallation : les données de jeu ne sont supprimées que si l'utilisateur le demande }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent then
    if MsgBox(CustomMessage('SupprimerDonnees'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      DelTree(ExpandConstant('{localappdata}\Misty'), True, True, True);
end;
