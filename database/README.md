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
