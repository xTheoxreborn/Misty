//! Statut Discord (Rich Presence).
//!
//! Un seul client pour toute l'application (plusieurs clients avec le même ID se volent la connexion).
//! Il vit dans son propre thread : Discord peut être lent ou fermé, l'interface ne doit jamais attendre.

use super::{app_info, loc, settings};
use discord_rich_presence::{activity, DiscordIpc, DiscordIpcClient};
use std::sync::mpsc::{self, Sender};
use std::sync::{Mutex, OnceLock};

/// Statut demandé (le texte est traduit au moment de l'envoi).
#[derive(Clone)]
pub enum Statut {
    Launcher,
    PackEditeur,
    EnJeu { version: String, mode: String },
}

enum Message {
    Definir(Statut),
    Reappliquer,
    Arreter,
}

fn canal() -> &'static Mutex<Sender<Message>> {
    static CANAL: OnceLock<Mutex<Sender<Message>>> = OnceLock::new();
    CANAL.get_or_init(|| {
        let (tx, rx) = mpsc::channel::<Message>();
        std::thread::spawn(move || boucle(rx));
        Mutex::new(tx)
    })
}

/// Option "Rich Presence Discord" (activée par défaut).
pub fn active() -> bool {
    settings::get_bool(settings::RICH_PRESENCE, true)
}

pub fn definir(statut: Statut) {
    let _ = canal().lock().unwrap().send(Message::Definir(statut));
}

/// À appeler quand l'option ou la langue change.
pub fn reappliquer() {
    let _ = canal().lock().unwrap().send(Message::Reappliquer);
}

pub fn arreter() {
    let _ = canal().lock().unwrap().send(Message::Arreter);
}

fn boucle(rx: mpsc::Receiver<Message>) {
    let debut = chrono::Utc::now().timestamp();
    let mut client: Option<DiscordIpcClient> = None;
    let mut actuel: Option<Statut> = None;

    while let Ok(message) = rx.recv() {
        match message {
            Message::Definir(s) => actuel = Some(s),
            Message::Reappliquer => {}
            Message::Arreter => {
                if let Some(c) = client.as_mut() {
                    let _ = c.close();
                }
                return;
            }
        }

        // Connexion à la demande (Discord a pu être lancé après Misty)
        if client.is_none() {
            if let Ok(mut c) = DiscordIpcClient::new(app_info::DISCORD_APP_ID) {
                if c.connect().is_ok() {
                    client = Some(c);
                }
            }
        }
        let Some(c) = client.as_mut() else { continue };

        let resultat = match (&actuel, active()) {
            (Some(statut), true) => {
                let (details, state) = match statut {
                    Statut::Launcher => (None, loc::t("Discord_Launcher")),
                    Statut::PackEditeur => (None, loc::t("Discord_Pack")),
                    Statut::EnJeu { version, mode } => (Some(loc::t("Discord_Jeu")), format!("{version} {mode}")),
                };
                let mut a = activity::Activity::new().state(&state).timestamps(activity::Timestamps::new().start(debut));
                if let Some(d) = details.as_deref() {
                    a = a.details(d);
                }
                c.set_activity(a)
            }
            _ => c.clear_activity(),
        };
        // Discord fermé entre-temps : on se reconnectera au prochain message
        if resultat.is_err() {
            client = None;
        }
    }
}
