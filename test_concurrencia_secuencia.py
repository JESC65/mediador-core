import json
import ssl
import subprocess
import sys
import time
import urllib.request
import threading
from concurrent.futures import ThreadPoolExecutor

BASE_URL = "https://localhost:7250"
TIMESTAMP = int(time.time())
ID_CORR_A = f"COU-REQ-010-{TIMESTAMP}"
ID_CORR_B = f"COU-REQ-011-{TIMESTAMP}"

ctx = ssl.create_default_context()
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE

def cotizar(id_correlacion, id_envio_courier):
    payload = {
        "idCorrelacion": id_correlacion,
        "idEnvioCourier": str(id_envio_courier),
        "idCourier": 1,
        "ubigeoOrigen": "150101",
        "ubigeoDestino": "130101",
        "tipoCarga": "CARGA_LIVIANA",
        "fechaSolicitud": "2026-10-28T14:50:00-05:00",
        "paquetes": [
            {
                "descripcion": f"Paquete prueba correlativo {id_correlacion}",
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
        opciones = body.get("opciones", [])
        # Seleccionar Transportista 7 (Expreso Andino, puerto 7201)
        opcion = next((o for o in opciones if o.get("idTransportista") == 7), opciones[0])
        print(f"[COTIZAR] {id_correlacion} -> Exito HTTP {resp.status}. Opcion: {opcion['idOpcion']}")
        return opcion["idOpcion"]

def post_confirmar(barrier, req_id, payload):
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        f"{BASE_URL}/confirmar",
        data=data,
        headers={"Content-Type": "application/json"}
    )
    barrier.wait()
    t0 = time.perf_counter()
    try:
        with urllib.request.urlopen(req, context=ctx) as resp:
            elapsed = time.perf_counter() - t0
            body = json.loads(resp.read().decode("utf-8"))
            return {
                "id": req_id,
                "correlacion": payload["idCorrelacion"],
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
            "correlacion": payload["idCorrelacion"],
            "status": e.code,
            "elapsed": elapsed,
            "body": body
        }
    except Exception as ex:
        elapsed = time.perf_counter() - t0
        return {
            "id": req_id,
            "correlacion": payload["idCorrelacion"],
            "status": -1,
            "elapsed": elapsed,
            "error": str(ex)
        }

def consultar_base_datos(id_corr_a, id_corr_b):
    query = f"""
    SET NOCOUNT ON;
    PRINT '=== MED.Envio para los dos clientes concurrentes ===';
    SELECT IdEnvio, IdCorrelacion, CodigoSeguimiento, Estado, IdTransportistaElegida, FechaConfirmacion
    FROM MED.Envio WHERE IdCorrelacion IN ('{id_corr_a}', '{id_corr_b}');

    PRINT '=== Secuencias en Base de Datos ===';
    SELECT name, current_value, start_value, increment FROM sys.sequences;
    """
    res = subprocess.run(
        ["sqlcmd", "-S", "localhost", "-E", "-d", "MediadorDev", "-Q", query],
        capture_output=True,
        text=True
    )
    return res.stdout

def main():
    print("=" * 75)
    print("QA AUTOMATIZADO: PRUEBA DE CONCURRENCIA - PENDIENTE 3 (SECUENCIA / CORRELATIVO)")
    print("=" * 75)
    print(f"Cliente A: {ID_CORR_A}")
    print(f"Cliente B: {ID_CORR_B}")

    # Paso 1: Generar cotizaciones independientes para ambos clientes
    print("\n--- PASO 1: Generar cotizaciones independientes ---")
    id_opcion_a = cotizar(ID_CORR_A, 1010)
    id_opcion_b = cotizar(ID_CORR_B, 1011)

    payload_a = {
        "idCorrelacion": ID_CORR_A,
        "idOpcion": id_opcion_a,
        "remitente": {
            "tipoDocumento": "DNI",
            "numeroDocumento": "11112222",
            "nombres": "Cliente Uno",
            "apellidos": "Prueba Concurrencia",
            "telefono": "911111111",
            "direccion": "Av. Principal 100",
            "ubigeo": "150101"
        },
        "consignado": {
            "tipoDocumento": "DNI",
            "numeroDocumento": "33334444",
            "nombres": "Destinatario Uno",
            "apellidos": "Prueba Uno",
            "telefono": "922222222",
            "direccion": "Calle Secundaria 200",
            "ubigeo": "130101"
        }
    }

    payload_b = {
        "idCorrelacion": ID_CORR_B,
        "idOpcion": id_opcion_b,
        "remitente": {
            "tipoDocumento": "DNI",
            "numeroDocumento": "55556666",
            "nombres": "Cliente Dos",
            "apellidos": "Prueba Concurrencia",
            "telefono": "933333333",
            "direccion": "Av. Los Sauces 300",
            "ubigeo": "150101"
        },
        "consignado": {
            "tipoDocumento": "DNI",
            "numeroDocumento": "77778888",
            "nombres": "Destinatario Dos",
            "apellidos": "Prueba Dos",
            "telefono": "944444444",
            "direccion": "Calle Los Laureles 400",
            "ubigeo": "130101"
        }
    }

    # Paso 2: Disparar 2 peticiones HTTP POST estrictamente simultáneas con Barrier
    print("\n--- PASO 2: Disparando 2 peticiones POST /confirmar SIMULTANEAS con payloads distintos ---")
    barrier = threading.Barrier(2)
    results = []

    with ThreadPoolExecutor(max_workers=2) as executor:
        fut_a = executor.submit(post_confirmar, barrier, "A", payload_a)
        fut_b = executor.submit(post_confirmar, barrier, "B", payload_b)
        results = [fut_a.result(), fut_b.result()]

    print("\n--- PASO 3: Resultados de las 2 peticiones HTTP ---")
    codigos = []
    estados = []
    for r in results:
        body = r.get("body", {})
        status = r["status"]
        estado = body.get("estado") if isinstance(body, dict) else "ERROR"
        codigo = body.get("codigoSeguimiento") if isinstance(body, dict) else None
        estados.append(estado)
        if codigo:
            codigos.append(codigo)
        print(f"\nRespuesta Cliente {r['id']} ({r['correlacion']}):")
        print(f"  HTTP Status: {status}")
        print(f"  Tiempo: {r['elapsed']:.3f} s")
        print(f"  Respuesta: {json.dumps(body, indent=4, ensure_ascii=False)}")

    # Análisis de respuestas
    print("\n" + "=" * 75)
    print("EVALUACION DE CODIGOS DE SEGUIMIENTO Y CONCURRENCIA:")
    print(f"  Estados recibidos: {estados}")
    print(f"  Codigos de seguimiento generados: {codigos}")

    # Paso 4: Consulta a la base de datos
    print("\n--- PASO 4: Consulta a la Base de Datos MediadorDev ---")
    db_output = consultar_base_datos(ID_CORR_A, ID_CORR_B)
    print(db_output)

    # Veredicto de prueba
    print("=" * 75)
    print("VEREDICTO FINAL DE QA:")
    ambos_exitosos = (estados == ["CONFIRMADO", "CONFIRMADO"])
    codigos_distintos = (len(codigos) == 2 and codigos[0] != codigos[1])

    if ambos_exitosos and codigos_distintos:
        print("  RESULTADO: EXITOSO (PASO LA PRUEBA)")
        print(f"  EVIDENCIA: Dos confirmaciones simultaneas para ids distintos obtuvieron codigos correlativos unicos ({codigos[0]} y {codigos[1]})")
        print("  ESTADO DE TAREA: Cerrado")
    else:
        print("  RESULTADO: FALLO / A MEDIAS")
        if not ambos_exitosos:
            print(f"  MOTIVO: Al menos una peticion no termino en CONFIRMADO (Estados: {estados})")
        elif not codigos_distintos:
            print(f"  MOTIVO: Hubo colision o duplicidad en codigos de seguimiento: {codigos}")
        print("  ESTADO DE TAREA: A medias")
    print("=" * 75)

if __name__ == "__main__":
    main()
