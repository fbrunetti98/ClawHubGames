# Architettura

```
src/
  ClawHub.Core        net10.0, nessuna dipendenza da Windows → testabile ovunque
    Models/           Game, DetectedGame, LaunchInfo, ArtworkSet, UserLibrary, AppSettings
    Abstractions/     ILibraryProvider (uno per store), IGameLauncher
    Parsing/          VDF/ACF di Steam, manifest Epic, installerdata.xml EA, codici Battle.net
    Services/         LibraryService (unione store + dati utente), ricerca
    Artwork/          ArtworkService (cache locale), client SteamGridDB
    Localization/     Localizer + Strings/it.json, Strings/en.json
    Storage/          JsonFileStore (scrittura atomica), AppPaths
  ClawHub.Providers   net10.0-windows: registro, file system, PackageManager
    Stores/           Steam, Epic, GOG, Xbox, EA, Ubisoft, Battle.net
    Infrastructure/   RegistryReader, ShellGameLauncher
  ClawHub.App         WinUI 3 non pacchettizzata e autosufficiente
tests/
  ClawHub.Core.Tests  xUnit v3
```

## Come vengono trovati e avviati i giochi

| Store | Dove legge | Come avvia |
|---|---|---|
| Steam | `libraryfolders.vdf` + `appmanifest_*.acf` | `steam://rungameid/<appid>` |
| Epic | `%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item` | `com.epicgames.launcher://apps/<ns>:<id>:<app>?action=launch` |
| GOG | `HKLM\SOFTWARE\WOW6432Node\GOG.com\Games` | eseguibile diretto (senza DRM) |
| Xbox / Game Pass | pacchetti dell'utente con `MicrosoftGame.config` | `shell:AppsFolder\<AUMID>` |
| Ubisoft Connect | `HKLM\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs` | `uplay://launch/<id>/0` |
| EA app *(sperimentale)* | `__Installer\installerdata.xml` nelle cartelle dei giochi | `origin2://game/launch?offerIds=…` o eseguibile |
| Battle.net *(sperimentale)* | voci di disinstallazione "Blizzard Entertainment" | `Battle.net.exe --exec="launch <codice>"` |
| Manuale | `library.json` | eseguibile + argomenti |

## Dati dell'utente

In `%LocalAppData%\ClawHubGames`:
- `library.json`: preferiti, collezioni, giochi nascosti, nomi personalizzati, giochi manuali, ultima scansione
- `settings.json`: lingua, chiave SteamGridDB, store disattivati
- `cache\artwork\`: immagini scaricate

L'id di un gioco è `<store>:<id nello store>` (es. `steam:1245620`): resta lo stesso tra una scansione e l'altra,
quindi preferiti e collezioni non si perdono. Se uno store dà errore, i suoi giochi della scansione precedente restano.
