# Medición con carga — Bloque 5 (25 de noviembre de 2026)

Pruebas ejecutadas en la VM de Google Cloud contra `http://localhost:5250` (Transportista 7, Expreso Andino) siguiendo `docs/nube/03_plan_carga.md`. Horas registradas en UTC (hora de la VM).

## Resumen de corridas

| Corrida | Ritmo | Duración | Total | Errores (`!= CONFIRMADO`) | Mediana (p50) | Percentil 95 (p95) | CPU máx. VM | Memoria máx. VM | Memoria máx. Mediador |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **Corrida 1** | 50/min | 10 min (`17:31`–`17:41`) | 500 | 59 (11.8 %) | 43 ms | 52 ms | 4.05 % | 4 491.5 MB (54.9 %) | N/D* |
| **Corrida 2** | 100/min | 10 min (`17:46`–`17:56`) | 1 000 | 3 (0.3 %) | 46 ms | 54 ms | 5.56 % | 4 285.3 MB (52.3 %) | N/D* |

*\*Nota sobre procesos:* En las tomas de PowerShell solo se capturó `SQL Server` (~471.9 MB WS / ~611 MB Private) porque la sesión no tenía permisos elevados para leer la línea de comandos de los servicios `MediadorCore` y `SimuladorEETT`.

## Hallazgo técnico en Corrida 1 y corrección aplicada

- **Incidente (`22:39:50` UTC, envío 442/500):** Del envío 1 al 441 todas las confirmaciones respondieron `CONFIRMADO` (mediana `43 ms`). A partir del 442, los 59 envíos restantes devolvieron `ERROR_HTTP_500`.
- **Causa raíz:** En `database/01_esquema.sql`, la columna `MED.Envio.CodigoSeguimiento` tenía una restricción `UNIQUE` sin filtro (`UQ__Envio__6A877754BAE60951`). En SQL Server, esto solo permite un único valor `NULL` en toda la tabla. Al quedar un envío cotizado sin confirmar (`CodigoSeguimiento = NULL`), SQL Server rechazó todos los `POST /cotizar` posteriores.
- **Corrección aplicada antes de la Corrida 2:** Se reemplazó la restricción por un índice único filtrado (`WHERE CodigoSeguimiento IS NOT NULL`):
  ```sql
  ALTER TABLE MED.Envio DROP CONSTRAINT UQ__Envio__6A877754BAE60951;
  CREATE UNIQUE NONCLUSTERED INDEX UQ_Envio_CodigoSeguimiento
      ON MED.Envio(CodigoSeguimiento)
      WHERE CodigoSeguimiento IS NOT NULL;