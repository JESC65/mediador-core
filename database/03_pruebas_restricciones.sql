USE MediadorDev;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @IdEnvio1 INT;
    INSERT INTO MED.Envio (IdCorrelacion, IdEnvioCourier, IdCourier, UbigeoDestino, PesoTotalKg, CantidadPaquetes, Estado)
    SELECT CONCAT('PRUEBA-1-', CONVERT(VARCHAR(36), NEWID())), 'ENV-TEST-1', IdCourier, '130101', 10.00, 1, 'COTIZADO'
    FROM MED.Courier WHERE Ruc = '20512345678';
    SET @IdEnvio1 = SCOPE_IDENTITY();

    INSERT INTO MED.Cotizacion (IdEnvio, IdTransportista, IdOpcion, PrecioProveedor, VigenteHasta)
    VALUES (@IdEnvio1, 999999, CONCAT('OPC-TEST-1-', CONVERT(VARCHAR(36), NEWID())), 38.00, SYSDATETIMEOFFSET());

    ROLLBACK TRANSACTION;
    PRINT 'PRUEBA 1 - MAL: el insert con transportista inexistente NO debía pasar.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT CONCAT('PRUEBA 1 - OK: rechazado como se esperaba. ', ERROR_MESSAGE());
END CATCH
GO

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @IdEnvio2 INT;
    INSERT INTO MED.Envio (IdCorrelacion, IdEnvioCourier, IdCourier, UbigeoDestino, PesoTotalKg, CantidadPaquetes, Estado)
    SELECT CONCAT('PRUEBA-2-', CONVERT(VARCHAR(36), NEWID())), 'ENV-TEST-2', IdCourier, '130101', 10.00, 1, 'CONFIRMADO'
    FROM MED.Courier WHERE Ruc = '20512345678';
    SET @IdEnvio2 = SCOPE_IDENTITY();

    INSERT INTO MED.PrecioVenta (IdEnvio, PrecioCobrado) VALUES (@IdEnvio2, 50.00);
    INSERT INTO MED.PrecioVenta (IdEnvio, PrecioCobrado) VALUES (@IdEnvio2, 55.00);

    ROLLBACK TRANSACTION;
    PRINT 'PRUEBA 2 - MAL: el segundo precio de venta NO debía insertarse.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT CONCAT('PRUEBA 2 - OK: rechazado como se esperaba. ', ERROR_MESSAGE());
END CATCH
GO

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @IdRutaExistente INT = (SELECT TOP 1 IdRuta FROM MED.Ruta ORDER BY IdRuta);

    INSERT INTO MED.TarifaProveedor (IdRuta, TipoCarga, PesoDesdeKg, PesoHastaKg, Precio, PrecioMinimo, VigenteDesde)
    VALUES (@IdRutaExistente, 'CARGA_LIVIANA', 10.00, 5.00, 20.00, 20.00, '2026-01-01');

    ROLLBACK TRANSACTION;
    PRINT 'PRUEBA 3 - MAL: el tramo con pesoHasta menor que pesoDesde NO debía insertarse.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT CONCAT('PRUEBA 3 - OK: rechazado como se esperaba. ', ERROR_MESSAGE());
END CATCH
GO

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @IdEnvio4 INT;
    INSERT INTO MED.Envio (IdCorrelacion, IdEnvioCourier, IdCourier, UbigeoDestino, PesoTotalKg, CantidadPaquetes, Estado)
    SELECT CONCAT('PRUEBA-4-', CONVERT(VARCHAR(36), NEWID())), 'ENV-TEST-4', IdCourier, '130101', 10.00, 1, 'COTIZADO'
    FROM MED.Courier WHERE Ruc = '20512345678';
    SET @IdEnvio4 = SCOPE_IDENTITY();

    INSERT INTO MED.EnvioPersona (IdEnvio, Rol, Nombres, Apellidos)
    VALUES (@IdEnvio4, 'TRANSPORTISTA', 'Nombre', 'Apellido');

    ROLLBACK TRANSACTION;
    PRINT 'PRUEBA 4 - MAL: la persona con rol inválido NO debía insertarse.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT CONCAT('PRUEBA 4 - OK: rechazado como se esperaba. ', ERROR_MESSAGE());
END CATCH
GO

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @IdEventoPrueba VARCHAR(80) = CONCAT('EVT-TEST-', CONVERT(VARCHAR(36), NEWID()));

    INSERT INTO MED.EventoRecibido (IdEvento, TipoEvento, FechaEvento)
    VALUES (@IdEventoPrueba, 'BUS_ASIGNADO', SYSDATETIMEOFFSET());

    INSERT INTO MED.EventoRecibido (IdEvento, TipoEvento, FechaEvento)
    VALUES (@IdEventoPrueba, 'BUS_ASIGNADO', SYSDATETIMEOFFSET());

    ROLLBACK TRANSACTION;
    PRINT 'PRUEBA 5 - MAL: el evento repetido NO debía insertarse dos veces.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT CONCAT('PRUEBA 5 - OK: rechazado como se esperaba. ', ERROR_MESSAGE());
END CATCH
GO

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO MED.Envio (IdCorrelacion, IdEnvioCourier, IdCourier, UbigeoDestino, PesoTotalKg, CantidadPaquetes, Estado)
    SELECT CONCAT('PRUEBA-6-', CONVERT(VARCHAR(36), NEWID())), 'ENV-TEST-6', IdCourier, '130101', 10.00, 1, 'COTIZADO'
    FROM MED.Courier WHERE Ruc = '20512345678';

    ROLLBACK TRANSACTION;
    PRINT 'PRUEBA 6 - OK: el envío sin código de seguimiento se insertó sin problema.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT CONCAT('PRUEBA 6 - MAL: no debía fallar. ', ERROR_MESSAGE());
END CATCH
GO

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @IdEnvio7 INT;
    INSERT INTO MED.Envio (IdCorrelacion, IdEnvioCourier, IdCourier, UbigeoDestino, PesoTotalKg, CantidadPaquetes, Estado)
    SELECT CONCAT('PRUEBA-7-', CONVERT(VARCHAR(36), NEWID())), 'ENV-TEST-7', IdCourier, '130101', 10.00, 1, 'COTIZADO'
    FROM MED.Courier WHERE Ruc = '20512345678';
    SET @IdEnvio7 = SCOPE_IDENTITY();

    INSERT INTO MED.EnvioEstado (IdEnvio, Estado, Origen) VALUES (@IdEnvio7, 'COTIZADO', 'MEDIADOR');
    INSERT INTO MED.EnvioEstado (IdEnvio, Estado, Origen) VALUES (@IdEnvio7, 'CONFIRMADO', 'MEDIADOR');
    INSERT INTO MED.EnvioEstado (IdEnvio, Estado, Origen) VALUES (@IdEnvio7, 'EN_VIAJE', 'EETT');

    IF (SELECT COUNT(*) FROM MED.EnvioEstado WHERE IdEnvio = @IdEnvio7) = 3
        PRINT 'PRUEBA 7 - OK: las tres filas de historial se insertaron sin problema.';
    ELSE
        PRINT 'PRUEBA 7 - MAL: no quedaron las tres filas esperadas.';

    ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT CONCAT('PRUEBA 7 - MAL: no debía fallar. ', ERROR_MESSAGE());
END CATCH
GO
