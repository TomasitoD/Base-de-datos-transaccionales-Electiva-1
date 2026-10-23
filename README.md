# ETL de Opiniones de Clientes

Carga automática de seis archivos CSV en una base de datos MySQL transaccional para el Sistema de Análisis de Opiniones de Clientes. Lee los archivos, valida cada fila, rechaza las inválidas con su motivo, inserta respetando las relaciones y muestra cuántas filas se procesaron, insertaron, omitieron y rechazaron.

## Requisitos
- .NET SDK 10 (`net10.0`; desarrollado con 10.0.301).
- MySQL 8 o superior (desarrollado con 26.10.0, instalado con Homebrew). Sin Docker.
- Paquetes NuGet (se restauran solos): MySqlConnector 2.6.2, CsvHelper 33.1.0, xUnit 2.9.3.

## Estructura
```
OpinionesETL.sln
src/
  ETL.Entities/   modelos y contratos (sin acceso a datos)
  ETL.DAL/        acceso a MySQL solo con procedimientos almacenados
  ETL.BLL/        lectura de CSV, validación, coordinación y reporte
  ETL.Consola/    configuración, resumen en consola y códigos de salida
tests/ETL.Tests/  pruebas xUnit (no necesitan base de datos)
database/         scripts SQL y su README
data/             los 6 archivos CSV de entrada (no se versiona en Git; va en el paquete de entrega)
docs/             plan de implementación
```

## Preparar la base de datos
Detalles y cómo leer las pruebas en [`database/README.md`](database/README.md). Orden:

1. **Usuario `etl`.** Copiar `database/00_usuario.sql.example`, cambiar `<PASSWORD>` por una contraseña propia en la copia (no guardarla en el repositorio) y ejecutarla con un usuario administrador:
   `mysql -u root -p < mi_usuario.sql`
2. `database/01_crear_base_datos.sql`: base `AnalisisOpinionesClientes` y tablas.
3. `database/02_procedimientos.sql`: procedimientos almacenados.
4. `database/03_vistas.sql`: vistas.
5. `database/05_pruebas_base_datos.sql` (opcional): pruebas de las reglas de la base; terminan en `ROLLBACK` y no dejan datos.
6. Ejecutar el ETL (siguiente sección).
7. `database/04_consultas_validacion.sql`: consultas de validación, que se ejecutan después de la carga.

Cada script se ejecuta así, y se puede repetir sin perder datos:
`mysql -u <usuario> -p --default-character-set=utf8mb4 < database/01_crear_base_datos.sql`

Después de ejecutar las pruebas del paso 5, si las tablas siguen vacías, `database/README.md` explica cómo reiniciar los contadores `AUTO_INCREMENT`.

## Ejecutar el ETL
Tres variables de entorno, todas obligatorias:

| Variable | Contenido |
|---|---|
| `ETL_DATA_DIR` | Carpeta con los 6 CSV (por ejemplo `data`). |
| `ETL_REPORT_DIR` | Carpeta donde se escriben los reportes (se crea si no existe). |
| `ETL_CONNECTION_STRING` | Cadena de conexión a MySQL. |

```
export ETL_DATA_DIR=data
export ETL_REPORT_DIR=output
export ETL_CONNECTION_STRING='Server=localhost;Database=AnalisisOpinionesClientes;User=etl;Password=<PASSWORD>;SslMode=Preferred'
dotnet run --project src/ETL.Consola
```

La cadena de conexión nunca se imprime ni se guarda en los reportes. Para revisar los CSV sin tocar la base: `dotnet run --project src/ETL.Consola -- --diagnostico` (solo necesita `ETL_DATA_DIR`).

Pruebas automáticas: `dotnet test OpinionesETL.sln`.

### Códigos de salida
| Código | Significado |
|---|---|
| 0 | Carga completa, sin rechazos. |
| 2 | Carga completa con filas rechazadas por reglas de datos. Lo confirmado queda guardado. |
| 1 | Fallo técnico o de configuración (variable ausente, archivo o encabezado faltante, error de base de datos). Si el fallo ocurre durante la carga se revierte todo. |

## Cómo leer el resumen
El ETL imprime una tabla por archivo y un total:

- **Procesados:** filas leídas del archivo.
- **Insertados:** filas nuevas guardadas.
- **Omitidos:** filas que ya existían exactamente iguales, o repetidas dentro del archivo.
- **Rechazados:** filas inválidas, con referencias inexistentes, o con una clave ya existente pero contenido distinto (conflicto).
- **NoConfirmados:** filas leídas cuando la carga falló y se revirtió; en una carga normal es 0.

Siempre se cumple `Procesados = Insertados + Omitidos + Rechazados + NoConfirmados`. Las 5 fuentes técnicas que crea el ETL se informan aparte, en `fuentes_tecnicas`, para no mezclarlas con los archivos. Al final se muestran los conteos reales de las cuatro tablas.

En `ETL_REPORT_DIR` se escriben tres archivos con la fecha UTC en el nombre: `reporte-<fecha>.txt`, `reporte-<fecha>.json` y `rechazados-<fecha>.csv` (archivo, ordinal, identificador y motivos de cada fila rechazada, sin datos personales).

Con los datos de este proyecto la primera carga dio 1700 filas procesadas, 1206 insertadas y 494 rechazadas, y dejó Clientes 500, Productos 200, FuenteDatos 105 (100 del CSV y 5 técnicas) y Opiniones 406 (200 web, 200 de redes sociales y 6 de encuestas).

## Reglas principales
- Los códigos con prefijo (`C007`, `P016`) se enlazan con el cliente o producto externo `7` y `16`, y se conserva el código original en la opinión. Los identificadores internos los asigna MySQL; nunca se suponen iguales a los externos.
- Una fila con una referencia informada que no existe se rechaza con todos los motivos. Un cliente vacío en redes sociales es válido y queda en NULL.
- Una clave repetida con el mismo contenido se omite. Con contenido distinto se rechaza como conflicto y no se sobrescribe.
- El ETL se puede ejecutar varias veces: no borra tablas y no duplica filas. Las pruebas automáticas cubren la segunda ejecución.
- Toda la carga ocurre en una transacción que se confirma solo al final.

## Limitaciones
- **494 encuestas rechazadas.** `surveys_part1.csv` referencia clientes y productos que no están en `clients.csv` ni `products.csv`; solo 6 de las 500 filas tienen ambos. El ETL las rechaza y registra el motivo en vez de inventar datos.
- **Rating web a puntaje.** El CSV web trae un `Rating` de 1 a 5 y la base guarda `PuntajeSatisfaccion`. Se copia el mismo valor; es una decisión propia del proyecto, no viene de la guía.
- **Clasificación NULL en web y redes.** Solo las encuestas traen clasificación; en web y redes queda pendiente (NULL), nunca "Neutra". Las redes sociales no tienen puntaje.
- **Fuentes técnicas.** Las reseñas y comentarios usan 5 fuentes creadas por el ETL (`ETL:WEB:WEB_REVIEWS`, `ETL:CSV:ENCUESTA_INTERNA` y una por red social). No recuperan la relación con las 100 fuentes de `fuente_datos.csv`, que se cargan pero ninguna opinión referencia.
- No hay interfaz gráfica, API ni procesamiento en segundo plano; eso corresponde a etapas posteriores del proyecto.
