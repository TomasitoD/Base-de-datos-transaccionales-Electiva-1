CREATE DATABASE IF NOT EXISTS AnalisisOpinionesClientes
    DEFAULT CHARACTER SET utf8mb4
    DEFAULT COLLATE utf8mb4_0900_as_cs;

USE AnalisisOpinionesClientes;

CREATE TABLE IF NOT EXISTS Clientes (
    IdCliente INT NOT NULL AUTO_INCREMENT,
    IdExterno VARCHAR(50) NOT NULL,
    Nombre VARCHAR(120) NOT NULL,
    Email VARCHAR(254) NULL,
    CONSTRAINT pk_clientes PRIMARY KEY (IdCliente),
    CONSTRAINT uq_clientes_idexterno UNIQUE (IdExterno),
    CONSTRAINT ck_clientes_idexterno CHECK (CHAR_LENGTH(TRIM(IdExterno)) > 0),
    CONSTRAINT ck_clientes_nombre CHECK (CHAR_LENGTH(TRIM(Nombre)) > 0)
);

CREATE TABLE IF NOT EXISTS Productos (
    IdProducto INT NOT NULL AUTO_INCREMENT,
    IdExterno VARCHAR(50) NOT NULL,
    Nombre VARCHAR(150) NOT NULL,
    Categoria VARCHAR(100) NOT NULL,
    CONSTRAINT pk_productos PRIMARY KEY (IdProducto),
    CONSTRAINT uq_productos_idexterno UNIQUE (IdExterno),
    CONSTRAINT ck_productos_idexterno CHECK (CHAR_LENGTH(TRIM(IdExterno)) > 0),
    CONSTRAINT ck_productos_nombre CHECK (CHAR_LENGTH(TRIM(Nombre)) > 0),
    CONSTRAINT ck_productos_categoria CHECK (CHAR_LENGTH(TRIM(Categoria)) > 0)
);

CREATE TABLE IF NOT EXISTS FuenteDatos (
    IdFuente INT NOT NULL AUTO_INCREMENT,
    IdExterno VARCHAR(100) NOT NULL,
    TipoFuente VARCHAR(50) NOT NULL,
    FechaCarga DATETIME NOT NULL,
    CONSTRAINT pk_fuentedatos PRIMARY KEY (IdFuente),
    CONSTRAINT uq_fuentedatos_idexterno UNIQUE (IdExterno),
    CONSTRAINT ck_fuentedatos_idexterno CHECK (CHAR_LENGTH(TRIM(IdExterno)) > 0),
    CONSTRAINT ck_fuentedatos_tipo CHECK (CHAR_LENGTH(TRIM(TipoFuente)) > 0)
);

CREATE TABLE IF NOT EXISTS Opiniones (
    IdOpinion BIGINT NOT NULL AUTO_INCREMENT,
    Canal VARCHAR(20) NOT NULL,
    IdExterno VARCHAR(50) NOT NULL,
    IdCliente INT NULL,
    IdProducto INT NOT NULL,
    IdFuente INT NOT NULL,
    Fecha DATETIME NOT NULL,
    Comentario MEDIUMTEXT NOT NULL,
    Clasificacion VARCHAR(10) NULL,
    PuntajeSatisfaccion TINYINT UNSIGNED NULL,
    IdClienteOrigen VARCHAR(50) NULL,
    IdProductoOrigen VARCHAR(50) NOT NULL,
    CONSTRAINT pk_opiniones PRIMARY KEY (IdOpinion),
    CONSTRAINT uq_opiniones_canal_idexterno UNIQUE (Canal, IdExterno),
    CONSTRAINT fk_opiniones_cliente FOREIGN KEY (IdCliente) REFERENCES Clientes (IdCliente),
    CONSTRAINT fk_opiniones_producto FOREIGN KEY (IdProducto) REFERENCES Productos (IdProducto),
    CONSTRAINT fk_opiniones_fuente FOREIGN KEY (IdFuente) REFERENCES FuenteDatos (IdFuente),
    CONSTRAINT ck_opiniones_canal CHECK (Canal IN ('Web', 'Encuesta', 'RedSocial')),
    CONSTRAINT ck_opiniones_idexterno CHECK (CHAR_LENGTH(TRIM(IdExterno)) > 0),
    CONSTRAINT ck_opiniones_comentario CHECK (CHAR_LENGTH(TRIM(Comentario)) > 0),
    CONSTRAINT ck_opiniones_productoorigen CHECK (CHAR_LENGTH(TRIM(IdProductoOrigen)) > 0),
    CONSTRAINT ck_opiniones_clasificacion CHECK (Clasificacion IS NULL OR Clasificacion IN ('Positiva', 'Neutra', 'Negativa')),
    CONSTRAINT ck_opiniones_puntaje CHECK (PuntajeSatisfaccion IS NULL OR PuntajeSatisfaccion BETWEEN 1 AND 5)
);
