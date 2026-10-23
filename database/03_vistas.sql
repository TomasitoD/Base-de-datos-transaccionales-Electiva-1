USE AnalisisOpinionesClientes;

CREATE OR REPLACE VIEW vw_OpinionesDetalle AS
SELECT
    o.IdOpinion,
    o.Canal,
    o.IdExterno AS IdExternoOpinion,
    o.Fecha,
    c.IdExterno AS IdExternoCliente,
    c.Nombre AS Cliente,
    p.IdExterno AS IdExternoProducto,
    p.Nombre AS Producto,
    p.Categoria,
    f.IdExterno AS Fuente,
    f.TipoFuente,
    o.Comentario,
    o.Clasificacion,
    o.PuntajeSatisfaccion
FROM Opiniones o
INNER JOIN Productos p ON p.IdProducto = o.IdProducto
INNER JOIN FuenteDatos f ON f.IdFuente = o.IdFuente
LEFT JOIN Clientes c ON c.IdCliente = o.IdCliente;

CREATE OR REPLACE VIEW vw_OpinionesPorCanal AS
SELECT
    Canal,
    COUNT(*) AS Total,
    COUNT(Clasificacion) AS Clasificadas,
    COUNT(*) - COUNT(Clasificacion) AS Pendientes,
    ROUND(AVG(PuntajeSatisfaccion), 2) AS PromedioPuntaje
FROM Opiniones
GROUP BY Canal;

CREATE OR REPLACE VIEW vw_ReferenciasHuerfanas AS
SELECT
    (SELECT COUNT(*) FROM Opiniones o
     LEFT JOIN Clientes c ON c.IdCliente = o.IdCliente
     WHERE o.IdCliente IS NOT NULL AND c.IdCliente IS NULL) AS ClientesInexistentes,
    (SELECT COUNT(*) FROM Opiniones o
     LEFT JOIN Productos p ON p.IdProducto = o.IdProducto
     WHERE p.IdProducto IS NULL) AS ProductosInexistentes,
    (SELECT COUNT(*) FROM Opiniones o
     LEFT JOIN FuenteDatos f ON f.IdFuente = o.IdFuente
     WHERE f.IdFuente IS NULL) AS FuentesInexistentes;
