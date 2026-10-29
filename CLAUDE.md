# CLAUDE.md

Istruzioni per chi lavora su questo repository con Claude Code.

## Come lavorare con Fabio

- Rispondi in **italiano**.
- **Prima discuti, poi scrivi codice.** Per nuove funzionalità o scelte di interfaccia proponi le opzioni con una raccomandazione,
  fai le domande necessarie e aspetta le risposte prima di implementare.
- `main` va tenuto aggiornato: committa e fai push su `main` a fine lavoro. Le release si fanno solo su richiesta (vedi `docs/RELEASE.md`).

## Il progetto

Claw Hub Games è un launcher open source (MIT) per **tutti i PC handheld Windows** (MSI Claw, ROG Ally, Legion Go…), da usare col controller.
Grafica ispirata a Steam Big Picture (tema scuro, copertine grandi, righe orizzontali), ma con un design originale:
niente copie dell'interfaccia o degli asset di Steam.

Decisioni prese:
- **Solo launcher** (libreria + avvio). TDP, ventole, LED e controller restano a [ClawTweaks](https://github.com/enterTheVoidCode/ClawTweaks).
  Non copiare codice da ClawTweaks: è AGPLv3 e renderebbe AGPL anche questo progetto.
- **Fase 1:** giochi installati + aggiunta manuale, per tutti gli store: Steam, Epic, GOG, Xbox/Game Pass, EA app, Ubisoft Connect, Battle.net, Rockstar Games.
  I giochi posseduti ma non installati (con login agli store) sono la **fase 2**.
- Funzioni richieste: ricerca, preferiti, collezioni personalizzate, pagina di dettaglio del gioco, raggruppamento per store.
- Il launcher **resta in background** mentre si gioca e si richiama con il **tasto MSI**: ClawTweaks può avviare un eseguibile,
  quindi serve l'istanza singola (riaprire l'exe porta in primo piano la finestra esistente).
- **Avvio come home della modalità a schermo intero di Windows:** prima tramite [AnyFSE](https://github.com/ashpynov/AnyFSE)
  (eseguibile personalizzato), poi in modo nativo con un pacchetto MSIX con `windows.gamingApp` e la capability
  `Microsoft.appCategory.gamingHome` (stesso approccio di AnyFSE, licenza MIT, riutilizzabile citando l'autore).
- **Interfaccia:** barra laterale con le sezioni + griglia di copertine 2:3 a 5 colonne, accento ciano, apertura su "Tutti i giochi".
  Tasti: A dettaglio, X gioca, Y preferito, B indietro, LB/RB **sempre** schede in alto (anche nel dettaglio).
  Dettaglio a tutto schermo con gioca, preferito, collezioni, nascondi; i nascosti finiscono nella voce "Nascosti".
  View apre la ricerca; tastiera a schermo QWERTY (A scrive, B cancella, X spazio, Y maiuscole, Menu conferma, View annulla).
  Collezioni gestite da Impostazioni; giochi manuali dal menu Start o con "Sfoglia…"; "Altro" nel dettaglio per rinominare.
  Tempo di gioco solo con la Milestone 3. Anteprima di riferimento pubblicata come artifact "Claw Hub Home".
- **Installer Velopack** (solo x64, packId `ClawHubGamesApp`, installato in `%LocalAppData%\ClawHubGamesApp`) con aggiornamenti
  automatici dalle release di GitHub; resta anche lo zip portable. I dati utente restano in `%LocalAppData%\ClawHubGames`.
- **Milestone 3:** tasto MSI apre/chiude il launcher, riduzione a icona durante il gioco e ritorno automatico,
  tempo di gioco anche per i giochi avviati fuori dal launcher, menu "In gioco" con chiudi gioco, avvia con Windows.
- Copertine da SteamGridDB con la **chiave API inserita dall'utente** nelle impostazioni.
- Lingue: **italiano e inglese** fin da subito, scelta nelle impostazioni e cambiabile senza riavvio (`src/ClawHub.Core/Localization/Strings/*.json`,
  le chiavi devono essere identiche in tutte le lingue: c'è un test).

Lo stato e i prossimi passi sono in [ROADMAP.md](ROADMAP.md), la struttura del codice in [docs/ARCHITETTURA.md](docs/ARCHITETTURA.md).

## Build e test

- .NET 10 (`global.json`), test con xUnit v3 sul runner Microsoft.Testing.Platform:
  `dotnet test --project tests/ClawHub.Core.Tests`
- `ClawHub.Core` e i test si compilano ovunque. `ClawHub.Providers` si compila anche fuori da Windows (`EnableWindowsTargeting`).
- **L'app WinUI 3 (`src/ClawHub.App`) si compila solo su Windows**: il compilatore XAML non gira su macOS/Linux. Per verificarla usa la CI
  (`.github/workflows/ci.yml`), che pubblica l'app e controlla i file essenziali con `.github/scripts/Test-PublishOutput.ps1`.
- L'app non è pacchettizzata ed è autosufficiente. `EnableMsixTooling` deve restare attivo, altrimenti manca `ClawHubGames.pri` e l'app non parte
  (è successo con la v0.1.0-alpha.1).

## Ambiente di Fabio

Sviluppa su Mac, compila su un PC Windows e prova tutto su un MSI Claw 8 AI+.
EA app, Battle.net e Rockstar Games sono sperimentali: vanno verificati sul dispositivo reale.
