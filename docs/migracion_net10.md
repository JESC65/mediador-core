# Migración de Solución a .NET 10

Documento de seguimiento para la migración de los tres componentes de la solución (`Mediador`, `Conector Courier` y `Simulador EETT`) desde **.NET 8** hacia **.NET 10**.

---

## 1. Configuración de SDK y TargetFramework

- **TargetFramework**: Se actualizó `<TargetFramework>net10.0</TargetFramework>` en los 9 proyectos `.csproj` de la solución.
- **global.json**: Se verificó que no existía ningún archivo `global.json` fijando la versión 8 en los repositorios; el entorno utiliza el SDK de .NET 10 (`10.0.302`).

---

## 2. Actualización de Paquetes NuGet

A continuación se detalla la actualización de dependencias correspondientes a `Microsoft.EntityFrameworkCore.*` y `Microsoft.Extensions.*` a la versión estable **10.0.14**:

| Paquete | Versión anterior | Nueva versión |
| :--- | :---: | :---: |
| `Microsoft.EntityFrameworkCore` | `8.0.0` | `10.0.14` |
| `Microsoft.EntityFrameworkCore.SqlServer` | `8.0.0` / `8.0.8` | `10.0.14` |
| `Microsoft.Extensions.Configuration.Abstractions` | `8.0.0` | `10.0.14` |
| `Microsoft.Extensions.Hosting.Abstractions` | `8.0.0` | `10.0.14` |
| `Microsoft.Extensions.Http` | `8.0.0` / `10.0.12` | `10.0.14` |

### Detalle por Proyecto

1. **`mediador-core`**:
   - [Mediador.ApplicationCore.csproj](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.ApplicationCore/Mediador.ApplicationCore.csproj): `net10.0`
   - [Mediador.Infrastructure.csproj](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.Infrastructure/Mediador.Infrastructure.csproj): `net10.0`
     - `Microsoft.EntityFrameworkCore.SqlServer`: `8.0.8` → `10.0.14`
     - `Microsoft.Extensions.Http`: `10.0.12` → `10.0.14`
   - [Mediador.Presentation.csproj](file:///d:/Proyectos/Jelaf/Courier/mediador-core/Mediador.Presentation/Mediador.Presentation.csproj): `net10.0`

2. **`mediador-conector-courier`**:
   - [Courier.ApplicationCore.csproj](file:///d:/Proyectos/Jelaf/Courier/mediador-conector-courier/Courier.ApplicationCore/Courier.ApplicationCore.csproj): `net10.0`
   - [Courier.Infrastructure.csproj](file:///d:/Proyectos/Jelaf/Courier/mediador-conector-courier/Courier.Infrastructure/Courier.Infrastructure.csproj): `net10.0`
     - `Microsoft.EntityFrameworkCore`: `8.0.0` → `10.0.14`
     - `Microsoft.EntityFrameworkCore.SqlServer`: `8.0.0` → `10.0.14`
   - [Courier.Presentation.csproj](file:///d:/Proyectos/Jelaf/Courier/mediador-conector-courier/Courier.Presentation/Courier.Presentation.csproj): `net10.0`

3. **`mediador-simulador-eett`**:
   - [SimuladorEETT.ApplicationCore.csproj](file:///d:/Proyectos/Jelaf/Courier/mediador-simulador-eett/SimuladorEETT.ApplicationCore/SimuladorEETT.ApplicationCore.csproj): `net10.0`
   - [SimuladorEETT.Infrastructure.csproj](file:///d:/Proyectos/Jelaf/Courier/mediador-simulador-eett/SimuladorEETT.Infrastructure/SimuladorEETT.Infrastructure.csproj): `net10.0`
     - `Microsoft.Extensions.Configuration.Abstractions`: `8.0.0` → `10.0.14`
     - `Microsoft.Extensions.Hosting.Abstractions`: `8.0.0` → `10.0.14`
     - `Microsoft.Extensions.Http`: `8.0.0` → `10.0.14`
   - [SimuladorEETT.Presentation.csproj](file:///d:/Proyectos/Jelaf/Courier/mediador-simulador-eett/SimuladorEETT.Presentation/SimuladorEETT.Presentation.csproj): `net10.0`

---

## Errores de compilación resueltos

<!-- Registra aquí los errores de compilación encontrados y cómo fueron corregidos -->
*No se han registrado errores de compilación aún. Espacio reservado para validación manual.*

---

## Advertencias pendientes

<!-- Registra aquí las advertencias (warnings) detectadas durante la compilación manual -->
*No se han registrado advertencias pendientes aún. Espacio reservado para validación manual.*

---

## 3. Comparativa de Resultados de Pruebas (.NET 8 vs .NET 10)

Evaluación del ciclo completo aplicando la regla de negocio: se ignoran diferencias operativas esperadas en identificadores (`idCorrelacion`, `idOpcion`, `idEvento`), fechas/vigencias y correlativos de `codigoSeguimiento`.

| Paso | resultado con .NET 8 | resultado con .NET 10 | ¿igual? |
| :--- | :--- | :--- | :--- |
| **1. Cotizar (`POST /cotizar`)** | 200 OK - Estado COTIZADO, 3 opciones ordenadas (precios: 15.3, 18, 22.5) | 200 OK - Estado COTIZADO, 3 opciones ordenadas (precios: 15.3, 18, 22.5) | Sí |
| **2. Confirmar (`POST /confirmar`)** | 200 OK - Estado CONFIRMADO, Transportista 8, precioFinal 15.3 | 200 OK - Estado CONFIRMADO, Transportista 8, precioFinal 15.3 | Sí |
| **3. Evento (`POST /eventos`)** | 200 OK - recibido: true (BUS_ASIGNADO) | 200 OK - recibido: true (BUS_ASIGNADO) | Sí |
| **4. Consultar Estado (`GET /envio/{codigo}`)** | 200 OK - EstadoActual EN_VIAJE, busAsignado T5X-840, historial de 3 estados | 200 OK - EstadoActual EN_VIAJE, busAsignado T5X-840, historial de 3 estados | Sí |
| **5. Simulador (`POST /cotizar`)** | 200 OK - disponible: true, precio: 21.6 | 200 OK - disponible: true, precio: 18 | No (Discrepancia en precio: 21.6 en .NET 8 vs 18 en .NET 10. La línea base registró el valor con recargo +20% del escenario PRECIO_DISTINTO, mientras que .NET 10 ejecutó la tarifa base normal de 18.00) |
| **6. Confirmar - Repetición (`POST /confirmar`)** | 200 OK - Estado YA_CONFIRMADO (Idempotencia) | Estado YA_CONFIRMADO | Sí |
| **7. Falla: Precio Distinto (`POST /cotizar`)** | 200 OK - disponible: true, precio: 25.92 | disponible: true, precio: 25.92 | Sí |
| **8. Falla: Sin Respuesta (`POST /cotizar`)** | 408 TIMEOUT - Conexión expirada (EmptyResult) | 408 TIMEOUT | Sí |
| **9. Falla: Error 500 (`POST /cotizar`)** | 500 INTERNAL_SERVER_ERROR - Falla simulada | 500 INTERNAL_SERVER_ERROR | Sí |

