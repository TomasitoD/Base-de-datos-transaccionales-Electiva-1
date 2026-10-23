# Nota de diseño técnico

## Objetivo
Cargar seis archivos CSV (clientes, productos, fuentes, reseñas web, encuestas y comentarios de redes sociales) en una base MySQL transaccional. El proceso valida cada fila, rechaza las inválidas con su motivo, inserta respetando las relaciones entre tablas y muestra cuántas filas se procesaron, insertaron, omitieron y rechazaron. Se puede ejecutar varias veces sin duplicar datos.

## Arquitectura
Cuatro proyectos con dependencias en una sola dirección (Consola → BLL/DAL → Entities):

| Proyecto | Responsabilidad |
|---|---|
| `ETL.Entities` | Modelos (`ClienteNuevo`, `OpinionNueva`, `Rechazo`, `MetricasArchivo`...) y contratos `ISesionCarga` e `IFabricaSesion`. No tiene acceso a datos. |
| `ETL.DAL` | `SesionCargaMySql`: una conexión y una transacción por ejecución. Solo llama procedimientos almacenados, con parámetros tipados y con tamaño; nunca concatena SQL. |
| `ETL.BLL` | `LectorCsv` (CsvHelper), `Normalizador`, validadores de catálogos y de opiniones, `CoordinadorCarga` y `Reporte`. |
| `ETL.Consola` | Lee la configuración, construye el coordinador, imprime el resumen y devuelve el código de salida. |

La BLL depende solo del contrato `ISesionCarga`, por lo que se prueba con una sesión falsa en memoria sin tocar MySQL.

## Modelo de datos
Base `AnalisisOpinionesClientes`, `utf8mb4` con collation `utf8mb4_0900_as_cs` (distingue mayúsculas y acentos al comparar contenido).

- `Clientes`(`IdCliente`, `IdExterno`, `Nombre`, `Email` opcional)
- `Productos`(`IdProducto`, `IdExterno`, `Nombre`, `Categoria`)
- `FuenteDatos`(`IdFuente`, `IdExterno`, `TipoFuente`, `FechaCarga`)
- `Opiniones`(`IdOpinion`, `Canal`, `IdExterno`, `IdCliente` opcional, `IdProducto`, `IdFuente`, `Fecha`, `Comentario`, `Clasificacion` opcional, `PuntajeSatisfaccion` opcional, `IdClienteOrigen`, `IdProductoOrigen`)

Cada tabla tiene clave interna `AUTO_INCREMENT` y una columna `IdExterno` única que guarda el identificador de origen. La clave interna nunca se deduce del identificador externo. `Opiniones` es única por `Canal` + `IdExterno` y tiene 3 claves foráneas (cliente, producto, fuente). En total hay 13 restricciones `CHECK`: textos obligatorios no vacíos, `Canal` en (`Web`, `Encuesta`, `RedSocial`), clasificación en (`Positiva`, `Neutra`, `Negativa`) y puntaje entre 1 y 5.

## Reglas de transformación
- **Prefijos.** `C007` se enlaza con el cliente externo `7` y `P016` con el producto externo `16`: se quita el prefijo alfabético y los ceros a la izquierda. El código original se conserva en `IdClienteOrigen` e `IdProductoOrigen`.
- **Identificador de la opinión.** `IdExterno` de `Opiniones` es el código original (`W0001`, `T0001`, o el número de la encuesta).
- **Clasificación.** Solo las encuestas la traen. En web y redes queda NULL (pendiente); nunca se sustituye por "Neutra".
- **Puntaje.** Encuestas: el valor del CSV. Web: el `Rating` (1 a 5) se copia como puntaje; es una decisión de diseño del proyecto, porque los datos de origen no definen una equivalencia. Redes: sin puntaje.
- **Cliente opcional.** Un cliente vacío solo es válido en redes sociales y se guarda NULL. En web y encuestas es un rechazo. Un cliente informado que no existe siempre se rechaza; no se convierte en NULL.
- **Fuentes técnicas.** Los CSV de opiniones no indican de cuál de las 100 fuentes de `fuente_datos.csv` vienen, y asignarles una arbitrariamente sería inventar la relación. El ETL crea 5 fuentes propias (`ETL:WEB:WEB_REVIEWS`, `ETL:CSV:ENCUESTA_INTERNA`, `ETL:SOCIAL:INSTAGRAM`, `ETL:SOCIAL:TWITTER`, `ETL:SOCIAL:FACEBOOK`), una por origen. Su `FechaCarga` se fija al crearlas y se reutiliza en las ejecuciones siguientes, para que repetir la carga no genere un conflicto.

## Flujo de carga
1. Se verifica que existan los 6 archivos y sus encabezados; si falla, se informa antes de abrir la base.
2. Se abre una sesión (conexión + transacción).
3. Se cargan, en orden: clientes, productos, las 100 fuentes del CSV, las 5 fuentes técnicas, reseñas web, encuestas y comentarios de redes.
4. Las opiniones se validan contra los identificadores internos reales devueltos por la base. Solo se usan los de filas insertadas u omitidas; una fila en conflicto no se usa para enlazar a sus dependientes.
5. Se leen los conteos de las cuatro tablas y se confirma la transacción al final.
6. Ante cualquier excepción se hace rollback, el estado es `Fallida` y todas las filas leídas se informan como no confirmadas.

## Validaciones y motivos de rechazo
Cada fila inválida se rechaza con el archivo, el ordinal del registro (no de la línea física, porque un comentario puede ocupar varias líneas), el identificador y todos los motivos. Una fila con dos referencias ausentes cuenta como un solo rechazo con dos motivos.

- Estructura: encabezados por nombre, incluidos los acentuados (`Categoría`, `Clasificación`, `PuntajeSatisfacción`); archivo con BOM; filas con columnas de más o de menos; comillas sin cerrar.
- Campos: obligatorios vacíos, longitudes del esquema, email con formato `x@y.z`, fecha `yyyy-MM-dd` válida, `Rating` y puntaje enteros de 1 a 5, clasificación permitida, `TipoFuente` en (`Web`, `CSV`, `Red Social`), red social en (`Instagram`, `Twitter`, `Facebook`).
- Referencias: `cliente X no existe`, `producto X no existe`, `IdCliente vacío` (web y encuestas).
- Duplicados: dentro de un archivo, una clave repetida con el mismo contenido cuenta como omitida; con contenido distinto se rechaza como `IdExterno X repetido con contenido distinto`.

## Idempotencia
Los procedimientos `sp_Upsert*` buscan por `IdExterno` (opiniones: `Canal` + `IdExterno`) y devuelven por parámetro `OUT` un estado: `Insertado` si no existía, `Omitido` si existía idéntico y `Conflicto` si existía con contenido distinto; en ese caso no se modifica nada. Si el INSERT falla por clave duplicada (error 1062), un manejador vuelve a leer la fila existente y la compara en vez de abortar. `sp_UpsertOpinion` lanza un error si el tipo de la fuente no corresponde al canal. El ETL nunca borra tablas para recargar.

## Métricas y códigos de salida
Por archivo y en total: procesados, insertados, omitidos, rechazados y no confirmados, con la identidad `Procesados = Insertados + Omitidos + Rechazados + NoConfirmados`. Las fuentes técnicas se informan aparte. Se escriben `reporte-<fecha>.txt`, `reporte-<fecha>.json` y `rechazados-<fecha>.csv`.

| Código | Significado |
|---|---|
| 0 | Carga completa sin rechazos |
| 2 | Carga completa con rechazos por reglas de datos |
| 1 | Fallo técnico o de configuración |

Si el reporte falla después de confirmar la transacción, se informa que los datos quedaron confirmados y que falló el reporte.

## Pruebas
- 84 pruebas xUnit sin base de datos: normalizador, lector CSV, validadores, coordinador con una sesión falsa (carga normal, segunda ejecución, fallo técnico con rollback, conflicto de catálogo, reutilización de la fecha de las fuentes técnicas), reporte y diagnóstico.
- `database/05_pruebas_base_datos.sql`: comprueba dentro de una transacción con `ROLLBACK` el rechazo de una clave foránea rota, de un puntaje 6, de una clave `Canal` + `IdExterno` repetida y de un canal incompatible con su fuente, y los tres estados de los procedimientos.
- Ejecuciones reales: la primera carga, dos reejecuciones, tres fallos controlados (encabezado faltante, archivo faltante y rollback sobre una base desechable) y las consultas de `database/04_consultas_validacion.sql`.

## Resultados
La primera carga procesó 1700 filas: 1206 insertadas y 494 rechazadas. Quedaron Clientes 500, Productos 200, FuenteDatos 105 (100 del CSV y 5 técnicas) y Opiniones 406 (200 web, 200 de redes sociales y 6 de encuestas). En las reejecuciones el resultado fue 0 insertadas, 1206 omitidas y 494 rechazadas, con los mismos conteos finales.

## Limitaciones
- Los 494 rechazos son encuestas: el CSV referencia clientes y productos que no están en los catálogos (solo 6 de 500 filas tienen ambos). 386 filas tienen dos motivos y 108 uno.
- La equivalencia `Rating` → puntaje es una decisión de diseño del proyecto.
- Las fuentes técnicas no recuperan la relación con las 100 fuentes de `fuente_datos.csv`, que se cargan pero ninguna opinión referencia.
- No hay interfaz gráfica, API ni procesamiento en segundo plano.
