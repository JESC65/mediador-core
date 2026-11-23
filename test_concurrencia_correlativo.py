import json
import ssl
import subprocess
import sys
import time
from datetime import datetime
import urllib.request
import urllib.error
import threading
from concurrent.futures import ThreadPoolExecutor

N = 20
BASE_URL = "https://localhost:7250"
TIMESTAMP = int(time.time())

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
        "fechaSolicitud": datetime.now().strftime("%Y-%m-%dT%H:%M:%S-05:00"),
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

def consultar_base_datos(ids_correlacion):
    in_clause = ", ".join(f"'{c}'" for c in ids_correlacion)
    query = f"""
    SET NOCOUNT ON;
    PRINT '=== MED.Envio para los {len(ids_correlacion)} envios concurrentes ===';
    SELECT IdEnvio, IdCorrelacion, CodigoSeguimiento, Estado, IdTransportistaElegida, FechaConfirmacion
    FROM MED.Envio WHERE IdCorrelacion IN ({in_clause})
    ORDER BY IdEnvio;

    PRINT '=== Contenido de MED.Correlativo ===';
    SELECT Anio, Ultimo FROM MED.Correlativo;
    """
    res = subprocess.run(
        ["sqlcmd", "-S", "localhost", "-E", "-d", "MediadorDev", "-Q", query],
        capture_output=True,
        text=True
    )
    return res.stdout

def main():
    inicio_dt = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    print("=" * 80)
    print("QA AUTOMATIZADO: PRUEBA DE CONCURRENCIA - PENDIENTE 3 (CORRELATIVO SIN CHOQUES)")
    print(f"Fecha y hora de inicio: {inicio_dt}")
    print(f"Envíos concurrentes (N): {N}")
    print("=" * 80)

    # Paso 1: Generar cotizaciones independientes para los N envíos
    print(f"\n--- PASO 1: Generar {N} cotizaciones independientes ---")
    envios = []
    for i in range(1, N + 1):
        id_corr = f"COU-CORR-{TIMESTAMP}-{i:02d}"
        id_envio_courier = TIMESTAMP * 100 + i
        id_opcion = cotizar(id_corr, id_envio_courier)
        payload = {
            "idCorrelacion": id_corr,
            "idOpcion": id_opcion,
            "remitente": {
                "tipoDocumento": "DNI",
                "numeroDocumento": f"10{i:06d}",
                "nombres": f"Remitente {i}",
                "apellidos": "Prueba Concurrencia",
                "telefono": f"91{i:07d}"[:9],
                "direccion": f"Av. Principal {100 + i}",
                "ubigeo": "150101"
            },
            "consignado": {
                "tipoDocumento": "DNI",
                "numeroDocumento": f"20{i:06d}",
                "nombres": f"Consignado {i}",
                "apellidos": "Prueba Concurrencia",
                "telefono": f"92{i:07d}"[:9],
                "direccion": f"Calle Destino {200 + i}",
                "ubigeo": "130101"
            }
        }
        envios.append((i, id_corr, payload))

    # Paso 2: Disparar N peticiones HTTP POST estrictamente simultáneas con Barrier
    print(f"\n--- PASO 2: Disparando {N} peticiones POST /confirmar SIMULTÁNEAS con Barrier ---")
    barrier = threading.Barrier(N)
    results = []

    with ThreadPoolExecutor(max_workers=N) as executor:
        futures = [
            executor.submit(post_confirmar, barrier, f"{i:02d}", payload)
            for i, id_corr, payload in envios
        ]
        results = [f.result() for f in futures]

    print(f"\n--- PASO 3: Resultados de las {N} peticiones HTTP ---")
    codigos = []
    estados = []
    fallas = []

    for r in results:
        body = r.get("body", {})
        status = r["status"]
        estado = body.get("estado") if isinstance(body, dict) else "ERROR"
        codigo = body.get("codigoSeguimiento") if isinstance(body, dict) else None

        estados.append(estado)
        if codigo:
            codigos.append(codigo)

        if not (status == 200 and estado == "CONFIRMADO" and codigo):
            fallas.append(r)

        print(f"Envío #{r['id']} ({r['correlacion']}) -> Status HTTP: {status} | Tiempo: {r['elapsed']:.3f} s | Estado: {estado} | Código: {codigo}")

    # Análisis de respuestas
    print("\n" + "=" * 80)
    print("EVALUACIÓN DE CÓDIGOS DE SEGUIMIENTO Y CONCURRENCIA:")
    print(f"  Total peticiones: {N}")
    print(f"  Confirmaciones HTTP 200 con estado 'CONFIRMADO': {estados.count('CONFIRMADO')}/{N}")
    print(f"  Códigos de seguimiento generados: {len(codigos)}/{N}")
    codigos_unicos = len(set(codigos))
    print(f"  Códigos de seguimiento únicos: {codigos_unicos}/{N}")

    # Paso 4: Consulta a la base de datos
    print("\n--- PASO 4: Consulta a la Base de Datos MediadorDev ---")
    ids_corr_list = [id_corr for _, id_corr, _ in envios]
    db_output = consultar_base_datos(ids_corr_list)
    print(db_output)

    # Veredicto de prueba
    print("=" * 80)
    print("VEREDICTO FINAL DE QA:")
    sin_500_ni_conexion = all(r["status"] == 200 for r in results)
    todos_confirmados = all(e == "CONFIRMADO" for e in estados)
    todos_codigos_distintos = (len(codigos) == N and codigos_unicos == N)

    if sin_500_ni_conexion and todos_confirmados and todos_codigos_distintos:
        print("  RESULTADO: EXITOSO (PASÓ LA PRUEBA)")
        print(f"  EVIDENCIA: Las {N} confirmaciones simultáneas respondieron HTTP 200 con estado CONFIRMADO y {N} códigos correlativos únicos distintos.")
        if codigos:
            print(f"  RANGO DE CÓDIGOS GENERADOS: {min(codigos)} .. {max(codigos)}")
    else:
        print("  RESULTADO: FALLA")
        if not sin_500_ni_conexion:
            print("  MOTIVO: Se presentaron errores HTTP o fallas de conexión:")
        if not todos_confirmados:
            print(f"  MOTIVO: No todos los envíos obtuvieron estado CONFIRMADO (Estados recibidos: {estados})")
        if not todos_codigos_distintos:
            print(f"  MOTIVO: Hubo colisión o faltaron códigos de seguimiento (Únicos: {codigos_unicos} de {N})")

        print("\n  DETALLE DE PETICIONES FALLIDAS:")
        for f in fallas:
            print(f"    - Envío #{f['id']} ({f['correlacion']}): Status={f.get('status')}, Error/Body={f.get('error') or f.get('body')}")
    print("=" * 80)

if __name__ == "__main__":
    main()

