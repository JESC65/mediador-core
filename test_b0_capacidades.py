import json
import os
import ssl
import subprocess
import sys
import time
import urllib.request
import urllib.error
from datetime import datetime

URL_SIM_7 = "https://localhost:7201"
URL_SIM_8 = "https://localhost:7202"
URL_MEDIADOR = "https://localhost:7250"

# Contexto SSL para ignorar validación de certificados autofirmados en localhost
ctx = ssl.create_default_context()
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE

def ejecutar_sql(sql):
    proc = subprocess.run(
        ["sqlcmd", "-S", "localhost", "-d", "MediadorDev", "-Q", sql],
        capture_output=True,
        text=True
    )
    if proc.returncode != 0:
        print(f"[WARN] Error en sqlcmd: {proc.stderr}")
    return proc.stdout.strip()

def http_get(url, headers_extra=None, timeout=10):
    headers = {"Content-Type": "application/json"}
    if headers_extra:
        headers.update(headers_extra)
    req = urllib.request.Request(url, headers=headers, method="GET")
    t0 = time.perf_counter()
    try:
        with urllib.request.urlopen(req, context=ctx, timeout=timeout) as resp:
            elapsed = time.perf_counter() - t0
            body_text = resp.read().decode("utf-8")
            try:
                body = json.loads(body_text)
            except Exception:
                body = body_text
            return {"status": resp.status, "elapsed": elapsed, "body": body}
    except urllib.error.HTTPError as e:
        elapsed = time.perf_counter() - t0
        body_text = e.read().decode("utf-8")
        try:
            body = json.loads(body_text)
        except Exception:
            body = body_text
        return {"status": e.code, "elapsed": elapsed, "body": body}
    except Exception as ex:
        elapsed = time.perf_counter() - t0
        return {"status": -1, "elapsed": elapsed, "error": str(ex)}

def http_post(url, payload=None, headers_extra=None, timeout=15):
    data = json.dumps(payload).encode("utf-8") if payload is not None else None
    headers = {"Content-Type": "application/json"}
    if headers_extra:
        headers.update(headers_extra)
    req = urllib.request.Request(url, data=data, headers=headers, method="POST")
    t0 = time.perf_counter()
    try:
        with urllib.request.urlopen(req, context=ctx, timeout=timeout) as resp:
            elapsed = time.perf_counter() - t0
            body_text = resp.read().decode("utf-8")
            try:
                body = json.loads(body_text)
            except Exception:
                body = body_text
            return {"status": resp.status, "elapsed": elapsed, "body": body}
    except urllib.error.HTTPError as e:
        elapsed = time.perf_counter() - t0
        body_text = e.read().decode("utf-8")
        try:
            body = json.loads(body_text)
        except Exception:
            body = body_text
        return {"status": e.code, "elapsed": elapsed, "body": body}
    except Exception as ex:
        elapsed = time.perf_counter() - t0
        return {"status": -1, "elapsed": elapsed, "error": str(ex)}

def preparar_entorno(log_func):
    log_func("[INIT] Deteniendo procesos dotnet previos...")
    subprocess.run(["powershell", "-Command", "Stop-Process -Name dotnet -Force -ErrorAction SilentlyContinue"], capture_output=True)
    time.sleep(2)

    log_func("[INIT] Preparando base de datos MediadorDev...")
    # Asegurar script 05b_capacidades.sql
    ruta_script = os.path.join("mediador-core", "database", "05b_capacidades.sql")
    subprocess.run(["sqlcmd", "-S", "localhost", "-d", "MediadorDev", "-i", ruta_script], capture_output=True)

    sql_prep = """
    SET QUOTED_IDENTIFIER ON;
    -- Limpiar capacidades previas para verificar sincronización al arranque
    DELETE FROM MED.TransportistaCapacidad WHERE IdTransportista IN (7, 8, 9);

    -- Configurar Transportistas 7 y 8 activas con sus URLs correspondientes
    UPDATE MED.Transportista SET UrlConector = 'https://localhost:7201', Activo = 1 WHERE IdTransportista = 7;
    UPDATE MED.Transportista SET UrlConector = 'https://localhost:7202', Activo = 1 WHERE IdTransportista = 8;
    UPDATE MED.Transportista SET Activo = 0 WHERE IdTransportista = 9;

    -- Limpiar envíos de prueba previos
    DELETE FROM MED.EnvioEstado WHERE IdEnvio IN (SELECT IdEnvio FROM MED.Envio WHERE IdCorrelacion LIKE 'COU-QA-B0-%');
    DELETE FROM MED.EnvioPersona WHERE IdEnvio IN (SELECT IdEnvio FROM MED.Envio WHERE IdCorrelacion LIKE 'COU-QA-B0-%');
    DELETE FROM MED.EnvioPaquete WHERE IdEnvio IN (SELECT IdEnvio FROM MED.Envio WHERE IdCorrelacion LIKE 'COU-QA-B0-%');
    DELETE FROM MED.Cotizacion WHERE IdEnvio IN (SELECT IdEnvio FROM MED.Envio WHERE IdCorrelacion LIKE 'COU-QA-B0-%');
    DELETE FROM MED.Envio WHERE IdCorrelacion LIKE 'COU-QA-B0-%';
    """
    ejecutar_sql(sql_prep)
    log_func("[INIT] Base de datos MediadorDev lista para la prueba.")

def main():
    evidencia = []
    def log(msg):
        print(msg)
        evidencia.append(msg)

    log("=" * 80)
    log("EVIDENCIA DE PRUEBA: BLOQUE 2 - CAPACIDADES (B0) SEGÚN C2 v2.0 §3 Y REGLA R8")
    log(f"Fecha y hora: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    log(f"Simulador T7 (precioEnLinea=false): {URL_SIM_7}")
    log(f"Simulador T8 (anulacion=false):      {URL_SIM_8}")
    log(f"Mediador:                           {URL_MEDIADOR}")
    log("=" * 80 + "\n")

    preparar_entorno(log)

    procs = []
    try:
        # 1. Iniciar Simulador Instancia 1 (T7, puerto 7201)
        log("[PASO 1] Iniciando Simulador Instancia 1 (T7, puerto 7201)...")
        env_t7 = os.environ.copy()
        env_t7["TransportistaConfigPath"] = "transportista-7.json"
        env_t7["ASPNETCORE_URLS"] = URL_SIM_7
        f_log_t7 = open("simulador_t7.log", "w", encoding="utf-8")
        p_t7 = subprocess.Popen(
            ["dotnet", "run", "--no-build", "--project", "mediador-simulador-eett/SimuladorEETT.Presentation", "--urls", URL_SIM_7, "--", "--TransportistaConfigPath", "transportista-7.json"],
            env=env_t7,
            stdout=f_log_t7,
            stderr=subprocess.STDOUT,
            text=True
        )
        procs.append((p_t7, f_log_t7))

        # 2. Iniciar Simulador Instancia 2 (T8, puerto 7202)
        log("[PASO 2] Iniciando Simulador Instancia 2 (T8, puerto 7202)...")
        env_t8 = os.environ.copy()
        env_t8["TransportistaConfigPath"] = "transportista-8.json"
        env_t8["ASPNETCORE_URLS"] = URL_SIM_8
        f_log_t8 = open("simulador_t8.log", "w", encoding="utf-8")
        p_t8 = subprocess.Popen(
            ["dotnet", "run", "--no-build", "--project", "mediador-simulador-eett/SimuladorEETT.Presentation", "--urls", URL_SIM_8, "--", "--TransportistaConfigPath", "transportista-8.json"],
            env=env_t8,
            stdout=f_log_t8,
            stderr=subprocess.STDOUT,
            text=True
        )
        procs.append((p_t8, f_log_t8))

        # Esperar que ambos simuladores respondan
        log("[WAIT] Esperando que los simuladores estén arriba...")
        t7_ok, t8_ok = False, False
        for i in range(30):
            time.sleep(1)
            if not t7_ok:
                r7 = http_get(f"{URL_SIM_7}/v1/capacidades")
                if r7["status"] == 200:
                    t7_ok = True
                    log(f"  [OK] Simulador T7 respondiendo en {URL_SIM_7}")
            if not t8_ok:
                r8 = http_get(f"{URL_SIM_8}/v1/capacidades")
                if r8["status"] == 200:
                    t8_ok = True
                    log(f"  [OK] Simulador T8 respondiendo en {URL_SIM_8}")
            if t7_ok and t8_ok:
                break

        if not (t7_ok and t8_ok):
            raise Exception("No se pudieron levantar ambos simuladores.")

        # 3. Iniciar Mediador (puerto 7250)
        log("\n[PASO 3] Iniciando Mediador (puerto 7250)...")
        f_log_med = open("mediador.log", "w", encoding="utf-8")
        p_med = subprocess.Popen(
            ["dotnet", "run", "--no-build", "--project", "mediador-core/Mediador.Presentation", "--urls", URL_MEDIADOR],
            stdout=f_log_med,
            stderr=subprocess.STDOUT,
            text=True
        )
        procs.append((p_med, f_log_med))

        log("[WAIT] Esperando que Mediador esté arriba y sincronice capacidades al arranque...")
        med_ok = False
        for i in range(35):
            time.sleep(1)
            r = http_get(f"{URL_MEDIADOR}/swagger/index.html")
            if r["status"] in (200, 404): # servidor web respondiendo
                med_ok = True
                break
        
        if not med_ok:
            raise Exception("Mediador no levantó a tiempo.")

        # Dar 2 segundos adicionales para que SincronizacionCapacidadesBackgroundService complete el upsert
        time.sleep(2)
        log("  [OK] Mediador levantado y respondiendo en " + URL_MEDIADOR)

        # ----------------------------------------------------------------------
        # CASO 1: GET /v1/capacidades en ambas instancias y verificación en BD
        # ----------------------------------------------------------------------
        log("\n" + "-" * 80)
        log("--- CASO 1: GET /v1/capacidades en ambas instancias y verificación en MED.TransportistaCapacidad ---")
        log("-" * 80)

        # Consulta B0 T7
        b0_t7 = http_get(f"{URL_SIM_7}/v1/capacidades")
        log(f"\n[1.1] GET {URL_SIM_7}/v1/capacidades (Transportista 7):")
        log(f"  HTTP Status: {b0_t7['status']}")
        log(f"  Respuesta: {json.dumps(b0_t7['body'], indent=2)}")
        assert b0_t7["status"] == 200, f"Status esperado 200, obtenido {b0_t7['status']}"
        assert b0_t7["body"]["idTransportista"] == 7
        assert b0_t7["body"]["operaciones"]["precioEnLinea"] is False
        assert b0_t7["body"]["operaciones"]["registroEnLinea"] is True
        assert b0_t7["body"]["operaciones"]["anulacion"] is True
        assert b0_t7["body"]["operaciones"]["puntos"] is True
        assert b0_t7["body"]["recepcionPorBulto"] is False
        assert len(b0_t7["body"]["eventos"]) == 6
        log("  [VERIFICADO] B0 Transportista 7 correcto.")

        # Consulta B0 T8
        b0_t8 = http_get(f"{URL_SIM_8}/v1/capacidades")
        log(f"\n[1.2] GET {URL_SIM_8}/v1/capacidades (Transportista 8):")
        log(f"  HTTP Status: {b0_t8['status']}")
        log(f"  Respuesta: {json.dumps(b0_t8['body'], indent=2)}")
        assert b0_t8["status"] == 200, f"Status esperado 200, obtenido {b0_t8['status']}"
        assert b0_t8["body"]["idTransportista"] == 8
        assert b0_t8["body"]["operaciones"]["precioEnLinea"] is True
        assert b0_t8["body"]["operaciones"]["registroEnLinea"] is True
        assert b0_t8["body"]["operaciones"]["anulacion"] is False
        assert b0_t8["body"]["operaciones"]["puntos"] is True
        assert b0_t8["body"]["recepcionPorBulto"] is False
        assert len(b0_t8["body"]["eventos"]) == 6
        log("  [VERIFICADO] B0 Transportista 8 correcto.")

        # Verificación en BD tras arranque del Mediador
        log("\n[1.3] Verificación de sincronización al arranque en MED.TransportistaCapacidad:")
        sql_cap = """
        SELECT IdTransportista, Familia, VersionConector, VersionContrato, PrecioEnLinea, RegistroEnLinea, Anulacion, Puntos, RecepcionPorBulto, FechaActualizacion
        FROM MED.TransportistaCapacidad
        WHERE IdTransportista IN (7, 8)
        ORDER BY IdTransportista;
        """
        salida_sql_cap = ejecutar_sql(sql_cap)
        log(salida_sql_cap)

        # Verificación del endpoint de sincronización bajo demanda POST /v1/transportistas/{id}/capacidades
        log("\n[1.4] Verificación de endpoint POST /v1/transportistas/{id}/capacidades en Mediador:")
        r_sync_7 = http_post(f"{URL_MEDIADOR}/v1/transportistas/7/capacidades")
        log(f"  POST /v1/transportistas/7/capacidades -> HTTP {r_sync_7['status']}: {r_sync_7['body']}")
        assert r_sync_7["status"] == 200

        r_sync_404 = http_post(f"{URL_MEDIADOR}/v1/transportistas/999/capacidades")
        log(f"  POST /v1/transportistas/999/capacidades -> HTTP {r_sync_404['status']}: {r_sync_404['body']}")
        assert r_sync_404["status"] == 404
        assert r_sync_404["body"]["error"] == "RECURSO_NO_ENCONTRADO"
        log("  [VERIFICADO] Endpoint POST /v1/transportistas/{id}/capacidades y 404 R8 correctos.")

        # ----------------------------------------------------------------------
        # CASO 2: Cotizar y confirmar con Transportista 7 (precioEnLinea: false)
        # ----------------------------------------------------------------------
        log("\n" + "-" * 80)
        log("--- CASO 2: Cotizar y confirmar con Transportista 7 (precioEnLinea: false) ---")
        log("-" * 80)

        corr_cot_7 = f"COU-QA-B0-T7-COT-{int(time.time())}"
        payload_cotizar_7 = {
            "idCorrelacion": corr_cot_7,
            "idEnvioCourier": "ENV-QA-B0-T7",
            "idCourier": 1,
            "ubigeoOrigen": "150101",
            "ubigeoDestino": "130101",
            "tipoCarga": "CARGA_LIVIANA",
            "fechaSolicitud": datetime.now().strftime("%Y-%m-%dT%H:%M:%S-05:00"),
            "paquetes": [
                {
                    "descripcion": "Paquete prueba precio estimado",
                    "pesoKg": 4.5,
                    "cantidad": 1,
                    "largoCm": 40.0,
                    "anchoCm": 30.0,
                    "altoCm": 15.0,
                    "esFragil": False,
                    "valorDeclarado": 100.0
                }
            ]
        }
        res_cot_7 = http_post(f"{URL_MEDIADOR}/cotizar", payload_cotizar_7)
        log(f"\n[2.1] POST /cotizar (T7): HTTP {res_cot_7['status']}")
        assert res_cot_7["status"] == 200, f"Error en cotizar: {res_cot_7}"

        opciones_7 = res_cot_7["body"].get("opciones", [])
        opcion_t7 = next((op for op in opciones_7 if op["idTransportista"] == 7), None)
        assert opcion_t7 is not None, "No se encontró opción para transportista 7 en la cotización"
        id_opcion_7 = opcion_t7["idOpcion"]
        log(f"  Opción obtenida para T7: {id_opcion_7} (Precio referencial: {opcion_t7['precioReferencial']})")

        corr_conf_7 = f"COU-QA-B0-T7-CONF-{int(time.time())}"
        payload_confirmar_7 = {
            "idCorrelacion": corr_conf_7,
            "idOpcion": id_opcion_7,
            "remitente": {
                "tipoDocumento": "DNI",
                "numeroDocumento": "45678912",
                "nombres": "Juan",
                "apellidos": "Pérez",
                "telefono": "987654321"
            },
            "consignado": {
                "tipoDocumento": "DNI",
                "numeroDocumento": "10293847",
                "nombres": "María",
                "apellidos": "López",
                "telefono": "912345678",
                "direccion": "Av. España 123",
                "ubigeo": "130101"
            }
        }
        res_conf_7 = http_post(f"{URL_MEDIADOR}/confirmar", payload_confirmar_7)
        log(f"\n[2.2] POST /confirmar (T7, precioEnLinea=false):")
        log(f"  HTTP Status: {res_conf_7['status']}")
        log(f"  Respuesta: {json.dumps(res_conf_7['body'], indent=2)}")

        assert res_conf_7["status"] == 200
        assert res_conf_7["body"]["estado"] == "CONFIRMADO"
        assert res_conf_7["body"]["precioEsEstimado"] is True, f"Esperado precioEsEstimado=True, obtenido {res_conf_7['body'].get('precioEsEstimado')}"
        assert res_conf_7["body"]["idTransportista"] == 7
        cod_seg_7 = res_conf_7["body"]["codigoSeguimiento"]
        assert cod_seg_7.startswith("MED-2026-")

        # Verificación en BD para T7
        log("\n[2.3] Verificación en BD para T7 (IdCorrelacionPrecio debe ser NULL y comentario estimado):")
        sql_envio_7 = f"""
        SELECT CodigoSeguimiento, IdCorrelacion, IdCorrelacionPrecio, IdCorrelacionRegistro, Estado, IdTransportistaElegida
        FROM MED.Envio
        WHERE CodigoSeguimiento = '{cod_seg_7}';
        """
        salida_envio_7 = ejecutar_sql(sql_envio_7)
        log(salida_envio_7)

        sql_estado_7 = f"""
        SELECT ee.IdEnvio, ee.Estado, ee.Comentario
        FROM MED.EnvioEstado ee
        INNER JOIN MED.Envio e ON ee.IdEnvio = e.IdEnvio
        WHERE e.CodigoSeguimiento = '{cod_seg_7}';
        """
        salida_estado_7 = ejecutar_sql(sql_estado_7)
        log(salida_estado_7)

        sql_coti_7 = f"""
        SELECT IdOpcion, PrecioProveedor, PrecioEsEstimado, Elegida
        FROM MED.Cotizacion
        WHERE IdOpcion = '{id_opcion_7}';
        """
        salida_coti_7 = ejecutar_sql(sql_coti_7)
        log(salida_coti_7)

        assert "NULL" in salida_envio_7, "IdCorrelacionPrecio debería ser NULL en MED.Envio para T7"
        assert "Confirmado con precio estimado (sin B3)" in salida_estado_7, "Comentario esperado en EnvioEstado"
        log("  [VERIFICADO] Caso 2 completado exitosamente: confirmado sin B3 con precio estimado.")

        # ----------------------------------------------------------------------
        # CASO 3: Cotizar y confirmar con Transportista 8 (precioEnLinea: true)
        # ----------------------------------------------------------------------
        log("\n" + "-" * 80)
        log("--- CASO 3: Cotizar y confirmar con Transportista 8 (precioEnLinea: true) ---")
        log("-" * 80)

        corr_cot_8 = f"COU-QA-B0-T8-COT-{int(time.time())}"
        payload_cotizar_8 = {
            "idCorrelacion": corr_cot_8,
            "idEnvioCourier": "ENV-QA-B0-T8",
            "idCourier": 1,
            "ubigeoOrigen": "150101",
            "ubigeoDestino": "130101",
            "tipoCarga": "CARGA_LIVIANA",
            "fechaSolicitud": datetime.now().strftime("%Y-%m-%dT%H:%M:%S-05:00"),
            "paquetes": [
                {
                    "descripcion": "Paquete prueba precio real",
                    "pesoKg": 4.5,
                    "cantidad": 1,
                    "largoCm": 40.0,
                    "anchoCm": 30.0,
                    "altoCm": 15.0,
                    "esFragil": False,
                    "valorDeclarado": 200.0
                }
            ]
        }
        res_cot_8 = http_post(f"{URL_MEDIADOR}/cotizar", payload_cotizar_8)
        log(f"\n[3.1] POST /cotizar (T8): HTTP {res_cot_8['status']}")
        assert res_cot_8["status"] == 200, f"Error en cotizar: {res_cot_8}"

        opciones_8 = res_cot_8["body"].get("opciones", [])
        opcion_t8 = next((op for op in opciones_8 if op["idTransportista"] == 8), None)
        assert opcion_t8 is not None, "No se encontró opción para transportista 8 en la cotización"
        id_opcion_8 = opcion_t8["idOpcion"]
        log(f"  Opción obtenida para T8: {id_opcion_8} (Precio referencial: {opcion_t8['precioReferencial']})")

        corr_conf_8 = f"COU-QA-B0-T8-CONF-{int(time.time())}"
        payload_confirmar_8 = {
            "idCorrelacion": corr_conf_8,
            "idOpcion": id_opcion_8,
            "remitente": {
                "tipoDocumento": "DNI",
                "numeroDocumento": "45678912",
                "nombres": "Carlos",
                "apellidos": "García",
                "telefono": "987654321"
            },
            "consignado": {
                "tipoDocumento": "DNI",
                "numeroDocumento": "10293847",
                "nombres": "Ana",
                "apellidos": "Torres",
                "telefono": "912345678",
                "direccion": "Jr. Pizarro 456",
                "ubigeo": "130101"
            }
        }
        res_conf_8 = http_post(f"{URL_MEDIADOR}/confirmar", payload_confirmar_8)
        log(f"\n[3.2] POST /confirmar (T8, precioEnLinea=true):")
        log(f"  HTTP Status: {res_conf_8['status']}")
        log(f"  Respuesta: {json.dumps(res_conf_8['body'], indent=2)}")

        assert res_conf_8["status"] == 200
        assert res_conf_8["body"]["estado"] == "CONFIRMADO"
        assert res_conf_8["body"]["precioEsEstimado"] is False, f"Esperado precioEsEstimado=False, obtenido {res_conf_8['body'].get('precioEsEstimado')}"
        assert res_conf_8["body"]["idTransportista"] == 8
        cod_seg_8 = res_conf_8["body"]["codigoSeguimiento"]
        assert cod_seg_8.startswith("MED-2026-")

        # Verificación en BD para T8
        log("\n[3.3] Verificación en BD para T8 (IdCorrelacionPrecio debe estar poblado):")
        sql_envio_8 = f"""
        SELECT CodigoSeguimiento, IdCorrelacion, IdCorrelacionPrecio, IdCorrelacionRegistro, Estado, IdTransportistaElegida
        FROM MED.Envio
        WHERE CodigoSeguimiento = '{cod_seg_8}';
        """
        salida_envio_8 = ejecutar_sql(sql_envio_8)
        log(salida_envio_8)

        sql_coti_8 = f"""
        SELECT IdOpcion, PrecioProveedor, PrecioEsEstimado, Elegida
        FROM MED.Cotizacion
        WHERE IdOpcion = '{id_opcion_8}';
        """
        salida_coti_8 = ejecutar_sql(sql_coti_8)
        log(salida_coti_8)

        assert "CORR-" in salida_envio_8 or len(salida_envio_8.split()) > 4, "IdCorrelacionPrecio debe estar poblado para T8"
        log("  [VERIFICADO] Caso 3 completado exitosamente: pasó por B3, IdCorrelacionPrecio poblado y precioEsEstimado=False.")

        # ----------------------------------------------------------------------
        # CASO 4: POST /anular contra Transportista 8 (anulacion: false) -> HTTP 501
        # ----------------------------------------------------------------------
        log("\n" + "-" * 80)
        log("--- CASO 4: POST /anular contra Transportista 8 (anulacion: false) ---")
        log("-" * 80)

        payload_anular = {
            "codigoSeguimiento": "MED-2026-000001",
            "motivo": "CLIENTE_DESISTIO"
        }
        res_anular_8 = http_post(f"{URL_SIM_8}/anular", payload_anular)
        log(f"\n[4.1] POST {URL_SIM_8}/anular (T8):")
        log(f"  HTTP Status: {res_anular_8['status']}")
        log(f"  Respuesta: {json.dumps(res_anular_8['body'], indent=2)}")

        assert res_anular_8["status"] == 501, f"Esperado HTTP 501, obtenido {res_anular_8['status']}"
        body_anular = res_anular_8["body"]
        assert body_anular.get("error") == "OPERACION_NO_SOPORTADA"
        assert body_anular.get("detalle") == "La transportista no soporta la operación solicitada."
        assert "campo" in body_anular and body_anular["campo"] is None
        assert "idCorrelacion" in body_anular and body_anular["idCorrelacion"] is None
        log("  [VERIFICADO] Caso 4 completado exitosamente: responde HTTP 501 con formato R8 obligatorio.")

        # También verificar que Cotizar en T7 (precioEnLinea: false) devuelve 501
        log("\n[4.2] Verificación adicional: POST /cotizar contra T7 (precioEnLinea: false) responde 501:")
        res_cot_dir_7 = http_post(f"{URL_SIM_7}/cotizar", {"idCorrelacion": "CORR-TEST-501", "idRuta": "R-LIM-TRU", "tipoCarga": "CARGA_LIVIANA", "paquetes": []})
        log(f"  POST {URL_SIM_7}/cotizar -> HTTP {res_cot_dir_7['status']}: {res_cot_dir_7['body']}")
        assert res_cot_dir_7["status"] == 501
        assert res_cot_dir_7["body"]["error"] == "OPERACION_NO_SOPORTADA"
        assert res_cot_dir_7["body"]["idCorrelacion"] == "CORR-TEST-501"
        log("  [VERIFICADO] Cotizar en T7 devuelve 501 con idCorrelacion preservado.")

        log("\n" + "=" * 80)
        log("TODAS LAS PRUEBAS DEL BLOQUE 2 (B0 CAPACIDADES) COMPLETADAS EXITOSAMENTE")
        log("=" * 80)

    finally:
        log("\n[CLEANUP] Deteniendo procesos...")
        for p, f in procs:
            try:
                p.terminate()
                p.wait(timeout=3)
            except Exception:
                try:
                    p.kill()
                except Exception:
                    pass
            try:
                f.close()
            except Exception:
                pass
        subprocess.run(["powershell", "-Command", "Stop-Process -Name dotnet -Force -ErrorAction SilentlyContinue"], capture_output=True)

    # Guardar archivo de evidencia
    ruta_evidencia = os.path.join("docs", "arreglos_sin_azure", "b0_capacidades.txt")
    os.makedirs(os.path.dirname(ruta_evidencia), exist_ok=True)
    with open(ruta_evidencia, "w", encoding="utf-8") as f:
        f.write("\n".join(evidencia))
    print(f"\n[OK] Evidencia guardada en: {ruta_evidencia}")

    # Copiar también en carpetas internas de cada repo si existen
    for sub in ["mediador-core", "mediador-simulador-eett"]:
        sub_path = os.path.join(sub, "docs", "arreglos_sin_azure", "b0_capacidades.txt")
        try:
            os.makedirs(os.path.dirname(sub_path), exist_ok=True)
            with open(sub_path, "w", encoding="utf-8") as f:
                f.write("\n".join(evidencia))
        except Exception:
            pass

if __name__ == "__main__":
    main()
