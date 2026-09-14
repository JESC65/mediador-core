USE MediadorDev;
GO

INSERT INTO MED.Courier (Ruc, RazonSocial, Activo)
VALUES ('20512345678', 'COURIER DEMO SAC', 1);
GO

SET IDENTITY_INSERT MED.Transportista ON;

INSERT INTO MED.Transportista (IdTransportista, Ruc, RazonSocial, UrlConector, Activo)
VALUES
    (7, '90000001007', 'Expreso Andino',           'https://localhost:7201', 1),
    (8, '90000001008', 'Transportes Rápido Sur',    'https://localhost:7202', 1),
    (9, '90000001009', 'Carga Segura del Norte',    'https://localhost:7203', 1);

SET IDENTITY_INSERT MED.Transportista OFF;
GO

DECLARE @IdCourier INT = (SELECT IdCourier FROM MED.Courier WHERE Ruc = '20512345678');

INSERT INTO MED.Convenio (IdCourier, IdTransportista, FechaInicio, FechaFin, Activo)
VALUES
    (@IdCourier, 7, '2026-01-01', NULL, 1),
    (@IdCourier, 8, '2026-01-01', NULL, 1),
    (@IdCourier, 9, '2026-01-01', NULL, 1);
GO

INSERT INTO MED.Ruta
    (IdTransportista, CodigoRutaExterno, UbigeoOrigen, UbigeoDestino, NombreOrigen, NombreDestino,
     FrecuenciaSalidas, HoraSalidaHabitual, AceptaFragil, PesoMaximoKg, Activa)
VALUES
    (7, 'R-LIM-TRU', '150101', '130101', 'Lima', 'Trujillo',  'Diaria',                      '18:30', 0, 50.00, 1),
    (7, 'R-LIM-ARE', '150101', '040101', 'Lima', 'Arequipa',  'Lunes, miercoles y viernes',  '20:00', 1, 80.00, 1),
    (8, 'R-LIM-TRU', '150101', '130101', 'Lima', 'Trujillo',  'Diaria',                      '18:30', 0, 50.00, 1),
    (8, 'R-LIM-ARE', '150101', '040101', 'Lima', 'Arequipa',  'Lunes, miercoles y viernes',  '20:00', 0, 80.00, 1),
    (9, 'R-LIM-TRU', '150101', '130101', 'Lima', 'Trujillo',  'Diaria',                      '18:30', 1, 100.00, 1),
    (9, 'R-LIM-ARE', '150101', '040101', 'Lima', 'Arequipa',  'Lunes, miercoles y viernes',  '20:00', 1, 100.00, 1);
GO

INSERT INTO MED.TarifaProveedor (IdRuta, TipoCarga, PesoDesdeKg, PesoHastaKg, Precio, PrecioMinimo, VigenteDesde, Activo)
SELECT r.IdRuta, v.TipoCarga, v.PesoDesdeKg, v.PesoHastaKg, v.Precio, v.PrecioMinimo, '2026-01-01', 1
FROM MED.Ruta r
JOIN (VALUES
    (7, 'R-LIM-TRU', 'CARGA_LIVIANA',  0.00,  5.00,  18.00,  18.00),
    (7, 'R-LIM-TRU', 'CARGA_LIVIANA',  5.01, 20.00,  38.00,  38.00),
    (7, 'R-LIM-TRU', 'CARGA_LIVIANA', 20.01, 50.00,  75.00,  75.00),
    (7, 'R-LIM-TRU', 'CARGA_PESADA',   0.00, 50.00,  95.00,  95.00),
    (7, 'R-LIM-ARE', 'CARGA_LIVIANA',  0.00, 10.00,  25.00,  25.00),
    (7, 'R-LIM-ARE', 'CARGA_LIVIANA', 10.01, 80.00,  60.00,  60.00),
    (7, 'R-LIM-ARE', 'CARGA_PESADA',   0.00, 80.00, 140.00, 140.00),
    (8, 'R-LIM-TRU', 'CARGA_LIVIANA',  0.00,  5.00,  15.30,  15.30),
    (8, 'R-LIM-TRU', 'CARGA_LIVIANA',  5.01, 20.00,  32.30,  32.30),
    (8, 'R-LIM-TRU', 'CARGA_LIVIANA', 20.01, 50.00,  63.75,  63.75),
    (8, 'R-LIM-TRU', 'CARGA_PESADA',   0.00, 50.00,  80.75,  80.75),
    (8, 'R-LIM-ARE', 'CARGA_LIVIANA',  0.00, 10.00,  21.25,  21.25),
    (8, 'R-LIM-ARE', 'CARGA_LIVIANA', 10.01, 80.00,  51.00,  51.00),
    (8, 'R-LIM-ARE', 'CARGA_PESADA',   0.00, 80.00, 119.00, 119.00),
    (9, 'R-LIM-TRU', 'CARGA_LIVIANA',  0.00,  5.00,  22.50,  22.50),
    (9, 'R-LIM-TRU', 'CARGA_LIVIANA',  5.01, 20.00,  47.50,  47.50),
    (9, 'R-LIM-TRU', 'CARGA_LIVIANA', 20.01, 50.00,  93.75,  93.75),
    (9, 'R-LIM-TRU', 'CARGA_PESADA',   0.00, 50.00, 118.75, 118.75),
    (9, 'R-LIM-ARE', 'CARGA_LIVIANA',  0.00, 10.00,  31.25,  31.25),
    (9, 'R-LIM-ARE', 'CARGA_LIVIANA', 10.01, 80.00,  75.00,  75.00),
    (9, 'R-LIM-ARE', 'CARGA_PESADA',   0.00, 80.00, 175.00, 175.00)
) AS v(IdTransportista, CodigoRutaExterno, TipoCarga, PesoDesdeKg, PesoHastaKg, Precio, PrecioMinimo)
    ON v.IdTransportista = r.IdTransportista AND v.CodigoRutaExterno = r.CodigoRutaExterno;
GO

SELECT * FROM MED.Courier;
SELECT * FROM MED.Transportista ORDER BY IdTransportista;
SELECT * FROM MED.Convenio ORDER BY IdTransportista;
SELECT * FROM MED.Ruta ORDER BY IdTransportista, CodigoRutaExterno;

SELECT
    t.IdTransportista, r.CodigoRutaExterno, tp.TipoCarga,
    tp.PesoDesdeKg, tp.PesoHastaKg, tp.Precio, tp.PrecioMinimo
FROM MED.TarifaProveedor tp
JOIN MED.Ruta r ON r.IdRuta = tp.IdRuta
JOIN MED.Transportista t ON t.IdTransportista = r.IdTransportista
ORDER BY t.IdTransportista, r.CodigoRutaExterno, tp.PesoDesdeKg;
