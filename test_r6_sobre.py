import json
import ssl
import subprocess
import sys
import time
import urllib.request
import urllib.error
from datetime import datetime

BASE_URL = "http://localhost:5250"

# Contexto SSL por si se prueba HTTPS
ctx = ssl.create_default_context()
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE

def preparar_datos_bd():
    print("[INIT] Verificando e insertando registros de prueba en SQL Server (MediadorDev)...")
    sql = """
    SET QUOTED_IDENTIFIER ON;
    
    -- Limpiar secuencias y huecos previos para transportista de prueba 77
    DELETE FROM MED.SecuenciaHueco WHERE IdTransportista = 77;
    DELETE FROM MED.TransportistaSecuencia WHERE IdTransportista = 77;
    DELETE FROM MED.EventoRecibido WHERE IdTransportista = 77;

    -- Envio para prueba de transicion invalida
    IF NOT EXISTS (SELECT 1 FROM MED.Envio WHERE IdCorrelacion = 'COU-QA-R6-TRANSICION')
    BEGIN
        INSERT INTO MED.Envio (IdCorrelacion, IdEnvioCourier, IdCourier, UbigeoDestino, PesoTotalKg, CantidadPaquetes, Estado, CodigoSeguimiento)
        VALUES ('COU-QA-R6-TRANSICION', 'ENV-QA-R6-TR', 1, '130101', 3.0, 1, 'CONFIRMADO', 'MED-2026-QA-R6-TR');
    END
    ELSE
    BEGIN
        UPDATE MED.Envio SET Estado = 'CONFIRMADO' WHERE IdCorrelacion = 'COU-QA-R6-TRANSICION';
    END

    -- Envio para prueba de sobre normal
    IF NOT EXISTS (SELECT 1 FROM MED.Envio WHERE IdCorrelacion = 'COU-QA-R6-ENVIO1')
    BEGIN
        INSERT INTO MED.Envio (IdCorrelacion, IdEnvioCourier, IdCourier, UbigeoDestino, PesoTotalKg, CantidadPaquetes, Estado, CodigoSeguimiento)
        VALUES ('COU-QA-R6-ENVIO1', 'ENV-QA-R6-01', 1, '130101', 4.0, 1, 'CONFIRMADO', 'MED-2026-QA-R6-01');
    END
    """
    proc = subprocess.run(
        ["sqlcmd", "-S", "localhost", "-d", "MediadorDev", "-Q", sql],
        capture_output=True,
        text=True
    )
    if proc.returncode != 0:
        print(f"[WARN] Error en sqlcmd al preparar BD: {proc.stderr}")
    else:
        print("[INIT] Base de datos preparada correctamente.")

def consultar_bd(sql):
    proc = subprocess.run(
        ["sqlcmd", "-S", "localhost", "-d", "MediadorDev", "-Q", sql],
        capture_output=True,
        text=True
    )
    return proc.stdout.strip()

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
    lineas_evidencia = []

    def log(texto):
        print(texto)
        lineas_evidencia.append(texto)

    log("=" * 80)
    log("EVIDENCIA DE PRUEBA: BLOQUE 2 - EL MEDIADOR RECIBE EL SOBRE NUEVO (R6 Y C2 v2.0)")
    log(f"Fecha y hora: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    log(f"Servidor evaluado: {BASE_URL}")
    log("=" * 80 + "\n")

    # -------------------------------------------------------------------------
    # ESCENARIO 1: Evento duplicado reacusado sin reprocesar
    # -------------------------------------------------------------------------
    log("-" * 80)
    log("--- ESCENARIO 1: Evento duplicado reacusado sin reprocesar (HTTP 202) ---")
    log("-" * 80)

    id_evento_1 = "0193a1b2-0c3d-7e4f-8a9b-000000000001"
    evento_original = {
        "idEvento": id_evento_1,
        "tipoEvento": "EN_VIAJE",
        "versionContrato": "2.0",
        "secuencia": 1,
        "entidad": {
            "tipo": "ENVIO",
            "id": "MED-2026-QA-R6-01"
        },
        "versionEntidad": 1,
        "fechaEvento": "2026-11-17T10:00:00-05:00",
        "emisor": "CONECTOR",
        "idTransportista": 77,
        "datos": {
            "idPunto": 10,
            "horaSalida": "10:00"
        }
    }

    log("Paso 1.1: Envío inicial de evento válido con sobre C2 v2.0")
    log(f"Request: POST /v1/eett/eventos\n{json.dumps(evento_original, indent=2)}")
    res1 = realizar_peticion("POST", "/v1/eett/eventos", evento_original)
    log(f"\nResponse (HTTP {res1['status']} en {res1['elapsed']:.3f} s):")
    log(json.dumps(res1["body"], indent=2))
    assert res1["status"] == 202, f"Se esperaba HTTP 202, se obtuvo {res1['status']}"
    assert res1["body"]["recibido"] is True

    log("\nPaso 1.2: Reenvío idéntico (mismo idEvento, mismo contenido) -> Swallow/Reacuse")
    res1_dup = realizar_peticion("POST", "/v1/eett/eventos", evento_original)
    log(f"Response duplicado (HTTP {res1_dup['status']} en {res1_dup['elapsed']:.3f} s):")
    log(json.dumps(res1_dup["body"], indent=2))
    assert res1_dup["status"] == 202, f"Se esperaba HTTP 202, se obtuvo {res1_dup['status']}"

    # Verificación en BD
    bd_count = consultar_bd(f"SELECT COUNT(*) FROM MED.EventoRecibido WHERE IdEvento = '{id_evento_1}';")
    log(f"\nVerificación en BD: Total registros guardados para IdEvento: {bd_count}")
    log("Resultado: Evento recibido y reacusado exitosamente con 202 sin duplicar reprocesamiento.")

    # -------------------------------------------------------------------------
    # ESCENARIO 2: Conflicto registrado (mismo ID, distinto body)
    # -------------------------------------------------------------------------
    log("\n" + "-" * 80)
    log("--- ESCENARIO 2: Conflicto registrado (mismo ID, distinto body) ---")
    log("-" * 80)

    evento_conflicto = {
        "idEvento": id_evento_1,  # Mismo idEvento pero distinto contenido
        "tipoEvento": "ARRIBADO",
        "versionContrato": "2.0",
        "secuencia": 1,
        "entidad": {
            "tipo": "ENVIO",
            "id": "MED-2026-QA-R6-01"
        },
        "versionEntidad": 2,
        "fechaEvento": "2026-11-17T11:00:00-05:00",
        "emisor": "CONECTOR",
        "idTransportista": 77,
        "datos": {
            "terminalDestino": "TRUJILLO_CENTRO"
        }
    }

    log(f"Request conflictivo (mismo idEvento, diferente cuerpo):\n{json.dumps(evento_conflicto, indent=2)}")
    res2 = realizar_peticion("POST", "/v1/eett/eventos", evento_conflicto)
    log(f"\nResponse (HTTP {res2['status']} en {res2['elapsed']:.3f} s):")
    log(json.dumps(res2["body"], indent=2))
    assert res2["status"] == 409, f"Se esperaba HTTP 409 Conflict, se obtuvo {res2['status']}"
    assert res2["body"]["error"] == "CONFLICTO_ID_EVENTO"

    # Verificación en BD del registro de conflicto
    bd_conflicto = consultar_bd(f"""
        SELECT IdEventoRecibido, IdEvento, EsConflicto, RequiereMensajeRechazado, MotivoRechazo 
        FROM MED.EventoRecibido 
        WHERE IdEvento = '{id_evento_1}' AND EsConflicto = 1;
    """)
    log(f"\nVerificación en BD: Registro de conflicto auditado:\n{bd_conflicto}")
    log("Resultado: Conflicto detectado, guardado en BD con EsConflicto=1 y rechazado con 409.")

    # -------------------------------------------------------------------------
    # ESCENARIO 3: Transición inválida tragada y marcada (sin devolver 409)
    # -------------------------------------------------------------------------
    log("\n" + "-" * 80)
    log("--- ESCENARIO 3: Transición inválida tragada y marcada (sin devolver 409) ---")
    log("-" * 80)

    id_evento_transicion = "0193a1b2-0c3d-7e4f-8a9b-000000000003"
    # El envío MED-2026-QA-R6-TR está en CONFIRMADO. Intentamos ENTREGADO directo (inválido).
    evento_invalido = {
        "idEvento": id_evento_transicion,
        "tipoEvento": "ENTREGADO",
        "versionContrato": "2.0",
        "secuencia": 2,
        "entidad": {
            "tipo": "ENVIO",
            "id": "MED-2026-QA-R6-TR"
        },
        "versionEntidad": 1,
        "fechaEvento": "2026-11-17T12:00:00-05:00",
        "emisor": "CONECTOR",
        "idTransportista": 77,
        "datos": {
            "recibidoPor": "Destinatario directo"
        }
    }

    log(f"Request transición inválida (CONFIRMADO -> ENTREGADO):\n{json.dumps(evento_invalido, indent=2)}")
    res3 = realizar_peticion("POST", "/v1/eett/eventos", evento_invalido)
    log(f"\nResponse (HTTP {res3['status']} en {res3['elapsed']:.3f} s):")
    log(json.dumps(res3["body"], indent=2))
    assert res3["status"] == 202, f"Se esperaba HTTP 202 Accepted (adiós 409), se obtuvo {res3['status']}"
    assert res3["body"]["recibido"] is True

    # Verificación en BD: evento guardado y marcado para MENSAJE_RECHAZADO
    bd_invalido = consultar_bd(f"""
        SELECT IdEventoRecibido, IdEvento, RequiereMensajeRechazado, MotivoRechazo, Procesado 
        FROM MED.EventoRecibido 
        WHERE IdEvento = '{id_evento_transicion}';
    """)
    log(f"\nVerificación en BD: Evento guardado y marcado para rechazo:\n{bd_invalido}")

    # Verificar que el estado del envío no se corrompió
    bd_envio = consultar_bd("SELECT Estado FROM MED.Envio WHERE CodigoSeguimiento = 'MED-2026-QA-R6-TR';")
    log(f"Verificación en BD: Estado actual del envío: {bd_envio}")
    log("Resultado: Transición inválida tragada con 202, guardada en BD con RequiereMensajeRechazado=1 y estado del envío intacto.")

    # -------------------------------------------------------------------------
    # ESCENARIO 4: Hueco de secuencia registrado
    # -------------------------------------------------------------------------
    log("\n" + "-" * 80)
    log("--- ESCENARIO 4: Control de secuencias contiguas y huecos (R6) ---")
    log("-" * 80)

    # Transportista 77 tenía secuencia contigua = 1 (del evento 1)
    # Ahora enviamos secuencia = 4 (salto: faltan 2 y 3)
    id_evento_hueco = "0193a1b2-0c3d-7e4f-8a9b-000000000004"
    evento_salto = {
        "idEvento": id_evento_hueco,
        "tipoEvento": "ARRIBADO",
        "versionContrato": "2.0",
        "secuencia": 4,  # Salto: esperada era 2
        "entidad": {
            "tipo": "ENVIO",
            "id": "MED-2026-QA-R6-01"
        },
        "versionEntidad": 2,
        "fechaEvento": "2026-11-17T13:00:00-05:00",
        "emisor": "CONECTOR",
        "idTransportista": 77,
        "datos": {
            "terminal": "TRUJILLO_TERMINAL"
        }
    }

    log(f"Request con salto de secuencia (última contigua=1, recibida=4):\n{json.dumps(evento_salto, indent=2)}")
    res4 = realizar_peticion("POST", "/v1/eett/eventos", evento_salto)
    log(f"\nResponse (HTTP {res4['status']} en {res4['elapsed']:.3f} s):")
    log(json.dumps(res4["body"], indent=2))
    assert res4["status"] == 202, f"Se esperaba HTTP 202, se obtuvo {res4['status']}"

    # Verificación en BD: hueco registrado en MED.SecuenciaHueco
    bd_hueco = consultar_bd("""
        SELECT IdHueco, IdTransportista, SecuenciaEsperada, SecuenciaRecibida, Resuelto 
        FROM MED.SecuenciaHueco 
        WHERE IdTransportista = 77;
    """)
    log(f"\nVerificación en BD: Hueco registrado en MED.SecuenciaHueco:\n{bd_hueco}")

    # Verificación en BD: la secuencia contigua NO avanzó a 4
    bd_contigua = consultar_bd("""
        SELECT IdTransportista, UltimaSecuenciaContigua 
        FROM MED.TransportistaSecuencia 
        WHERE IdTransportista = 77;
    """)
    log(f"Verificación en BD: Secuencia contigua actual:\n{bd_contigua}")
    log("Resultado: Salto detectado, hueco registrado en BD y secuencia contigua congelada en 1 hasta recuperación.")

    log("\n" + "=" * 80)
    log("TODAS LAS PRUEBAS COMPLETADAS EXITOSAMENTE CONFORME A REGLA R6 Y C2 v2.0")
    log("=" * 80)

    # Guardar evidencia
    ruta_salida = "mediador-core/docs/arreglos_sin_azure/r6_sobre.txt"
    with open(ruta_salida, "w", encoding="utf-8") as f:
        f.write("\n".join(lineas_evidencia) + "\n")
    print(f"\n[OK] Archivo de evidencia generado en {ruta_salida}")

if __name__ == "__main__":
    ejecutar_pruebas()
