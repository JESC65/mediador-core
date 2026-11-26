# Medición con carga — Bloque 5 (25 de noviembre de 2026)

Pruebas ejecutadas en la VM de Google Cloud contra `http://localhost:5250` (Transportista 7, Expreso Andino) siguiendo `docs/nube/03_plan_carga.md`. Horas registradas en UTC (hora de la VM).

## Resumen de corridas

| Corrida | Ritmo | Duración | Total | Errores (`!= CONFIRMADO`) | Mediana (p50) | Percentil 95 (p95) | CPU máx. VM | Memoria máx. VM | Memoria máx. Mediador |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **Corrida 1** | 50/min | 10 min (`17:31`–`17:41`) | 500 | 59 (11.8 %) | 43 ms | 52 ms | 4.05 % | 4 491.5 MB (54.9 %) | N/D* |
| **Corrida 2** | 100/min | 10 min (`17:46`–`17:56`) | 1 000 | 3 (0.3 %) | 46 ms | 54 ms | 5.56 % | 4 285.3 MB (52.3 %) | N/D* |

*\*Nota sobre procesos:* En las tomas de PowerShell solo se capturó `SQL Server` (~471.9 MB WS / ~611 MB Private) porque la sesión no tenía permisos elevados para leer la línea de comandos de los servicios `MediadorCore` y `SimuladorEETT`.

## Hallazgo técnico en Corrida 1 y corrección aplicada (no válida, se repite)

Qué fallaba: La restricción UNIQUE sin filtro sobre MED.Envio.CodigoSeguimiento solo admitía un valor NULL en toda la tabla, rechazando nuevas cotizaciones si quedaba un envío sin confirmar.

Cómo se encontró: Al ejecutar la corrida de 50/min, a partir del envío 442/500 todas las operaciones devolvieron ERROR_HTTP_500 (59 errores, 11,8 %).

Cómo se corrigió: Se reemplazó la restricción por el índice único filtrado UQ_Envio_CodigoSeguimiento (WHERE CodigoSeguimiento IS NOT NULL), versionado en database/07_indice_codigo_seguimiento.sql.