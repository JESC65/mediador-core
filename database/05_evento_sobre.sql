USE MediadorDev;
GO

-- Bloque 2: Adaptar MED.EventoRecibido al sobre C2 v2.0 §5 y regla R6.
-- Idempotente: se puede ejecutar múltiples veces sin fallar.

-- 1. Nuevas columnas en MED.EventoRecibido
IF COL_LENGTH('MED.EventoRecibido', 'Secuencia') IS NULL
BEGIN
    ALTER TABLE MED.EventoRecibido ADD Secuencia INT NULL;
END
GO

IF COL_LENGTH('MED.EventoRecibido', 'VersionContrato') IS NULL
BEGIN
    ALTER TABLE MED.EventoRecibido ADD VersionContrato VARCHAR(10) NULL;
END
GO

IF COL_LENGTH('MED.EventoRecibido', 'EntidadTipo') IS NULL
BEGIN
    ALTER TABLE MED.EventoRecibido ADD EntidadTipo VARCHAR(30) NULL;
END
GO

IF COL_LENGTH('MED.EventoRecibido', 'EntidadId') IS NULL
BEGIN
    ALTER TABLE MED.EventoRecibido ADD EntidadId VARCHAR(80) NULL;
END
GO

IF COL_LENGTH('MED.EventoRecibido', 'VersionEntidad') IS NULL
BEGIN
    ALTER TABLE MED.EventoRecibido ADD VersionEntidad INT NULL;
END
GO

IF COL_LENGTH('MED.EventoRecibido', 'Emisor') IS NULL
BEGIN
    ALTER TABLE MED.EventoRecibido ADD Emisor VARCHAR(50) NULL;
END
GO

IF COL_LENGTH('MED.EventoRecibido', 'HuellaCuerpo') IS NULL
BEGIN
    ALTER TABLE MED.EventoRecibido ADD HuellaCuerpo VARCHAR(256) NULL;
END
GO

IF COL_LENGTH('MED.EventoRecibido', 'RequiereMensajeRechazado') IS NULL
BEGIN
    ALTER TABLE MED.EventoRecibido ADD RequiereMensajeRechazado BIT NOT NULL CONSTRAINT DF_EventoRecibido_RequiereMensajeRechazado DEFAULT 0;
END
GO

IF COL_LENGTH('MED.EventoRecibido', 'MotivoRechazo') IS NULL
BEGIN
    ALTER TABLE MED.EventoRecibido ADD MotivoRechazo VARCHAR(100) NULL;
END
GO

IF COL_LENGTH('MED.EventoRecibido', 'EsConflicto') IS NULL
BEGIN
    ALTER TABLE MED.EventoRecibido ADD EsConflicto BIT NOT NULL CONSTRAINT DF_EventoRecibido_EsConflicto DEFAULT 0;
END
GO

-- 2. Eliminar restricción UNIQUE sobre IdEvento si existe, para permitir registrar intentos conflictivos
DECLARE @ConstraintName NVARCHAR(200);
SELECT @ConstraintName = name 
FROM sys.key_constraints 
WHERE parent_object_id = OBJECT_ID('MED.EventoRecibido') AND [type] = 'UQ';

IF @ConstraintName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE MED.EventoRecibido DROP CONSTRAINT ' + @ConstraintName);
END
GO

-- Índice no único sobre IdEvento para búsquedas eficientes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EventoRecibido_IdEvento' AND object_id = OBJECT_ID('MED.EventoRecibido'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_EventoRecibido_IdEvento ON MED.EventoRecibido (IdEvento);
END
GO

-- 3. Tabla para control de secuencias contiguas por transportista
IF OBJECT_ID('MED.TransportistaSecuencia') IS NULL
BEGIN
    CREATE TABLE MED.TransportistaSecuencia (
        IdTransportista INT NOT NULL CONSTRAINT PK_TransportistaSecuencia PRIMARY KEY,
        UltimaSecuenciaContigua INT NOT NULL DEFAULT 0,
        FechaActualizacion DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
    );
END
GO

-- 4. Tabla para registrar huecos de secuencias detectados
IF OBJECT_ID('MED.SecuenciaHueco') IS NULL
BEGIN
    CREATE TABLE MED.SecuenciaHueco (
        IdHueco INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SecuenciaHueco PRIMARY KEY,
        IdTransportista INT NOT NULL,
        SecuenciaEsperada INT NOT NULL,
        SecuenciaRecibida INT NOT NULL,
        FechaDeteccion DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        Resuelto BIT NOT NULL DEFAULT 0,
        FechaResolucion DATETIMEOFFSET NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SecuenciaHueco_Transportista' AND object_id = OBJECT_ID('MED.SecuenciaHueco'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_SecuenciaHueco_Transportista ON MED.SecuenciaHueco (IdTransportista, Resuelto);
END
GO
