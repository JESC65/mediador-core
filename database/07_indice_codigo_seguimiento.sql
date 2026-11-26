USE MediadorDev;
GO

-- Reemplaza la restricción UNIQUE sin filtro sobre MED.Envio.CodigoSeguimiento por un índice único filtrado (WHERE IS NOT NULL) para permitir múltiples envíos cotizados sin confirmar.

DECLARE @ConstraintName NVARCHAR(200);

SELECT @ConstraintName = kc.name
FROM sys.key_constraints kc
INNER JOIN sys.index_columns ic
    ON kc.parent_object_id = ic.object_id AND kc.unique_index_id = ic.index_id
INNER JOIN sys.columns c
    ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE kc.parent_object_id = OBJECT_ID('MED.Envio')
  AND kc.[type] = 'UQ'
  AND c.name = 'CodigoSeguimiento';

IF @ConstraintName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE MED.Envio DROP CONSTRAINT [' + @ConstraintName + ']');
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UQ_Envio_CodigoSeguimiento'
      AND object_id = OBJECT_ID('MED.Envio')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_Envio_CodigoSeguimiento
        ON MED.Envio(CodigoSeguimiento)
        WHERE CodigoSeguimiento IS NOT NULL;
END
GO