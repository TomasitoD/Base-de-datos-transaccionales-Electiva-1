USE AnalisisOpinionesDW;

SELECT 'DimProducto' AS Dimension,
       (SELECT COUNT(*) FROM AnalisisOpinionesClientes.Productos) AS Esperadas,
       (SELECT COUNT(*) FROM DimProducto) AS TotalDW,
       (SELECT COUNT(*) FROM AnalisisOpinionesClientes.Productos p
        LEFT JOIN DimProducto d ON d.IdProductoOrigen = p.IdProducto
        WHERE d.SkProducto IS NULL
           OR NOT (d.CodigoExterno <=> p.IdExterno AND d.Nombre <=> p.Nombre AND d.Categoria <=> p.Categoria)) AS FaltantesODiferentes
UNION ALL
SELECT 'DimCliente',
       (SELECT COUNT(*) FROM AnalisisOpinionesClientes.Clientes) + 1,
       (SELECT COUNT(*) FROM DimCliente),
       (SELECT COUNT(*) FROM AnalisisOpinionesClientes.Clientes c
        LEFT JOIN DimCliente d ON d.IdClienteOrigen = c.IdCliente
        WHERE d.SkCliente IS NULL OR NOT (d.CodigoExterno <=> c.IdExterno AND d.Nombre <=> c.Nombre))
       + (SELECT IF(COUNT(*) = 1, 0, 1) FROM DimCliente WHERE CodigoExterno = 'SIN_CLIENTE')
UNION ALL
SELECT 'DimFecha',
       (SELECT COUNT(DISTINCT DATE(Fecha)) FROM AnalisisOpinionesClientes.Opiniones),
       (SELECT COUNT(*) FROM DimFecha),
       (SELECT COUNT(*) FROM (SELECT DISTINCT DATE(o.Fecha) AS Dia FROM AnalisisOpinionesClientes.Opiniones o) f
        LEFT JOIN DimFecha d ON d.Fecha = f.Dia
        WHERE d.SkFecha IS NULL OR d.SkFecha <> CAST(DATE_FORMAT(f.Dia, '%Y%m%d') AS UNSIGNED))
UNION ALL
SELECT 'DimFuente',
       (SELECT COUNT(*) FROM AnalisisOpinionesClientes.FuenteDatos),
       (SELECT COUNT(*) FROM DimFuente),
       (SELECT COUNT(*) FROM AnalisisOpinionesClientes.FuenteDatos s
        LEFT JOIN DimFuente d ON d.IdFuenteOrigen = s.IdFuente
        WHERE d.SkFuente IS NULL
           OR NOT (d.CodigoExterno <=> s.IdExterno AND d.TipoFuente <=> s.TipoFuente AND d.FechaCarga <=> s.FechaCarga))
UNION ALL
SELECT 'DimClasificacion', 4, (SELECT COUNT(*) FROM DimClasificacion),
       (SELECT COUNT(*) FROM (SELECT 1 AS Sk, 'Positiva' AS Nombre UNION ALL SELECT 2, 'Neutra'
                              UNION ALL SELECT 3, 'Negativa' UNION ALL SELECT 4, 'Pendiente') v
        LEFT JOIN DimClasificacion d ON d.SkClasificacion = v.Sk
        WHERE d.SkClasificacion IS NULL OR NOT (d.Nombre <=> v.Nombre));

SELECT 'Muestra DimProducto' AS Tabla;
SELECT SkProducto, IdProductoOrigen, CodigoExterno, Categoria FROM DimProducto ORDER BY SkProducto LIMIT 5;

SELECT 'Muestra DimCliente (solo codigos)' AS Tabla;
(SELECT SkCliente, IdClienteOrigen, CodigoExterno FROM DimCliente WHERE IdClienteOrigen IS NOT NULL ORDER BY SkCliente LIMIT 4)
UNION ALL
(SELECT SkCliente, IdClienteOrigen, CodigoExterno FROM DimCliente WHERE CodigoExterno = 'SIN_CLIENTE');

SELECT 'Muestra DimFecha' AS Tabla;
SELECT SkFecha, Fecha, Anio, Trimestre, Mes, NombreMes, Dia FROM DimFecha ORDER BY SkFecha LIMIT 5;

SELECT 'Muestra DimFuente' AS Tabla;
SELECT SkFuente, IdFuenteOrigen, CodigoExterno, TipoFuente, FechaCarga FROM DimFuente ORDER BY SkFuente LIMIT 5;

SELECT 'DimClasificacion' AS Tabla;
SELECT SkClasificacion, Nombre FROM DimClasificacion ORDER BY SkClasificacion;
