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

L'app non è pacchettizzata: si estrae lo zip e si avvia `ClawHubGames.exe`. Il runtime .NET e il Windows App SDK
sono inclusi, non serve installare altro.
