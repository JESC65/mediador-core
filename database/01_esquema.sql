CREATE DATABASE MediadorDev;
GO
USE MediadorDev;
GO
CREATE SCHEMA MED;
GO

CREATE TABLE MED.Courier (
    IdCourier       INT IDENTITY PRIMARY KEY,
    Ruc             VARCHAR(11)  NOT NULL UNIQUE,
    RazonSocial     VARCHAR(200) NOT NULL,
    NombreComercial VARCHAR(200) NULL,
    Activo          BIT NOT NULL DEFAULT 1,
    FechaRegistro   DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);
GO

CREATE TABLE MED.Transportista (
    IdTransportista INT IDENTITY PRIMARY KEY,
    Ruc             VARCHAR(11)  NOT NULL UNIQUE,
    RazonSocial     VARCHAR(200) NOT NULL,
    NombreComercial VARCHAR(200) NULL,
    UrlConector     VARCHAR(300) NULL,
    Telefono        VARCHAR(30)  NULL,
    Activo          BIT NOT NULL DEFAULT 1,
    FechaRegistro   DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);
GO

CREATE TABLE MED.Convenio (
    IdConvenio      INT IDENTITY PRIMARY KEY,
    IdCourier       INT NOT NULL,
    IdTransportista INT NOT NULL,
    FechaInicio     DATE NOT NULL,
    FechaFin        DATE NULL,
    Observacion     VARCHAR(500) NULL,
    Activo          BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_Convenio_Courier       FOREIGN KEY (IdCourier)       REFERENCES MED.Courier(IdCourier),
    CONSTRAINT FK_Convenio_Transportista FOREIGN KEY (IdTransportista) REFERENCES MED.Transportista(IdTransportista),
    CONSTRAINT UQ_Convenio UNIQUE (IdCourier, IdTransportista, FechaInicio)
);
GO

CREATE TABLE MED.Ruta (
    IdRuta            INT IDENTITY PRIMARY KEY,
    IdTransportista   INT NOT NULL,
    CodigoRutaExterno VARCHAR(50) NOT NULL,
    UbigeoOrigen      VARCHAR(6)  NOT NULL,
    UbigeoDestino     VARCHAR(6)  NOT NULL,
    NombreOrigen      VARCHAR(100) NULL,
    NombreDestino     VARCHAR(100) NULL,
    FrecuenciaSalidas VARCHAR(100) NULL,
    HoraSalidaHabitual VARCHAR(10) NULL,
    AceptaFragil      BIT NOT NULL DEFAULT 0,
    PesoMaximoKg      DECIMAL(8,2) NULL,
    Activa            BIT NOT NULL DEFAULT 1,
    FechaActualizacion DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT FK_Ruta_Transportista FOREIGN KEY (IdTransportista) REFERENCES MED.Transportista(IdTransportista),
    CONSTRAINT UQ_Ruta UNIQUE (IdTransportista, CodigoRutaExterno)
);
GO
CREATE INDEX IX_Ruta_Ubigeos ON MED.Ruta (UbigeoOrigen, UbigeoDestino, Activa);
GO

CREATE TABLE MED.TarifaProveedor (
    IdTarifaProveedor INT IDENTITY PRIMARY KEY,
    IdRuta            INT NOT NULL,
    TipoCarga         VARCHAR(30)  NOT NULL,
    PesoDesdeKg       DECIMAL(8,2) NOT NULL,
    PesoHastaKg       DECIMAL(8,2) NOT NULL,
    Precio            DECIMAL(10,2) NOT NULL,
    PrecioMinimo      DECIMAL(10,2) NULL,
    VigenteDesde      DATE NOT NULL,
    VigenteHasta      DATE NULL,
    Activo            BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_Tarifa_Ruta FOREIGN KEY (IdRuta) REFERENCES MED.Ruta(IdRuta),
    CONSTRAINT CK_Tarifa_Tramo CHECK (PesoHastaKg >= PesoDesdeKg)
);
GO
CREATE INDEX IX_Tarifa_Busqueda ON MED.TarifaProveedor (IdRuta, TipoCarga, PesoDesdeKg, PesoHastaKg, Activo);
GO

CREATE TABLE MED.Envio (
    IdEnvio               INT IDENTITY PRIMARY KEY,
    CodigoSeguimiento     VARCHAR(50) NULL UNIQUE,
    IdCorrelacion         VARCHAR(80) NOT NULL,
    IdEnvioCourier        VARCHAR(50) NOT NULL,
    IdCourier             INT NOT NULL,
    UbigeoOrigen          VARCHAR(6)  NULL,
    UbigeoDestino         VARCHAR(6)  NOT NULL,
    TipoCarga             VARCHAR(30) NULL,
    PesoTotalKg           DECIMAL(8,2) NOT NULL,
    CantidadPaquetes      INT NOT NULL,
    Estado                VARCHAR(40) NOT NULL,
    IdTransportistaElegida INT NULL,
    IdCotizacionElegida   INT NULL,
    FechaRegistro         DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    FechaConfirmacion     DATETIMEOFFSET NULL,
    Observacion           VARCHAR(500) NULL,
    CONSTRAINT FK_Envio_Courier FOREIGN KEY (IdCourier) REFERENCES MED.Courier(IdCourier),
    CONSTRAINT UQ_Envio_Correlacion UNIQUE (IdCorrelacion)
);
GO
CREATE INDEX IX_Envio_CourierOrigen ON MED.Envio (IdCourier, IdEnvioCourier);
GO

CREATE TABLE MED.EnvioPaquete (
    IdEnvioPaquete INT IDENTITY PRIMARY KEY,
    IdEnvio        INT NOT NULL,
    Descripcion    VARCHAR(300) NULL,
    PesoKg         DECIMAL(8,2) NOT NULL,
    Cantidad       INT NOT NULL DEFAULT 1,
    LargoCm        DECIMAL(8,2) NULL,
    AnchoCm        DECIMAL(8,2) NULL,
    AltoCm         DECIMAL(8,2) NULL,
    EsFragil       BIT NULL,
    ValorDeclarado DECIMAL(10,2) NULL,
    CONSTRAINT FK_Paquete_Envio FOREIGN KEY (IdEnvio) REFERENCES MED.Envio(IdEnvio)
);
GO

CREATE TABLE MED.EnvioPersona (
    IdEnvioPersona  INT IDENTITY PRIMARY KEY,
    IdEnvio         INT NOT NULL,
    Rol             VARCHAR(15) NOT NULL,
    TipoDocumento   VARCHAR(20) NULL,
    NumeroDocumento VARCHAR(20) NULL,
    Nombres         VARCHAR(200) NULL,
    Apellidos       VARCHAR(200) NULL,
    Telefono        VARCHAR(30)  NULL,
    Direccion       VARCHAR(300) NULL,
    Ubigeo          VARCHAR(6)   NULL,
    CONSTRAINT FK_Persona_Envio FOREIGN KEY (IdEnvio) REFERENCES MED.Envio(IdEnvio),
    CONSTRAINT CK_Persona_Rol CHECK (Rol IN ('REMITENTE','CONSIGNADO')),
    CONSTRAINT UQ_Persona_Rol UNIQUE (IdEnvio, Rol)
);
GO

CREATE TABLE MED.Cotizacion (
    IdCotizacion      INT IDENTITY PRIMARY KEY,
    IdEnvio           INT NOT NULL,
    IdTransportista   INT NOT NULL,
    IdTarifaProveedor INT NULL,
    IdOpcion          VARCHAR(30) NOT NULL,
    PrecioProveedor   DECIMAL(10,2) NULL,
    PrecioEsEstimado  BIT NOT NULL DEFAULT 1,
    FrecuenciaSalidas VARCHAR(100) NULL,
    Elegida           BIT NOT NULL DEFAULT 0,
    Descartada        BIT NOT NULL DEFAULT 0,
    MotivoDescarte    VARCHAR(50) NULL,
    FechaCotizacion   DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    VigenteHasta      DATETIMEOFFSET NOT NULL,
    CONSTRAINT FK_Cotizacion_Envio         FOREIGN KEY (IdEnvio)           REFERENCES MED.Envio(IdEnvio),
    CONSTRAINT FK_Cotizacion_Transportista FOREIGN KEY (IdTransportista)   REFERENCES MED.Transportista(IdTransportista),
    CONSTRAINT FK_Cotizacion_Tarifa        FOREIGN KEY (IdTarifaProveedor) REFERENCES MED.TarifaProveedor(IdTarifaProveedor),
    CONSTRAINT UQ_Cotizacion_Opcion UNIQUE (IdOpcion)
);
GO

CREATE TABLE MED.PrecioVenta (
    IdPrecioVenta  INT IDENTITY PRIMARY KEY,
    IdEnvio        INT NOT NULL,
    PrecioCobrado  DECIMAL(10,2) NOT NULL,
    Moneda         VARCHAR(3) NOT NULL DEFAULT 'PEN',
    ModalidadPago  VARCHAR(30) NULL,
    FechaRegistro  DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT FK_PrecioVenta_Envio FOREIGN KEY (IdEnvio) REFERENCES MED.Envio(IdEnvio),
    CONSTRAINT UQ_PrecioVenta_Envio UNIQUE (IdEnvio)
);
GO

CREATE TABLE MED.EnvioEstado (
    IdEnvioEstado INT IDENTITY PRIMARY KEY,
    IdEnvio       INT NOT NULL,
    Estado        VARCHAR(40) NOT NULL,
    FechaEstado   DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    Origen        VARCHAR(20) NOT NULL,
    Comentario    VARCHAR(500) NULL,
    Usuario       VARCHAR(100) NULL,
    CONSTRAINT FK_Estado_Envio FOREIGN KEY (IdEnvio) REFERENCES MED.Envio(IdEnvio),
    CONSTRAINT CK_Estado_Origen CHECK (Origen IN ('COURIER','EETT','MEDIADOR'))
);
GO
CREATE INDEX IX_Estado_Envio ON MED.EnvioEstado (IdEnvio, FechaEstado);
GO

CREATE TABLE MED.EventoRecibido (
    IdEventoRecibido INT IDENTITY PRIMARY KEY,
    IdEvento         VARCHAR(80) NOT NULL UNIQUE,
    TipoEvento       VARCHAR(40) NOT NULL,
    IdTransportista  INT NULL,
    IdEnvio          INT NULL,
    FechaEvento      DATETIMEOFFSET NOT NULL,
    FechaRecepcion   DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    Procesado        BIT NOT NULL DEFAULT 0,
    Contenido        NVARCHAR(MAX) NULL
);
GO

CREATE TABLE MED.EnvioDocumento (
    IdEnvioDocumento INT IDENTITY PRIMARY KEY,
    IdEnvio       INT NOT NULL,
    TipoDocumento VARCHAR(50) NOT NULL,
    Serie         VARCHAR(10) NULL,
    Numero        VARCHAR(20) NULL,
    FechaEmision  DATE NULL,
    CONSTRAINT FK_Documento_Envio FOREIGN KEY (IdEnvio) REFERENCES MED.Envio(IdEnvio)
);
GO

SELECT COUNT(*) AS TotalTablas
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = 'MED';

SELECT COUNT(*) AS TotalClavesForaneas
FROM sys.foreign_keys;
