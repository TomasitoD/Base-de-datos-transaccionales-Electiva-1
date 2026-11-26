USE AnalisisOpinionesDW;

START TRANSACTION;

SET @antes_producto = (SELECT COUNT(*) FROM DimProducto);
SET @antes_cliente = (SELECT COUNT(*) FROM DimCliente);
SET @antes_fecha = (SELECT COUNT(*) FROM DimFecha);
SET @antes_fuente = (SELECT COUNT(*) FROM DimFuente);
SET @antes_clasificacion = (SELECT COUNT(*) FROM DimClasificacion);

INSERT INTO DimProducto (IdProductoOrigen, CodigoExterno, Nombre, Categoria)
SELECT p.IdProducto, p.IdExterno, p.Nombre, p.Categoria
FROM AnalisisOpinionesClientes.Productos p
WHERE NOT EXISTS (SELECT 1 FROM DimProducto d WHERE d.IdProductoOrigen = p.IdProducto);

INSERT INTO DimCliente (IdClienteOrigen, CodigoExterno, Nombre)
SELECT c.IdCliente, c.IdExterno, c.Nombre
FROM AnalisisOpinionesClientes.Clientes c
WHERE NOT EXISTS (SELECT 1 FROM DimCliente d WHERE d.IdClienteOrigen = c.IdCliente);

INSERT INTO DimCliente (IdClienteOrigen, CodigoExterno, Nombre)
SELECT NULL, 'SIN_CLIENTE', 'Sin cliente'
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM DimCliente d WHERE d.CodigoExterno = 'SIN_CLIENTE');

INSERT INTO DimFecha (SkFecha, Fecha, Anio, Trimestre, Mes, NombreMes, Dia)
SELECT CAST(DATE_FORMAT(f.Dia, '%Y%m%d') AS UNSIGNED), f.Dia, YEAR(f.Dia), QUARTER(f.Dia), MONTH(f.Dia),
       ELT(MONTH(f.Dia), 'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto',
           'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'),
       DAY(f.Dia)
FROM (SELECT DISTINCT DATE(o.Fecha) AS Dia FROM AnalisisOpinionesClientes.Opiniones o) f
WHERE NOT EXISTS (SELECT 1 FROM DimFecha d WHERE d.Fecha = f.Dia);

INSERT INTO DimFuente (IdFuenteOrigen, CodigoExterno, TipoFuente, FechaCarga)
SELECT s.IdFuente, s.IdExterno, s.TipoFuente, s.FechaCarga
FROM AnalisisOpinionesClientes.FuenteDatos s
WHERE NOT EXISTS (SELECT 1 FROM DimFuente d WHERE d.IdFuenteOrigen = s.IdFuente);

INSERT INTO DimClasificacion (SkClasificacion, Nombre)
SELECT v.Sk, v.Nombre
FROM (SELECT 1 AS Sk, 'Positiva' AS Nombre
      UNION ALL SELECT 2, 'Neutra'
      UNION ALL SELECT 3, 'Negativa'
      UNION ALL SELECT 4, 'Pendiente') v
WHERE NOT EXISTS (SELECT 1 FROM DimClasificacion d WHERE d.SkClasificacion = v.Sk);

SELECT 'DimProducto' AS Dimension, (SELECT COUNT(*) FROM DimProducto) AS TotalDW,
       @antes_producto AS Existentes, (SELECT COUNT(*) FROM DimProducto) - @antes_producto AS Insertadas
UNION ALL SELECT 'DimCliente', (SELECT COUNT(*) FROM DimCliente), @antes_cliente,
       (SELECT COUNT(*) FROM DimCliente) - @antes_cliente
UNION ALL SELECT 'DimFecha', (SELECT COUNT(*) FROM DimFecha), @antes_fecha,
       (SELECT COUNT(*) FROM DimFecha) - @antes_fecha
UNION ALL SELECT 'DimFuente', (SELECT COUNT(*) FROM DimFuente), @antes_fuente,
       (SELECT COUNT(*) FROM DimFuente) - @antes_fuente
UNION ALL SELECT 'DimClasificacion', (SELECT COUNT(*) FROM DimClasificacion), @antes_clasificacion,
       (SELECT COUNT(*) FROM DimClasificacion) - @antes_clasificacion;

COMMIT;
