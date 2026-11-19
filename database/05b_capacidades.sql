USE MediadorDev;
GO

-- Bloque 2: Capacidades (B0) según C2 v2.0 §3 y Regla R8
-- Idempotente: se puede ejecutar múltiples veces sin error.

IF OBJECT_ID('MED.TransportistaCapacidad', 'U') IS NULL
BEGIN
    CREATE TABLE MED.TransportistaCapacidad (
        IdTransportista INT NOT NULL,
        Familia VARCHAR(100) NULL,
        VersionConector VARCHAR(30) NULL,
        VersionContrato VARCHAR(10) NULL,
        PrecioEnLinea BIT NOT NULL CONSTRAINT DF_TransportistaCapacidad_PrecioEnLinea DEFAULT 1,
        RegistroEnLinea BIT NOT NULL CONSTRAINT DF_TransportistaCapacidad_RegistroEnLinea DEFAULT 1,
        Anulacion BIT NOT NULL CONSTRAINT DF_TransportistaCapacidad_Anulacion DEFAULT 1,
        Puntos BIT NOT NULL CONSTRAINT DF_TransportistaCapacidad_Puntos DEFAULT 1,
        RecepcionPorBulto BIT NOT NULL CONSTRAINT DF_TransportistaCapacidad_RecepcionPorBulto DEFAULT 0,
        EventosJson NVARCHAR(MAX) NULL,
        FechaActualizacion DATETIMEOFFSET NOT NULL CONSTRAINT DF_TransportistaCapacidad_FechaActualizacion DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_TransportistaCapacidad PRIMARY KEY (IdTransportista),
        CONSTRAINT FK_TransportistaCapacidad_Transportista FOREIGN KEY (IdTransportista) REFERENCES MED.Transportista(IdTransportista)
    );
END
GO
