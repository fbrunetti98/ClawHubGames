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

**2c** (chiude la milestone)
- Tastiera a schermo per il controller (QWERTY, numeri, simboli; funziona anche con touch e tastiera fisica)
- Ricerca con il tasto View (o F3 / Ctrl+F), risultati mentre si scrive
- Impostazioni complete: chiave SteamGridDB con verifica, store da cercare, giochi nascosti ovunque,
  scarica di nuovo le copertine, lingua, schermo intero
- Gestione delle collezioni dalle impostazioni: rinomina, sposta, elimina
- Aggiunta manuale dei giochi: programmi del menu Start oppure "Sfoglia…"
- Menu "Altro" nel dettaglio: rinomina, ripristina il nome, rimuovi i giochi aggiunti a mano

Rimandato: installer con aggiornamento automatico dalle release di GitHub (proposta: Velopack).

## Milestone 3 — Integrazione con il sistema (v0.3.0-alpha.1)
- Istanza singola: riaprire l'exe (tasto MSI tramite ClawTweaks, AnyFSE, menu Start) porta in primo piano il launcher;
  se è già davanti e c'è un gioco aperto, si torna al gioco
- Dopo l'avvio di un gioco il launcher si riduce a icona e si mette in pausa; quando il gioco si chiude torna in primo piano
- Partite riconosciute dai processi che girano dalla cartella del gioco, anche se avviati fuori dal launcher:
  tempo di gioco e ultima partita reali
- Etichetta e menu "In gioco": torna al gioco, chiudi il gioco (con conferma), resta nel launcher
- Avvia con Windows (impostazione, spenta di partenza)
- Installer Velopack (x64) con aggiornamenti automatici dalle release di GitHub, opzione "Ricevi versioni di anteprima"
- Avvio come home della modalità a schermo intero tramite AnyFSE (già verificato)

## Milestone 4 — Home nativa della modalità a schermo intero
- Pacchetto MSIX con estensione `windows.gamingApp` e capability `gamingHome` (approccio di AnyFSE, licenza MIT)

## Fase 2 — Giochi posseduti non installati
- Login agli store (Steam Web API, Epic, GOG…) e installazione dal launcher
