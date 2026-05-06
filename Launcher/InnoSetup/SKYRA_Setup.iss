; ═══════════════════════════════════════════════════════════════
; SKYRA ERP — Script Inno Setup (SKYRA_Setup.iss)
; Crée un installateur unique "SKYRA_Setup.exe"
; Ce script doit être compilé avec Inno Setup Compiler
; ═══════════════════════════════════════════════════════════════

#define MyAppName "SKYRA ERP"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Votre Société"
#define MyAppURL "https://sitevitrineerp.vercel.app/"
#define MyAppExeName "SkyraERP.exe"
#define MyBootstrapperExe "ERP.Bootstrapper.exe"
#define MyWatchdogExe "ERP.Watchdog.exe"
#define MyInstallDir "C:\SKYRA"

[Setup]
; ── Informations de base ─────────────────────────────────────
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; ── Dossier d'installation ───────────────────────────────────
DefaultDirName={#MyInstallDir}
DefaultGroupName={#MyAppName}
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
DisableProgramGroupPage=yes

; ── Sortie ────────────────────────────────────────────────────
OutputDir=output
OutputBaseFilename=SKYRA_Setup
SetupIconFile=Assets\skyra_icon.ico
Compression=lzma2/ultra64
SolidCompression=yes

; ── Droits Administrateur OBLIGATOIRES (pour Service Windows) 
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; ── Style et UX ──────────────────────────────────────────────
WizardStyle=modern
; WizardImageFile=Assets\installer_banner.bmp
; WizardSmallImageFile=Assets\installer_icon_small.bmp
SetupLogging=yes

; ── Redémarrage ───────────────────────────────────────────────
; JAMAIS de redémarrage forcé — on respecte l'utilisateur
AlwaysRestart=no
RestartIfNeededByRun=no

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
; Raccourci créé automatiquement par le Bootstrapper après installation

[Files]
; ── ERP ASP.NET Core (maintenu léger, binaires téléchargés au boot) ──
Source: "Assets\placeholder.txt"; DestDir: "{#MyInstallDir}\ERP"; Flags: ignoreversion

; ── Bootstrapper ─────────────────────────────────────────────
Source: "..\..\publish\bootstrapper\*"; DestDir: "{#MyInstallDir}\Installer"; Flags: ignoreversion recursesubdirs createallsubdirs

; ── Watchdog (Service Windows) ───────────────────────────────
Source: "..\..\publish\watchdog\*"; DestDir: "{#MyInstallDir}\Watchdog"; Flags: ignoreversion recursesubdirs createallsubdirs

; ── Ressources (Légères - Les ZIPs lourds sont exclus car téléchargés) ──
Source: "..\..\publish\Resources\*"; Excludes: "*.zip"; DestDir: "{#MyInstallDir}\Installer\Resources"; Flags: ignoreversion recursesubdirs createallsubdirs

; ── Version initiale ─────────────────────────────────────────
Source: "Assets\version.txt"; DestDir: "{#MyInstallDir}"; Flags: ignoreversion

[Icons]
; Raccourcis créés par le Bootstrapper après installation (SkyraERP.exe n'existe pas encore ici)

[Run]
; ── Étape 1 : Enregistrer le Watchdog comme Service Windows ──
; On stop + delete d'abord pour garantir que la nouvelle version est toujours enregistrée (réinstallation)
Filename: "sc.exe"; Parameters: "stop SKYRA_Watchdog"; Flags: runhidden
Filename: "sc.exe"; Parameters: "delete SKYRA_Watchdog"; Flags: runhidden

Filename: "sc.exe"; Parameters: "create SKYRA_Watchdog binPath=""{#MyInstallDir}\Watchdog\{#MyWatchdogExe}"" start=auto DisplayName=""SKYRA ERP Watchdog"""; \
  Flags: runhidden; StatusMsg: "Installation du service de surveillance..."

Filename: "sc.exe"; Parameters: "description SKYRA_Watchdog ""Service de surveillance et mise à jour automatique de SKYRA ERP"""; \
  Flags: runhidden

; ── Étape 2 : Démarrer le Watchdog ───────────────────────────
Filename: "sc.exe"; Parameters: "start SKYRA_Watchdog"; \
  Flags: runhidden; StatusMsg: "Démarrage du service de surveillance..."

; ── Étape 3 : Lancer le Bootstrapper via ShellExec (Pascal Script, voir [Code]) ──

[UninstallRun]
; ── Nettoyage propre à la désinstallation ────────────────────
Filename: "sc.exe"; Parameters: "stop SKYRA_Watchdog"; Flags: runhidden
Filename: "sc.exe"; Parameters: "delete SKYRA_Watchdog"; Flags: runhidden
Filename: "netsh.exe"; Parameters: "advfirewall firewall delete rule name=""SKYRA ERP"""; Flags: runhidden

[UninstallDelete]
; Supprimer tout le dossier d'installation (sauf la DB si l'utilisateur veut conserver ses données)
Type: filesandordirs; Name: "{#MyInstallDir}\Installer"
Type: filesandordirs; Name: "{#MyInstallDir}\Watchdog"
Type: filesandordirs; Name: "{#MyInstallDir}\*.exe"
Type: filesandordirs; Name: "{#MyInstallDir}\*.dll"
Type: files; Name: "{#MyInstallDir}\*.json"

[Code]
// Lance le Bootstrapper via ShellExec pour respecter son manifest requireAdministrator.
// [Run] utilise CreateProcess qui échoue avec le code 740 sur les exes requireAdministrator.
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssDone then
    ShellExec('', ExpandConstant('{#MyInstallDir}\Installer\{#MyBootstrapperExe}'),
              '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
end;
