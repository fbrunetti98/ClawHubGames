# Roadmap

## Milestone 1 — Fondamenta ✅
- Struttura della soluzione, CI e release automatiche da tag
- Lettura dei giochi installati: Steam, Epic, GOG, Xbox/Game Pass, Ubisoft Connect
- EA app, Battle.net e Rockstar Games (sperimentali: da verificare su dispositivi reali)
- Giochi aggiunti a mano (eseguibili, emulatori)
- Preferiti, collezioni personalizzate, giochi nascosti, ricerca, raggruppamento per store
- Copertine: cache di Steam / CDN Steam, SteamGridDB con chiave dell'utente
- Italiano e inglese, cambio lingua senza riavvio
- App minima: griglia, ricerca, avvio

## Milestone 2 — Interfaccia "da divano"
- Design definitivo ispirato a Big Picture (tema scuro, copertine grandi, righe orizzontali, sfondo dinamico)
- Navigazione completa con il controller (Windows.Gaming.Input)
- Home: giocati di recente, preferiti, collezioni, store
- Pagina di dettaglio del gioco (hero, logo, ultima partita, azioni)
- Gestione collezioni e aggiunta manuale dei giochi dall'interfaccia
- Impostazioni: lingua, chiave SteamGridDB, store attivi, giochi nascosti
- Download delle copertine in background

## Milestone 3 — Integrazione con il sistema
- Istanza singola: riaprire l'eseguibile (es. dal tasto MSI tramite ClawTweaks) riporta in primo piano il launcher
- Resta in background mentre si gioca e torna in primo piano alla chiusura del gioco
- Avvio come home della modalità a schermo intero di Windows tramite AnyFSE (eseguibile personalizzato)
- Installer

## Milestone 4 — Home nativa della modalità a schermo intero
- Pacchetto MSIX con estensione `windows.gamingApp` e capability `gamingHome` (approccio di AnyFSE, licenza MIT)

## Fase 2 — Giochi posseduti non installati
- Login agli store (Steam Web API, Epic, GOG…) e installazione dal launcher
