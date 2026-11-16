# Lista de Pendientes - Proyecto Mediador

Documento de seguimiento de tareas, pendientes técnicos y pruebas de aseguramiento de calidad (QA) del componente Mediador.

---

## Tabla de Seguimiento de Pendientes

| N.º | Estado | Evidencia | Qué falta |
| :---: | :---: | :--- | :--- |
| 1 | Sin empezar | A la espera de accesos RDP al servidor de JELAF | Credenciales e instalación |
| 2 | Cerrado | Prueba de 3 peticiones simultáneas deja una sola fila | Nada |
| 3 | Cerrado | Correlativo atómico por año con tabla MED.Correlativo y SP MED.SiguienteCorrelativo. 20 confirmaciones simultáneas sin choques ni errores 500 en docs/arreglos_sin_azure/03_concurrencia.txt | Nada |
| 4 | A medias | Modelo de BD tiene columnas en MED.EnvioPaquete, pero API ignora largoCm, anchoCm, altoCm, valorDeclarado y motivo (se guardan NULL) | Agregar campos a PaqueteCotizarDto y CotizarRequestDto, y mapear su persistencia en EnvioRepository |
| 5 | Cerrado | Documentado en docs/migracion_net10.md (Pasos 7, 8 y 9: PRECIO_DISTINTO, SIN_RESPUESTA y ERROR_500 verificados) | Nada |
| 6 | Sin empezar | No se encontró script SQL para MED.Tb_EnvioMediador en el Conector Courier (solo existe mapeo de entidad en CourierDbContext) | Crear script DDL SQL para generar el esquema MED y la tabla MED.Tb_EnvioMediador en Dbcourier_dev |
| 7 | Cerrado | Por decisión: el Courier nuevo llamará directo al Mediador (sin código) | Nada |
| 8 | Sin empezar | No existen controladores ni métodos de servicio para Entrega física (A2c) ni Anulación (A4 y B5) | Implementar endpoints y lógica de negocio para entrega física (A2c) y anulación hacia transportistas (A4 y B5) |
| 9 | Cerrado | PruebaSimuladorController retirado; responde HTTP 404 Not Found verificado en docs/arreglos_sin_azure/09_ruta_retirada.txt | Nada |
| 10 | Sin empezar | La API responde HTTP 200 a peticiones sin clave; no existe validación ni middleware de API Key | Implementar autenticación obligatoria por API Key (X-API-Key) para clientes autorizados |
| 11 | Sin empezar | Controladores exponen rutas planas (/cotizar, /confirmar, etc.) sin prefijo /v1/ | Versionar los endpoints anteponiendo /v1/ en los Route attributes o convención global |
| 12 | Cerrado | READMEs actualizados en los 3 repositorios (hoy) | Nada |
| 13 | Cerrado | Ramas net10 con ciclo completo probado (docs/migracion_net10.md) | Nada |
| 14 | Cerrado | B4 sin respuesta: reintentar la misma transportista, sin descartar opción, envío en PENDIENTE_CONFIRMACION y reintento con misma guía. Evidencia en docs/arreglos_sin_azure/b4_sin_respuesta.txt | Nada |
| 15 | Cerrado | idCorrelacion estable sin fecha (R5): persistido en MED.Envio (IdCorrelacionPrecio e IdCorrelacionRegistro), mismo UUID tras reiniciar Mediador. Evidencia en docs/arreglos_sin_azure/idcorrelacion_estable.txt | Nada |
| 16 | Cerrado | Un solo formato de errores (R8): formato JSON unificado `{ error, detalle, campo, idCorrelacion }` para errores 4xx y 5xx mediante MediadorApiException y ManejadorExcepcionesMiddleware. Evidencia en docs/arreglos_sin_azure/r8_errores.txt y catálogo en docs/errores.md | Nada |
| 17 | Cerrado | Bandeja de eventos del simulador (B7 y R6): persistencia local en bandeja_eventos.json previa al envío HTTP, sobre C2 v2.0 §5 con secuencias por transportista y endpoint GET /v1/eventos?desde=N. Evidencia en docs/arreglos_sin_azure/b7_bandeja.txt | Nada |

---

## Detalle y Evidencia de Pruebas QA

### Pendiente N.º 2: Candado contra confirmaciones simultáneas

- **Objetivo**: Verificar que el sistema impida confirmaciones duplicadas cuando se envían múltiples solicitudes `POST /confirmar` de manera estrictamente simultánea con el mismo payload e `idCorrelacion`.
- **Mecanismo Evaluado**:
  - `ConcurrentDictionary<int, SemaphoreSlim> _candados` en memoria por `IdEnvio` en [ConfirmacionMediadorService.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.ApplicationCore/Services/ConfirmacionMediadorService.cs).
  - Relectura con candado adquirido y validación de idempotencia (`CodigoSeguimientoEnvio is not null -> YA_CONFIRMADO`).
- **Entorno de Prueba**:
  - Mediador levantado en `https://localhost:7250`.
  - Simulador EETT levantado en `https://localhost:7201`.
  - Base de Datos SQL Server `MediadorDev` en `localhost`.
  - Script automatizado con sincronización estricta por barrera (`threading.Barrier(3)` en hilos concurrentes).
- **Parámetros de Prueba**:
  - `IdCorrelacion`: `COU-QA-LOCK-1793216318`
  - `IdOpcion`: `OPT-022834` (Transportista 7 - Expreso Andino)
  - 3 peticiones HTTP POST simultáneas enviadas a `/confirmar`.
- **Resultados HTTP Obtenidos**:
  - **Petición #1**: `HTTP 200 OK` - Estado: `CONFIRMADO`, `CodigoSeguimiento`: `MED-2026-000002` (Tiempo: 0.138s).
  - **Petición #2**: `HTTP 200 OK` - Estado: `YA_CONFIRMADO`, `CodigoSeguimiento`: `MED-2026-000002` (Tiempo: 0.144s).
  - **Petición #3**: `HTTP 200 OK` - Estado: `YA_CONFIRMADO`, `CodigoSeguimiento`: `MED-2026-000002` (Tiempo: 0.142s).
- **Resultados en Base de Datos (`MediadorDev`)**:
  - `MED.Envio`: Exactamente **1 fila** generada con `Estado = 'CONFIRMADO'` y `CodigoSeguimiento = 'MED-2026-000002'`.
  - `MED.EnvioEstado`: Exactamente **2 registros** de transición (`COTIZADO` y `CONFIRMADO`), sin registros repetidos.
  - `MED.EnvioPersona`: Exactamente **2 filas** (1 `REMITENTE` y 1 `CONSIGNADO`), sin duplicados.
- **Veredicto QA**: **Aprobado / Cerrado**.

---

### Pendiente N.º 3: Correlativo del código con generación atómica por año

- **Objetivo**: Garantizar que ante confirmaciones concurrentes de envíos distintos (`IdEnvio` diferentes), el sistema genere códigos de seguimiento correlativos atómicos únicos por año sin colisiones ni errores 500.
- **Mecanismo Evaluado**:
  - Tabla `MED.Correlativo` y procedimiento almacenado `MED.SiguienteCorrelativo` con actualización atómica por año (`database/03_correlativo.sql`).
  - [ConfirmacionRepository.GenerarSiguienteCodigoSeguimiento()](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.Infrastructure/Repositories/ConfirmacionRepository.cs).
- **Entorno de Prueba**:
  - Mediador levantado en `https://localhost:7250`, Simulador EETT en `https://localhost:7201`, SQL Server `MediadorDev`.
  - Script automatizado con barrera estricta (`threading.Barrier(20)` disparando 20 confirmaciones simultáneas).
- **Resultados Obtenidos**:
  - 20 confirmaciones simultáneas de envíos independientes respondieron `HTTP 200 OK` con `estado: "CONFIRMADO"`.
  - 20 códigos únicos generados sin un solo error 500 ni choque de clave única (`UQ_Envio_CodigoSeguimiento`).
  - Evidencia completa en [docs/arreglos_sin_azure/03_concurrencia.txt](file:///d:/Proyectos/Jelaf/Courier/mediador-core/docs/arreglos_sin_azure/03_concurrencia.txt).
- **Veredicto QA**: **Aprobado / Cerrado**.

---

### Pendiente N.º 4: Dimensiones, valor declarado y motivo

- **Objetivo**: Evaluar si el modelo de datos y los endpoints del Mediador aceptan y persisten correctamente las dimensiones (`largoCm`, `anchoCm`, `altoCm`), el `valorDeclarado` y el motivo de cotización.
- **Mecanismo Evaluado**:
  - DTOs de entrada: [CotizarRequestDto.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.ApplicationCore/Dtos/CotizarRequestDto.cs) y [PaqueteCotizarDto.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.ApplicationCore/Dtos/PaqueteCotizarDto.cs).
  - Repositorio de persistencia: [EnvioRepository.CrearEnvioCotizado](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.Infrastructure/Repositories/EnvioRepository.cs#L38-L72).
  - Modelo y tabla SQL: [EnvioPaquete.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.Infrastructure/Models/EnvioPaquete.cs) y `MED.EnvioPaquete`.
- **Prueba Ejecutada**:
  - Se envió petición a `POST /cotizar` con payload conteniendo `largoCm: 30`, `anchoCm: 20`, `altoCm: 15`, `valorDeclarado: 150` y `motivo: "Prueba de motivo de cotizacion"`.
- **Resultados en Base de Datos**:
  - `MED.EnvioPaquete`: Las columnas `LargoCm`, `AnchoCm`, `AltoCm` y `ValorDeclarado` se guardaron con valor `NULL`.
  - `MED.Envio`: El motivo fue descartado; `Observacion` quedó en `NULL` y no existe columna para almacenar el motivo de cotización.
- **Causa Raíz Detectada**:
  1. `PaqueteCotizarDto` solo define `Descripcion`, `PesoKg`, `Cantidad` y `EsFragil`. Carece de las propiedades de dimensiones y valor declarado.
  2. `CotizarRequestDto` no contiene campo de motivo.
  3. `EnvioRepository.CrearEnvioCotizado` no asigna estos valores en la inserción de `EnvioPaquete`.
- **Veredicto QA**: **Falla / A medias**.

---

### Pendiente N.º 5: Escenarios de falla

- **Objetivo**: Verificar que se encuentren documentados y validados los resultados de los escenarios de falla controlada (`SIN_RESPUESTA`, `PRECIO_DISTINTO`, `ERROR_500`).
- **Mecanismo Evaluado**:
  - Documento [docs/migracion_net10.md](file:///d:/Proyectos/Jelaf/Courier/mediador-core/docs/migracion_net10.md).
  - Archivos de evidencia en `docs/linea_base_net8/`.
- **Resultados Verificados**:
  - **Paso 7 (PRECIO_DISTINTO)**: `POST /cotizar` retorna `HTTP 200 OK` con recargo del +20% (`precio: 25.92`), validado idéntico entre .NET 8 y .NET 10.
  - **Paso 8 (SIN_RESPUESTA)**: `POST /cotizar` retorna `HTTP 408 TIMEOUT` por desconexión/conexión expirada (`EmptyResult`), validado idéntico entre .NET 8 y .NET 10.
  - **Paso 9 (ERROR_500)**: `POST /cotizar` retorna `HTTP 500 INTERNAL_SERVER_ERROR` simulando error del transportista, validado idéntico entre .NET 8 y .NET 10.
- **Veredicto QA**: **Aprobado / Cerrado**.

---

### Pendiente N.º 6: Script de tabla en Conector

- **Objetivo**: Confirmar la existencia del script SQL para la creación de la tabla `MED.Tb_EnvioMediador` en el repositorio del Conector Courier.
- **Mecanismo Evaluado**:
  - Búsqueda recursiva en el repositorio `mediador-conector-courier` e historial git.
- **Resultados Obtenidos**:
  - No existe ningún archivo `.sql` ni carpeta de migraciones/scripts de base de datos dentro del repositorio `mediador-conector-courier`.
  - La tabla `MED.Tb_EnvioMediador` únicamente se encuentra referenciada a nivel de código C# en [CourierDbContext.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-conector-courier/Courier.Infrastructure/Data/CourierDbContext.cs#L70-L75) y en la entidad [EnvioMediador.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-conector-courier/Courier.Infrastructure/Models/EnvioMediador.cs).
- **Veredicto QA**: **Pendiente / Sin empezar**.

---

### Pendiente N.º 8: Entrega física y anulación

- **Objetivo**: Verificar la existencia e implementación de endpoints o lógica de negocio para la "Entrega física" (A2c) y la "Anulación" (A4 desde Courier al Mediador y B5 desde Mediador a la Transportista).
- **Mecanismo Evaluado**:
  - Controladores en [Mediador.Presentation/Controllers](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.Presentation/Controllers).
  - Clientes HTTP en [SimuladorTransportistaClient.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.Infrastructure/Repositories/SimuladorTransportistaClient.cs).
  - Servicios de aplicación [EventoMediadorService.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.ApplicationCore/Services/EventoMediadorService.cs).
- **Resultados Obtenidos**:
  - No existen controladores ni endpoints para `POST /anular` o `POST /entrega` en el Mediador.
  - `SimuladorTransportistaClient` solo implementa `ConsultarPrecio` y `Registrar`; no cuenta con métodos para invocar `/anular` en las transportistas.
  - Aunque `EventoMediadorService` reconoce estados como `ENTREGADO_A_TRANSPORTISTA` y `ANULADO`, no hay flujos que originen ni orquesten estas acciones.
- **Veredicto QA**: **Pendiente / Sin empezar**.

---

### Pendiente N.º 9: Rutas de prueba protegidas

- **Objetivo**: Asegurar que las rutas de prueba hacia el simulador no estén expuestas en entornos productivos.
- **Mecanismo Evaluado**:
  - Retiro definitivo de `PruebaSimuladorController.cs`.
- **Resultados Obtenidos**:
  - Las rutas `/prueba/simulador/{idTransportista}/cotizar` y `/prueba/simulador/{idTransportista}/registrar` responden `HTTP 404 Not Found`.
  - Evidencia registrada en [docs/arreglos_sin_azure/09_ruta_retirada.txt](file:///d:/Proyectos/Jelaf/Courier/mediador-core/docs/arreglos_sin_azure/09_ruta_retirada.txt).
- **Veredicto QA**: **Aprobado / Cerrado**.

---

### Pendiente N.º 10: Una clave de API por cliente

- **Objetivo**: Validar si la API del Mediador requiere autenticación mediante API Key y rechaza peticiones no autorizadas.
- **Mecanismo Evaluado**:
  - Configuración de middlewares y servicios en [Program.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.Presentation/Program.cs).
  - Verificación de atributos `[Authorize]` en controladores.
  - Prueba empírica lanzando peticiones HTTP sin ningún encabezado de autorización.
- **Resultados Obtenidos**:
  - Ningún endpoint valida encabezados como `X-API-Key` ni tokens.
  - Peticiones anónimas hacia `/cotizar`, `/confirmar`, `/eventos` y `/envio/{codigo}` responden con `HTTP 200 OK`.
- **Veredicto QA**: **Pendiente / Sin empezar**.

---

### Pendiente N.º 11: Rutas versionadas

- **Objetivo**: Comprobar si los endpoints expuestos por los controladores de la API implementan versionamiento con el prefijo `/v1/`.
- **Mecanismo Evaluado**:
  - Atributos `[Route(...)]` en todos los controladores de [Mediador.Presentation/Controllers](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.Presentation/Controllers).
- **Resultados Obtenidos**:
  - Los controladores definen rutas raíz sin versionar: `[Route("cotizar")]`, `[Route("confirmar")]`, `[Route("eventos")]` y `[Route("envio")]`.
  - Ninguna ruta incluye el prefijo `/v1/`.
- **Veredicto QA**: **Pendiente / Sin empezar**.

---

### Corrección: Bloque 2 · B4 sin respuesta: reintentar la misma, nunca otra

- **Objetivo**: Evitar que ante fallas de timeout o comunicación al llamar a B4 (`POST /registrar`), el Mediador descarte la transportista y pase a ofrecer otra alternativa, lo cual ocasionaría que un mismo paquete quede registrado dos veces en dos transportistas distintas.
- **Mecanismo Implementado**:
  - En [ConfirmacionMediadorService.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.ApplicationCore/Services/ConfirmacionMediadorService.cs): cuando `!resultadoRegistro.Exitosa`, se deja de llamar a `MarcarDescartada` y `ObtenerSiguienteAlternativa`. Se persiste el envío en `PENDIENTE_CONFIRMACION` mediante `GuardarPendienteConfirmacion(idEnvio, codigoSeguimiento, null)` y se retorna el estado `PENDIENTE_CONFIRMACION`.
  - Idempotencia en [ConfirmacionMediadorService.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.ApplicationCore/Services/ConfirmacionMediadorService.cs): un reintento posterior cuando el envío está en `PENDIENTE_CONFIRMACION` no devuelve `YA_CONFIRMADO` ni expira por `VigenteHasta`, sino que continúa el registro en la misma transportista reutilizando el `CodigoSeguimiento` previamente asignado.
  - Idempotencia en el Simulador ([EnvioRegistradoRepository.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-simulador-eett/SimuladorEETT.Infrastructure/Repositories/EnvioRegistradoRepository.cs)): registro indexado por `idCorrelacion` (C2 v2.0 §4/§5), garantizando que peticiones repetidas devuelvan la misma `numeroGuiaEETT` y `FechaRegistro`.
- **Resultados Verificados**:
  - Prueba de timeout controlada con `X-Escenario: SIN_RESPUESTA`: `POST /confirmar` agotó los reintentos síncronos y respondió `PENDIENTE_CONFIRMACION`.
  - En BD: ninguna opción fue descartada (`Descartada = 0` para las 3 transportistas) y ninguna otra fue consultada.
  - Al retirar el escenario y reintentar, se confirmó exitosamente con el mismo `codigoSeguimiento` y entregó una sola guía en el Simulador.
  - Evidencia completa en [docs/arreglos_sin_azure/b4_sin_respuesta.txt](file:///d:/Proyectos/Jelaf/Courier/mediador-core/docs/arreglos_sin_azure/b4_sin_respuesta.txt).
- **Veredicto QA**: **Aprobado / Cerrado**.

---

### Corrección: Bloque 3 · idCorrelacion estable sin fecha (Regla R5)

- **Objetivo**: Evitar que el identificador `idCorrelacion` hacia la transportista se recalcule con la fecha del día (`MED-aaaa-MM-dd-idEnvio`), rompiendo la idempotencia de R5 en reintentos que cruzan la medianoche o se ejecutan días después.
- **Mecanismo Implementado**:
  - Script SQL [database/04_idcorrelacion.sql](file:///d:/Proyectos/Jelaf/Courier/mediador-core/database/04_idcorrelacion.sql) (idempotente) que agrega a `MED.Envio` las columnas `IdCorrelacionPrecio` e `IdCorrelacionRegistro`.
  - [IdCorrelacionGenerator.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.ApplicationCore/Utilidades/IdCorrelacionGenerator.cs) y [ConfirmacionRepository.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.Infrastructure/Repositories/ConfirmacionRepository.cs): genera y persiste el identificador sin fecha (UUID v7/v4 según soporte de runtime) antes de realizar la primera llamada HTTP a B3 (`/cotizar`) y B4 (`/registrar`).
  - [ConfirmacionMediadorService.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.ApplicationCore/Services/ConfirmacionMediadorService.cs): retiro definitivo de `ArmarIdCorrelacionSimulador`. Todo reintento (incluso tras reiniciar el Mediador) lee el identificador guardado en `MED.Envio` y nunca lo recalcula.
- **Resultados Verificados**:
  - Se provocó `SIN_RESPUESTA` en B4, quedando el envío en `PENDIENTE_CONFIRMACION` con su `IdCorrelacionRegistro` generado y persistido en `MED.Envio`.
  - Se detuvo por completo y se reinició el servicio Mediador.
  - Se repitió `POST /confirmar`: leyó el `idCorrelacion` guardado en BD, envió exactamente el mismo valor al Simulador y se confirmó con una sola guía.
  - Evidencia completa en [docs/arreglos_sin_azure/idcorrelacion_estable.txt](file:///d:/Proyectos/Jelaf/Courier/mediador-core/docs/arreglos_sin_azure/idcorrelacion_estable.txt).
- **Veredicto QA**: **Aprobado / Cerrado**.

---

### Corrección: Bloque 2 · Un solo formato de errores (Regla R8)

- **Objetivo**: Asegurar que todos los errores HTTP (códigos 4xx y 5xx) del Mediador devuelvan un formato JSON uniforme `{ error, detalle, campo, idCorrelacion }` (con `campo: null` cuando no aplique, Regla R11), protegiendo los detalles técnicos en errores 500 y manteniendo los estados de negocio de confirmación dentro de `HTTP 200 OK`.
- **Mecanismo Evaluado**:
  - Excepción personalizada `MediadorApiException` en [MediadorApiException.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.ApplicationCore/Exceptions/MediadorApiException.cs).
  - Middleware común [ManejadorExcepcionesMiddleware.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.Presentation/Middlewares/ManejadorExcepcionesMiddleware.cs) registrado al inicio de la canalización en [Program.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.Presentation/Program.cs).
  - Refactorización de controladores para lanzar excepciones tipadas:
    - [CotizarController.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.Presentation/Controllers/CotizarController.cs): Lanza HTTP 409 con `ESTADO_INVALIDO` ante conflictos de estado previo.
    - [ConfirmarController.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.Presentation/Controllers/ConfirmarController.cs): Lanza HTTP 404 con `OPCION_NO_ENCONTRADA` y `campo: "idOpcion"`. Los resultados de negocio (`OPCION_EXPIRADA`, `YA_CONFIRMADO`, `PENDIENTE_CONFIRMACION`, etc.) se conservan íntegros en `HTTP 200 OK`.
    - [EventosController.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.Presentation/Controllers/EventosController.cs): Lanza HTTP 409 con `ESTADO_INVALIDO` ante transiciones no permitidas.
  - Script automatizado de pruebas `test_r8_errores.py`.
- **Entorno de Prueba**:
  - Mediador levantado en `https://localhost:7250`, SQL Server `MediadorDev`.
- **Resultados Verificados**:
  - **Escenario 1 (409 en Cotizar)**: `POST /cotizar` con `idCorrelacion` de un envío ya `CONFIRMADO` (`COU-QA-409-CONFIRMADO`) respondió `HTTP 409 Conflict` con `error: "ESTADO_INVALIDO"`, `campo: null`, `idCorrelacion: "COU-QA-409-CONFIRMADO"`.
  - **Escenario 2 (404 en Confirmar)**: `POST /confirmar` con `idOpcion: "OPT-INEXISTENTE"` respondió `HTTP 404 Not Found` con `error: "OPCION_NO_ENCONTRADA"`, `campo: "idOpcion"`, `idCorrelacion: "COU-QA-404-NOTFOUND"`.
  - **Escenario 3 (409 en Eventos)**: `POST /eventos` notificando transición ilegal `ENTREGADO` sobre un envío en estado `COTIZADO` respondió `HTTP 409 Conflict` con `error: "ESTADO_INVALIDO"`, `campo: null`, `idCorrelacion: null`.
  - **Escenario 4 (500 Error Interno Forzado)**: `POST /cotizar` provocando excepción no controlada respondió `HTTP 500 Internal Server Error` con `error: "ERROR_INTERNO"`, detalle genérico sin exponer el stack trace, `campo: null`, y el `idCorrelacion` capturado en el log del servidor.
  - Evidencia completa en [docs/arreglos_sin_azure/r8_errores.txt](file:///d:/Proyectos/Jelaf/Courier/mediador-core/docs/arreglos_sin_azure/r8_errores.txt) y catálogo normativo en [docs/errores.md](file:///d:/Proyectos/Jelaf/Courier/mediador-core/docs/errores.md).
- **Veredicto QA**: **Aprobado / Cerrado**.

---

### Corrección: Bloque 3 · Bandeja de eventos del simulador (Reglas R6 y B7)

- **Objetivo**: Garantizar que el Simulador EETT guarde los eventos de trazabilidad en una bandeja persistente antes de intentar su envío por HTTP, asignando secuencias crecientes por transportista y exponiendo la operación B7 (`GET /v1/eventos?desde=N`) para recuperación tras cortes o caídas del Mediador.
- **Mecanismo Evaluado**:
  - Repositorio persistente `BandejaEventosRepository` en `SimuladorEETT.Infrastructure/Repositories/BandejaEventosRepository.cs` respaldado en archivo JSON local `bandeja_eventos.json`.
  - Sobre de evento actualizado según contrato C2 v2.0 §5: `idEvento` (UUID v7/v4), `secuencia` incremental monótona, `versionContrato: "2.0"`, `entidad: { tipo: "ENVIO", id: "..." }`, `versionEntidad: 1` y `emisor: "CONECTOR"`.
  - Guardado en bandeja previo a la llamada HTTP en `EventoMediadorService.cs`, con captura de excepciones de red para no perder eventos cuando el Mediador no responde.
  - Controlador B7 `EventosController` en `SimuladorEETT.Presentation/Controllers/EventosController.cs` con ruta `GET /v1/eventos?desde=N`.
  - Script automatizado de pruebas `test_b7_bandeja.py`.
- **Entorno de Prueba**:
  - Simulador EETT levantado en `https://localhost:7201`.
  - Mediador deliberadamente apagado / no disponible para validar la persistencia sin pérdida de mensajes ante fallas de red.
- **Resultados Verificados**:
  - Se emitieron 3 eventos secuenciales con `POST /simular-evento` (`RECEPCION_CONFIRMADA`, `BUS_ASIGNADO` y `EN_VIAJE`).
  - Al estar el Mediador apagado, los eventos se persistieron en `bandeja_eventos.json` con `enviado: false` sin arrojar error no controlado en el Simulador.
  - Se consultó `GET /v1/eventos?desde=0`: se recuperaron exactamente los 3 eventos con secuencias 1, 2 y 3 en orden ascendente, cumpliendo con todos los campos de C2 v2.0 §5.
  - Se comprobó la supervivencia ante reinicio del servicio Simulador, validando que la consulta con `desde=2` devuelve únicamente el evento con secuencia 3.
  - Evidencia completa en [docs/arreglos_sin_azure/b7_bandeja.txt](file:///d:/Proyectos/Jelaf/Courier/mediador-core/docs/arreglos_sin_azure/b7_bandeja.txt) (y en el Simulador) y justificación en `README.md` del Simulador.
- **Veredicto QA**: **Aprobado / Cerrado**.
