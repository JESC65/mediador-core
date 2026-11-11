USE MediadorDev;
GO

IF OBJECT_ID('MED.Correlativo') IS NULL
CREATE TABLE MED.Correlativo (
    Anio   smallint NOT NULL CONSTRAINT PK_Correlativo PRIMARY KEY,
    Ultimo int      NOT NULL
);
GO

-- Siembra 2026 con el mayor número ya usado (MED-2026-000123 -> 123)
IF NOT EXISTS (SELECT 1 FROM MED.Correlativo WHERE Anio = 2026)
INSERT MED.Correlativo (Anio, Ultimo)
SELECT 2026, ISNULL(MAX(CAST(SUBSTRING(CodigoSeguimiento, 10, 6) AS int)), 0)
FROM MED.Envio
WHERE CodigoSeguimiento LIKE 'MED-2026-%';
GO

CREATE OR ALTER PROCEDURE MED.SiguienteCorrelativo
    @Anio smallint, @Numero int OUTPUT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRAN;
    UPDATE MED.Correlativo WITH (UPDLOCK, HOLDLOCK)
    SET @Numero = Ultimo = Ultimo + 1
    WHERE Anio = @Anio;
    IF @@ROWCOUNT = 0
    BEGIN
        INSERT MED.Correlativo (Anio, Ultimo) VALUES (@Anio, 1);
        SET @Numero = 1;
    END
    COMMIT;
END
GO

