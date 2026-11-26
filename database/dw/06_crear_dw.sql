CREATE DATABASE IF NOT EXISTS AnalisisOpinionesDW
    DEFAULT CHARACTER SET utf8mb4
    DEFAULT COLLATE utf8mb4_0900_as_cs;

USE AnalisisOpinionesDW;

CREATE TABLE IF NOT EXISTS DimProducto (
    SkProducto INT NOT NULL AUTO_INCREMENT,
    IdProductoOrigen INT NOT NULL,
    CodigoExterno VARCHAR(50) NOT NULL,
    Nombre VARCHAR(150) NOT NULL,
    Categoria VARCHAR(100) NOT NULL,
    CONSTRAINT pk_dimproducto PRIMARY KEY (SkProducto),
    CONSTRAINT uq_dimproducto_origen UNIQUE (IdProductoOrigen)
);

CREATE TABLE IF NOT EXISTS DimCliente (
    SkCliente INT NOT NULL AUTO_INCREMENT,
    IdClienteOrigen INT NULL,
    CodigoExterno VARCHAR(50) NOT NULL,
    Nombre VARCHAR(120) NOT NULL,
    CONSTRAINT pk_dimcliente PRIMARY KEY (SkCliente),
    CONSTRAINT uq_dimcliente_origen UNIQUE (IdClienteOrigen),
    CONSTRAINT uq_dimcliente_codigo UNIQUE (CodigoExterno)
);

CREATE TABLE IF NOT EXISTS DimFecha (
    SkFecha INT NOT NULL,
    Fecha DATE NOT NULL,
    Anio SMALLINT NOT NULL,
    Trimestre TINYINT NOT NULL,
    Mes TINYINT NOT NULL,
    NombreMes VARCHAR(12) NOT NULL,
    Dia TINYINT NOT NULL,
    CONSTRAINT pk_dimfecha PRIMARY KEY (SkFecha),
    CONSTRAINT uq_dimfecha_fecha UNIQUE (Fecha)
);

CREATE TABLE IF NOT EXISTS DimFuente (
    SkFuente INT NOT NULL AUTO_INCREMENT,
    IdFuenteOrigen INT NOT NULL,
    CodigoExterno VARCHAR(100) NOT NULL,
    TipoFuente VARCHAR(50) NOT NULL,
    FechaCarga DATETIME NOT NULL,
    CONSTRAINT pk_dimfuente PRIMARY KEY (SkFuente),
    CONSTRAINT uq_dimfuente_origen UNIQUE (IdFuenteOrigen)
);

CREATE TABLE IF NOT EXISTS DimClasificacion (
    SkClasificacion TINYINT NOT NULL,
    Nombre VARCHAR(20) NOT NULL,
    CONSTRAINT pk_dimclasificacion PRIMARY KEY (SkClasificacion),
    CONSTRAINT uq_dimclasificacion_nombre UNIQUE (Nombre)
);

SELECT TABLE_NAME AS Dimension
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'AnalisisOpinionesDW'
ORDER BY TABLE_NAME;
