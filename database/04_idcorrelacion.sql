USE MediadorDev;
GO

-- Bloque 3: Columnas para persistir idCorrelacion único y estable (sin fecha, R5)
-- hacia la transportista para las operaciones B3 (Precio) y B4 (Registro).
-- Idempotente: se puede ejecutar múltiples veces sin error.

IF COL_LENGTH('MED.Envio', 'IdCorrelacionPrecio') IS NULL
BEGIN
    ALTER TABLE MED.Envio
    ADD IdCorrelacionPrecio VARCHAR(80) NULL;
END
GO

IF COL_LENGTH('MED.Envio', 'IdCorrelacionRegistro') IS NULL
BEGIN
    ALTER TABLE MED.Envio
    ADD IdCorrelacionRegistro VARCHAR(80) NULL;
END
GO
