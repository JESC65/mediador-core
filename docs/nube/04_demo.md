# Verificación de Demo y Reglas de Lámina 7 (`04_demo.md`)

## 1. Verificación técnica en código — 3 reglas de la Lámina 7

### Regla 1: "Repetir un mensaje no duplica nada."
**Resultado:** **Cierta** (con matiz explícito para reintentos tras `PENDIENTE_CONFIRMACION`).

1. **Cotización (`POST /cotizar`)** — [CotizacionMediadorService.cs](../../Mediador.ApplicationCore/Services/CotizacionMediadorService.cs) (`Cotizar`, líneas 29–40) y [EnvioRepository.cs](../../Mediador.Infrastructure/Repositories/EnvioRepository.cs) (`BuscarPorIdCorrelacion`, líneas 19–36):
   - Al recibir `idCorrelacion`, busca si ya existe en `MED.Envios`.
   - Si existe con estado `"COTIZADO"`, devuelve las opciones previas no descartadas (`_cotizacionRepositorio.ObtenerNoDescartadas`) sin crear un nuevo envío ni nuevas cotizaciones.
   - Si existe en otro estado distinto de `"COTIZADO"`, retorna conflicto y [CotizarController.cs](../../Mediador.Presentation/Controllers/CotizarController.cs) (`Post`, líneas 25–33) responde HTTP `409 Conflict` (`ESTADO_INVALIDO`).
2. **Confirmación (`POST /confirmar`)** — [ConfirmacionMediadorService.cs](../../Mediador.ApplicationCore/Services/ConfirmacionMediadorService.cs) (`Confirmar` y `EjecutarConfirmacion`, líneas 35–85) y [ConfirmacionRepository.cs](../../Mediador.Infrastructure/Repositories/ConfirmacionRepository.cs) (`AsegurarIdCorrelacionRegistro`, líneas 260–272):
   - `Confirmar` serializa peticiones simultáneas sobre el mismo envío mediante un `SemaphoreSlim` por `IdEnvio` (`_candados`, líneas 19 y 46–62) y relee la opción dentro del candado (línea 56) para observar el resultado de cualquier confirmación concurrente previa.
   - *Nota de arquitectura:* el comentario en las líneas 14–16 advierte que `_candados` es en memoria y cubre una sola instancia del Mediador (para varias instancias detrás de un balanceador debería migrarse a un candado en BD como `sp_getapplock`).
   - En `EjecutarConfirmacion` (Paso 1, líneas 74–85), si el envío ya tiene `CodigoSeguimientoEnvio` y `EstadoEnvio == "CONFIRMADO"`, responde inmediatamente `Estado = "YA_CONFIRMADO"` devolviendo el mismo `CodigoSeguimiento` sin invocar B3/B4 ni insertar duplicados.
   - **Matiz B4 (`PENDIENTE_CONFIRMACION`):** si un primer intento falló al comunicarse con la transportista en B4 y quedó en `PENDIENTE_CONFIRMACION`, al repetir el `POST /confirmar` **no** devuelve `YA_CONFIRMADO` (líneas 71–73): continúa el flujo para reintentar el registro en la misma transportista reutilizando el mismo `CodigoSeguimientoEnvio` (líneas 206–207) y el mismo `IdCorrelacionRegistro` estable persistido en `MED.Envio` (líneas 211–216), evitando crear un segundo envío en la transportista.
3. **Eventos de transportista (`POST /eventos`)** — [EventoMediadorService.cs](../../Mediador.ApplicationCore/Services/EventoMediadorService.cs) (`ProcesarEvento`, líneas 49–98) y [EventoRepository.cs](../../Mediador.Infrastructure/Repositories/EventoRepository.cs) (`ObtenerEventoExistente`, líneas 21–38):
   - Calcula la huella SHA-256 (`CalcularHuella`, líneas 285–290) del contenido serializado y verifica si `request.IdEvento` ya fue registrado.
   - Si existe con igual huella/contenido (`sonIguales`, líneas 61–73), retorna `Recibido = true` (HTTP 202) sin reprocesar ni duplicar estados.
   - Si el `idEvento` se repite con contenido distinto, registra el conflicto (`EsConflicto = true`, `MotivoRechazo = "CONFLICTO_CONTENIDO"`) y devuelve `409 Conflict` sin alterar el envío (líneas 76–97).

---

### Regla 2: "Confirmar es todo o nada."
**Resultado:** **Cierta en cuanto al resultado de confirmación** (con matices sobre persistencia previa e incertidumbre de red en B4).

1. **Transacción atómica al confirmar** — [ConfirmacionRepository.cs](../../Mediador.Infrastructure/Repositories/ConfirmacionRepository.cs) (`ConfirmarTransaccional`, líneas 180–216):
   - Abre una transacción explícita (`_db.Database.BeginTransactionAsync()`) para actualizar `MED.Envios` (`CodigoSeguimiento`, `Estado = "CONFIRMADO"`, `IdTransportistaElegida`, `IdCotizacionElegida`, `FechaConfirmacion`), marcar `Cotizacion.Elegida = true` e insertar el historial `"CONFIRMADO"` en `MED.EnvioEstados`. Ante cualquier fallo ejecuta `RollbackAsync()`.
2. **Manejo de rechazos de negocio y fallas de comunicación** — [ConfirmacionMediadorService.cs](../../Mediador.ApplicationCore/Services/ConfirmacionMediadorService.cs) (líneas 104–287):
   - Si B3 (`ConsultarPrecio`) falla o responde `Disponible = false`, o si B4 (`Registrar`) responde `Aceptado = false`, marca la opción como descartada (`MarcarDescartada`) y pasa a la siguiente alternativa (`ObtenerSiguienteAlternativa`) o responde `SIN_OPCIONES_DISPONIBLES`, sin dejar el envío en estado `CONFIRMADO`.
   - **Matiz 1 (pasos previos a la transacción):** `GuardarPersonas` (línea 99), `AsegurarIdCorrelacionPrecio` / `AsegurarIdCorrelacionRegistro` y la obtención del correlativo (`GenerarSiguienteCodigoSeguimiento`, `ConfirmacionRepository.cs` líneas 162–178, que usa `MED.SiguienteCorrelativo` en transacción corta) ocurren antes de `ConfirmarTransaccional`; por diseño, si la confirmación no cierra, puede quedar un hueco de correlativo o los datos de personas guardados, pero el envío nunca queda `CONFIRMADO`.
   - **Matiz 2 (`PENDIENTE_CONFIRMACION` en B4):** si B4 agota los reintentos síncronos (`r_esperas_sincronas`) sin respuesta (timeout/caída de red), el Mediador **no** descarta la opción ni salta a otra transportista (líneas 240–256), ya que la primera pudo haber registrado el envío antes del corte. Persiste `Estado = "PENDIENTE_CONFIRMACION"` con el `CodigoSeguimiento` reservado (`GuardarPendienteConfirmacion`, `ConfirmacionRepository.cs` líneas 218–244) para recuperación posterior. Asimismo, si B4 aceptó y falló `ConfirmarTransaccional` (líneas 274–287), guarda `PENDIENTE_CONFIRMACION` preservando `NumeroGuiaEETT` en `Observacion` para no perder la guía emitida.

---

### Regla 3: "Una cotización vencida ya no se confirma."
**Resultado:** **Cierta** (con excepción explícita para reintentos de `PENDIENTE_CONFIRMACION`).

1. **Cálculo de vigencia al cotizar** — [CotizacionMediadorService.cs](../../Mediador.ApplicationCore/Services/CotizacionMediadorService.cs) (`Cotizar` y `EvaluarCandidata`, líneas 61 y 110):
   - Cada opción se guarda con `VigenteHasta = RelojLima.Ahora().Add(_parametros.t_vigencia_cotizacion)`.
2. **Validación al confirmar** — [ConfirmacionMediadorService.cs](../../Mediador.ApplicationCore/Services/ConfirmacionMediadorService.cs) (`EjecutarConfirmacion`, Paso 2, líneas 87–95):
   - Evalúa `if (candidata.EstadoEnvio != "PENDIENTE_CONFIRMACION" && RelojLima.Ahora() > candidata.VigenteHasta)`.
   - Si la cotización venció, corta la ejecución antes de guardar personas o llamar a B3/B4 y responde `Estado = "OPCION_EXPIRADA"`.
   - **Matiz:** si el envío ya había quedado en `PENDIENTE_CONFIRMACION` (porque el intento original dentro de vigencia sufrió un corte en B4), se permite reintentar esa misma confirmación aunque haya expirado `VigenteHasta`, para poder conciliar el registro en la transportista.

---

## 2. Registro de pruebas locales — Acto 2 (Idempotencia y concurrencia)

**Fecha de ejecución:** `2026-11-24` (hora local `-05:00`)  
**Entorno:** `http://localhost:5250` (Mediador) + `https://localhost:7201` (Simulador EETT T7, `Expreso Andino`, `precioEnLinea: false`).  
**Ruta probada:** Lima (`150101`) → Arequipa (`040101`), `CARGA_LIVIANA`, `12.5` kg, `idCourier: 1`.

> **Nota operativa previa:** Antes de ejecutar las pruebas se sincronizó B0 mediante `POST /v1/transportistas/7/capacidades` (HTTP `200`, `precioEnLinea: false`), ya que el Mediador había arrancado antes que el Simulador T7 y conservaba `PrecioEnLinea = 1` en `MED.TransportistaCapacidad`.

### Repetición 1
- **`idCorrelacion`:** `ACTO2-REP1-20261124155619-5e3fc4`
- **Opción elegida (`idOpcion` / transportista):** `OPT-D42FC4` (`Expreso Andino`, precio referencial `S/ 60.00`)
- **Paso 1 — `POST /confirmar` inicial:** HTTP `200` · `estado: "CONFIRMADO"` · `codigoSeguimiento: "MED-2026-000088"` · `precioFinal: 60.00` · `96 ms`
- **Paso 2 — Misma confirmación repetida:** HTTP `200` · `estado: "YA_CONFIRMADO"` · `codigoSeguimiento: "MED-2026-000088"` · `31 ms`
- **Paso 3 — 20 confirmaciones idénticas en paralelo:** `20/20` HTTP `200` · `1` código de seguimiento distinto (`MED-2026-000088`) · estados recibidos: `YA_CONFIRMADO: 20`

### Repetición 2
- **`idCorrelacion`:** `ACTO2-REP2-20261124155620-0fef2f`
- **Opción elegida (`idOpcion` / transportista):** `OPT-C108F0` (`Expreso Andino`, precio referencial `S/ 60.00`)
- **Paso 1 — `POST /confirmar` inicial:** HTTP `200` · `estado: "CONFIRMADO"` · `codigoSeguimiento: "MED-2026-000089"` · `precioFinal: 60.00` · `63 ms`
- **Paso 2 — Misma confirmación repetida:** HTTP `200` · `estado: "YA_CONFIRMADO"` · `codigoSeguimiento: "MED-2026-000089"` · `26 ms`
- **Paso 3 — 20 confirmaciones idénticas en paralelo:** `20/20` HTTP `200` · `1` código de seguimiento distinto (`MED-2026-000089`) · estados recibidos: `YA_CONFIRMADO: 20`

### Repetición 3
- **`idCorrelacion`:** `ACTO2-REP3-20261124155621-84c1dd`
- **Opción elegida (`idOpcion` / transportista):** `OPT-AD8CAA` (`Expreso Andino`, precio referencial `S/ 60.00`)
- **Paso 1 — `POST /confirmar` inicial:** HTTP `200` · `estado: "CONFIRMADO"` · `codigoSeguimiento: "MED-2026-000090"` · `precioFinal: 60.00` · `68 ms`
- **Paso 2 — Misma confirmación repetida:** HTTP `200` · `estado: "YA_CONFIRMADO"` · `codigoSeguimiento: "MED-2026-000090"` · `27 ms`
- **Paso 3 — 20 confirmaciones idénticas en paralelo:** `20/20` HTTP `200` · `1` código de seguimiento distinto (`MED-2026-000090`) · estados recibidos: `YA_CONFIRMADO: 20`

**Resultado Acto 2:** **3/3 repeticiones exitosas** sin duplicados.

---

## 3. Registro de pruebas locales — Acto 3 (Resiliencia ante caída en B4)

**Fecha de ejecución:** `2026-11-24` (hora local `-05:00`)

### Repetición 1
- **`idCorrelacion`:** `ACTO3-REP1-20261124155640-7a2a65` (ciclo `cotizar` → `confirmar` con Simulador T7 detenido)
- **Con simulador apagado (`POST /confirmar`):**
  - Código HTTP: `200`
  - `estado`: `"PENDIENTE_CONFIRMACION"`
  - `codigoSeguimiento`: `"MED-2026-000091"`
  - `motivo`: `"La transportista no respondió al registro dentro del tiempo esperado. Reintente la confirmación."`
  - Tiempo de respuesta: `33645 ms` (incluye los reintentos síncronos `r_esperas_sincronas`: `2s + 5s + 10s`)
- **Tras levantar el Simulador T7 (reenvío del mismo `POST /confirmar`):**
  - Camino aplicado: reintento directo del mismo `POST /confirmar` (sin recotizar).
  - Código HTTP: `200` · `estado`: `"CONFIRMADO"` · `codigoSeguimiento`: `"MED-2026-000091"` (mismo código reservado) · `precioFinal`: `60.00` · `208 ms`

### Repetición 2
- **`idCorrelacion`:** `ACTO3-REP2-20261124155714-eb4704`
- **Con simulador apagado (`POST /confirmar`):**
  - Código HTTP: `200`
  - `estado`: `"PENDIENTE_CONFIRMACION"`
  - `codigoSeguimiento`: `"MED-2026-000092"`
  - `motivo`: `"La transportista no respondió al registro dentro del tiempo esperado. Reintente la confirmación."`
  - Tiempo de respuesta: `33616 ms`
- **Tras levantar el Simulador T7 (reenvío del mismo `POST /confirmar`):**
  - Camino aplicado: reintento directo del mismo `POST /confirmar`.
  - Código HTTP: `200` · `estado`: `"CONFIRMADO"` · `codigoSeguimiento`: `"MED-2026-000092"` · `precioFinal`: `60.00` · `239 ms`

### Repetición 3
- **`idCorrelacion`:** `ACTO3-REP3-20261124155748-49f167`
- **Con simulador apagado (`POST /confirmar`):**
  - Código HTTP: `200`
  - `estado`: `"PENDIENTE_CONFIRMACION"`
  - `codigoSeguimiento`: `"MED-2026-000093"`
  - `motivo`: `"La transportista no respondió al registro dentro del tiempo esperado. Reintente la confirmación."`
  - Tiempo de respuesta: `33679 ms`
- **Tras levantar el Simulador T7 (reenvío del mismo `POST /confirmar`):**
  - Camino aplicado: reintento directo del mismo `POST /confirmar`.
  - Código HTTP: `200` · `estado`: `"CONFIRMADO"` · `codigoSeguimiento`: `"MED-2026-000093"` · `precioFinal`: `60.00` · `232 ms`

**Conclusión Acto 3:** **Sí es estable y determinístico para mostrar en la demo (3/3 repeticiones idénticas).** Con `precioEnLinea: false` sincronizado en T7, la caída del simulador durante B4 devuelve consistentemente `PENDIENTE_CONFIRMACION` en ~`33.6 s` reservando el `codigoSeguimiento`, y al levantar el simulador el reintento con el botón `Confirmar` completa el registro en ~`210–240 ms` pasando a `CONFIRMADO` con el mismo código. *(Precondición de demo: asegurar que las capacidades de T7 estén sincronizadas con `precioEnLinea: false` antes de iniciar.)*

---

## 4. Prueba local del script de carga (`scripts/carga/carga.ps1`)

- **Comando ejecutado:**
  ```powershell
  $env:MEDIADOR_URL = "http://localhost:5250"
  .\scripts\carga\carga.ps1 -Ritmo 10 -DuracionMinutos 1
  ```
- **Archivo CSV generado:** `scripts/carga/resultados_20261124_155856.csv` (`MED-2026-000094` a `MED-2026-000103`)
- **Resumen impreso en consola:**
  - Total de operaciones ejecutadas: `10`
  - Total de errores (`!= CONFIRMADO`): `0`
  - Mediana (`p50`): `77 ms`
  - Percentil 95 (`p95`): `493 ms`
- **Tiempos individuales registrados (ms):** `493, 107, 275, 222, 69, 72, 66, 99, 77, 75` (todas con resultado `CONFIRMADO`).
