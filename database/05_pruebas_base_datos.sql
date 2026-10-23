USE AnalisisOpinionesClientes;

DROP PROCEDURE IF EXISTS sp_EjecutarPruebasTemporal;
DROP TEMPORARY TABLE IF EXISTS ResultadosPruebas;
CREATE TEMPORARY TABLE ResultadosPruebas (
    Orden INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    Prueba VARCHAR(120) NOT NULL,
    Resultado VARCHAR(4) NOT NULL
) ENGINE = MEMORY;

DELIMITER $$

CREATE PROCEDURE sp_EjecutarPruebasTemporal()
BEGIN
    DECLARE vIdCliente INT;
    DECLARE vIdProducto INT;
    DECLARE vIdFuenteWeb INT;
    DECLARE vIdOpinion BIGINT;
    DECLARE vEstado VARCHAR(10);
    DECLARE vFalloEsperado TINYINT;
    DECLARE vPrevios INT;

    SELECT (SELECT COUNT(*) FROM Clientes) + (SELECT COUNT(*) FROM Productos)
         + (SELECT COUNT(*) FROM FuenteDatos) + (SELECT COUNT(*) FROM Opiniones)
    INTO vPrevios;

    START TRANSACTION;

    CALL sp_UpsertCliente('PRUEBA-C1', 'Cliente de prueba', NULL, vIdCliente, vEstado);
    CALL sp_UpsertProducto('PRUEBA-P1', 'Producto de prueba', 'Categoria de prueba', vIdProducto, vEstado);
    CALL sp_UpsertFuente('PRUEBA-F1', 'Web', '2026-01-01 00:00:00', vIdFuenteWeb, vEstado);

    SET vFalloEsperado = 0;
    BEGIN
        DECLARE CONTINUE HANDLER FOR 1452 SET vFalloEsperado = 1;
        INSERT INTO Opiniones (Canal, IdExterno, IdProducto, IdFuente, Fecha, Comentario, IdProductoOrigen)
        VALUES ('Web', 'PRUEBA-FK', 999999999, vIdFuenteWeb, '2026-01-01', 'Comentario', 'P1');
    END;
    INSERT INTO ResultadosPruebas (Prueba, Resultado)
    VALUES ('FK rota rechazada', IF(vFalloEsperado = 1, 'PASS', 'FAIL'));

    SET vFalloEsperado = 0;
    BEGIN
        DECLARE CONTINUE HANDLER FOR 3819 SET vFalloEsperado = 1;
        INSERT INTO Opiniones (Canal, IdExterno, IdProducto, IdFuente, Fecha, Comentario, PuntajeSatisfaccion, IdProductoOrigen)
        VALUES ('Web', 'PRUEBA-CHECK', vIdProducto, vIdFuenteWeb, '2026-01-01', 'Comentario', 6, 'P1');
    END;
    INSERT INTO ResultadosPruebas (Prueba, Resultado)
    VALUES ('CHECK de puntaje 6 rechazado', IF(vFalloEsperado = 1, 'PASS', 'FAIL'));

    INSERT INTO Opiniones (Canal, IdExterno, IdProducto, IdFuente, Fecha, Comentario, IdProductoOrigen)
    VALUES ('Web', 'PRUEBA-UNICA', vIdProducto, vIdFuenteWeb, '2026-01-01', 'Comentario', 'P1');
    SET vFalloEsperado = 0;
    BEGIN
        DECLARE CONTINUE HANDLER FOR 1062 SET vFalloEsperado = 1;
        INSERT INTO Opiniones (Canal, IdExterno, IdProducto, IdFuente, Fecha, Comentario, IdProductoOrigen)
        VALUES ('Web', 'PRUEBA-UNICA', vIdProducto, vIdFuenteWeb, '2026-01-01', 'Otro comentario', 'P1');
    END;
    INSERT INTO ResultadosPruebas (Prueba, Resultado)
    VALUES ('UNIQUE Canal+IdExterno rechazado', IF(vFalloEsperado = 1, 'PASS', 'FAIL'));

    SET vFalloEsperado = 0;
    BEGIN
        DECLARE CONTINUE HANDLER FOR SQLSTATE '45000' SET vFalloEsperado = 1;
        CALL sp_UpsertOpinion('Encuesta', 'PRUEBA-TIPO', vIdCliente, vIdProducto, vIdFuenteWeb,
                              '2026-01-01', 'Comentario', 'Positiva', 4, 'C1', 'P1', vIdOpinion, vEstado);
    END;
    INSERT INTO ResultadosPruebas (Prueba, Resultado)
    VALUES ('Canal incompatible con TipoFuente lanza error', IF(vFalloEsperado = 1, 'PASS', 'FAIL'));

    CALL sp_UpsertCliente('PRUEBA-C1', 'Cliente de prueba', NULL, vIdCliente, vEstado);
    INSERT INTO ResultadosPruebas (Prueba, Resultado)
    VALUES ('SP repetido devuelve Omitido', IF(vEstado = 'Omitido', 'PASS', 'FAIL'));

    CALL sp_UpsertCliente('PRUEBA-C1', 'Nombre distinto', NULL, vIdCliente, vEstado);
    INSERT INTO ResultadosPruebas (Prueba, Resultado)
    VALUES ('SP con contenido distinto devuelve Conflicto', IF(vEstado = 'Conflicto', 'PASS', 'FAIL'));

    CALL sp_UpsertCliente('PRUEBA-C2', 'Otro cliente', NULL, vIdCliente, vEstado);
    INSERT INTO ResultadosPruebas (Prueba, Resultado)
    VALUES ('SP nuevo devuelve Insertado', IF(vEstado = 'Insertado', 'PASS', 'FAIL'));

    ROLLBACK;

    INSERT INTO ResultadosPruebas (Prueba, Resultado)
    SELECT 'Tablas sin cambios tras ROLLBACK',
           IF((SELECT COUNT(*) FROM Clientes) + (SELECT COUNT(*) FROM Productos)
              + (SELECT COUNT(*) FROM FuenteDatos) + (SELECT COUNT(*) FROM Opiniones) = vPrevios, 'PASS', 'FAIL');
END$$

DELIMITER ;

CALL sp_EjecutarPruebasTemporal();
SELECT Prueba, Resultado FROM ResultadosPruebas ORDER BY Orden;
SELECT IF(SUM(Resultado = 'FAIL') = 0, 'TODAS LAS PRUEBAS PASARON', 'HAY PRUEBAS EN FAIL') AS Resumen FROM ResultadosPruebas;

DROP PROCEDURE sp_EjecutarPruebasTemporal;
DROP TEMPORARY TABLE ResultadosPruebas;
