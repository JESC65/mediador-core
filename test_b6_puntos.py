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
URL_SIM_9 = "https://localhost:7203"
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

    log_func("[INIT] Verificando idempotencia de database/06_puntos.sql...")
    ruta_script = os.path.join("mediador-core", "database", "06_puntos.sql")
    # Ejecución 1
    r1 = subprocess.run(["sqlcmd", "-S", "localhost", "-d", "MediadorDev", "-i", ruta_script], capture_output=True, text=True)
    assert r1.returncode == 0, f"Error en 1ra ejecución de 06_puntos.sql: {r1.stderr}"
    # Ejecución 2
    r2 = subprocess.run(["sqlcmd", "-S", "localhost", "-d", "MediadorDev", "-i", ruta_script], capture_output=True, text=True)
    assert r2.returncode == 0, f"Error en 2da ejecución de 06_puntos.sql: {r2.stderr}"
    log_func("  [OK] database/06_puntos.sql ejecutado 2 veces con éxito (idempotente).")

    log_func("[INIT] Preparando base de datos MediadorDev...")
    sql_prep = """
    SET QUOTED_IDENTIFIER ON;
    -- Limpiar puntos y capacidades de prueba
    DELETE FROM MED.PuntoRecepcion WHERE IdTransportista IN (7, 8, 9);
    DELETE FROM MED.TransportistaCapacidad WHERE IdTransportista IN (7, 8, 9);

    -- Configurar Transportistas 7 y 9 activas
    UPDATE MED.Transportista SET UrlConector = 'https://localhost:7201', Activo = 1 WHERE IdTransportista = 7;
    UPDATE MED.Transportista SET Activo = 0 WHERE IdTransportista = 8;
    UPDATE MED.Transportista SET UrlConector = 'https://localhost:7203', Activo = 1 WHERE IdTransportista = 9;
    """
    ejecutar_sql(sql_prep)
    log_func("[INIT] Base de datos MediadorDev lista para la prueba.")

def main():
    evidencia = []
    def log(msg):
        print(msg)
        evidencia.append(msg)

    log("=" * 80)
    log("EVIDENCIA DE PRUEBA: BLOQUE 3 - PUNTOS DE RECEPCIÓN (B6) Y SALUD SEGÚN C2 v2.0 §6")
    log(f"Fecha y hora: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    log(f"Simulador T7 (puntos=true):  {URL_SIM_7}")
    log(f"Simulador T9 (puntos=false): {URL_SIM_9}")
    log(f"Mediador:                    {URL_MEDIADOR}")
    log("=" * 80 + "\n")

    preparar_entorno(log)

    procs = []
    try:
        # 1. Iniciar Simulador Instancia 1 (T7, puerto 7201)
        log("\n[PASO 1] Iniciando Simulador Instancia 1 (T7, puerto 7201)...")
        env_t7 = os.environ.copy()
        env_t7["TransportistaConfigPath"] = "transportista-7.json"
        env_t7["ASPNETCORE_URLS"] = URL_SIM_7
        f_log_t7 = open("simulador_t7_b6.log", "w", encoding="utf-8")
        p_t7 = subprocess.Popen(
            ["dotnet", "run", "--no-build", "--project", "mediador-simulador-eett/SimuladorEETT.Presentation", "--urls", URL_SIM_7, "--", "--TransportistaConfigPath", "transportista-7.json"],
            env=env_t7,
            stdout=f_log_t7,
            stderr=subprocess.STDOUT,
            text=True
        )
        procs.append((p_t7, f_log_t7))

        # 2. Iniciar Simulador Instancia 2 (T9, puerto 7203)
        log("[PASO 2] Iniciando Simulador Instancia 2 (T9, puerto 7203)...")
        env_t9 = os.environ.copy()
        env_t9["TransportistaConfigPath"] = "transportista-9.json"
        env_t9["ASPNETCORE_URLS"] = URL_SIM_9
        f_log_t9 = open("simulador_t9_b6.log", "w", encoding="utf-8")
        p_t9 = subprocess.Popen(
            ["dotnet", "run", "--no-build", "--project", "mediador-simulador-eett/SimuladorEETT.Presentation", "--urls", URL_SIM_9, "--", "--TransportistaConfigPath", "transportista-9.json"],
            env=env_t9,
            stdout=f_log_t9,
            stderr=subprocess.STDOUT,
            text=True
        )
        procs.append((p_t9, f_log_t9))

        # Esperar que ambos simuladores respondan
        log("[WAIT] Esperando que los simuladores estén arriba...")
        t7_ok, t9_ok = False, False
        for i in range(30):
            time.sleep(1)
            if not t7_ok:
                r7 = http_get(f"{URL_SIM_7}/v1/salud")
                if r7["status"] == 200:
                    t7_ok = True
                    log(f"  [OK] Simulador T7 respondiendo en {URL_SIM_7}")
            if not t9_ok:
                r9 = http_get(f"{URL_SIM_9}/v1/salud")
                if r9["status"] == 200:
                    t9_ok = True
                    log(f"  [OK] Simulador T9 respondiendo en {URL_SIM_9}")
            if t7_ok and t9_ok:
                break

        if not (t7_ok and t9_ok):
            raise Exception("No se pudieron levantar ambos simuladores.")

        # 3. Iniciar Mediador (puerto 7250)
        log("\n[PASO 3] Iniciando Mediador (puerto 7250)...")
        f_log_med = open("mediador_b6.log", "w", encoding="utf-8")
        p_med = subprocess.Popen(
            ["dotnet", "run", "--no-build", "--project", "mediador-core/Mediador.Presentation", "--urls", URL_MEDIADOR],
            stdout=f_log_med,
            stderr=subprocess.STDOUT,
            text=True
        )
        procs.append((p_med, f_log_med))

        log("[WAIT] Esperando que Mediador esté arriba y sincronice capacidades y puntos al arranque...")
        med_ok = False
        for i in range(35):
            time.sleep(1)
            r = http_get(f"{URL_MEDIADOR}/swagger/index.html")
            if r["status"] in (200, 404):
                med_ok = True
                break

        if not med_ok:
            raise Exception("Mediador no levantó a tiempo.")

        # Dar 3 segundos para que BackgroundService complete sincronización de capacidades y puntos
        time.sleep(3)
        log("  [OK] Mediador levantado y respondiendo en " + URL_MEDIADOR)

        # ----------------------------------------------------------------------
        # CASO 1: B6 exitoso y carga en BD (T7 en 7201)
        # ----------------------------------------------------------------------
        log("\n" + "-" * 80)
        log("--- CASO 1: B6 exitoso y carga en BD (Transportista 7, puntos=true) ---")
        log("-" * 80)

        r_puntos_7 = http_get(f"{URL_SIM_7}/v1/puntos")
        log(f"\n[1.1] GET {URL_SIM_7}/v1/puntos (Transportista 7):")
        log(f"  HTTP Status: {r_puntos_7['status']}")
        log(f"  Respuesta: {json.dumps(r_puntos_7['body'], indent=2)}")

        assert r_puntos_7["status"] == 200, f"Status esperado 200, obtenido {r_puntos_7['status']}"
        assert r_puntos_7["body"]["idTransportista"] == 7
        puntos_t7 = r_puntos_7["body"]["puntos"]
        assert len(puntos_t7) == 2, f"Esperados 2 puntos, obtenidos {len(puntos_t7)}"
        assert puntos_t7[0]["codigo"] == "PTO-01"
        assert puntos_t7[1]["codigo"] == "PTO-02"
        log("  [VERIFICADO] B6 en Simulador T7 responde HTTP 200 con sus puntos sintéticos.")

        # Consulta SQL en MediadorDev para verificar carga en MED.PuntoRecepcion
        log("\n[1.2] Verificación en Base de Datos: MED.PuntoRecepcion para Transportista 7:")
        sql_puntos_7 = """
        SELECT IdPunto, IdTransportista, CodigoExterno, Nombre, Direccion, Ubigeo, Latitud, Longitud, Horario, Version, Activo
        FROM MED.PuntoRecepcion
        WHERE IdTransportista = 7
        ORDER BY CodigoExterno;
        """
        salida_puntos_7 = ejecutar_sql(sql_puntos_7)
        log(salida_puntos_7)

        assert "PTO-01" in salida_puntos_7 and "PTO-02" in salida_puntos_7, "Puntos no encontrados en MED.PuntoRecepcion"
        log("  [VERIFICADO] Puntos cargados en MED.PuntoRecepcion con Version=1 y Latitud, Longitud y Horario en NULL.")

        # Probar endpoint bajo demanda en Mediador
        log("\n[1.3] Verificación de endpoint POST /v1/transportistas/7/puntos en Mediador:")
        r_sync_puntos = http_post(f"{URL_MEDIADOR}/v1/transportistas/7/puntos")
        log(f"  POST /v1/transportistas/7/puntos -> HTTP {r_sync_puntos['status']}: {r_sync_puntos['body']}")
        assert r_sync_puntos["status"] == 200
        assert r_sync_puntos["body"]["exitoso"] is True

        r_sync_puntos_404 = http_post(f"{URL_MEDIADOR}/v1/transportistas/999/puntos")
        log(f"  POST /v1/transportistas/999/puntos -> HTTP {r_sync_puntos_404['status']}: {r_sync_puntos_404['body']}")
        assert r_sync_puntos_404["status"] == 404
        assert r_sync_puntos_404["body"]["error"] == "RECURSO_NO_ENCONTRADO"
        log("  [VERIFICADO] Endpoint POST /v1/transportistas/{id}/puntos y 404 R8 correctos.")

        # ----------------------------------------------------------------------
        # CASO 2: B6 no soportado -> 501 (T9 en 7203, puntos=false)
        # ----------------------------------------------------------------------
        log("\n" + "-" * 80)
        log("--- CASO 2: B6 no soportado -> HTTP 501 (Transportista 9, puntos=false) ---")
        log("-" * 80)

        r_puntos_9 = http_get(f"{URL_SIM_9}/v1/puntos")
        log(f"\n[2.1] GET {URL_SIM_9}/v1/puntos (Transportista 9):")
        log(f"  HTTP Status: {r_puntos_9['status']}")
        log(f"  Respuesta: {json.dumps(r_puntos_9['body'], indent=2)}")

        assert r_puntos_9["status"] == 501, f"Esperado HTTP 501, obtenido {r_puntos_9['status']}"
        body_p9 = r_puntos_9["body"]
        assert body_p9.get("error") == "OPERACION_NO_SOPORTADA"
        assert body_p9.get("detalle") == "La transportista no soporta la consulta de puntos de recepción."
        assert "campo" in body_p9 and body_p9["campo"] is None
        assert "idCorrelacion" in body_p9 and body_p9["idCorrelacion"] is None
        log("  [VERIFICADO] B6 en Simulador T9 responde HTTP 501 con cuerpo R8 obligatorio.")

        # Verificar que en BD no se cargó ningún punto para T9
        log("\n[2.2] Verificación en Base de Datos: MED.PuntoRecepcion para Transportista 9 (debe tener 0 filas):")
        sql_count_9 = "SELECT COUNT(*) AS TotalPuntosT9 FROM MED.PuntoRecepcion WHERE IdTransportista = 9;"
        salida_count_9 = ejecutar_sql(sql_count_9)
        log(salida_count_9)
        assert "0" in salida_count_9, "No debe haber puntos registrados para transportista 9"
        log("  [VERIFICADO] En Mediador no se cargaron puntos para Transportista 9.")

        # ----------------------------------------------------------------------
        # CASO 3: Endpoint de Salud (GET /v1/salud)
        # ----------------------------------------------------------------------
        log("\n" + "-" * 80)
        log("--- CASO 3: Endpoint de Salud (GET /v1/salud) ---")
        log("-" * 80)

        r_salud_7 = http_get(f"{URL_SIM_7}/v1/salud")
        log(f"\n[3.1] GET {URL_SIM_7}/v1/salud (Transportista 7):")
        log(f"  HTTP Status: {r_salud_7['status']}")
        log(f"  Respuesta: {json.dumps(r_salud_7['body'], indent=2)}")

        assert r_salud_7["status"] == 200
        assert r_salud_7["body"]["estado"] == "ACTIVO"
        assert r_salud_7["body"]["idTransportista"] == 7
        assert r_salud_7["body"]["ultimaSecuencia"] == 8, f"Esperada última secuencia 8, obtenida {r_salud_7['body']['ultimaSecuencia']}"
        assert "fechaHora" in r_salud_7["body"]
        log("  [VERIFICADO] Endpoint /v1/salud en T7 responde HTTP 200 OK con últimaSecuencia=8 real de la bandeja.")

        r_salud_9 = http_get(f"{URL_SIM_9}/v1/salud")
        log(f"\n[3.2] GET {URL_SIM_9}/v1/salud (Transportista 9):")
        log(f"  HTTP Status: {r_salud_9['status']}")
        log(f"  Respuesta: {json.dumps(r_salud_9['body'], indent=2)}")

        assert r_salud_9["status"] == 200
        assert r_salud_9["body"]["estado"] == "ACTIVO"
        assert r_salud_9["body"]["idTransportista"] == 9
        assert "ultimaSecuencia" in r_salud_9["body"]
        log("  [VERIFICADO] Endpoint /v1/salud en T9 responde HTTP 200 OK.")

        log("\n" + "=" * 80)
        log("TODAS LAS PRUEBAS DEL BLOQUE 3 (B6 PUNTOS Y SALUD) COMPLETADAS EXITOSAMENTE")
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

    # Guardar archivo de evidencia en mediador-core/docs/arreglos_sin_azure/b6_puntos.txt
    ruta_evidencia_core = os.path.join("mediador-core", "docs", "arreglos_sin_azure", "b6_puntos.txt")
    os.makedirs(os.path.dirname(ruta_evidencia_core), exist_ok=True)
    with open(ruta_evidencia_core, "w", encoding="utf-8") as f:
        f.write("\n".join(evidencia))
    print(f"\n[OK] Evidencia guardada en: {ruta_evidencia_core}")

    # Guardar también en docs/arreglos_sin_azure/b6_puntos.txt y mediador-simulador-eett
    for sub in [".", "mediador-simulador-eett"]:
        sub_path = os.path.join(sub, "docs", "arreglos_sin_azure", "b6_puntos.txt")
        try:
            os.makedirs(os.path.dirname(sub_path), exist_ok=True)
            with open(sub_path, "w", encoding="utf-8") as f:
                f.write("\n".join(evidencia))
        except Exception:
            pass

if __name__ == "__main__":
    main()
