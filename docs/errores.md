# Catálogo de Errores HTTP — Regla R8 (Propuesta v2.1)

Documento normativo y de referencia técnica para el tratamiento y estandarización de errores HTTP en la plataforma **Mediador**, en cumplimiento de la **Regla R8** de los contratos de integración (C1, C2 y Reglas Comunes v1.1).

---

## 1. Formato Único de Error (Regla R8)

Todas las respuestas de error HTTP (códigos 4xx y 5xx) emitidas por la API del Mediador deben devolver estrictamente la siguiente estructura JSON en UTF-8 con cabecera `Content-Type: application/json; charset=utf-8`:

```json
{
  "error": "CODIGO_ERROR",
  "detalle": "Descripción clara y legible del motivo del error.",
  "campo": "nombreDelCampo",
  "idCorrelacion": "COU-2026-11-16-000123"
}
```

### Reglas de los Campos:
- **`error`** (string, obligatorio): Código mnemónico en mayúsculas y guiones bajos (screaming snake case) que identifica la categoría o causa del error.
- **`detalle`** (string, obligatorio): Explicación amigable para el consumidor sobre la causa del fallo o la acción esperada. En errores `500` nunca debe exponer detalles técnicos internos (stack traces o errores SQL).
- **`campo`** (string o `null`, obligatorio): Nombre en camelCase del campo del cuerpo o parámetro que causó el error. **Si el error es general, de estado o el campo no aplica, debe enviarse explícitamente en `null` (Regla R11)**.
- **`idCorrelacion`** (string o `null`, obligatorio): Identificador de correlación de la transacción recibido en la petición (o cabecera). En errores 500 no controlados, si no se envió en la petición, el servidor genera o asocia el identificador de rastreo para correlacionar con los logs del servidor.

> [!IMPORTANT]
> **Estados de Negocio en HTTP 200 OK (Operación A2 - Confirmar):**
> Los resultados de negocio de la operación de confirmación (`POST /confirmar`), tales como `OPCION_EXPIRADA`, `YA_CONFIRMADO`, `PENDIENTE_CONFIRMACION`, `PRECIO_CAMBIO` o `SIN_OPCIONES_DISPONIBLES`, representan estados válidos del ciclo de vida y viajan como respuesta de éxito `HTTP 200 OK` dentro del payload de negocio (`ConfirmarResponseDto`). **Bajo ninguna circunstancia deben tratarse como errores HTTP.**

---

## 2. Tabla de Códigos de Error HTTP (Propuesta v2.1)

| Código de Error (`error`) | HTTP Status | Tipo | ¿Se reintenta? | Descripción / Cuándo ocurre | Campo (`campo`) |
| :--- | :---: | :---: | :---: | :--- | :--- |
| **`SOLICITUD_INVALIDA`** | `400 Bad Request` | Permanente | No | El payload JSON está malformado, tipos de datos incompatibles o sintaxis inválida. | Nombre del campo o `null` |
| **`NO_AUTORIZADO`** | `401 Unauthorized` | Permanente | No | Falta la cabecera de autenticación (`X-API-Key`) o la clave no está registrada (Pendiente 10). | `null` |
| **`ACCESO_DENEGADO`** | `403 Forbidden` | Permanente | No | La clave de API es válida pero no tiene permisos para la entidad u operación solicitada. | `null` |
| **`OPCION_NO_ENCONTRADA`** | `404 Not Found` | Permanente | No | El identificador `idOpcion` enviado en `POST /confirmar` no existe en la base de datos o fue descartado. | `"idOpcion"` |
| **`RECURSO_NO_ENCONTRADO`** | `404 Not Found` | Permanente | No | El recurso solicitado por identificador no existe (ej. código de seguimiento en `GET /envio/{codigoSeguimiento}`). | `"codigoSeguimiento"` o `null` |
| **`TIMEOUT`** | `408 Request Timeout` | Temporal | Sí | Tiempo de espera agotado al consultar a una transportista externa dentro del límite (`t_precio_eett` o `t_registro_eett`). | `null` |
| **`ESTADO_INVALIDO`** | `409 Conflict` | Permanente | No | Conflicto de estado de la entidad: <br>1. En `POST /cotizar`: el `idCorrelacion` ya existe en un estado distinto de `COTIZADO`.<br>2. En `POST /eventos`: la transición solicitada no es válida para el estado actual del envío. | `null` |
| **`CORRELACION_DUPLICADA`** | `409 Conflict` | Permanente | No | Se detectó el mismo `idCorrelacion` con un payload o contenido diferente al registrado originalmente (violación de R5). | `"idCorrelacion"` |
| **`BULTO_EN_OTRO_TRASLADO`** | `409 Conflict` | Permanente | No | El bulto ya se encuentra asignado a otra orden de traslado activa. | `"idBulto"` |
| **`BULTO_EN_TRASLADO`** | `409 Conflict` | Permanente | No | Se intentó declarar entrega manual (A5) de un bulto que ya está en custodia de un traslado TL. | `"idBulto"` |
| **`ENVIO_EN_TRASLADO`** | `409 Conflict` | Permanente | No | Se intentó anular un envío con bultos en un traslado en curso (A4). | `"codigoSeguimiento"` |
| **`DATOS_INVALIDOS`** | `422 Unprocessable Entity` | Permanente | No | Violación de reglas semánticas de validación (ej. ubigeo no válido, peso negativo o mayor al límite). | Nombre del campo |
| **`MAXIMO_PARADAS_EXCEDIDO`** | `422 Unprocessable Entity` | Permanente | No | El traslado requiere más paradas que el parámetro permitido (`n_paradas_max`). | `"paradas"` |
| **`PUNTO_SIN_COORDENADAS`** | `422 Unprocessable Entity` | Permanente | No | El centro de acopio de origen no tiene coordenadas geográficas válidas para el traslado. | `"idPuntoOrigen"` |
| **`LIMITE_PETICIONES_EXCEDIDO`** | `429 Too Many Requests` | Temporal | Sí | El cliente superó la cuota de peticiones permitidas por unidad de tiempo (rate limiting). | `null` |
| **`ERROR_INTERNO`** | `500 Internal Server Error` | Temporal | Sí | Excepción no controlada o fallo técnico imprevisto en el Mediador. El detalle real va al log del servidor. | `null` |
| **`SERVICIO_NO_DISPONIBLE`** | `503 Service Unavailable` | Temporal | Sí | Caída de conexión con la base de datos o indisponibilidad temporal de servicios dependientes. | `null` |

---

## 3. Criterio de Clasificación: Temporal vs. Permanente

Conforme a la **Regla R8** de los Contratos:

### A. Errores Permanentes (No se reintentan)
- **Definición**: Errores causados por peticiones sintácticamente incorrectas, datos faltantes, violaciones de reglas de negocio, falta de autenticación o estados incompatibles de la entidad.
- **Comportamiento**: Un reintento idéntico producirá exactamente el mismo error. El cliente debe corregir la petición o el estado antes de volver a intentar.
- **Códigos**: `400`, `401`, `403`, `404`, `409`, `422`.

### B. Errores Temporales (Se reintentan)
- **Definición**: Fallos transitorios debidos a problemas de red, desconexiones, tiempos de espera expirados, contención de concurrencia o saturación de capacidad del servidor.
- **Comportamiento**: Pueden recuperarse automáticamente tras una espera controlada.
- **Política de Reintentos**:
  - Para peticiones síncronas cliente-servidor (C1/C2): Se reintentan aplicando los intervalos escalonados del parámetro `r_esperas_sincronas` (2 s, 5 s y 10 s; máximo 3 reintentos).
  - Si tras agotar los reintentos persiste la falla, se eleva la alerta respectiva.
- **Códigos**: `408`, `429`, `500`, `503`.

---

## 4. Ejemplos de Respuesta en Mediador

### Ejemplo 1: `404 Not Found` (Confirmar opción inexistente)
```http
HTTP/1.1 404 Not Found
Content-Type: application/json; charset=utf-8

{
  "error": "OPCION_NO_ENCONTRADA",
  "detalle": "No se encontró la opción de cotización 'OPT-999999'.",
  "campo": "idOpcion",
  "idCorrelacion": "COU-PRUEBA-001"
}
```

### Ejemplo 2: `409 Conflict` (Cotizar con idCorrelacion en estado avanzado)
```http
HTTP/1.1 409 Conflict
Content-Type: application/json; charset=utf-8

{
  "error": "ESTADO_INVALIDO",
  "detalle": "Ya existe un envío con este idCorrelacion en un estado distinto de COTIZADO (estado actual: CONFIRMADO).",
  "campo": null,
  "idCorrelacion": "COU-PRUEBA-001"
}
```

### Ejemplo 3: `409 Conflict` (Evento con transición ilegal)
```http
HTTP/1.1 409 Conflict
Content-Type: application/json; charset=utf-8

{
  "error": "ESTADO_INVALIDO",
  "detalle": "Transición de estado no permitida. El envío se encuentra en estado 'COTIZADO'.",
  "campo": null,
  "idCorrelacion": null
}
```

### Ejemplo 4: `500 Internal Server Error` (Fallo interno no controlado)
```http
HTTP/1.1 500 Internal Server Error
Content-Type: application/json; charset=utf-8

{
  "error": "ERROR_INTERNO",
  "detalle": "Ocurrió un error inesperado al procesar la solicitud.",
  "campo": null,
  "idCorrelacion": "COU-PRUEBA-001"
}
```
*(El error técnico detallado y el stack trace quedan registrados en el log del servidor bajo el `idCorrelacion` especificado).*
