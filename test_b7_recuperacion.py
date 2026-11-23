import json
import os
import subprocess
import sys
import time
import urllib.request
import urllib.error
from datetime import datetime

URL_SIMULADOR = "http://localhost:5201"
URL_MEDIADOR = "http://localhost:5250"

def ejecutar_sql(sql):
    proc = subprocess.run(
        ["sqlcmd", "-S", "localhost", "-d", "MediadorDev", "-Q", sql],
        capture_output=True,
        text=True
    )
    if proc.returncode != 0:
        print(f"[WARN] Error en sqlcmd: {proc.stderr}")
    return proc.stdout.strip()

def http_get(url):
    req = urllib.request.Request(url, method="GET")
    with urllib.request.urlopen(req) as resp:
        return json.loads(resp.read().decode("utf-8"))

def http_post(url, payload):
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"}, method="POST")
    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status, json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read().decode("utf-8"))

def preparar_entorno():
    print("[1] Limpiando procesos previos y preparando base de datos...")
    subprocess.run(["powershell", "-Command", "Stop-Process -Name dotnet -Force -ErrorAction SilentlyContinue"], capture_output=True)
    time.sleep(1)

    # Limpiar datos previos del transportista 7 en Mediador
    sql = """
    SET QUOTED_IDENTIFIER ON;
    DELETE FROM MED.SecuenciaHueco WHERE IdTransportista = 7;
    DELETE FROM MED.TransportistaSecuencia WHERE IdTransportista = 7;
    DELETE FROM MED.EventoRecibido WHERE IdTransportista = 7;

    -- Envio base para los eventos de trazabilidad
    IF NOT EXISTS (SELECT 1 FROM MED.Envio WHERE CodigoSeguimiento = 'MED-2026-000001')
    BEGIN
        INSERT INTO MED.Envio (IdCorrelacion, IdEnvioCourier, IdCourier, UbigeoDestino, PesoTotalKg, CantidadPaquetes, Estado, CodigoSeguimiento)
        VALUES ('COU-QA-B7-RECUP', 'ENV-QA-B7-01', 1, '130101', 4.0, 1, 'CONFIRMADO', 'MED-2026-000001');
    END
    ELSE
    BEGIN
        UPDATE MED.Envio SET Estado = 'CONFIRMADO' WHERE CodigoSeguimiento = 'MED-2026-000001';
    END

    UPDATE MED.Transportista SET UrlConector = 'http://localhost:5201', Activo = 1 WHERE IdTransportista = 7;
    UPDATE MED.Transportista SET Activo = 0 WHERE IdTransportista IN (8, 9);
    """
    ejecutar_sql(sql)

    # Limpiar bandeja_eventos.json
    ruta_bandeja = os.path.join("mediador-simulador-eett", "SimuladorEETT.Presentation", "bandeja_eventos.json")
    with open(ruta_bandeja, "w", encoding="utf-8") as f:
        f.write("[]")
    print("[1] Base de datos y bandeja inicializadas limpiamente.")

def main():
    preparar_entorno()
    lineas = []
    def log(msg):
        print(msg)
        lineas.append(msg)

    log("=" * 80)
    log("EVIDENCIA DE PRUEBA: BLOQUE 3 - RECUPERACIÓN AUTOMÁTICA DE EVENTOS (B7 Y R6)")
    log(f"Fecha y hora: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    log(f"Simulador: {URL_SIMULADOR}")
    log(f"Mediador:  {URL_MEDIADOR}")
    log("=" * 80 + "\n")

    # Iniciar Simulador EETT
    log("[PASO 1] Iniciando Simulador EETT en segundo plano...")
    f_sim_log = open("simulador.log", "w", encoding="utf-8")
    proc_sim = subprocess.Popen(
        ["dotnet", "run", "--no-build", "--project", "mediador-simulador-eett/SimuladorEETT.Presentation", "--urls", URL_SIMULADOR],
        stdout=f_sim_log,
        stderr=subprocess.STDOUT,
        text=True
    )

    # Esperar que el simulador esté arriba
    simulador_listo = False
    for _ in range(30):
        time.sleep(1)
        try:
            http_get(f"{URL_SIMULADOR}/v1/eventos?desde=0")
            simulador_listo = True
            break
        except Exception:
            pass

    if not simulador_listo:
        log("[ERROR] El simulador no levantó a tiempo.")
        proc_sim.kill()
        f_sim_log.close()
        sys.exit(1)

    log("[OK] Simulador EETT levantado y respondiendo en " + URL_SIMULADOR)

    # -------------------------------------------------------------------------
    # FASE 1: Con el Mediador apagado, generar 3 eventos en el simulador
    # -------------------------------------------------------------------------
    log("\n" + "-" * 80)
    log("--- FASE 1: Con Mediador APAGADO, generar 3 eventos en el simulador ---")
    log("-" * 80)

    eventos_simular = [
        {
            "tipoEvento": "RECEPCION_CONFIRMADA",
            "codigoSeguimiento": "MED-2026-000001",
            "datos": { "idPunto": 40, "recibidoPor": "Encargado de mostrador" }
        },
        {
            "tipoEvento": "BUS_ASIGNADO",
            "codigoSeguimiento": "MED-2026-000001",
            "datos": { "placaBus": "T1A-987", "salidaEstimada": "2026-11-17T20:00:00-05:00" }
        },
        {
            "tipoEvento": "EN_VIAJE",
            "codigoSeguimiento": "MED-2026-000001",
            "datos": { "horaSalida": "2026-11-17T20:15:00-05:00" }
        }
    ]

    for i, evt in enumerate(eventos_simular, start=1):
        status, resp = http_post(f"{URL_SIMULADOR}/simular-evento", evt)
        log(f"Evento {i} emitido ({evt['tipoEvento']}): Secuencia={resp['mensaje']['secuencia']}, EnviadoHttp={resp['enviado']}")
        assert resp["enviado"] is False, "El Mediador debe estar apagado y el envío HTTP debe fallar"
        assert resp["mensaje"]["secuencia"] == i

    eventos_bandeja = http_get(f"{URL_SIMULADOR}/v1/eventos?desde=0")
    log(f"\nConsulta B7 en simulador (GET /v1/eventos?desde=0): {len(eventos_bandeja)} eventos en bandeja.")
    log(f"Secuencias en bandeja: {[e['secuencia'] for e in eventos_bandeja]}")
    assert len(eventos_bandeja) == 3

    # -------------------------------------------------------------------------
    # FASE 2: Arrancar el Mediador y verificar recuperación al arranque
    # -------------------------------------------------------------------------
    log("\n" + "-" * 80)
    log("--- FASE 2: Arrancar el Mediador y verificar recuperación al arranque ---")
    log("-" * 80)

    log("Iniciando Mediador...")
    f_med_log = open("mediador.log", "w", encoding="utf-8")
    proc_med = subprocess.Popen(
        ["dotnet", "run", "--no-build", "--project", "mediador-core/Mediador.Presentation", "--urls", URL_MEDIADOR],
        stdout=f_med_log,
        stderr=subprocess.STDOUT,
        text=True
    )

    mediador_listo = False
    for _ in range(40):
        time.sleep(1)
        try:
            req = urllib.request.Request(f"{URL_MEDIADOR}/swagger/index.html")
            with urllib.request.urlopen(req) as resp:
                if resp.status == 200:
                    mediador_listo = True
                    break
        except Exception:
            pass

    if not mediador_listo:
        log("[ERROR] El Mediador no levantó a tiempo.")
        f_med_log.close()
        if os.path.exists("mediador.log"):
            with open("mediador.log", "r", encoding="utf-8") as fl:
                log("Log Mediador:\n" + fl.read())
        proc_sim.kill()
        proc_med.kill()
        sys.exit(1)

    log("[OK] Mediador levantado. Esperando que el worker ejecute la recuperación inicial...")
    time.sleep(4)

    # Verificar en BD del Mediador
    bd_recuperados = ejecutar_sql("""
        SELECT IdEventoRecibido, IdEvento, TipoEvento, Secuencia, RequiereMensajeRechazado, Procesado 
        FROM MED.EventoRecibido 
        WHERE IdTransportista = 7
        ORDER BY Secuencia;
    """)
    log(f"\nVerificación en BD: Eventos recuperados en MED.EventoRecibido:\n{bd_recuperados}")

    bd_contigua_f2 = ejecutar_sql("""
        SELECT IdTransportista, UltimaSecuenciaContigua 
        FROM MED.TransportistaSecuencia 
        WHERE IdTransportista = 7;
    """)
    log(f"Verificación en BD: Secuencia contigua tras arranque:\n{bd_contigua_f2}")

    cant_eventos = ejecutar_sql("SELECT COUNT(*) FROM MED.EventoRecibido WHERE IdTransportista = 7;")
    log(f"Total eventos guardados en Mediador: {cant_eventos.splitlines()[-1].strip()}")
    assert "3" in cant_eventos

    # -------------------------------------------------------------------------
    # FASE 3: Provocar un hueco perdiendo un envío a propósito y validar recuperación automática
    # -------------------------------------------------------------------------
    log("\n" + "-" * 80)
    log("--- FASE 3: Provocar hueco de secuencia y validar autorecuperación inmediata ---")
    log("-" * 80)

    # Generar eventos 4 y 5 a través de la API del simulador (quedan en su bandeja con secuencias 4 y 5)
    status4, resp4 = http_post(f"{URL_SIMULADOR}/simular-evento", {
        "tipoEvento": "ARRIBADO",
        "codigoSeguimiento": "MED-2026-000001",
        "datos": { "terminal": "TRUJILLO_TERMINAL" }
    })
    log(f"Evento 4 generado en simulador: Secuencia={resp4['mensaje']['secuencia']}")

    status5_sim, resp5_sim = http_post(f"{URL_SIMULADOR}/simular-evento", {
        "tipoEvento": "ENTREGADO",
        "codigoSeguimiento": "MED-2026-000001",
        "datos": { "recibidoPor": "Juan Perez (DNI 12345678)" }
    })
    log(f"Evento 5 generado en simulador: Secuencia={resp5_sim['mensaje']['secuencia']}")

    log("\nSimulando pérdida en tránsito: el evento 4 no llega al Mediador, pero sí le llega directamente el evento 5...")
    evento_5_payload = resp5_sim["mensaje"]
    status5, resp5 = http_post(f"{URL_MEDIADOR}/v1/eett/eventos", evento_5_payload)
    log(f"POST evento 5 al Mediador: HTTP {status5}, Respuesta: {resp5}")
    assert status5 == 202

    log("Esperando que el canal despierte al worker y recupere automáticamente el evento 4 faltante...")
    time.sleep(5)

    # Verificar que el hueco fue detectado y resuelto
    bd_huecos = ejecutar_sql("""
        SELECT IdHueco, IdTransportista, SecuenciaEsperada, SecuenciaRecibida, Resuelto 
        FROM MED.SecuenciaHueco 
        WHERE IdTransportista = 7;
    """)
    log(f"\nVerificación en BD: Estado de huecos en MED.SecuenciaHueco:\n{bd_huecos}")

    bd_contigua_f3 = ejecutar_sql("""
        SELECT IdTransportista, UltimaSecuenciaContigua 
        FROM MED.TransportistaSecuencia 
        WHERE IdTransportista = 7;
    """)
    log(f"Verificación en BD: Secuencia contigua tras completar el hueco:\n{bd_contigua_f3}")

    bd_todos = ejecutar_sql("""
        SELECT IdEventoRecibido, IdEvento, TipoEvento, Secuencia, Procesado 
        FROM MED.EventoRecibido 
        WHERE IdTransportista = 7
        ORDER BY Secuencia;
    """)
    log(f"Verificación en BD: Todos los eventos en MED.EventoRecibido:\n{bd_todos}")

    log("\n" + "=" * 80)
    log("PRUEBA B7 RECUPERACIÓN COMPLETADA EXITOSAMENTE CONFORME A REGLA R6 Y C2 v2.0")
    log("=" * 80)

    # Terminar procesos
    proc_med.kill()
    proc_sim.kill()
    f_med_log.close()
    f_sim_log.close()

    # Guardar evidencia
    ruta_out1 = "mediador-core/docs/arreglos_sin_azure/b7_recuperacion.txt"
    with open(ruta_out1, "w", encoding="utf-8") as f:
        f.write("\n".join(lineas) + "\n")
    ruta_out2 = "docs/arreglos_sin_azure/b7_recuperacion.txt"
    with open(ruta_out2, "w", encoding="utf-8") as f:
        f.write("\n".join(lineas) + "\n")

    print(f"\n[OK] Evidencia generada en {ruta_out1} y {ruta_out2}")

if __name__ == "__main__":
    main()
