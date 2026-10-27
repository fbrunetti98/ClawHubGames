# Controlla che la cartella pubblicata contenga i file senza i quali l'app non parte.
param([Parameter(Mandatory)][string]$Path)

$required = @(
    'ClawHubGames.exe',
    'ClawHubGames.dll',
    'ClawHubGames.pri',          # risorse XAML compilate (App.xaml, MainWindow.xaml)
    'ClawHub.Core.dll',
    'ClawHub.Providers.dll',
    'Microsoft.WindowsAppRuntime.dll'
)

$missing = $required | Where-Object { -not (Test-Path (Join-Path $Path $_)) }
if ($missing) {
    Write-Error "Mancano file nella cartella pubblicata ${Path}: $($missing -join ', ')"
    exit 1
}
Write-Host "Output pubblicato completo: $Path"
