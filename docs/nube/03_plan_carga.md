# Plan de Medición con Carga (`03_plan_carga.md`)

## 1. Objetivo y entorno de prueba

Evaluar el comportamiento, la estabilidad y el consumo de recursos del **Mediador** desplegado en la VM de Google Cloud bajo carga sostenida de cotización y confirmación, comparando dos niveles de ritmo frente a la línea base en reposo (`docs/nube/02_medicion.txt`).

- **Endpoint bajo prueba:** `http://localhost:5250` (ejecución local dentro de la VM).
- **Servicios activos en la VM:**
  - `MediadorCore` (servicio Windows via NSSM, escucha en `http://localhost:5250`).
  - `SimuladorEETT` (servicio Windows via NSSM, escucha en `http://localhost:5201`, configurado con `transportista-7.json`).
  - `MSSQLSERVER` (SQL Server 2022, base de datos `MediadorDev`).
- **Script de carga:** `scripts/carga/carga.ps1`.
  - Por cada operación ejecuta el ciclo completo `POST /cotizar` → `POST /confirmar`.
  - **Parámetros fijos del escenario:** Transportista `7` (`Expreso Andino`), ruta `150101` → `040101`, tipo de carga `CARGA_LIVIANA`, peso `12.5` kg.
- **Variables de entorno (sin secretos en archivos):**
  - `MEDIADOR_URL`: URL base del Mediador (`http://localhost:5250`).
  - `MEDIADOR_API_KEY`: clave de API enviada en la cabecera `X-Api-Key` (si aplica en el entorno; se define únicamente en la sesión de PowerShell sin guardarse en disco ni en el repositorio).

---

## 2. Corridas planificadas

La secuencia completa consta de una referencia inicial, dos corridas de 10 minutos cada una y un descanso intermedio de 5 minutos:

| Etapa | Duración | Ritmo | Operaciones planificadas | Comando / Acción |
|---|---|---|---|---|
| **Referencia inicial** | Puntual (antes de empezar) | Sin carga | — | Ejecutar el comando de medición por proceso en la VM y anotar la hora exacta. |
| **Corrida 1** | 10 minutos | 50 confirmaciones/min | 500 | `.\carga.ps1 -Ritmo 50 -DuracionMinutos 10` |
| **Descanso** | 5 minutos | Sin carga | — | Esperar 5 minutos completos para estabilizar CPU, memoria y conexiones. |
| **Corrida 2** | 10 minutos | 100 confirmaciones/min | 1000 | `.\carga.ps1 -Ritmo 100 -DuracionMinutos 10` |

---

## 3. Qué se mide en cada corrida

### 3.1. Tablero de Cloud Monitoring
Registrar la ventana completa de tiempo desde la **referencia inicial** hasta **1–2 minutos después del cierre de la Corrida 2**, exportando las gráficas de:
- **CPU** de la VM (`compute.googleapis.com/instance/cpu/utilization`).
- **Memoria** de la VM (`state = used`).
- **Disco** (lectura y escritura de bytes).
- **Red** (bytes recibidos y enviados).

### 3.2. Script de carga (`scripts/carga/carga.ps1`)
Al finalizar (o al interrumpir con `Ctrl+C`), el script cierra el CSV y reporta en consola:
- **Total de operaciones ejecutadas.**
- **Total de errores** (toda respuesta con estado distinto de `CONFIRMADO`, incluyendo errores HTTP o de conexión).
- **Mediana (`p50`)** de latencia en milisegundos (ciclo completo cotizar + confirmar).
- **Percentil 95 (`p95`)** de latencia en milisegundos.
- **Archivo CSV generado** (`hora,identificador,resultado,milisegundos`).

### 3.3. Medición por proceso en la VM
Captura puntual de estado del sistema y procesos desde PowerShell:
- **VM Total:** CPU total (`%`), RAM usada / RAM total (`MB` y `%`) y Disco `C:` (Usado / Libre en `GB`).
- **Detalle por proceso (`PID`, `RAM_WS_MB`, `Private_MB`, `CPU_Segundos`):**
  - `MediadorCore`
  - `SimuladorEETT`
  - `SQL Server`

En cada corrida se toman **tres mediciones**:
1. **Antes** de iniciar la corrida (minuto 0).
2. **A la mitad** de la corrida (minuto 5).
3. **Al terminar** la corrida (minuto 10, inmediatamente al cerrar el script).

---

## 4. Paso a paso operativo

Abrir **dos ventanas de PowerShell** dentro de la VM (preferentemente como Administrador para acceder sin restricciones a contadores de rendimiento y servicios).

### Preparación previa (Ventana 1 y Ventana 2)

En **Ventana 1** (ejecución de carga), ubicarse en la carpeta del script, verificar que los servicios estén activos y definir las variables de entorno de la sesión:

```powershell
# Verificar que los servicios estén en ejecución
Get-Service MSSQLSERVER, SimuladorEETT, MediadorCore | Format-Table Name, Status, StartType

# Ir a la carpeta del script de carga (ajustar la ruta al clon del repositorio en la VM)
cd C:\ruta\al\repositorio\mediador-core\scripts\carga

# Configurar variables de entorno solo en memoria de la sesión
$env:MEDIADOR_URL = "http://localhost:5250"
# Si el entorno requiere clave de API, asignarla sin dejarla en archivos:
# $env:MEDIADOR_API_KEY = Read-Host "Ingrese MEDIADOR_API_KEY"
```

En **Ventana 2** (medición por proceso), definir la función `Measure-CargaVM` una sola vez para reutilizarla en todas las tomas:

```powershell
function Measure-CargaVM {
    param([string]$Etiqueta = "MEDICION")

    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $os = Get-CimInstance Win32_OperatingSystem
    $ramTotal = [math]::Round($os.TotalVisibleMemorySize / 1024, 1)
    $ramLibre = [math]::Round($os.FreePhysicalMemory / 1024, 1)
    $ramUsada = [math]::Round($ramTotal - $ramLibre, 1)
    $ramPct = [math]::Round(($ramUsada / $ramTotal) * 100, 1)
    $cpuRaw = (Get-CimInstance Win32_PerfFormattedData_PerfOS_Processor -Filter "Name='_Total'" -ErrorAction SilentlyContinue).PercentProcessorTime
    if ($null -eq $cpuRaw) {
        $cpuRaw = (Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average
    }
    $cpuVm = [math]::Round([double]$cpuRaw, 2)
    $disco = Get-PSDrive C
    $discoUsado = [math]::Round($disco.Used / 1GB, 2)
    $discoLibre = [math]::Round($disco.Free / 1GB, 2)

    Write-Host "=== ${Etiqueta}: $ts ==="
    Write-Host "VM Total -> CPU: $cpuVm% | RAM: $ramUsada / $ramTotal MB ($ramPct%) | Disco C: Usado $discoUsado GB / Libre $discoLibre GB"

    function Get-DetalleProceso($nombreServicio, $patronCmd, $nombreSalida) {
        $svc = Get-CimInstance Win32_Service -Filter "Name='$nombreServicio'" -ErrorAction SilentlyContinue
        $wp = $null
        if ($svc -and $svc.ProcessId -gt 0) {
            $svcProc = Get-CimInstance Win32_Process -Filter "ProcessId=$($svc.ProcessId)" -ErrorAction SilentlyContinue
            if ($svcProc -and $svcProc.Name -match 'nssm') {
                $wp = Get-CimInstance Win32_Process -Filter "ParentProcessId=$($svc.ProcessId)" -ErrorAction SilentlyContinue | Select-Object -First 1
            }
            else {
                $wp = $svcProc
            }
        }
        if (-not $wp -and $patronCmd) {
            $wp = Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like "*$patronCmd*" } | Select-Object -First 1
        }
        if ($wp) {
            $p = Get-Process -Id $wp.ProcessId -ErrorAction SilentlyContinue
            if ($p) {
                return [pscustomobject]@{
                    Proceso      = $nombreSalida
                    PID          = $p.Id
                    RAM_WS_MB    = [math]::Round($p.WorkingSet64 / 1MB, 1)
                    Private_MB   = [math]::Round($p.PrivateMemorySize64 / 1MB, 1)
                    CPU_Segundos = [math]::Round($p.CPU, 2)
                }
            }
        }
        return [pscustomobject]@{
            Proceso      = $nombreSalida
            PID          = "-"
            RAM_WS_MB    = "-"
            Private_MB   = "-"
            CPU_Segundos = "-"
        }
    }

    @(
        (Get-DetalleProceso "MediadorCore" "Mediador.Presentation" "MediadorCore"),
        (Get-DetalleProceso "SimuladorEETT" "SimuladorEETT" "SimuladorEETT"),
        (Get-DetalleProceso "MSSQLSERVER" "sqlservr" "SQL Server")
    ) | Format-Table -AutoSize
}
```

### Secuencia de ejecución

1. **Referencia inicial (antes de empezar):**
   - En **Ventana 2**, tomar la referencia inicial y anotar la hora exacta:
     ```powershell
     Measure-CargaVM -Etiqueta "REFERENCIA INICIAL"
     ```

2. **Corrida 1 (50 confirmaciones/min durante 10 minutos):**
   - En **Ventana 2**, tomar la medición previa de la Corrida 1:
     ```powershell
     Measure-CargaVM -Etiqueta "CORRIDA 1 (50/min) - ANTES"
     ```
   - En **Ventana 1**, iniciar la Corrida 1 y anotar la hora de inicio:
     ```powershell
     .\carga.ps1 -Ritmo 50 -DuracionMinutos 10
     ```
   - Al cumplirse el **minuto 5**, ejecutar en **Ventana 2**:
     ```powershell
     Measure-CargaVM -Etiqueta "CORRIDA 1 (50/min) - MITAD (MIN 5)"
     ```
   - Al terminar la Corrida 1 (**minuto 10**), ejecutar inmediatamente en **Ventana 2**:
     ```powershell
     Measure-CargaVM -Etiqueta "CORRIDA 1 (50/min) - FINAL (MIN 10)"
     ```
   - Guardar la salida de resumen de **Ventana 1** y anotar el nombre del archivo CSV generado.

3. **Descanso (5 minutos sin carga):**
   - No enviar peticiones durante 5 minutos. Monitorear que la CPU y los procesos regresen a valores estables antes de iniciar la Corrida 2.

4. **Corrida 2 (100 confirmaciones/min durante 10 minutos):**
   - En **Ventana 2**, tomar la medición previa de la Corrida 2:
     ```powershell
     Measure-CargaVM -Etiqueta "CORRIDA 2 (100/min) - ANTES"
     ```
   - En **Ventana 1**, iniciar la Corrida 2 y anotar la hora de inicio:
     ```powershell
     .\carga.ps1 -Ritmo 100 -DuracionMinutos 10
     ```
   - Al cumplirse el **minuto 5**, ejecutar en **Ventana 2**:
     ```powershell
     Measure-CargaVM -Etiqueta "CORRIDA 2 (100/min) - MITAD (MIN 5)"
     ```
   - Al terminar la Corrida 2 (**minuto 10**), ejecutar inmediatamente en **Ventana 2**:
     ```powershell
     Measure-CargaVM -Etiqueta "CORRIDA 2 (100/min) - FINAL (MIN 10)"
     ```
   - Guardar la salida de resumen de **Ventana 1** y anotar el nombre del archivo CSV generado.

5. **Cierre y exportación de gráficas (espera de 1–2 minutos):**
   - Al finalizar la Corrida 2, esperar entre **1 y 2 minutos** sin cerrar la VM para asegurar que el Google Cloud Ops Agent envíe y consolide las últimas muestras en Cloud Monitoring.
   - En el tablero de Cloud Monitoring, fijar el rango de tiempo desde la **hora exacta de la referencia inicial** hasta el **cierre posterior a la Corrida 2** y exportar/capturar las gráficas de CPU, memoria (`state = used`), disco y red.

---

## 5. Criterios de parada

Durante cualquiera de las corridas, **detener inmediatamente el script con `Ctrl+C`** en **Ventana 1** si se cumple alguna de las siguientes condiciones:

1. **Tasa de errores > 5 %:** más del 5 % de las operaciones ejecutadas devuelven un estado distinto de `CONFIRMADO` (en Corrida 1 equivale a superar ~25 errores sobre 500 operaciones o una racha sostenida > 5 % en consola; en Corrida 2, superar ~50 errores sobre 1000 o una racha sostenida > 5 %).
2. **Memoria de la máquina > 90 %:** el porcentaje de RAM usada de la VM supera el **90 %** (observado en el tablero de Cloud Monitoring o en `Measure-CargaVM` en Ventana 2).

**Procedimiento si se activa un criterio de parada:**
- **No repetir la corrida detenida.**
- Esperar unos segundos a que el bloque `finally` de `carga.ps1` cierre el CSV e imprima el resumen parcial.
- Ejecutar `Measure-CargaVM -Etiqueta "PARADA ANTICIPADA"` en **Ventana 2** para dejar registro del estado de los procesos.
- Anotar la **hora exacta** de la detención, el motivo (errores > 5 % o RAM > 90 %) y todo lo observado en consola y en el tablero.
- Continuar con el resto del plan (descanso y siguiente paso o cierre y exportación de gráficas).

---

## 6. Entregables esperados tras la ejecución (Bloque 5)

Al concluir las pruebas del Bloque 5, se deben versionar en `docs/nube/`:

1. **`docs/nube/03_carga.md`:** informe con las salidas por proceso de cada toma y una tabla resumen con una fila por corrida y las siguientes columnas:
   - Ritmo (`50/min`, `100/min`)
   - Total de operaciones
   - Errores (`!= CONFIRMADO`)
   - Mediana (`p50`, ms)
   - Percentil 95 (`p95`, ms)
   - CPU máxima de la VM (`%`)
   - Memoria máxima de la máquina (`MB` / `%`)
   - Memoria máxima del Mediador (`RAM_WS_MB` / `Private_MB`)
2. **Archivos CSV de ambas corridas:** copiar ambos `.csv` generados por `carga.ps1` a `docs/nube/`.
3. **Gráficas exportadas de Cloud Monitoring:** guardar en `docs/nube/` las gráficas de CPU, memoria (`state = used`), disco y red correspondientes a toda la ventana de prueba (referencia inicial hasta cierre de la Corrida 2).
