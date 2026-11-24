param(
    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 10000)]
    [int]$Ritmo,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 1440)]
    [int]$DuracionMinutos,

    [int]$IdTransportista = 7,

    [string]$ArchivoCsv
)

$ErrorActionPreference = "Stop"

# Validar variables de entorno sin exponer secretos ni URLs fijas en el código
$baseUrl = $env:MEDIADOR_URL
if ([string]::IsNullOrWhiteSpace($baseUrl)) {
    Write-Error "La variable de entorno MEDIADOR_URL no está definida. Ejemplo: `$env:MEDIADOR_URL = 'http://localhost:5250'"
    exit 1
}
$baseUrl = $baseUrl.TrimEnd('/')

$headers = @{
    "Content-Type" = "application/json; charset=utf-8"
}
if (-not [string]::IsNullOrWhiteSpace($env:MEDIADOR_API_KEY)) {
    $headers["X-Api-Key"] = $env:MEDIADOR_API_KEY.Trim()
}

if ([string]::IsNullOrWhiteSpace($ArchivoCsv)) {
    $marca = Get-Date -Format "yyyyMMdd_HHmmss"
    $ArchivoCsv = Join-Path $PSScriptRoot "resultados_$marca.csv"
}

$directorioCsv = Split-Path -Path $ArchivoCsv -Parent
if (-not [string]::IsNullOrWhiteSpace($directorioCsv) -and -not (Test-Path $directorioCsv)) {
    New-Item -ItemType Directory -Path $directorioCsv -Force | Out-Null
}

function Invoke-JsonPost {
    param(
        [string]$Url,
        [hashtable]$BodyObj,
        [hashtable]$Headers
    )

    $json = $BodyObj | ConvertTo-Json -Depth 10 -Compress
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)

    try {
        $resp = Invoke-RestMethod -Uri $Url -Method Post -Headers $Headers -Body $bytes -ContentType "application/json; charset=utf-8"
        return @{
            Ok     = $true
            Status = 200
            Datos  = $resp
            Error  = $null
        }
    }
    catch {
        $status = 0
        $respObj = $_.Exception.Response
        if ($null -ne $respObj) {
            try {
                if ($respObj -is [System.Net.HttpWebResponse]) {
                    $status = [int]$respObj.StatusCode
                }
                elseif ($null -ne $respObj.StatusCode) {
                    $status = [int]$respObj.StatusCode
                }
            }
            catch {
                $status = 0
            }
        }
        return @{
            Ok     = $false
            Status = $status
            Datos  = $null
            Error  = $_.Exception.Message
        }
    }
}

function Get-Percentil {
    param(
        [long[]]$ValoresOrdenados,
        [double]$Percentil
    )
    if ($null -eq $ValoresOrdenados -or $ValoresOrdenados.Count -eq 0) {
        return 0
    }
    $idx = [int][Math]::Ceiling($Percentil * $ValoresOrdenados.Count) - 1
    if ($idx -lt 0) { $idx = 0 }
    if ($idx -ge $ValoresOrdenados.Count) { $idx = $ValoresOrdenados.Count - 1 }
    return $ValoresOrdenados[$idx]
}

$totalOperaciones = $Ritmo * $DuracionMinutos
$intervaloMs = 60000.0 / $Ritmo
$zonaLima = [TimeSpan]::FromHours(-5)

$tiempos = New-Object 'System.Collections.Generic.List[long]'
$totalEjecutadas = 0
$totalErrores = 0
$writer = $null

Write-Host "Iniciando prueba de carga contra $baseUrl" -ForegroundColor Cyan
Write-Host "Ritmo: $Ritmo/min | Duración: $DuracionMinutos min | Total planeado: $totalOperaciones | Transportista: $IdTransportista"
Write-Host "Archivo CSV: $ArchivoCsv"
Write-Host "Presiona Ctrl+C para detener de forma segura en cualquier momento." -ForegroundColor DarkGray

try {
    $utf8SinBom = New-Object System.Text.UTF8Encoding($false)
    $writer = New-Object System.IO.StreamWriter($ArchivoCsv, $false, $utf8SinBom)
    $writer.AutoFlush = $true
    $writer.WriteLine("hora,identificador,resultado,milisegundos")

    $relojPrueba = [System.Diagnostics.Stopwatch]::StartNew()

    for ($i = 0; $i -lt $totalOperaciones; $i++) {
        $objetivoMs = $i * $intervaloMs
        $faltanteMs = [int][Math]::Floor($objetivoMs - $relojPrueba.Elapsed.TotalMilliseconds)
        if ($faltanteMs -gt 0) {
            Start-Sleep -Milliseconds $faltanteMs
        }

        $sufijo = [Guid]::NewGuid().ToString("N").Substring(0, 8)
        $idCorrelacion = "CARGA-{0}-{1:D4}-{2}" -f (Get-Date).ToString("yyyyMMddHHmmss"), ($i + 1), $sufijo
        $identificador = $idCorrelacion
        $resultado = "ERROR_DESCONOCIDO"

        $sw = [System.Diagnostics.Stopwatch]::StartNew()

        try {
            $bodyCotizar = @{
                idCorrelacion  = $idCorrelacion
                idEnvioCourier = $idCorrelacion
                idCourier      = 1
                ubigeoOrigen   = "150101"
                ubigeoDestino  = "040101"
                tipoCarga      = "CARGA_LIVIANA"
                paquetes       = @(
                    @{
                        descripcion = "Paquete carga prueba"
                        pesoKg      = 12.5
                        cantidad    = 1
                        esFragil    = $false
                    }
                )
            }

            $resCotizar = Invoke-JsonPost -Url "$baseUrl/cotizar" -BodyObj $bodyCotizar -Headers $headers
            if (-not $resCotizar.Ok) {
                $resultado = if ($resCotizar.Status -gt 0) { "ERROR_HTTP_$($resCotizar.Status)" } else { "ERROR_CONEXION" }
            }
            else {
                $datosCot = $resCotizar.Datos
                $opciones = $datosCot.opciones
                $opcion = $null
                if ($null -ne $opciones) {
                    $opcion = $opciones | Where-Object { $_.idTransportista -eq $IdTransportista } | Select-Object -First 1
                }

                if ($null -eq $opcion) {
                    $resultado = if (-not [string]::IsNullOrWhiteSpace($datosCot.motivo)) { "SIN_OPCION_$($datosCot.motivo)" } else { "SIN_OPCION_T$IdTransportista" }
                }
                else {
                    $bodyConfirmar = @{
                        idCorrelacion = $idCorrelacion
                        idOpcion      = $opcion.idOpcion
                        remitente     = @{
                            tipoDocumento   = "DNI"
                            numeroDocumento = "45678912"
                            nombres         = "Ana Maria"
                            apellidos       = "Rojas Perez"
                            telefono        = "987654321"
                        }
                        consignado    = @{
                            tipoDocumento   = "DNI"
                            numeroDocumento = "10293847"
                            nombres         = "Luis"
                            apellidos       = "Quispe Mamani"
                            telefono        = "912345678"
                            direccion       = "Av. Ejemplo 123, Arequipa"
                            ubigeo          = "040101"
                        }
                    }

                    $resConfirmar = Invoke-JsonPost -Url "$baseUrl/confirmar" -BodyObj $bodyConfirmar -Headers $headers
                    if (-not $resConfirmar.Ok) {
                        $resultado = if ($resConfirmar.Status -gt 0) { "ERROR_HTTP_$($resConfirmar.Status)" } else { "ERROR_CONEXION" }
                    }
                    else {
                        $datosConf = $resConfirmar.Datos
                        if (-not [string]::IsNullOrWhiteSpace($datosConf.codigoSeguimiento)) {
                            $identificador = $datosConf.codigoSeguimiento
                        }
                        $resultado = if (-not [string]::IsNullOrWhiteSpace($datosConf.estado)) { $datosConf.estado } else { "SIN_ESTADO" }
                    }
                }
            }
        }
        finally {
            $sw.Stop()
        }

        $ms = [long]$sw.ElapsedMilliseconds
        $horaIso = [System.DateTimeOffset]::UtcNow.ToOffset($zonaLima).ToString("yyyy-MM-ddTHH:mm:ss.fffzzz")

        $writer.WriteLine("$horaIso,$identificador,$resultado,$ms")
        $tiempos.Add($ms)
        $totalEjecutadas++
        if ($resultado -ne "CONFIRMADO") {
            $totalErrores++
        }

        Write-Host ("[{0}/{1}] {2} -> {3} ({4} ms)" -f $totalEjecutadas, $totalOperaciones, $identificador, $resultado, $ms)
    }
}
finally {
    if ($null -ne $writer) {
        $writer.Flush()
        $writer.Dispose()
    }

    $ordenados = [long[]]($tiempos.ToArray() | Sort-Object)
    $p50 = Get-Percentil -ValoresOrdenados $ordenados -Percentil 0.50
    $p95 = Get-Percentil -ValoresOrdenados $ordenados -Percentil 0.95

    Write-Host ""
    Write-Host "================ RESUMEN DE CARGA ================" -ForegroundColor Cyan
    Write-Host "Total de operaciones ejecutadas : $totalEjecutadas"
    Write-Host "Total de errores (!= CONFIRMADO): $totalErrores"
    Write-Host "Mediana (p50)                   : $p50 ms"
    Write-Host "Percentil 95 (p95)              : $p95 ms"
    Write-Host "CSV guardado en                 : $ArchivoCsv"
    Write-Host "==================================================" -ForegroundColor Cyan
}
