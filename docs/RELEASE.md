# Pubblicare una release

1. Aggiorna `main` e verifica che la CI sia verde.
2. Crea e invia un tag di versione:
   ```bash
   git tag v0.1.0
   git push origin v0.1.0
   ```
3. Il workflow **Release** esegue i test, compila l'app per `win-x64` e `win-arm64` e crea la release su GitHub
   con gli zip `ClawHubGames-<versione>-<architettura>.zip` e le note generate dai commit.

In alternativa, senza creare il tag a mano: su GitHub **Actions → Release → Run workflow**, scegli `main` e scrivi
il tag (es. `v0.1.0-alpha.3`). Il workflow crea tag e release sull'ultimo commit di `main`. È il modo da usare
dalle sessioni cloud di Claude Code, che non possono inviare tag.

Un tag con trattino (es. `v0.2.0-beta.1`) viene pubblicato come **pre-release**.

Per x64 il workflow crea anche l'installer con [Velopack](https://velopack.io):
`ClawHubGamesApp-win-x64-Setup.exe` più i pacchetti `.nupkg` e `releases.win-x64.json` che servono agli
aggiornamenti automatici. L'app installata (in `%LocalAppData%\ClawHubGamesApp`) controlla le release di GitHub
all'avvio e ogni 6 ore; le pre-release solo se l'opzione "Ricevi versioni di anteprima" è attiva.
I dati dell'utente restano in `%LocalAppData%\ClawHubGames` e non vengono toccati da installazione o disinstallazione.

Versione portable: si estrae lo zip e si avvia `ClawHubGames.exe` (non si aggiorna da sola). Il runtime .NET e il Windows App SDK
sono inclusi, non serve installare altro.
