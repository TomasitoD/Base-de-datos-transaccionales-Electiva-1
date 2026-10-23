-- Consultas de validacion. Ejecutar DESPUES de la carga del ETL.
-- Solo lectura: SELECT y CALL de un procedimiento que solo cuenta filas.
-- Uso: mysql -u <usuario> -p --default-character-set=utf8mb4 -t < database/04_consultas_validacion.sql

USE AnalisisOpinionesClientes;

-- 1. Conteo por tabla
-- Demuestra: cuantas filas hay realmente en cada tabla, para compararlo con el resumen del ETL.
-- Esperado tras la 1a y 2a ejecucion: Clientes 500, Productos 200, FuenteDatos 105, Opiniones 406.
SELECT '1. Conteo por tabla' AS Consulta;
CALL sp_ObtenerConteos();

-- 2. Opiniones con cliente informado que no existe
-- Demuestra: que ninguna opinion apunta a un cliente inexistente y que ningun cliente informado
--            se convirtio en NULL (IdClienteOrigen con valor pero IdCliente sin resolver).
-- Esperado: 0 filas. Tabla poblada (406 opiniones), por lo que el vacio si es una prueba.
SELECT '2. Opiniones con cliente informado inexistente' AS Consulta;
SELECT o.IdOpinion, o.Canal, o.IdExterno, o.IdCliente, o.IdClienteOrigen
FROM Opiniones o
LEFT JOIN Clientes c ON c.IdCliente = o.IdCliente
WHERE (o.IdCliente IS NOT NULL AND c.IdCliente IS NULL)
   OR (o.IdClienteOrigen IS NOT NULL AND o.IdCliente IS NULL);

-- 3. Opiniones con producto que no existe
-- Demuestra: que toda opinion tiene un producto valido en la tabla Productos.
-- Esperado: 0 filas.
SELECT '3. Opiniones con producto inexistente' AS Consulta;
SELECT o.IdOpinion, o.Canal, o.IdExterno, o.IdProducto, o.IdProductoOrigen
FROM Opiniones o
LEFT JOIN Productos p ON p.IdProducto = o.IdProducto
WHERE p.IdProducto IS NULL;

-- 4. Opiniones con fuente que no existe
-- Demuestra: que toda opinion tiene una fuente valida en la tabla FuenteDatos.
-- Esperado: 0 filas.
SELECT '4. Opiniones con fuente inexistente' AS Consulta;
SELECT o.IdOpinion, o.Canal, o.IdExterno, o.IdFuente
FROM Opiniones o
LEFT JOIN FuenteDatos f ON f.IdFuente = o.IdFuente
WHERE f.IdFuente IS NULL;

-- 5. Claves Canal + IdExterno repetidas
-- Demuestra: que no hay opiniones duplicadas, tambien despues de ejecutar el ETL mas de una vez.
-- Esperado: 0 filas.
SELECT '5. Canal + IdExterno repetidos' AS Consulta;
SELECT Canal, IdExterno, COUNT(*) AS Veces
FROM Opiniones
GROUP BY Canal, IdExterno
HAVING COUNT(*) > 1;

-- 6. Totales, clasificadas y pendientes por canal
-- Demuestra: como se reparten las opiniones; solo las encuestas traen clasificacion.
-- Esperado: Encuesta 6 total / 6 clasificadas / 0 pendientes; RedSocial 200 / 0 / 200; Web 200 / 0 / 200.
--           Total 406. PromedioPuntaje: Web 3.10 (Rating), Encuesta 2.83, RedSocial NULL (sin puntaje).
SELECT '6. Totales por canal' AS Consulta;
SELECT * FROM vw_OpinionesPorCanal ORDER BY Canal;

-- 7. Detalle con nombres de producto y cliente
-- Demuestra: que la vista une opiniones con producto, fuente y cliente (LEFT JOIN) y que las
--            opiniones sin cliente se conservan con Cliente en NULL.
-- Esperado: 20 filas; las primeras 10 son opiniones de RedSocial sin cliente (Cliente NULL),
--           las siguientes 10 tienen cliente.
SELECT '7. Detalle de opiniones (20 filas)' AS Consulta;
(SELECT IdOpinion, Canal, IdExternoOpinion, Fecha, Cliente, Producto, Categoria, Fuente, Clasificacion, PuntajeSatisfaccion
 FROM vw_OpinionesDetalle
 WHERE Cliente IS NULL
 ORDER BY IdOpinion
 LIMIT 10)
UNION ALL
(SELECT IdOpinion, Canal, IdExternoOpinion, Fecha, Cliente, Producto, Categoria, Fuente, Clasificacion, PuntajeSatisfaccion
 FROM vw_OpinionesDetalle
 WHERE Cliente IS NOT NULL
 ORDER BY IdOpinion
 LIMIT 10);

-- 8. Compatibilidad del tipo de fuente con el canal
-- Demuestra: que Web usa fuentes tipo Web, Encuesta tipo CSV y RedSocial tipo Red Social.
-- Esperado: 0 filas. El extra E2 muestra las combinaciones reales, para no confiar solo en el vacio.
SELECT '8. Canal incompatible con TipoFuente' AS Consulta;
SELECT o.IdOpinion, o.Canal, o.IdExterno, f.IdExterno AS Fuente, f.TipoFuente
FROM Opiniones o
INNER JOIN FuenteDatos f ON f.IdFuente = o.IdFuente
WHERE NOT ((o.Canal = 'Web' AND f.TipoFuente = 'Web')
        OR (o.Canal = 'Encuesta' AND f.TipoFuente = 'CSV')
        OR (o.Canal = 'RedSocial' AND f.TipoFuente = 'Red Social'));

-- 9. Claves foraneas y CHECK existentes y aplicados
-- Demuestra: que las 3 FK de Opiniones existen y estan aplicadas (ENFORCED = YES), con sus reglas,
--            y que los CHECK de las 4 tablas tambien estan aplicados.
-- Esperado: 3 filas FOREIGN KEY (cliente, producto, fuente) con Aplicada = YES y regla NO ACTION;
--           13 filas CHECK, todas con Aplicada = YES (en total 16 filas).
SELECT '9a. FK y CHECK existentes y aplicados' AS Consulta;
SELECT tc.CONSTRAINT_TYPE AS Tipo,
       tc.TABLE_NAME AS Tabla,
       tc.CONSTRAINT_NAME AS Restriccion,
       rc.REFERENCED_TABLE_NAME AS ReferenciaA,
       rc.UPDATE_RULE AS ReglaUpdate,
       rc.DELETE_RULE AS ReglaDelete,
       tc.ENFORCED AS Aplicada
FROM information_schema.TABLE_CONSTRAINTS tc
LEFT JOIN information_schema.REFERENTIAL_CONSTRAINTS rc
       ON rc.CONSTRAINT_SCHEMA = tc.CONSTRAINT_SCHEMA
      AND rc.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
      AND rc.TABLE_NAME = tc.TABLE_NAME
WHERE tc.TABLE_SCHEMA = DATABASE()
  AND tc.CONSTRAINT_TYPE IN ('FOREIGN KEY', 'CHECK')
ORDER BY tc.CONSTRAINT_TYPE DESC, tc.TABLE_NAME, tc.CONSTRAINT_NAME;

-- 9b. Verificacion de FK no deshabilitadas
-- Demuestra: que la comprobacion de claves foraneas esta activa (foreign_key_checks) y que no hay
--            restricciones sin aplicar.
-- Esperado: ForeignKeyChecksSesion = 1, ForeignKeyChecksGlobal = 1, RestriccionesNoAplicadas = 0.
SELECT '9b. foreign_key_checks y restricciones no aplicadas' AS Consulta;
SELECT @@SESSION.foreign_key_checks AS ForeignKeyChecksSesion,
       @@GLOBAL.foreign_key_checks AS ForeignKeyChecksGlobal,
       (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
        WHERE TABLE_SCHEMA = DATABASE() AND CONSTRAINT_TYPE IN ('FOREIGN KEY', 'CHECK') AND ENFORCED <> 'YES')
       AS RestriccionesNoAplicadas;

-- Consultas extra de apoyo

-- E1. La vista de detalle no pierde opiniones
-- Demuestra: que el LEFT JOIN conserva las opiniones sin cliente (filas de la vista = filas de la tabla).
-- Esperado: FilasVista = FilasTabla = 406; SinCliente = 88.
SELECT 'E1. Vista de detalle frente a la tabla' AS Consulta;
SELECT (SELECT COUNT(*) FROM vw_OpinionesDetalle) AS FilasVista,
       (SELECT COUNT(*) FROM Opiniones) AS FilasTabla,
       (SELECT COUNT(*) FROM vw_OpinionesDetalle WHERE Cliente IS NULL) AS SinCliente;

-- E2. Combinaciones reales de canal y fuente
-- Demuestra: que cada canal usa solo sus fuentes tecnicas (complementa la consulta 8, que es un vacio).
-- Esperado: Encuesta con ETL:CSV:ENCUESTA_INTERNA (6); RedSocial con INSTAGRAM, TWITTER y FACEBOOK
--           (suman 200); Web con ETL:WEB:WEB_REVIEWS (200).
SELECT 'E2. Opiniones por canal y fuente' AS Consulta;
SELECT o.Canal, f.IdExterno AS Fuente, f.TipoFuente, COUNT(*) AS Opiniones
FROM Opiniones o
INNER JOIN FuenteDatos f ON f.IdFuente = o.IdFuente
GROUP BY o.Canal, f.IdExterno, f.TipoFuente
ORDER BY o.Canal, f.IdExterno;

-- E3. Trazabilidad de codigos originales
-- Demuestra: que el codigo original (C007, P016, 8537...) corresponde al cliente y producto enlazados.
-- Esperado: 0 filas.
SELECT 'E3. Codigo original distinto del enlazado' AS Consulta;
SELECT o.IdOpinion, o.Canal, o.IdExterno, o.IdClienteOrigen, c.IdExterno AS ClienteEnlazado,
       o.IdProductoOrigen, p.IdExterno AS ProductoEnlazado
FROM Opiniones o
INNER JOIN Productos p ON p.IdProducto = o.IdProducto
LEFT JOIN Clientes c ON c.IdCliente = o.IdCliente
WHERE TRIM(LEADING '0' FROM REGEXP_REPLACE(o.IdProductoOrigen, '^[A-Za-z]+', '')) <> p.IdExterno
   OR (o.IdCliente IS NOT NULL
       AND TRIM(LEADING '0' FROM REGEXP_REPLACE(o.IdClienteOrigen, '^[A-Za-z]+', '')) <> c.IdExterno);

-- E4. Fuentes del CSV y fuentes tecnicas
-- Demuestra: que las 100 fuentes de fuente_datos.csv y las 5 fuentes tecnicas del ETL estan cargadas.
-- Esperado: 100 fuentes de CSV (IdExterno F###) y 5 tecnicas (ETL:...).
SELECT 'E4. Fuentes cargadas por origen' AS Consulta;
SELECT IF(IdExterno LIKE 'ETL:%', 'Tecnica del ETL', 'fuente_datos.csv') AS Origen, COUNT(*) AS Fuentes
FROM FuenteDatos
GROUP BY Origen
ORDER BY Origen;

-- E5. Valores fuera de rango
-- Demuestra: que no hay puntajes fuera de 1-5, clasificaciones invalidas ni textos vacios,
--            aunque los CHECK ya los impiden.
-- Esperado: 0 filas.
SELECT 'E5. Puntaje, clasificacion o texto invalido' AS Consulta;
SELECT IdOpinion, Canal, IdExterno, Clasificacion, PuntajeSatisfaccion
FROM Opiniones
WHERE PuntajeSatisfaccion NOT BETWEEN 1 AND 5
   OR (Clasificacion IS NOT NULL AND Clasificacion NOT IN ('Positiva', 'Neutra', 'Negativa'))
   OR CHAR_LENGTH(TRIM(Comentario)) = 0
   OR CHAR_LENGTH(TRIM(IdProductoOrigen)) = 0;
