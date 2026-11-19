USE MediadorDev;
GO

-- Bloque 3: Puntos de recepción (B6) según C2 v2.0 §6
-- Idempotente: se puede ejecutar múltiples veces sin error.

IF OBJECT_ID('MED.PuntoRecepcion', 'U') IS NULL
BEGIN
    CREATE TABLE MED.PuntoRecepcion (
        IdPunto INT IDENTITY(1,1) NOT NULL,
        IdTransportista INT NOT NULL,
        CodigoExterno VARCHAR(50) NOT NULL,
        Nombre VARCHAR(200) NOT NULL,
        Direccion VARCHAR(300) NOT NULL,
        Ubigeo VARCHAR(6) NOT NULL,
        Latitud DECIMAL(9,6) NULL,
        Longitud DECIMAL(9,6) NULL,
        Horario VARCHAR(200) NULL,
        TiempoServicioMinutos INT NULL,
        Version INT NOT NULL CONSTRAINT DF_PuntoRecepcion_Version DEFAULT 1,
        Activo BIT NOT NULL CONSTRAINT DF_PuntoRecepcion_Activo DEFAULT 1,
        FechaActualizacion DATETIMEOFFSET NOT NULL CONSTRAINT DF_PuntoRecepcion_FechaActualizacion DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_PuntoRecepcion PRIMARY KEY (IdPunto),
        CONSTRAINT FK_PuntoRecepcion_Transportista FOREIGN KEY (IdTransportista) REFERENCES MED.Transportista(IdTransportista),
        CONSTRAINT UQ_PuntoRecepcion UNIQUE (IdTransportista, CodigoExterno)
    );
END
GO
