# Mediador

Componente central que compara tarifas entre un courier y varias transportistas, confirma el envío elegido, y recibe los avisos de estado de la transportista hasta la entrega. No expone nada al cliente final — solo habla con el conector del courier y con los conectores/simulador de las transportistas.

## Instalación desde cero

Requisitos: .NET 8 SDK, SQL Server (local o de desarrollo), Git.

1. Clonar el repositorio y entrar a la carpeta del Mediador.
2. Restaurar el esquema y los datos de referencia, en este orden, contra la base `MediadorDev`:
   ```
   database/scripts/01_esquema.sql       -- crea el esquema MED y las 13 tablas
   database/scripts/02_datos_prueba.sql  -- courier demo, 3 transportistas, 6 rutas, tarifas
   ```
3. En `Mediador.Presentation/appsettings.Development.json`, configurar la cadena de conexión:
   ```json
   { "ConnectionStrings": { "MediadorDev": "Server=...;Database=MediadorDev;..." } }
   ```
4. Restaurar paquetes y levantar:
   ```
   dotnet restore
   dotnet run --project Mediador.Presentation
   ```
5. Por defecto queda escuchando en `https://localhost:7250`. Swagger disponible en `https://localhost:7250/swagger`.

**Orden con el resto del sistema:** el Mediador debe estar arriba *antes* de levantar el conector Courier (que le llama) y necesita que el simulador (o los conectores reales de las transportistas) esté accesible antes de intentar cualquier `POST /confirmar` — sin eso, las confirmaciones fallan en el paso de consultar precio real, no por un error del Mediador.

## Prueba mínima

```
POST /cotizar
{
  "idCorrelacion": "COU-TEST-001",
  "idEnvioCourier": "1",
  "idCourier": 1,
  "ubigeoOrigen": "150101",
  "ubigeoDestino": "130101",
  "tipoCarga": "CARGA_LIVIANA",
  "fechaSolicitud": "2026-09-28T09:00:00-05:00",
  "paquetes": [{ "descripcion": "Prueba", "pesoKg": 5, "cantidad": 1, "esFragil": false }]
}
```
Si el entorno está bien instalado, responde con las tres transportistas ordenadas por precio. Si responde `SIN_OPCIONES` o vacío, revisar que `02_datos_prueba.sql` haya corrido.

## Claves de configuración

| Clave | Para qué sirve |
|---|---|
| `ConnectionStrings:MediadorDev` | Cadena de conexión a la base SQL Server del Mediador |
| `Simulador:Escenarios` | **Solo desarrollo.** Mapa `puerto/host → escenario` (`NORMAL`, `PRECIO_DISTINTO`, `NO_DISPONIBLE`, `LENTO`, `SIN_RESPUESTA`, `ERROR_500`) para forzar que una transportista falle de forma controlada en pruebas, vía el encabezado `X-Escenario`. Debe quedar vacío o ausente en producción — no cambia nada si no está. |

## Despliegue

### Requisitos
- **.NET 10 ASP.NET Core Runtime** instalado en el servidor o entorno de ejecución.
- **SQL Server** con el esquema `MED` inicializado y datos de referencia restaurados (`01_esquema.sql`, `02_datos_prueba.sql`, `03_correlativo.sql`).

### Cómo publicar con el script
Ejecutar el script PowerShell desde el repositorio:

```powershell
.\scripts\publicar.ps1
```

Por defecto, genera los binarios en `C:\publicado\mediador`. Si se requiere otra ruta, se puede indicar el parámetro `-Destino`:

```powershell
.\scripts\publicar.ps1 -Destino "D:\mi-carpeta\mediador"
```

El script compila en configuración `Release` y ejecuta una regla crítica de seguridad: limpia la cadena de conexión en `C:\publicado\mediador\appsettings.json` (dejándola en blanco `""`) y elimina `appsettings.Development.json` de la carpeta publicada, garantizando que ninguna cadena de conexión quede embebida en el paquete.

### Variables de entorno obligatorias
La configuración sensible y los endpoints se suministran exclusivamente por variables de entorno:

- `ConnectionStrings__MediadorDev`: Cadena de conexión a la base de datos SQL Server (`MediadorDev`).
  - Ejemplo: `Server=localhost;Database=MediadorDev;Trusted_Connection=True;TrustServerCertificate=True;`
- `ASPNETCORE_URLS`: Direcciones y puertos de escucha del servicio web.
  - Ejemplo: `https://localhost:7250;http://localhost:5250`

### Cómo arrancar
Desde la carpeta de publicación (`C:\publicado\mediador`), definir las variables de entorno en la sesión de terminal y arrancar la DLL:

```powershell
cd C:\publicado\mediador
$env:ConnectionStrings__MediadorDev = "Server=localhost;Database=MediadorDev;Trusted_Connection=True;TrustServerCertificate=True;"
$env:ASPNETCORE_URLS = "https://localhost:7250;http://localhost:5250"
dotnet Mediador.Presentation.dll
```

### Cómo comprobar que responde
Enviar una petición `POST /cotizar` y verificar que devuelva HTTP `200 OK` con estado `COTIZADO` y las opciones de transporte disponibles:

```powershell
# En PowerShell:
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
$body = @'
{
  "idCorrelacion": "COU-PRUEBA-001",
  "idEnvioCourier": "ENV-9021",
  "idCourier": 1,
  "ubigeoOrigen": "150101",
  "ubigeoDestino": "130101",
  "tipoCarga": "CARGA_LIVIANA",
  "fechaSolicitud": "2026-10-27T10:00:00-05:00",
  "paquetes": [
    {
      "descripcion": "Caja con laptops y accesorios",
      "pesoKg": 4.5,
      "cantidad": 1,
      "largoCm": 40.0,
      "anchoCm": 30.0,
      "altoCm": 15.0,
      "esFragil": false,
      "valorDeclarado": 2500.0
    }
  ]
}
'@

Invoke-RestMethod -Uri "https://localhost:7250/cotizar" -Method Post -ContentType "application/json" -Body $body
```

O mediante `curl`:
```bash
curl -k -X POST https://localhost:7250/cotizar \
  -H "Content-Type: application/json" \
  -d '{
    "idCorrelacion": "COU-PRUEBA-001",
    "idEnvioCourier": "ENV-9021",
    "idCourier": 1,
    "ubigeoOrigen": "150101",
    "ubigeoDestino": "130101",
    "tipoCarga": "CARGA_LIVIANA",
    "fechaSolicitud": "2026-10-27T10:00:00-05:00",
    "paquetes": [
      {
        "descripcion": "Caja con laptops y accesorios",
        "pesoKg": 4.5,
        "cantidad": 1,
        "largoCm": 40.0,
        "anchoCm": 30.0,
        "altoCm": 15.0,
        "esFragil": false,
        "valorDeclarado": 2500.0
      }
    ]
  }'
```

### Paso a Azure
Pendiente del acceso.

## Instalación en la máquina de Martín

Pendiente de completar: el sábado 26 se intentó preparar el entorno en esa máquina, pero se detuvo porque la base `MediadorDev` en el servidor de pruebas todavía no estaba creada — no se llegó a levantar el servicio allí. En cuanto la base exista, los pasos a seguir son los mismos de la sección "Instalación desde cero" de arriba, apuntando la cadena de conexión al servidor de pruebas en vez de a una instancia local. Esta sección se actualiza con los pasos reales en cuanto se ejecuten.
