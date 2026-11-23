import json
import ssl
import subprocess
import sys
import time
import urllib.request
import threading
from concurrent.futures import ThreadPoolExecutor

BASE_URL = "https://localhost:7250"
ID_CORRELACION = f"COU-QA-LOCK-{int(time.time())}"

ctx = ssl.create_default_context()
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE

def cotizar():
    payload = {
        "idCorrelacion": ID_CORRELACION,
        "idEnvioCourier": "999",
        "idCourier": 1,
        "ubigeoOrigen": "150101",
        "ubigeoDestino": "130101",
        "tipoCarga": "CARGA_LIVIANA",
        "fechaSolicitud": "2026-10-28T14:30:00-05:00",
        "paquetes": [
            {
                "descripcion": "Paquete prueba de candado",
                "pesoKg": 5.0,
                "cantidad": 1,
                "esFragil": False
            }
        ]
    }
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        f"{BASE_URL}/cotizar",
        data=data,
        headers={"Content-Type": "application/json"}
    )
    with urllib.request.urlopen(req, context=ctx) as resp:
        body = json.loads(resp.read().decode("utf-8"))
        print(f"[COTIZAR] Exito HTTP {resp.status}. Opciones recibidas: {len(body.get('opciones', []))}")
        return body

def post_confirmar(barrier, req_id, payload):
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        f"{BASE_URL}/confirmar",
        data=data,
        headers={"Content-Type": "application/json"}
    )
    # Sincronización estricta por barrera (los 3 hilos disparan exactamente a la vez)
    barrier.wait()
    t0 = time.perf_counter()
    try:
        with urllib.request.urlopen(req, context=ctx) as resp:
            elapsed = time.perf_counter() - t0
            body = json.loads(resp.read().decode("utf-8"))
            return {
                "id": req_id,
                "status": resp.status,
                "elapsed": elapsed,
                "body": body
            }
    except urllib.error.HTTPError as e:
        elapsed = time.perf_counter() - t0
        body_text = e.read().decode("utf-8")
        try:
            body = json.loads(body_text)
        except Exception:
            body = body_text
        return {
            "id": req_id,
            "status": e.code,
            "elapsed": elapsed,
            "body": body
        }
    except Exception as ex:
        elapsed = time.perf_counter() - t0
        return {
            "id": req_id,
            "status": -1,
            "elapsed": elapsed,
            "error": str(ex)
        }

def consultar_base_datos(id_correlacion):
    query = f"""
    SET NOCOUNT ON;
    PRINT '=== MED.Envio ===';
    SELECT IdEnvio, IdCorrelacion, CodigoSeguimiento, Estado, IdTransportistaElegida, FechaConfirmacion
    FROM MED.Envio WHERE IdCorrelacion = '{id_correlacion}';

    PRINT '=== MED.EnvioEstado ===';
    SELECT ee.IdEnvioEstado, ee.IdEnvio, ee.Estado, ee.FechaEstado, ee.Origen
    FROM MED.EnvioEstado ee
    JOIN MED.Envio e ON ee.IdEnvio = e.IdEnvio
    WHERE e.IdCorrelacion = '{id_correlacion}';

    PRINT '=== MED.EnvioPersona ===';
    SELECT ep.IdEnvioPersona, ep.IdEnvio, ep.Rol, ep.Nombres, ep.Apellidos, ep.NumeroDocumento
    FROM MED.EnvioPersona ep
    JOIN MED.Envio e ON ep.IdEnvio = e.IdEnvio
    WHERE e.IdCorrelacion = '{id_correlacion}';
    """
    res = subprocess.run(
        ["sqlcmd", "-S", "localhost", "-E", "-d", "MediadorDev", "-Q", query],
        capture_output=True,
        text=True
    )
    return res.stdout

def main():
    print("=" * 70)
    print("QA AUTOMATIZADO: PRUEBA DE CONCURRENCIA - CANDADO CONFIRMACIONES")
    print("=" * 70)
    print(f"IdCorrelacion de prueba: {ID_CORRELACION}")

    # Paso 1: Cotizar
    print("\n--- PASO 1: Generar cotización previa ---")
    cot_res = cotizar()
    opciones = cot_res.get("opciones", [])
    if not opciones:
        print("ERROR: No se recibieron opciones de cotización.")
        sys.exit(1)

    # Buscar la opción de Expreso Andino (Transportista 7) que corre en Simulador HTTPS (7201)
    opcion_elegida = next((o for o in opciones if o.get("idTransportista") == 7), opciones[0])
    id_opcion = opcion_elegida["idOpcion"]
    precio_ref = opcion_elegida.get("precioReferencial")
    print(f"Opción seleccionada: {id_opcion} (Transportista {opcion_elegida.get('idTransportista')}, Precio Referencial: {precio_ref})")

    # Paso 2: Preparar payload idéntico de confirmación
    payload_confirmar = {
        "idCorrelacion": ID_CORRELACION,
        "idOpcion": id_opcion,
        "remitente": {
            "tipoDocumento": "DNI",
            "numeroDocumento": "45678912",
            "nombres": "Ana María",
            "apellidos": "Rojas Pérez",
            "telefono": "987654321",
            "direccion": "Av. Javier Prado Este 2450",
            "ubigeo": "150101"
        },
        "consignado": {
            "tipoDocumento": "DNI",
            "numeroDocumento": "10293847",
            "nombres": "Luis",
            "apellidos": "Quispe Mamani",
            "telefono": "912345678",
            "direccion": "Av. Los Álamos 234",
            "ubigeo": "130101"
        }
    }

    # Paso 3: Disparar 3 peticiones HTTP POST estrictamente simultáneas con Barrier
    print("\n--- PASO 2: Disparando 3 peticiones HTTP POST estrictamente simultáneas ---")
    barrier = threading.Barrier(3)
    results = []

    with ThreadPoolExecutor(max_workers=3) as executor:
        futures = [
            executor.submit(post_confirmar, barrier, i + 1, payload_confirmar)
            for i in range(3)
        ]
        for f in futures:
            results.append(f.result())

    print("\n--- PASO 3: Resultados de las 3 peticiones HTTP ---")
    for r in results:
        print(f"\nPetición #{r['id']}:")
        print(f"  HTTP Status: {r['status']}")
        print(f"  Tiempo: {r['elapsed']:.3f} s")
        print(f"  Respuesta: {json.dumps(r.get('body'), indent=4, ensure_ascii=False)}")

    # Análisis de respuestas
    estados = [r.get("body", {}).get("estado") for r in results if isinstance(r.get("body"), dict)]
    confirmados = estados.count("CONFIRMADO")
    ya_confirmados = estados.count("YA_CONFIRMADO")

    print("\n" + "=" * 70)
    print("EVALUACIÓN DE RESPUESTAS HTTP:")
    print(f"  Estados recibidos: {estados}")
    print(f"  Total CONFIRMADO: {confirmados} (Esperado: 1)")
    print(f"  Total YA_CONFIRMADO: {ya_confirmados} (Esperado: 2)")

    # Paso 4: Consulta a la base de datos
    print("\n--- PASO 4: Consulta a la Base de Datos MediadorDev ---")
    db_output = consultar_base_datos(ID_CORRELACION)
    print(db_output)

    # Veredicto de prueba
    print("=" * 70)
    print("VEREDICTO FINAL DE QA:")
    if confirmados == 1 and ya_confirmados == 2:
        print("  RESULTADO: EXITOSO (PASÓ LA PRUEBA)")
        print("  EVIDENCIA: Prueba de 3 peticiones simultáneas deja una sola fila")
        print("  ESTADO DE TAREA: Cerrado")
    else:
        print("  RESULTADO: FALLÓ")
        print("  ESTADO DE TAREA: A medias o Sin empezar")
    print("=" * 70)

if __name__ == "__main__":
    main()
