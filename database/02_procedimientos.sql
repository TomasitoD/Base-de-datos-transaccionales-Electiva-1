USE AnalisisOpinionesClientes;

DROP PROCEDURE IF EXISTS sp_UpsertCliente;
DROP PROCEDURE IF EXISTS sp_UpsertProducto;
DROP PROCEDURE IF EXISTS sp_UpsertFuente;
DROP PROCEDURE IF EXISTS sp_BuscarFuente;
DROP PROCEDURE IF EXISTS sp_UpsertOpinion;
DROP PROCEDURE IF EXISTS sp_ObtenerConteos;

DELIMITER $$

CREATE PROCEDURE sp_UpsertCliente(
    IN pIdExterno VARCHAR(50),
    IN pNombre VARCHAR(120),
    IN pEmail VARCHAR(254),
    OUT pIdCliente INT,
    OUT pEstado VARCHAR(10))
BEGIN
    DECLARE vNombre VARCHAR(120);
    DECLARE vEmail VARCHAR(254);
    DECLARE vExiste TINYINT DEFAULT 1;
    DECLARE vDuplicado TINYINT DEFAULT 0;
    DECLARE CONTINUE HANDLER FOR NOT FOUND SET vExiste = 0;
    DECLARE CONTINUE HANDLER FOR 1062 SET vDuplicado = 1;

    SET pIdCliente = NULL;
    SELECT IdCliente, Nombre, Email INTO pIdCliente, vNombre, vEmail
    FROM Clientes WHERE IdExterno = pIdExterno;

    IF vExiste = 0 THEN
        INSERT INTO Clientes (IdExterno, Nombre, Email) VALUES (pIdExterno, pNombre, pEmail);
        IF vDuplicado = 0 THEN
            SET pIdCliente = LAST_INSERT_ID();
            SET pEstado = 'Insertado';
        ELSE
            SET vExiste = 1;
            SELECT IdCliente, Nombre, Email INTO pIdCliente, vNombre, vEmail
            FROM Clientes WHERE IdExterno = pIdExterno;
        END IF;
    END IF;

    IF vExiste = 1 THEN
        IF vNombre <=> pNombre AND vEmail <=> pEmail THEN
            SET pEstado = 'Omitido';
        ELSE
            SET pEstado = 'Conflicto';
        END IF;
    END IF;
END$$

CREATE PROCEDURE sp_UpsertProducto(
    IN pIdExterno VARCHAR(50),
    IN pNombre VARCHAR(150),
    IN pCategoria VARCHAR(100),
    OUT pIdProducto INT,
    OUT pEstado VARCHAR(10))
BEGIN
    DECLARE vNombre VARCHAR(150);
    DECLARE vCategoria VARCHAR(100);
    DECLARE vExiste TINYINT DEFAULT 1;
    DECLARE vDuplicado TINYINT DEFAULT 0;
    DECLARE CONTINUE HANDLER FOR NOT FOUND SET vExiste = 0;
    DECLARE CONTINUE HANDLER FOR 1062 SET vDuplicado = 1;

    SET pIdProducto = NULL;
    SELECT IdProducto, Nombre, Categoria INTO pIdProducto, vNombre, vCategoria
    FROM Productos WHERE IdExterno = pIdExterno;

    IF vExiste = 0 THEN
        INSERT INTO Productos (IdExterno, Nombre, Categoria) VALUES (pIdExterno, pNombre, pCategoria);
        IF vDuplicado = 0 THEN
            SET pIdProducto = LAST_INSERT_ID();
            SET pEstado = 'Insertado';
        ELSE
            SET vExiste = 1;
            SELECT IdProducto, Nombre, Categoria INTO pIdProducto, vNombre, vCategoria
            FROM Productos WHERE IdExterno = pIdExterno;
        END IF;
    END IF;

    IF vExiste = 1 THEN
        IF vNombre <=> pNombre AND vCategoria <=> pCategoria THEN
            SET pEstado = 'Omitido';
        ELSE
            SET pEstado = 'Conflicto';
        END IF;
    END IF;
END$$

CREATE PROCEDURE sp_UpsertFuente(
    IN pIdExterno VARCHAR(100),
    IN pTipoFuente VARCHAR(50),
    IN pFechaCarga DATETIME,
    OUT pIdFuente INT,
    OUT pEstado VARCHAR(10))
BEGIN
    DECLARE vTipoFuente VARCHAR(50);
    DECLARE vFechaCarga DATETIME;
    DECLARE vExiste TINYINT DEFAULT 1;
    DECLARE vDuplicado TINYINT DEFAULT 0;
    DECLARE CONTINUE HANDLER FOR NOT FOUND SET vExiste = 0;
    DECLARE CONTINUE HANDLER FOR 1062 SET vDuplicado = 1;

    SET pIdFuente = NULL;
    SELECT IdFuente, TipoFuente, FechaCarga INTO pIdFuente, vTipoFuente, vFechaCarga
    FROM FuenteDatos WHERE IdExterno = pIdExterno;

    IF vExiste = 0 THEN
        INSERT INTO FuenteDatos (IdExterno, TipoFuente, FechaCarga) VALUES (pIdExterno, pTipoFuente, pFechaCarga);
        IF vDuplicado = 0 THEN
            SET pIdFuente = LAST_INSERT_ID();
            SET pEstado = 'Insertado';
        ELSE
            SET vExiste = 1;
            SELECT IdFuente, TipoFuente, FechaCarga INTO pIdFuente, vTipoFuente, vFechaCarga
            FROM FuenteDatos WHERE IdExterno = pIdExterno;
        END IF;
    END IF;

    IF vExiste = 1 THEN
        IF vTipoFuente <=> pTipoFuente AND vFechaCarga <=> pFechaCarga THEN
            SET pEstado = 'Omitido';
        ELSE
            SET pEstado = 'Conflicto';
        END IF;
    END IF;
END$$

CREATE PROCEDURE sp_BuscarFuente(IN pIdExterno VARCHAR(100))
BEGIN
    SELECT IdFuente, TipoFuente, FechaCarga FROM FuenteDatos WHERE IdExterno = pIdExterno;
END$$

CREATE PROCEDURE sp_UpsertOpinion(
    IN pCanal VARCHAR(20),
    IN pIdExterno VARCHAR(50),
    IN pIdCliente INT,
    IN pIdProducto INT,
    IN pIdFuente INT,
    IN pFecha DATETIME,
    IN pComentario MEDIUMTEXT,
    IN pClasificacion VARCHAR(10),
    IN pPuntaje TINYINT UNSIGNED,
    IN pIdClienteOrigen VARCHAR(50),
    IN pIdProductoOrigen VARCHAR(50),
    OUT pIdOpinion BIGINT,
    OUT pEstado VARCHAR(10))
BEGIN
    DECLARE vTipoFuente VARCHAR(50);
    DECLARE vTipoEsperado VARCHAR(50);
    DECLARE vFuenteEncontrada TINYINT DEFAULT 1;
    DECLARE vIdCliente INT;
    DECLARE vIdProducto INT;
    DECLARE vIdFuente INT;
    DECLARE vFecha DATETIME;
    DECLARE vComentario MEDIUMTEXT;
    DECLARE vClasificacion VARCHAR(10);
    DECLARE vPuntaje TINYINT UNSIGNED;
    DECLARE vIdClienteOrigen VARCHAR(50);
    DECLARE vIdProductoOrigen VARCHAR(50);
    DECLARE vExiste TINYINT DEFAULT 1;
    DECLARE vDuplicado TINYINT DEFAULT 0;
    DECLARE CONTINUE HANDLER FOR NOT FOUND SET vExiste = 0;
    DECLARE CONTINUE HANDLER FOR 1062 SET vDuplicado = 1;

    SET pIdOpinion = NULL;

    SELECT TipoFuente INTO vTipoFuente FROM FuenteDatos WHERE IdFuente = pIdFuente;
    IF vExiste = 0 THEN
        SET vFuenteEncontrada = 0;
        SET vExiste = 1;
    END IF;

    SET vTipoEsperado = CASE pCanal
        WHEN 'Web' THEN 'Web'
        WHEN 'Encuesta' THEN 'CSV'
        WHEN 'RedSocial' THEN 'Red Social'
        ELSE NULL
    END;

    IF vFuenteEncontrada = 1 AND vTipoEsperado IS NOT NULL AND NOT (vTipoFuente <=> vTipoEsperado) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'El TipoFuente de la fuente no es compatible con el canal de la opinion';
    END IF;

    SELECT IdOpinion, IdCliente, IdProducto, IdFuente, Fecha, Comentario, Clasificacion,
           PuntajeSatisfaccion, IdClienteOrigen, IdProductoOrigen
    INTO pIdOpinion, vIdCliente, vIdProducto, vIdFuente, vFecha, vComentario, vClasificacion,
         vPuntaje, vIdClienteOrigen, vIdProductoOrigen
    FROM Opiniones WHERE Canal = pCanal AND IdExterno = pIdExterno;

    IF vExiste = 0 THEN
        INSERT INTO Opiniones (Canal, IdExterno, IdCliente, IdProducto, IdFuente, Fecha, Comentario,
                               Clasificacion, PuntajeSatisfaccion, IdClienteOrigen, IdProductoOrigen)
        VALUES (pCanal, pIdExterno, pIdCliente, pIdProducto, pIdFuente, pFecha, pComentario,
                pClasificacion, pPuntaje, pIdClienteOrigen, pIdProductoOrigen);
        IF vDuplicado = 0 THEN
            SET pIdOpinion = LAST_INSERT_ID();
            SET pEstado = 'Insertado';
        ELSE
            SET vExiste = 1;
            SELECT IdOpinion, IdCliente, IdProducto, IdFuente, Fecha, Comentario, Clasificacion,
                   PuntajeSatisfaccion, IdClienteOrigen, IdProductoOrigen
            INTO pIdOpinion, vIdCliente, vIdProducto, vIdFuente, vFecha, vComentario, vClasificacion,
                 vPuntaje, vIdClienteOrigen, vIdProductoOrigen
            FROM Opiniones WHERE Canal = pCanal AND IdExterno = pIdExterno;
        END IF;
    END IF;

    IF vExiste = 1 THEN
        IF vIdCliente <=> pIdCliente AND vIdProducto <=> pIdProducto AND vIdFuente <=> pIdFuente
           AND vFecha <=> pFecha AND vComentario <=> pComentario AND vClasificacion <=> pClasificacion
           AND vPuntaje <=> pPuntaje AND vIdClienteOrigen <=> pIdClienteOrigen
           AND vIdProductoOrigen <=> pIdProductoOrigen THEN
            SET pEstado = 'Omitido';
        ELSE
            SET pEstado = 'Conflicto';
        END IF;
    END IF;
END$$

CREATE PROCEDURE sp_ObtenerConteos()
BEGIN
    SELECT 'Clientes' AS Tabla, COUNT(*) AS Total FROM Clientes
    UNION ALL SELECT 'Productos', COUNT(*) FROM Productos
    UNION ALL SELECT 'FuenteDatos', COUNT(*) FROM FuenteDatos
    UNION ALL SELECT 'Opiniones', COUNT(*) FROM Opiniones;
END$$

DELIMITER ;
