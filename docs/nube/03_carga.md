# Medición con carga — Bloques 4 y 5 (25 y 26 de noviembre de 2026)

Pruebas ejecutadas en la VM de Google Cloud contra `http://localhost:5250` (Transportista 7, Expreso Andino) siguiendo `docs/nube/03_plan_carga.md`.

## Resumen de corridas

| Corrida | Ritmo | Duración (hora Lima `-05:00`) | Total | Errores (`!= CONFIRMADO`) | Mediana (p50) | Percentil 95 (p95) | CPU máx. VM | Memoria máx. VM | Memoria máx. Mediador (WS / Private) | Disco máx. (tablero) | Red máx. (tablero) |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **Corrida 1 (25 nov — no válida, se repite)** | 50/min | 10 min (`17:31`–`17:41`) | 500 | 59 (11.8 %) | 43 ms | 52 ms | 4.05 % | 4 491.5 MB (54.9 %) | N/D* | N/D | N/D |
| **Corrida 2 (25 nov)** | 100/min | 10 min (`17:46`–`17:56`) | 1 000 | 3 (0.3 %) | 46 ms | 54 ms | 5.56 % | 4 285.3 MB (52.3 %) | N/D* | N/D | N/D |
| **Corrida 1 repetida (26 nov)** | 50/min | 10 min (`16:04`–`16:14`) | 500 | 2 (0.4 %) | 49 ms | 62 ms | 13.0 %** | 4 273.2 MB (52.2 %) | 14.9 MB / 7.5 MB | `0.31MiB/s` | `21.5KiB/s` |

*\*Nota 25 nov:* En las tomas del 25 de noviembre no se capturó `MediadorCore` por falta de permisos elevados en PowerShell.  
*\*\*Nota 26 nov:* CPU máx. tomada en PowerShell (`Measure-CargaVM`); validar el pico exacto en el tablero de Cloud Monitoring.  
*Archivos CSV:* `resultados_50.csv` (25 nov), `resultados_100.csv` (25 nov) y `resultados_20261126_210421.csv` (26 nov).

## Defecto encontrado en la corrida de 50/min (25 nov)

- **Qué fallaba:** La restricción `UNIQUE` sin filtro sobre `MED.Envio.CodigoSeguimiento` solo admitía un valor `NULL` en toda la tabla, rechazando nuevas cotizaciones si quedaba un envío sin confirmar.
- **Cómo se encontró:** Al ejecutar la corrida de 50/min el 25 de noviembre, a partir del envío 442/500 todas las operaciones devolvieron `ERROR_HTTP_500` (59 errores, 11.8 %), por lo que la corrida quedó **no válida, se repite**.
- **Cómo se corrigió:** Se reemplazó la restricción por el índice único filtrado `UQ_Envio_CodigoSeguimiento` (`WHERE CodigoSeguimiento IS NOT NULL`), versionado en `database/07_indice_codigo_seguimiento.sql`.

*(Nota sobre los errores residuales en las corridas de 100/min y 50/min repetida: corresponden a colisiones aleatorias en `MED.Cotizacion.IdOpcion` con formato `OPT-XXXXXX`, defecto independiente que queda pendiente de corrección.)*

---

## Tomas por proceso — Corrida 50/min repetida (26 nov)

- **Antes (`16:03:43` Lima / `21:03:43` UTC):** VM CPU `13 %` · RAM `4 233.6 / 8 188.4 MB (51.7 %)` · Disco C usado `60.74 GB` / libre `39.15 GB`
  - `MediadorCore` (PID 6252): `14.9 MB` WS / `7.5 MB` Private / `13.12 s` CPU
  - `SimuladorEETT` (PID 7800): `13.9 MB` WS / `6.5 MB` Private / `1.11 s` CPU
  - `SQL Server` (PID 3100): `476.0 MB` WS / `611.3 MB` Private / `214.52 s` CPU
- **Mitad — min 5 (`16:09:32` Lima / `21:09:32` UTC):** VM CPU `5 %` · RAM `4 271.4 / 8 188.4 MB (52.2 %)`
  - `MediadorCore` (PID 6252): `14.9 MB` WS / `7.5 MB` Private / `14.20 s` CPU
  - `SimuladorEETT` (PID 7800): `13.9 MB` WS / `6.5 MB` Private / `1.12 s` CPU
  - `SQL Server` (PID 3100): `476.0 MB` WS / `611.2 MB` Private / `218.81 s` CPU
- **Final — min 10 (`16:14:21` Lima / `21:14:21` UTC):** VM CPU `1 %` · RAM `4 273.2 / 8 188.4 MB (52.2 %)`
  - `MediadorCore` (PID 6252): `14.9 MB` WS / `7.5 MB` Private / `15.03 s` CPU
  - `SimuladorEETT` (PID 7800): `13.9 MB` WS / `6.5 MB` Private / `1.14 s` CPU
  - `SQL Server` (PID 3100): `476.1 MB` WS / `611.2 MB` Private / `223.19 s` CPU

---

## Prueba de corte (26 nov)

### Cronología y comportamiento observado
- **Inicio de la corrida (50/min, 10 min):** `16:19:48` Lima (`21:19:48` UTC) · CSV: `resultados_20261126_211948.csv`.
- **Corte del servicio:** `MediadorCore` detenido a las **`16:22`** Lima (`21:22` UTC) y levantado a las **`16:23`** Lima (`21:23` UTC, nuevo PID `7028`).
- **Fin de la corrida:** `16:29:58` Lima (`21:29:58` UTC).
- **Resultado del script:** `500` operaciones ejecutadas · `16` errores (`!= CONFIRMADO`) · mediana (`p50`) `52 ms` · percentil 95 (`p95`) `91 ms`.
- **Recursos durante la prueba:** RAM máx. VM `4 419.6 MB (54.0 %)` · CPU máx. puntual al reiniciar `25 %` · `MediadorCore` tras reinicio `14.0–14.5 MB` WS (`6.6–7.2 MB` Private).

### Consulta de verificación en base de datos

```sql
DECLARE @InicioCorte DATETIMEOFFSET = '2026-11-26 16:19:00 -05:00';

-- 1. Envíos por estado
SELECT Estado, COUNT(*) AS Cantidad
FROM MED.Envio
WHERE FechaRegistro >= @InicioCorte
GROUP BY Estado;

-- 2. Envíos a medias (sin confirmar o sin código de seguimiento)
SELECT COUNT(*) AS A_Medias
FROM MED.Envio
WHERE FechaRegistro >= @InicioCorte
  AND (Estado <> 'CONFIRMADO' OR CodigoSeguimiento IS NULL);

-- 3. Duplicados por IdCorrelacion
SELECT IdCorrelacion, COUNT(*) AS Repeticiones
FROM MED.Envio
WHERE FechaRegistro >= @InicioCorte
GROUP BY IdCorrelacion
HAVING COUNT(*) > 1;

-------
"El pico de las 16:33 en el tablero fue posterior a las pruebas y corresponde a la apertura/cierre de SSMS en la VM."