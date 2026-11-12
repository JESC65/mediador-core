param(
    [string]$Destino = "C:\publicado\mediador"
)

$ErrorActionPreference = "Stop"

# Ubicar el archivo de proyecto Mediador.Presentation.csproj
$candidatos = @(
    (Join-Path $PSScriptRoot "..\Mediador.Presentation\Mediador.Presentation.csproj"),
    (Join-Path $PSScriptRoot "..\mediador-core\Mediador.Presentation\Mediador.Presentation.csproj"),
    "Mediador.Presentation\Mediador.Presentation.csproj",
    "mediador-core\Mediador.Presentation\Mediador.Presentation.csproj"
)

$proyectoPath = $null
foreach ($candidato in $candidatos) {
    if (Test-Path $candidato) {
        $proyectoPath = (Resolve-Path $candidato).Path
        break
    }
}

if (-not $proyectoPath) {
    Write-Error "No se encontró el archivo de proyecto Mediador.Presentation.csproj."
    exit 1
}

Write-Host "Publicando Mediador en Release hacia '$Destino'..." -ForegroundColor Cyan
Write-Host "Proyecto: $proyectoPath"
Write-Host "Destino:  $Destino"

dotnet publish "$proyectoPath" -c Release -o "$Destino"

if ($LASTEXITCODE -ne 0) {
    Write-Error "La publicación falló con código de salida $LASTEXITCODE."
    exit $LASTEXITCODE
}

# Regla crítica de seguridad: La cadena de conexión nunca debe quedar dentro de la carpeta publicada.
$appsettingsPath = Join-Path $Destino "appsettings.json"
if (Test-Path $appsettingsPath) {
    Write-Host "Sanitizando $appsettingsPath (eliminando ConnectionStrings.MediadorDev)..." -ForegroundColor Cyan
    $config = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
    if ($config.ConnectionStrings) {
        $config.ConnectionStrings.MediadorDev = ""
    }
    $config | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath -Encoding utf8
}

$devSettingsPath = Join-Path $Destino "appsettings.Development.json"
if (Test-Path $devSettingsPath) {
    Write-Host "Eliminando $devSettingsPath de la carpeta publicada..." -ForegroundColor Cyan
    Remove-Item $devSettingsPath -Force
}

Write-Host "Publicación completada exitosamente y sanitizada en '$Destino'." -ForegroundColor Green
exit 0

