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
Layout scelto: barra laterale con le sezioni + griglia di copertine a 5 colonne, accento ciano, apertura su "Tutti i giochi".

**2a** (v0.2.0-alpha.1)
- Schermo intero senza bordi (F11 per la finestra)
- Barra laterale: tutti i giochi, continua a giocare, preferiti, collezioni, store
- Griglia di copertine, sfondo del gioco selezionato in dissolvenza
- Navigazione completa con controller (Windows.Gaming.Input) e tastiera; legenda dei tasti
- Schede Libreria / Impostazioni con LB/RB; impostazioni minime: aggiorna libreria, lingua, schermo intero, esci
- Download delle copertine e degli sfondi in background (cache di Steam, CDN, SteamGridDB)

**2b**
- Pagina di dettaglio a tutto schermo: immagine orizzontale con logo, pulsanti, informazioni
  (store, ultima partita, avvii dal launcher, collezioni, cartella)
- Azioni: gioca, preferito, collezioni (aggiungi/togli, nuova collezione), nascondi/mostra
- Sezione "Nascosti" nella barra laterale per far ricomparire i giochi nascosti

**2c**
- Impostazioni complete: chiave SteamGridDB, store attivi, giochi nascosti
- Rinomina ed eliminazione delle collezioni, aggiunta manuale dei giochi, tastiera a schermo
- Ricerca (tornerà con la tastiera a schermo)

Rimandato: installer con aggiornamento automatico dalle release di GitHub (proposta: Velopack).

## Milestone 3 — Integrazione con il sistema
- Tempo di gioco per tutti gli store (il launcher segue il processo del gioco)
- Istanza singola: riaprire l'eseguibile (es. dal tasto MSI tramite ClawTweaks) riporta in primo piano il launcher
- Resta in background mentre si gioca e torna in primo piano alla chiusura del gioco
- Avvio come home della modalità a schermo intero di Windows tramite AnyFSE (eseguibile personalizzato)
- Installer

## Milestone 4 — Home nativa della modalità a schermo intero
- Pacchetto MSIX con estensione `windows.gamingApp` e capability `gamingHome` (approccio di AnyFSE, licenza MIT)

## Fase 2 — Giochi posseduti non installati
- Login agli store (Steam Web API, Epic, GOG…) e installazione dal launcher
