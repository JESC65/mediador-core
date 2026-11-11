# Lista de Pendientes - Proyecto Mediador

Documento de seguimiento de tareas, pendientes técnicos y pruebas de aseguramiento de calidad (QA) del componente Mediador.

---

## Tabla de Seguimiento de Pendientes

| N.º | Estado | Evidencia | Qué falta |
| :---: | :---: | :--- | :--- |
| 1 | Sin empezar | A la espera de accesos RDP al servidor de JELAF | Credenciales e instalación |
| 2 | Cerrado | Prueba de 3 peticiones simultáneas deja una sola fila | Nada |
| 3 | A medias | Error 500 y colisión de clave única (UQ_Envio_CodigoSeguimiento); dos peticiones simultáneas calculan el mismo código correlativo | Crear SEQUENCE en SQL Server y usar NEXT VALUE FOR en GenerarSiguienteCodigoSeguimiento en vez de MAX()+1 en memoria |
| 4 | A medias | Modelo de BD tiene columnas en MED.EnvioPaquete, pero API ignora largoCm, anchoCm, altoCm, valorDeclarado y motivo (se guardan NULL) | Agregar campos a PaqueteCotizarDto y CotizarRequestDto, y mapear su persistencia en EnvioRepository |
| 5 | Cerrado | Documentado en docs/migracion_net10.md (Pasos 7, 8 y 9: PRECIO_DISTINTO, SIN_RESPUESTA y ERROR_500 verificados) | Nada |
| 6 | Sin empezar | No se encontró script SQL para MED.Tb_EnvioMediador en el Conector Courier (solo existe mapeo de entidad en CourierDbContext) | Crear script DDL SQL para generar el esquema MED y la tabla MED.Tb_EnvioMediador en Dbcourier_dev |
| 7 | Cerrado | Por decisión: el Courier nuevo llamará directo al Mediador (sin código) | Nada |
| 8 | Sin empezar | No existen controladores ni métodos de servicio para Entrega física (A2c) ni Anulación (A4 y B5) | Implementar endpoints y lógica de negocio para entrega física (A2c) y anulación hacia transportistas (A4 y B5) |
| 9 | Cerrado | PruebaSimuladorController no cuenta con #if DEBUG ni restricción por ambiente; responde incondicionalmente | Controlador eliminado el 11-nov-2026 por decisión del responsable técnico |
| 10 | Sin empezar | La API responde HTTP 200 a peticiones sin clave; no existe validación ni middleware de API Key | Implementar autenticación obligatoria por API Key (X-API-Key) para clientes autorizados |
| 11 | Sin empezar | Controladores exponen rutas planas (/cotizar, /confirmar, etc.) sin prefijo /v1/ | Versionar los endpoints anteponiendo /v1/ en los Route attributes o convención global |
| 12 | Cerrado | READMEs actualizados en los 3 repositorios (hoy) | Nada |
| 13 | Cerrado | Ramas net10 con ciclo completo probado (docs/migracion_net10.md) | Nada |

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

### Pendiente N.º 3: Correlativo del código con una secuencia de SQL Server

- **Objetivo**: Confirmar que ante confirmaciones concurrentes de clientes y envíos distintos (`IdEnvio` diferentes), el sistema genere códigos de seguimiento correlativos únicos y sin colisiones mediante una secuencia en base de datos.
- **Mecanismo Evaluado**:
  - [ConfirmacionRepository.GenerarSiguienteCodigoSeguimiento()](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.Infrastructure/Repositories/ConfirmacionRepository.cs#L147-L167).
  - Validación de existencia de secuencia en SQL Server (`sys.sequences`).
- **Entorno de Prueba**:
  - Mediador levantado en `https://localhost:7250`.
  - Simulador EETT levantado en `https://localhost:7201`.
  - Script automatizado con sincronización estricta por barrera (`threading.Barrier(2)` en [`test_concurrencia_secuencia.py`](file:///d:/Proyectos/Jelaf/Courier/test_concurrencia_secuencia.py)).
- **Parámetros de Prueba**:
  - Cliente A: `IdCorrelacion = COU-REQ-010-1793217195` (Envío cotizado independiente)
  - Cliente B: `IdCorrelacion = COU-REQ-011-1793217195` (Envío cotizado independiente)
  - Disparo de 2 peticiones `POST /confirmar` estrictamente simultáneas.
- **Resultados HTTP Obtenidos**:
  - **Cliente B**: `HTTP 200 OK` → `estado: "CONFIRMADO"`, `codigoSeguimiento: "MED-2026-000006"`.
  - **Cliente A**: `HTTP 500 Internal Server Error` → `DbUpdateException: Cannot insert duplicate key row in object 'MED.Envio' with unique index 'UQ_Envio_CodigoSeguimiento'. The duplicate key value is (MED-2026-000006)`.
- **Causa Raíz Detectada**:
  1. En `sys.sequences` de SQL Server no existe ninguna secuencia creada (0 secuencias).
  2. `GenerarSiguienteCodigoSeguimiento()` calcula el correlativo en memoria leyendo `_db.Envios.AsNoTracking().Where(...).Select(...).Max() + 1` sin ningún tipo de bloqueo ni reserva atómica.
  3. Al ejecutarse dos confirmaciones en paralelo para envíos distintos, ambas leen el mismo estado inicial de la BD, calculan el mismo código correlativo (`MED-2026-000006`), y la que intenta persistir en segundo lugar colisiona contra la restricción de unicidad, abortando con error 500.
- **Veredicto QA**: **Falla / A medias**.

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

- **Objetivo**: Comprobar si los endpoints de prueba poseen restricciones para evitar su exposición y ejecución en entornos productivos.
- **Mecanismo Evaluado**:
  - [PruebaSimuladorController.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.Presentation/Controllers/PruebaSimuladorController.cs).
  - Configuración del pipeline en [Program.cs](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.Presentation/Program.cs).
- **Resultados Obtenidos**:
  - Las rutas `POST /prueba/simulador/{idTransportista}/cotizar` y `POST /prueba/simulador/{idTransportista}/registrar` están expuestas de forma pública e incondicional.
  - No existe directiva `#if DEBUG`, verificación de entorno (`IHostEnvironment.IsDevelopment()`) ni filtros de autorización que restrinjan su consumo.
- **Veredicto QA**: **Pendiente / Sin empezar**.
- **Resolución**: PruebaSimuladorController eliminado; las rutas /prueba/simulador/... ya no existen.

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



