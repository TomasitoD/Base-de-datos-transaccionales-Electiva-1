# Base de datos transaccional

Base `AnalisisOpinionesClientes` en MySQL (`utf8mb4`, collation `utf8mb4_0900_as_cs`, sensible a mayúsculas y acentos).

## Requisitos
- MySQL 8 o superior (probado con 26.10.0, Homebrew).
- Un usuario con permisos para crear la base, tablas, procedimientos y vistas.
- Sin Docker y sin T-SQL.

## Orden de ejecución
```
mysql -u <usuario> -p --default-character-set=utf8mb4 < database/01_crear_base_datos.sql
mysql -u <usuario> -p --default-character-set=utf8mb4 < database/02_procedimientos.sql
mysql -u <usuario> -p --default-character-set=utf8mb4 < database/03_vistas.sql
mysql -u <usuario> -p --default-character-set=utf8mb4 < database/05_pruebas_base_datos.sql
```
`04_consultas_validacion.sql` se ejecuta después de la carga del ETL. `00_usuario.sql.example` es una plantilla: reemplazar `<PASSWORD>` localmente, sin guardar la contraseña en el repositorio.

Los scripts se pueden ejecutar más de una vez: las tablas usan `IF NOT EXISTS`, y los procedimientos y vistas se recrean sin tocar los datos.

## Procedimientos
Los `sp_Upsert*` buscan por `IdExterno` (opiniones: `Canal` + `IdExterno`) y devuelven por parámetro `OUT` el id interno y un estado:
- `Insertado`: no existía y se insertó.
- `Omitido`: existía con exactamente el mismo contenido.
- `Conflicto`: existía con contenido distinto; no se modifica.

La transacción la abre quien llama. `sp_UpsertOpinion` lanza un error (`SQLSTATE 45000`) si el `TipoFuente` de la fuente no corresponde al canal (Web con Web, Encuesta con CSV, RedSocial con Red Social).

## Cómo leer `05_pruebas_base_datos.sql`
Muestra una fila por prueba con `PASS` o `FAIL` y un resumen final. Todo corre dentro de una transacción que termina en `ROLLBACK`, y el script crea un procedimiento y una tabla temporales que elimina al terminar. Una última prueba comprueba que el número de filas no cambió.

Los contadores `AUTO_INCREMENT` de InnoDB no se revierten con `ROLLBACK`, así que tras correr las pruebas avanzan aunque las tablas queden vacías. Cuando las 4 tablas están en 0 filas, los contadores se reinician con `ALTER TABLE <tabla> AUTO_INCREMENT = 1` para que los primeros ids internos reales empiecen en 1. Nunca se hace sobre tablas con datos.

## Consultas de validación (`04_consultas_validacion.sql`)

Se ejecutan después de la carga. Solo leen datos. El resultado completo de una ejecución real se guarda en local con:

```
mysql -u <usuario> -p --default-character-set=utf8mb4 -t -vvv < database/04_consultas_validacion.sql
```

`-t` dibuja las tablas y `-vvv` imprime también `Empty set` cuando una consulta no devuelve filas. Sin `-vvv`, mysql no muestra nada en ese caso y un vacío se confunde con una consulta que no corrió.

Un resultado vacío solo prueba algo si la tabla tiene datos. Aquí las tablas ya están pobladas (Clientes 500, Productos 200, FuenteDatos 105, Opiniones 406), así que cada vacío significa que se buscaron filas malas entre 406 opiniones y no apareció ninguna. En una base recién creada esas mismas consultas también salen vacías y no demuestran nada.

| # | Qué comprueba | Resultado con la carga actual | Qué demuestra |
|---|---|---|---|
| 1 | Conteo por tabla con `sp_ObtenerConteos` | 500 / 200 / 105 / 406 | Que lo guardado coincide con el resumen del ETL (1206 insertados: 500 + 200 + 100 fuentes CSV + 406 opiniones, más 5 fuentes técnicas). |
| 2 | Cliente informado inexistente, o código de cliente informado sin enlazar | vacío | Que ninguna opinión apunta a un cliente que no existe y que ningún cliente informado se volvió NULL. Los 88 comentarios sociales sin cliente tienen `IdClienteOrigen` vacío, por eso no aparecen. |
| 3 | Producto inexistente | vacío | Que las 406 opiniones tienen producto. La FK ya lo impide; la consulta lo confirma sobre los datos. |
| 4 | Fuente inexistente | vacío | Igual que la anterior para las fuentes. |
| 5 | `Canal` + `IdExterno` repetidos | vacío | Que no hay duplicados, y la restricción `UNIQUE` los impediría aunque el ETL se ejecute varias veces. |
| 6 | `vw_OpinionesPorCanal` | Encuesta 6 / 6 clasificadas; RedSocial 200 / 0 clasificadas; Web 200 / 0 clasificadas | Que solo las encuestas traen clasificación y que web y redes quedan pendientes (NULL), nunca "Neutra". Promedios: Web 3.10 (del Rating), Encuesta 2.83, RedSocial sin puntaje. |
| 7 | `vw_OpinionesDetalle`, 10 filas sin cliente y 10 con cliente | 20 filas | Que la vista une producto, fuente y cliente y conserva las opiniones sin cliente (columna `Cliente` en NULL). |
| 8 | Tipo de fuente incompatible con el canal | vacío | Que Web usa fuentes Web, Encuesta usa CSV y RedSocial usa Red Social. Como un vacío solo no lo prueba, E2 muestra las combinaciones reales. |
| 9a | FK y CHECK en `information_schema` | 3 FK y 13 CHECK, todos con `Aplicada = YES` | Que las restricciones existen y MySQL las aplica. Las FK son `NO ACTION` en update y delete. |
| 9b | `foreign_key_checks` y restricciones no aplicadas | 1, 1 y 0 | Que la comprobación de FK está activa en la sesión y en el servidor y que ninguna restricción está sin aplicar. |

Consultas extra:

| # | Qué comprueba | Resultado con la carga actual | Qué demuestra |
|---|---|---|---|
| E1 | Filas de `vw_OpinionesDetalle` frente a `Opiniones` | 406, 406 y 88 sin cliente | Que el `LEFT JOIN` no pierde opiniones. |
| E2 | Opiniones por canal y fuente técnica | Encuesta 6; Web 200; RedSocial 68 Facebook, 77 Instagram y 55 Twitter | Que cada canal usa solo sus fuentes. Es la parte con datos de la consulta 8. |
| E3 | Código original distinto del cliente o producto enlazado | vacío | Que `IdClienteOrigen` e `IdProductoOrigen` corresponden al registro enlazado. Se comprobó aparte que la comparación coincide en las 406 opiniones con producto y en las 318 con cliente. |
| E4 | Fuentes cargadas por origen | 100 del CSV y 5 técnicas | Que están las 105 fuentes. |
| E5 | Puntaje fuera de 1 a 5, clasificación inválida o textos vacíos | vacío | Que los datos cumplen los CHECK. |

Los 494 rechazos de encuestas no se guardan en la base: están en el archivo `rechazados-<fecha>.csv` que genera el ETL. Las consultas de este archivo solo describen lo que sí se cargó.
