import json
import ssl
import subprocess
import sys
import time
import urllib.request
import urllib.error
from datetime import datetime

BASE_URL = "https://localhost:7250"

# Contexto SSL para ignorar certificado autofirmado local
ctx = ssl.create_default_context()
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE

def preparar_datos_bd():
    print("[INIT] Verificando e insertando registros de prueba en SQL Server (MediadorDev)...")
    sql = """
    SET QUOTED_IDENTIFIER ON;
    -- 1. Envio en CONFIRMADO para prueba de 409 en /cotizar
    IF NOT EXISTS (SELECT 1 FROM MED.Envio WHERE IdCorrelacion = 'COU-QA-409-CONFIRMADO')
    BEGIN
        INSERT INTO MED.Envio (IdCorrelacion, IdEnvioCourier, IdCourier, UbigeoDestino, PesoTotalKg, CantidadPaquetes, Estado, CodigoSeguimiento)
        VALUES ('COU-QA-409-CONFIRMADO', 'ENV-QA-409', 1, '130101', 5.0, 1, 'CONFIRMADO', 'MED-2026-QA-409');
    END

    -- 2. Envio en COTIZADO con CodigoSeguimiento para prueba de transicion ilegal (409 en /eventos)
    IF NOT EXISTS (SELECT 1 FROM MED.Envio WHERE IdCorrelacion = 'COU-QA-EVT-COTIZADO')
    BEGIN
        INSERT INTO MED.Envio (IdCorrelacion, IdEnvioCourier, IdCourier, UbigeoDestino, PesoTotalKg, CantidadPaquetes, Estado, CodigoSeguimiento)
        VALUES ('COU-QA-EVT-COTIZADO', 'ENV-QA-EVT', 1, '130101', 2.0, 1, 'COTIZADO', 'MED-2026-QA-COTIZADO');
    END
    """
    proc = subprocess.run(
        ["sqlcmd", "-S", "localhost", "-d", "MediadorDev", "-Q", sql],
        capture_output=True,
        text=True
    )
    if proc.returncode != 0:
        print(f"[WARN] Error en sqlcmd: {proc.stderr}")
    else:
        print("[INIT] Base de datos preparada correctamente.")

def realizar_peticion(metodo, ruta, payload, headers=None):
    url = f"{BASE_URL}{ruta}"
    data = json.dumps(payload).encode("utf-8") if payload is not None else None
    req_headers = {"Content-Type": "application/json"}
    if headers:
        req_headers.update(headers)

    req = urllib.request.Request(url, data=data, headers=req_headers, method=metodo)
    t0 = time.perf_counter()
    try:
        with urllib.request.urlopen(req, context=ctx) as resp:
            elapsed = time.perf_counter() - t0
            body_text = resp.read().decode("utf-8")
            try:
                body = json.loads(body_text)
            except Exception:
                body = body_text
            return {
                "status": resp.status,
                "elapsed": elapsed,
                "body": body,
                "headers": dict(resp.headers)
            }
    except urllib.error.HTTPError as e:
        elapsed = time.perf_counter() - t0
        body_text = e.read().decode("utf-8")
        try:
            body = json.loads(body_text)
        except Exception:
            body = body_text
        return {
            "status": e.code,
            "elapsed": elapsed,
            "body": body,
            "headers": dict(e.headers)
        }
    except Exception as ex:
        elapsed = time.perf_counter() - t0
        return {
            "status": -1,
            "elapsed": elapsed,
            "error": str(ex)
        }

def ejecutar_pruebas():
    preparar_datos_bd()
    resultados = []

    print("\n" + "=" * 80)
    print("EJECUTANDO PRUEBAS R8: UN SOLO FORMATO DE ERRORES")
    print(f"Fecha y hora: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"Servidor: {BASE_URL}")
    print("=" * 80 + "\n")

    # -------------------------------------------------------------------------
    # ESCENARIO 1: 409 Conflict en Cotizar
    # -------------------------------------------------------------------------
    print("--- [ESCENARIO 1] POST /cotizar (409 Conflict - idCorrelacion en CONFIRMADO) ---")
    payload_cotizar = {
        "idCorrelacion": "COU-QA-409-CONFIRMADO",
        "idEnvioCourier": "ENV-QA-409",
        "idCourier": 1,
        "ubigeoOrigen": "150101",
        "ubigeoDestino": "130101",
        "tipoCarga": "CARGA_LIVIANA",
        "fechaSolicitud": datetime.now().strftime("%Y-%m-%dT%H:%M:%S-05:00"),
        "paquetes": [
            {
                "descripcion": "Paquete prueba conflicto 409",
                "pesoKg": 5.0,
                "cantidad": 1,
                "esFragil": False
            }
        ]
    }
    r1 = realizar_peticion("POST", "/cotizar", payload_cotizar)
    print(f"Status recibido: HTTP {r1['status']} (esperado: 409)")
    print(f"Body: {json.dumps(r1['body'], indent=2, ensure_ascii=False)}\n")
    resultados.append(("ESCENARIO 1: 409 Conflict en Cotizar", "POST /cotizar", payload_cotizar, r1))

    # -------------------------------------------------------------------------
    # ESCENARIO 2: 404 Not Found en Confirmar
    # -------------------------------------------------------------------------
    print("--- [ESCENARIO 2] POST /confirmar (404 Not Found - idOpcion inexistente) ---")
    payload_confirmar = {
        "idCorrelacion": "COU-QA-404-NOTFOUND",
        "idOpcion": "OPT-INEXISTENTE",
        "remitente": {
            "tipoDocumento": "DNI",
            "numeroDocumento": "12345678",
            "nombres": "Juan",
            "apellidos": "Pérez",
            "telefono": "999888777"
        },
        "consignado": {
            "tipoDocumento": "DNI",
            "numeroDocumento": "87654321",
            "nombres": "Ana",
            "apellidos": "Gómez",
            "telefono": "999111222",
            "direccion": "Av. Principal 123",
            "ubigeo": "130101"
        }
    }
    r2 = realizar_peticion("POST", "/confirmar", payload_confirmar)
    print(f"Status recibido: HTTP {r2['status']} (esperado: 404)")
    print(f"Body: {json.dumps(r2['body'], indent=2, ensure_ascii=False)}\n")
    resultados.append(("ESCENARIO 2: 404 Not Found en Confirmar", "POST /confirmar", payload_confirmar, r2))

    # -------------------------------------------------------------------------
    # ESCENARIO 3: 409 Conflict en Eventos
    # -------------------------------------------------------------------------
    print("--- [ESCENARIO 3] POST /eventos (409 Conflict - transicion de estado ilegal) ---")
    payload_evento = {
        "idEvento": f"0193a1b2-0c3d-7e4f-8a9b-{int(time.time()):012x}"[:36],
        "tipoEvento": "ENTREGADO",
        "idTransportista": 7,
        "fechaEvento": datetime.now().strftime("%Y-%m-%dT%H:%M:%S-05:00"),
        "datos": {
            "codigoSeguimiento": "MED-2026-QA-COTIZADO"
        }
    }
    r3 = realizar_peticion("POST", "/eventos", payload_evento)
    print(f"Status recibido: HTTP {r3['status']} (esperado: 409)")
    print(f"Body: {json.dumps(r3['body'], indent=2, ensure_ascii=False)}\n")
    resultados.append(("ESCENARIO 3: 409 Conflict en Eventos", "POST /eventos", payload_evento, r3))

    # -------------------------------------------------------------------------
    # ESCENARIO 4: 500 Internal Server Error Forzado
    # -------------------------------------------------------------------------
    print("--- [ESCENARIO 4] POST /cotizar (500 Internal Server Error - Forzado) ---")
    payload_500 = {
        "idCorrelacion": "COU-QA-ERROR-500",
        "idEnvioCourier": "ENV-QA-500",
        "idCourier": 1,
        "ubigeoOrigen": "150101",
        "ubigeoDestino": "130101",
        "tipoCarga": "CARGA_LIVIANA",
        "paquetes": [
            {
                "descripcion": "Paquete prueba 500 forzado",
                "pesoKg": 1.0,
                "cantidad": 1,
                "esFragil": False
            }
        ]
    }
    r4 = realizar_peticion("POST", "/cotizar", payload_500)
    print(f"Status recibido: HTTP {r4['status']} (esperado: 500)")
    print(f"Body: {json.dumps(r4['body'], indent=2, ensure_ascii=False)}\n")
    resultados.append(("ESCENARIO 4: 500 Internal Server Error (Forzado)", "POST /cotizar", payload_500, r4))

    # -------------------------------------------------------------------------
    # Generar archivo de evidencia docs/arreglos_sin_azure/r8_errores.txt
    # -------------------------------------------------------------------------
    archivo_evidencia = "docs/arreglos_sin_azure/r8_errores.txt"
    with open(archivo_evidencia, "w", encoding="utf-8") as f:
        f.write("=" * 80 + "\n")
        f.write("EVIDENCIA DE PRUEBA: BLOQUE 2 - UN SOLO FORMATO DE ERRORES (REGLA R8)\n")
        f.write(f"Fecha y hora: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
        f.write(f"Servidor evaluado: {BASE_URL}\n")
        f.write("=" * 80 + "\n\n")

        for nombre, endpoint, payload, res in resultados:
            f.write("-" * 80 + "\n")
            f.write(f"--- {nombre} ---\n")
            f.write("-" * 80 + "\n")
            f.write(f"Request: {endpoint}\n")
            f.write(json.dumps(payload, indent=2, ensure_ascii=False) + "\n\n")
            f.write(f"Response (HTTP {res['status']} en {res.get('elapsed', 0):.3f} s):\n")
            f.write(json.dumps(res.get('body', {}), indent=2, ensure_ascii=False) + "\n\n")

    print(f"[OK] Archivo de evidencia generado con éxito en: {archivo_evidencia}")

if __name__ == "__main__":
    ejecutar_pruebas()
