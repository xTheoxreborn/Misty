//! Création / suppression du raccourci "Misty.lnk" vers l'exe courant.

use super::paths;
use std::path::Path;

const NOM_RACCOURCI: &str = "Misty.lnk";

#[cfg(windows)]
pub fn creer(dossier: &Path) -> anyhow::Result<()> {
    if dossier.as_os_str().is_empty() {
        return Ok(());
    }
    supprimer(dossier);

    let mut lnk = mslnk::ShellLink::new(paths::app_exe()).map_err(|e| anyhow::anyhow!("{e:?}"))?;
    lnk.set_working_dir(Some(paths::app_dir().to_string_lossy().to_string()));
    if paths::custom_icon().exists() {
        lnk.set_icon_location(Some(paths::custom_icon().to_string_lossy().to_string()));
    }
    lnk.create_lnk(dossier.join(NOM_RACCOURCI)).map_err(|e| anyhow::anyhow!("{e:?}"))?;
    Ok(())
}

#[cfg(not(windows))]
pub fn creer(_dossier: &Path) -> anyhow::Result<()> {
    Ok(())
}

pub fn supprimer(dossier: &Path) {
    if dossier.as_os_str().is_empty() {
        return;
    }
    let chemin = dossier.join(NOM_RACCOURCI);
    if chemin.exists() {
        let _ = std::fs::remove_file(chemin);
    }
}

/// Raccourcis créés par l'installateur (menu Démarrer, bureau) : s'ils visent encore l'ancien dossier
/// après une mise à jour, on les fait pointer vers l'exe actuel.
/// (Lecture de la cible via WScript.Shell, comme dans la version C#.)
#[cfg(windows)]
pub fn rediriger(ancien_dossier: &str) {
    use std::os::windows::process::CommandExt;
    const CREATE_NO_WINDOW: u32 = 0x0800_0000;

    let echapper = |s: &str| s.replace('\'', "''");
    let icone = if paths::custom_icon().exists() { paths::custom_icon() } else { paths::app_exe() };
    let script = format!(
        "$sh = New-Object -ComObject WScript.Shell; \
         foreach ($p in @([Environment]::GetFolderPath('Programs'), [Environment]::GetFolderPath('DesktopDirectory'))) {{ \
           $f = Join-Path $p 'Misty.lnk'; if (Test-Path $f) {{ $l = $sh.CreateShortcut($f); \
             if ($l.TargetPath -like '{ancien}*') {{ $l.TargetPath = '{exe}'; $l.WorkingDirectory = '{dir}'; $l.IconLocation = '{icone}'; $l.Save() }} }} }}",
        ancien = echapper(ancien_dossier),
        exe = echapper(&paths::app_exe().to_string_lossy()),
        dir = echapper(&paths::app_dir().to_string_lossy()),
        icone = echapper(&icone.to_string_lossy()),
    );
    let _ = std::process::Command::new("powershell")
        .args(["-NoProfile", "-NonInteractive", "-Command", &script])
        .creation_flags(CREATE_NO_WINDOW)
        .status();
}

#[cfg(not(windows))]
pub fn rediriger(_ancien_dossier: &str) {}
