# Claw Hub Games

Un launcher unico per tutti i giochi installati sui PC handheld Windows (MSI Claw, ROG Ally, Legion Go…),
pensato per essere usato con il controller.

> 🚧 In sviluppo: siamo alla milestone 1 (fondamenta). Vedi [ROADMAP.md](ROADMAP.md).

## Cosa fa

- Raccoglie in un'unica libreria i giochi di **Steam, Epic Games, GOG, Xbox / Game Pass, Ubisoft Connect, EA app, Battle.net e Rockstar Games**
- Permette di aggiungere a mano qualsiasi eseguibile o emulatore
- Ricerca, preferiti, collezioni personalizzate, raggruppamento per store
- Copertine da Steam e, con la tua chiave gratuita, da [SteamGridDB](https://www.steamgriddb.com/)
- Interfaccia in italiano e in inglese

Non regola TDP, ventole o controller: per quello su MSI Claw c'è [ClawTweaks](https://github.com/enterTheVoidCode/ClawTweaks),
che può anche aprire Claw Hub Games con il tasto MSI.

## Installazione

Scarica lo zip dell'ultima [release](../../releases), estrailo e avvia `ClawHubGames.exe`.

## Sviluppo

Requisiti: Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download), Visual Studio 2022/2026 con il carico
di lavoro *Sviluppo di applicazioni WinUI*.

```bash
dotnet test --project tests/ClawHub.Core.Tests
dotnet build src/ClawHub.App -r win-x64 -p:Platform=x64
```

`ClawHub.Core` e i test si compilano anche su macOS/Linux; l'app WinUI solo su Windows.
Dettagli in [docs/ARCHITETTURA.md](docs/ARCHITETTURA.md), rilasci in [docs/RELEASE.md](docs/RELEASE.md).

## Licenza

[MIT](LICENSE). Claw Hub Games non è affiliato a MSI, Valve, Epic, GOG, Microsoft, EA, Ubisoft o Blizzard.

---

## English

Claw Hub Games is a controller-friendly launcher that gathers the installed games from Steam, Epic, GOG,
Xbox / Game Pass, Ubisoft Connect, EA app, Battle.net and Rockstar Games on Windows handheld PCs, plus any game you add by hand.
It has search, favorites, custom collections, and artwork from Steam and SteamGridDB. The interface is available in
Italian and English. Download the latest release, extract it and run `ClawHubGames.exe`. MIT licensed.
