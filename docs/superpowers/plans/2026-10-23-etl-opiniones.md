# ETL de Opiniones de Clientes — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cargar automáticamente los 6 CSV en una BD MySQL transaccional, validando y rechazando con motivo, de forma idempotente, mostrando procesados/insertados/omitidos/rechazados.

**Architecture:** C# en 4 capas (Entities → contratos y modelos; DAL → MySQL vía procedimientos almacenados con una sola transacción; BLL → lectura CSV, validación, coordinación; Consola → configuración y resumen). Dos canales de prueba: xUnit sobre BLL con una sesión falsa, y SQL con rollback sobre la BD.

**Tech Stack:** .NET 10 (SDK 10.0.301), MySQL 26.10.0 (Homebrew), MySqlConnector, CsvHelper, xUnit.

**Spec:** `CONTEXT.md` (§4–§6) y `materiales/GUIA_ASIGNACION_CARGA_ETL_PARA_COMPANERO.md` (§4, §6, §9–§13). Decisiones del usuario: diseño de la guía 2 completo, modelo desde cero, Git local sin GitHub, pruebas SQL + xUnit básico.

## Leyenda
- 🛑 **PARADA:** me detengo; tú revisas y respondes antes de seguir.
- 👤 **TÚ:** acción que solo puedes hacer tú.
- Si el tiempo aprieta (límite hoy 11:59 pm), se recorta en este orden: xUnit (T5.3), pruebas SQL (T1.5), Git (T0.1). Nunca se recortan la ejecución real, la 2ª ejecución, las consultas ni las capturas.

## Global Constraints
- BD: MySQL; sin T-SQL, sin Docker; nombre `AnalisisOpinionesClientes`; `utf8mb4`.
- FK siempre activas; nunca `DROP DATABASE`, borrar tablas ni forzar IDs; sin borrar tablas para recargar.
- `IdExterno` UNIQUE + PK interna AUTO_INCREMENT; nunca suponer PK interna = externa.
- Acceso a datos solo en `ETL.DAL`, con procedimientos y parámetros tipados con tamaño; sin concatenar SQL.
- CsvHelper con cultura explícita (`CultureInfo.InvariantCulture`), encabezados por nombre; prohibido `Split(',')`.
- Fechas `yyyy-MM-dd`, sin convertir zona. Nunca sustituir error por fecha actual, 0, puntaje 3 o "Neutra".
- Clasificación web/redes = NULL. Rating web 1–5 → `PuntajeSatisfaccion` igual valor (**decisión del agente**, la guía no da la tabla). Redes sin puntaje.
- Código sin comentarios; métodos pequeños. Sin ORM, sin contenedor de DI.
- Secretos solo por `ETL_CONNECTION_STRING`; nunca impresos completos ni en docs/capturas.
- Variables: `ETL_DATA_DIR`, `ETL_REPORT_DIR`, `ETL_CONNECTION_STRING`. Códigos de salida: 0 sin rechazos, 2 con rechazos de datos, 1 fallo técnico.
- Identidad: `Procesados = Insertados + Omitidos + Rechazados + NoConfirmados`. Las 5 fuentes técnicas se informan aparte.
- Cifras previstas (solo tras ejecutar): 1ª carga 1206 insertados / 494 rechazados; tablas Clientes 500, Productos 200, FuenteDatos 105, Opiniones 406; 2ª carga 0 insertados, 1206 omitidos, 494 rechazados.

## Review Focus
1. **Encabezados con tilde y BOM** (`Categoría`, `Clasificación`, `PuntajeSatisfacción`): deben mapearse por nombre exacto y fallar con mensaje claro si falta uno. → test en T3.3.
2. **Comentario con comas, comillas y salto de línea:** una sola fila, sin desplazar columnas; el ordinal es de registro, no de línea física. → test en T3.3.
3. **Fila de encuesta con cliente y producto inexistentes:** una fila rechazada con dos motivos, contada una vez. → test en T3.5.
4. **Cliente social vacío (NULL, válido) vs informado inexistente (rechazo):** no confundirlos. → test en T3.5.
5. **Reejecución con fuente técnica ya creada:** reutiliza su `FechaCarga`; no hay conflicto artificial. → test en T4.2 y comprobación real en T5.1.

## Estructura de archivos
```
OpinionesETL.sln
src/ETL.Entities/   Modelos.cs  Contratos.cs
src/ETL.DAL/        SesionCargaMySql.cs  FabricaSesionMySql.cs
src/ETL.BLL/        LectorCsv.cs  Normalizador.cs  ValidadorCatalogos.cs  ValidadorOpiniones.cs  CoordinadorCarga.cs  Reporte.cs
src/ETL.Consola/    Program.cs  Configuracion.cs  ResumenConsola.cs
tests/ETL.Tests/    (un archivo por clase de BLL) + SesionFalsa.cs
database/           01_crear_base_datos.sql … 05_pruebas_base_datos.sql  README.md
data/  materiales/  evidencias/  docs/  output/
```

---

## FASE 0 — Preparación

### Task 0.1: Estructura, Git local y datos
**Files:** Create `.gitignore`, `data/`, `database/`, `evidencias/`, `output/`; Move los 6 CSV de la raíz a `data/`.
- [ ] **Step 1:** Mover los 6 CSV a `data/` y calcular `shasum -a 256 data/*.csv` en `output/huellas.txt` (los originales no se editan).
- [ ] **Step 2:** `.gitignore` con las exclusiones de la guía §5 (`/AGENTS.md /CONTEXT.md /data/ /materiales/ /output/ bin/ obj/ .vs/ .env* appsettings.Local.json *.user`), sin ignorar `docs/`.
- [ ] **Step 3:** `git init -b main`; verificar `git rev-parse --show-toplevel` = carpeta del proyecto. Commit `chore: add project structure`.
- **Verifica:** `git status --short` limpio; 6 archivos en `data/`.

🛑 **PARADA 1 — Reglas de datos.** 👤 **TÚ:** confirma `CONTEXT.md` §5, en especial: rechazar encuesta si falta cliente **o** producto; Rating web → Puntaje con el mismo valor; 5 fuentes técnicas; clasificación NULL en web/redes. Si el profesor dio otra regla, dímela ahora.

---

## FASE 1 — Base de datos MySQL

### Task 1.1: Servidor y usuario
- [ ] **Step 1:** 👤 **TÚ:** elige una contraseña para el usuario `etl` y no me la pegues en el chat; la pondrás en `ETL_CONNECTION_STRING`.
- [ ] **Step 2:** `brew services start mysql`; verificar `mysql -u root -e "SELECT VERSION();"`. Si root exige contraseña o falla, detenerme y avisarte.
- [ ] **Step 3:** Crear `database/00_usuario.sql.example` (sin contraseña real) con `CREATE USER 'etl'@'localhost' IDENTIFIED BY '<PASSWORD>'; GRANT ALL ON AnalisisOpinionesClientes.* TO 'etl'@'localhost';` y ejecutarlo con tu contraseña por variable de entorno temporal.
- **Verifica:** `mysql -u etl -p -e "SELECT 1"` responde.

### Task 1.2: `database/01_crear_base_datos.sql`
Re-ejecutable (`CREATE DATABASE IF NOT EXISTS`, `CREATE TABLE IF NOT EXISTS`), `utf8mb4_0900_as_cs` o equivalente sensible a mayúsculas/acentos para comparar contenido.
- **Clientes**(`IdCliente` INT AUTO_INCREMENT PK; `IdExterno` VARCHAR(50) NOT NULL UNIQUE; `Nombre` VARCHAR(120) NOT NULL; `Email` VARCHAR(254) NULL)
- **Productos**(`IdProducto` INT PK AI; `IdExterno` VARCHAR(50) UNIQUE NOT NULL; `Nombre` VARCHAR(150) NOT NULL; `Categoria` VARCHAR(100) NOT NULL)
- **FuenteDatos**(`IdFuente` INT PK AI; `IdExterno` VARCHAR(100) UNIQUE NOT NULL; `TipoFuente` VARCHAR(50) NOT NULL; `FechaCarga` DATETIME NOT NULL)
- **Opiniones**(`IdOpinion` BIGINT PK AI; `Canal` VARCHAR(20) NOT NULL; `IdExterno` VARCHAR(50) NOT NULL; `IdCliente` INT NULL FK; `IdProducto` INT NOT NULL FK; `IdFuente` INT NOT NULL FK; `Fecha` DATETIME NOT NULL; `Comentario` MEDIUMTEXT NOT NULL; `Clasificacion` VARCHAR(10) NULL; `PuntajeSatisfaccion` TINYINT UNSIGNED NULL; `IdClienteOrigen` VARCHAR(50) NULL; `IdProductoOrigen` VARCHAR(50) NOT NULL; UNIQUE(`Canal`,`IdExterno`))
- CHECK: textos obligatorios `CHAR_LENGTH(TRIM(x)) > 0`; `Canal IN ('Web','Encuesta','RedSocial')`; `Clasificacion IS NULL OR IN ('Positiva','Neutra','Negativa')`; `PuntajeSatisfaccion IS NULL OR BETWEEN 1 AND 5`.
- [ ] **Step 1:** Escribir el script. **Step 2:** Ejecutarlo dos veces seguidas (sin error ni pérdida). **Step 3:** `SHOW CREATE TABLE Opiniones` muestra 3 FK y los CHECK.
- Commit `feat: add transactional database schema`.

### Task 1.3: `database/02_procedimientos.sql`
Contrato (todos `IN` parámetros tipados con tamaño; `OUT pId`, `OUT pEstado VARCHAR(10)` con valores `'Insertado'|'Omitido'|'Conflicto'`; transacción la abre el llamador):
- `sp_UpsertCliente(pIdExterno VARCHAR(50), pNombre VARCHAR(120), pEmail VARCHAR(254), OUT pIdCliente INT, OUT pEstado VARCHAR(10))`
- `sp_UpsertProducto(pIdExterno, pNombre VARCHAR(150), pCategoria VARCHAR(100), OUT pIdProducto INT, OUT pEstado)`
- `sp_UpsertFuente(pIdExterno VARCHAR(100), pTipoFuente VARCHAR(50), pFechaCarga DATETIME, OUT pIdFuente INT, OUT pEstado)`
- `sp_BuscarFuente(pIdExterno VARCHAR(100))` → resultset `IdFuente, TipoFuente, FechaCarga` (0 o 1 fila)
- `sp_UpsertOpinion(pCanal, pIdExterno, pIdCliente INT, pIdProducto INT, pIdFuente INT, pFecha, pComentario MEDIUMTEXT, pClasificacion, pPuntaje TINYINT UNSIGNED, pIdClienteOrigen, pIdProductoOrigen, OUT pIdOpinion BIGINT, OUT pEstado)`; lanza `SIGNAL SQLSTATE '45000'` si el `TipoFuente` no es compatible con el canal (Web↔Web, Encuesta↔CSV, RedSocial↔Red Social).
- `sp_ObtenerConteos()` → `Tabla, Total` para las 4 tablas.
- Regla: buscar por `IdExterno` (o `Canal+IdExterno`); si no existe INSERT; si existe y todos los campos coinciden con `<=>` → `Omitido`; si difieren → `Conflicto` sin modificar. Manejador `DUPLICATE KEY (1062)` que vuelve a leer la fila.
- [ ] **Step 1:** Escribir y ejecutar. **Step 2:** Probar a mano cada SP con `CALL` dentro de `START TRANSACTION; … ROLLBACK;`: insertar → `Insertado`; repetir → `Omitido`; cambiar Nombre → `Conflicto`. Commit `feat: add ETL loading procedures`.

### Task 1.4: `database/03_vistas.sql`
- `vw_OpinionesDetalle` (Opiniones + Productos + FuenteDatos + **LEFT JOIN** Clientes).
- `vw_OpinionesPorCanal` (Canal, Total, Clasificadas, Pendientes, PromedioPuntaje).
- `vw_ReferenciasHuerfanas` (conteo de opiniones con cliente/producto/fuente inexistente por LEFT JOIN anti-join).
- [ ] Crear, `SELECT * FROM` cada una sobre tablas vacías (no falla). Commit `feat: add opinion validation views`.

### Task 1.5: `database/05_pruebas_base_datos.sql` y `database/README.md`
- [ ] Pruebas dentro de `START TRANSACTION … ROLLBACK`: FK rota rechazada, CHECK de puntaje 6 rechazado, UNIQUE Canal+IdExterno rechazado, canal/tipo incompatible lanza error. Cada prueba imprime `PASS`/`FAIL`.
- [ ] README: orden 01→03→05, requisitos, cómo leer resultados.
- **Verifica:** todas `PASS`; tablas siguen vacías tras las pruebas. Commit `test: verify transactional database rules`.

🛑 **PARADA 2 — Esquema.** 👤 **TÚ:** instala DBeaver (o Workbench), conéctate como `etl`, confirma que ves 4 tablas, 3 vistas y los SPs, y toma la **captura del esquema** → `evidencias/01_esquema.png`. Revisa la lista de columnas por si algo no te cuadra.

---

## FASE 2 — Solución .NET

### Task 2.1: Cuatro capas y proyecto de pruebas
- [ ] **Step 1:** Crear con `dotnet new sln -n OpinionesETL --format sln` y los proyectos de la guía §7 (`ETL.Entities`, `ETL.DAL`, `ETL.BLL`, `ETL.Consola` con `-f net10.0`), más `dotnet new xunit -n ETL.Tests -o tests/ETL.Tests -f net10.0` referenciando Entities y BLL.
- [ ] **Step 2:** Referencias: DAL→Entities; BLL→Entities; Consola→Entities, BLL, DAL. Paquetes: `MySqlConnector` en DAL, `CsvHelper` en BLL. Registrar versiones resueltas en `CONTEXT.md`.
- [ ] **Step 3:** `dotnet build OpinionesETL.sln` → 0 errores, sin ciclos. Commit `chore: add four-layer solution`.

🛑 **PARADA 3 (breve).** Te enseño el árbol y el resultado del build; sin acción tuya salvo decir "sigue".

---

## FASE 3 — Lectura y validación (sin escribir en BD)

### Task 3.1: Modelos y contratos — `src/ETL.Entities/Modelos.cs`, `Contratos.cs`
**Produces** (namespace `ETL.Entities`):
```csharp
public enum EstadoCarga { Insertado, Omitido, Conflicto }
public readonly record struct ResultadoUpsert(long Id, EstadoCarga Estado);
public sealed record ClienteNuevo(string IdExterno, string Nombre, string? Email);
public sealed record ProductoNuevo(string IdExterno, string Nombre, string Categoria);
public sealed record FuenteNueva(string IdExterno, string TipoFuente, DateTime FechaCarga);
public sealed record OpinionNueva(string Canal, string IdExterno, int? IdCliente, int IdProducto, int IdFuente, DateTime Fecha, string Comentario, string? Clasificacion, byte? PuntajeSatisfaccion, string? IdClienteOrigen, string IdProductoOrigen);
public sealed record Rechazo(string Archivo, int Ordinal, string IdExterno, IReadOnlyList<string> Motivos);
public sealed record MetricasArchivo(string Archivo, int Procesados, int Insertados, int Omitidos, int Rechazados, int NoConfirmados);
public interface ISesionCarga : IDisposable {
    ResultadoUpsert UpsertCliente(ClienteNuevo c);
    ResultadoUpsert UpsertProducto(ProductoNuevo p);
    ResultadoUpsert UpsertFuente(FuenteNueva f);
    FuenteNueva? BuscarFuente(string idExterno);
    ResultadoUpsert UpsertOpinion(OpinionNueva o);
    IReadOnlyDictionary<string, long> ObtenerConteos();
    void Confirmar();
    void Revertir();
}
public interface IFabricaSesion { ISesionCarga Abrir(); }
```
- [ ] Escribir; `dotnet build` verde. Commit junto con T3.2.

### Task 3.2: Normalizador — `src/ETL.BLL/Normalizador.cs`, test `NormalizadorTests`
**Produces:** `static string? Externo(string? codigo)` (`"C007"`→`"7"`, `"P016"`→`"16"`, `"7"`→`"7"`, `""`/null→null, `"X"`→lanza `FormatException`); `static bool EsVacio(string?)`.
- [ ] **Step 1:** Tests: `C007→7`, `P016→16`, `W0001→1`, `"42"→"42"`, `"  C007 "→7`, `""→null`, `"abc"→FormatException`. **Step 2:** `dotnet test tests/ETL.Tests --filter NormalizadorTests` → FAIL. **Step 3:** Implementar quitando el prefijo alfabético y ceros a la izquierda. **Step 4:** PASS. Commit `feat: add external id normalizer`.

### Task 3.3: Lector CSV — `src/ETL.BLL/LectorCsv.cs`, test `LectorCsvTests`
**Produces:** `sealed record FilaCsv(int Ordinal, IReadOnlyDictionary<string,string> Campos)`; `static IReadOnlyList<FilaCsv> Leer(string ruta, string[] encabezadosRequeridos)`; excepciones `ArchivoNoEncontradoException`, `EncabezadoInvalidoException(string archivo, IReadOnlyList<string> faltantes)`, `ArchivoMalFormadoException`.
- [ ] **Step 1 (tests con archivos temporales):** (a) comentario `"hola, \"mundo\"\nlinea 2"` → 1 fila, columnas intactas, ordinal 1; (b) archivo con BOM; (c) falta `Categoría` → `EncabezadoInvalidoException` con `faltantes=["Categoría"]`; (d) ruta inexistente → `ArchivoNoEncontradoException`; (e) fila con menos columnas → `ArchivoMalFormadoException`.
- [ ] **Step 2–4:** FAIL → implementar con `CsvReader` (`InvariantCulture`, `DetectDelimiter=false`, delimitador `,`, `PrepareHeaderForMatch` solo recorte, `BadDataFound` que lanza) → PASS. Commit `feat: add CSV input reader`.

### Task 3.4: Validador de catálogos — `src/ETL.BLL/ValidadorCatalogos.cs`, test `ValidadorCatalogosTests`
**Produces:** `sealed record Validado<T>(int Ordinal, T Valor)`; `sealed record ResultadoValidacion<T>(IReadOnlyList<Validado<T>> Validos, IReadOnlyList<Rechazo> Rechazos, int Omitidos)`; `static ResultadoValidacion<ClienteNuevo> Clientes(string archivo, IReadOnlyList<FilaCsv>)`; `Productos(...)`; `Fuentes(...)` (fuente: `IdExterno` = `F001` tal cual; `FechaCarga` `yyyy-MM-dd`).
- Reglas: obligatorios no vacíos (`Nombre`, `Categoria`, `TipoFuente`); longitudes del esquema (Nombre 120/150, Email 254); Email opcional pero con formato `x@y.z` si viene; `TipoFuente ∈ {Web, CSV, Red Social}`; fecha inválida → rechazo; mismo `IdExterno` repetido idéntico → `Omitidos++`; repetido con contenido distinto → `Rechazo` motivo `"conflicto"`.
- [ ] Tests: fila válida; Nombre vacío; Email `"a@"`; fecha `2025-13-40`; id repetido idéntico (Omitidos=1, Rechazos=0); id repetido distinto (1 válido, 1 rechazo). FAIL → implementar → PASS. Commit `feat: validate catalog records`.

### Task 3.5: Validador de opiniones — `src/ETL.BLL/ValidadorOpiniones.cs`, test `ValidadorOpinionesTests`
**Consumes:** `Normalizador`, `FilaCsv`, `ResultadoValidacion<T>`. **Produces:**
```csharp
public sealed record MapasReferencia(IReadOnlyDictionary<string,int> Clientes, IReadOnlyDictionary<string,int> Productos, IReadOnlyDictionary<string,int> Fuentes);
public static ResultadoValidacion<OpinionNueva> Web(string archivo, IReadOnlyList<FilaCsv> filas, MapasReferencia m);
public static ResultadoValidacion<OpinionNueva> Encuestas(...);
public static ResultadoValidacion<OpinionNueva> Social(...);
```
`Fuentes` se indexa por `IdExterno` de las fuentes técnicas.
- Canales y fuente: Web → `Canal="Web"`, fuente `ETL:WEB:WEB_REVIEWS`; Encuestas → `"Encuesta"`, `ETL:CSV:ENCUESTA_INTERNA` (columna `Fuente` debe ser `EncuestaInterna`); Social → `"RedSocial"`, `ETL:SOCIAL:` + `Fuente.ToUpperInvariant()` con `Fuente ∈ {Instagram, Twitter, Facebook}`.
- Web: `Rating` 1–5 entero → `PuntajeSatisfaccion` = Rating, `Clasificacion=null`. Encuestas: `Clasificacion ∈ {Positiva,Neutra,Negativa}`, `PuntajeSatisfaccion` 1–5. Social: sin puntaje ni clasificación. Todos: `Fecha` `yyyy-MM-dd`, `Comentario` no vacío; `IdClienteOrigen`/`IdProductoOrigen` conservan el código recibido.
- Referencias: cliente vacío → `IdCliente=null` solo en Social; cliente informado ausente de `m.Clientes` → motivo `"cliente {cod} no existe"`; producto ausente → `"producto {cod} no existe"`; ambos → **una** `Rechazo` con 2 motivos.
- [ ] **Step 1 (tests):** web válida (Rating 4 → Puntaje 4, Clasificacion null); web Rating 6 → rechazo; encuesta con cliente `8537` ausente y producto `366` ausente → 1 rechazo con 2 motivos; encuesta válida conserva clasificación; social con cliente vacío → válida con `IdCliente=null`; social con `C999` inexistente → rechazo (no NULL); social `Fuente=TikTok` → rechazo; id repetido idéntico → omitido.
- [ ] **Step 2–4:** FAIL → implementar → PASS. Commit `feat: validate opinion references`.

### Task 3.6: Diagnóstico sin escritura — `src/ETL.BLL/CoordinadorCarga.cs` (modo diagnóstico) y `Program.cs`
**Produces:** `sealed class CoordinadorCarga(IFabricaSesion fabrica, string carpetaDatos)` con `ResultadoEjecucion Diagnosticar()` que lee y valida los 6 archivos con referencias tomadas de los catálogos válidos **sin abrir sesión** (los IDs internos se simulan con el ordinal) y devuelve `MetricasArchivo` por archivo y los `Rechazo`.
- [ ] Ejecutar `dotnet run --project src/ETL.Consola -- --diagnostico` con `ETL_DATA_DIR=data`. **Verifica:** tabla por archivo; encuestas: 6 válidas / 494 rechazadas; web 200/0; social 200/0; catálogos 500/200/100. Si difiere, **detenerme y reportar**.

🛑 **PARADA 4 — Diagnóstico.** 👤 **TÚ:** revisa la tabla de diagnóstico y 5 rechazos de ejemplo con sus motivos; confirma que los 494 rechazos te parecen razonables. Si tus compañeros o el maestro esperan otro tratamiento de esas encuestas, es el momento.

---

## FASE 4 — Carga transaccional

### Task 4.1: DAL MySQL — `src/ETL.DAL/SesionCargaMySql.cs`, `FabricaSesionMySql.cs`
**Consumes:** `ISesionCarga`, `IFabricaSesion`. **Produces:** `FabricaSesionMySql(string cadenaConexion) : IFabricaSesion`; `SesionCargaMySql` abre una `MySqlConnection` y una `MySqlTransaction` al construirse; cada método llama su SP con `CommandType.StoredProcedure`, parámetros `MySqlDbType` con tamaño y lee `OUT pEstado`/`pId`; `Confirmar()` hace `Commit`, `Revertir()` hace `Rollback`; `Dispose` revierte si no se confirmó.
- [ ] **Step 1:** Implementar. **Step 2:** Prueba de humo desde la consola (`--prueba-conexion`): `SELECT 1` y `ObtenerConteos` → 4 tablas en 0. 👤 **TÚ:** exporta `ETL_CONNECTION_STRING='Server=localhost;Database=AnalisisOpinionesClientes;User=etl;Password=<la tuya>;SslMode=Preferred'`.
- Commit `feat: add MySQL load session`.

### Task 4.2: Coordinador de carga — `CoordinadorCarga.Ejecutar()`, test `CoordinadorCargaTests` con `tests/ETL.Tests/SesionFalsa.cs`
**Produces:** `ResultadoEjecucion Ejecutar()` con `Estado ∈ {Completada, CompletadaConRechazos, Fallida}`, lista de `MetricasArchivo`, lista de `Rechazo`, `FuentesTecnicas` (métricas aparte), `ConteosFinales`, `Inicio`, `Fin`.
- Flujo: verificar existencia de los 6 archivos y encabezados **antes** de abrir sesión; abrir sesión; clientes → productos → 100 fuentes → 5 fuentes técnicas (`BuscarFuente`; si existe, reutilizar su `FechaCarga`, si no `DateTime.UtcNow` truncado a segundos) → web → encuestas → social; mapas de externo→interno solo con filas cuyo estado no fue `Conflicto`; fila rechazada no inserta; `Confirmar()` solo al final; cualquier excepción técnica → `Revertir()`, `Estado=Fallida`, todas las filas leídas pasan a `NoConfirmados`.
- [ ] **Step 1 (tests con `SesionFalsa`):** (a) carga normal → `Procesados = Insertados+Omitidos+Rechazados+NoConfirmados` en cada archivo; (b) segunda ejecución sobre la misma falsa → insertados 0, omitidos 1206, sin conflicto de fuente técnica (reutiliza fecha); (c) la falsa lanza en `UpsertOpinion` → `Revertir` llamado, `Confirmar` no, `NoConfirmados > 0`, `Estado=Fallida`; (d) catálogo en `Conflicto` → sus dependientes se rechazan, no se asocian a otra versión.
- [ ] **Step 2–4:** FAIL → implementar → PASS. Commit `feat: orchestrate transactional data load`.

### Task 4.3: Reporte, configuración y consola
**Produces:** `Reporte.Escribir(ResultadoEjecucion, string carpeta)` → `output/reporte-<UTC>.txt`, `.json` y `rechazados.csv` (archivo, ordinal, id externo, motivos; sin datos personales completos); `Configuracion.Leer()` que exige las 3 variables y falla con mensaje si falta alguna; `ResumenConsola.Mostrar(...)` con tabla por archivo, total, fuentes técnicas aparte, conteos de tablas y estado; `Program.Main` devuelve 0/2/1 y **no** usa `ReadKey`; nunca imprime la cadena completa.
- Si falla la escritura del reporte después de `Confirmar()`: informar "datos confirmados; falló el reporte", código 1, sin hablar de rollback.
- [ ] Verificar con `dotnet run` sin variables → mensaje claro y código 1 (`echo $?`). Commit `feat: add ETL report and console summary`.

### Task 4.4: Primera ejecución real
- [ ] `ETL_DATA_DIR`, `ETL_REPORT_DIR` y `ETL_CONNECTION_STRING` definidas; `dotnet run --project src/ETL.Consola`. Comparar con las cifras previstas de **Global Constraints**; cualquier diferencia se investiga y se explica, no se maquilla.
- [ ] `sp_ObtenerConteos` coincide con el resumen. Guardar la salida en `output/`.

🛑 **PARADA 5 — Primera carga.** 👤 **TÚ:** mira la consola y abre las tablas en DBeaver (`Opiniones`, `Clientes`…). **Captura 1:** consola completa → `evidencias/02_primera_ejecucion.png`. **Captura 2:** `SELECT COUNT(*)` de cada tabla → `evidencias/03_tablas_pobladas.png`. Dime si algo no cuadra.

---

## FASE 5 — Reejecución, fallos y pruebas

### Task 5.1: Segunda ejecución idéntica
- [ ] Ejecutar de nuevo sin tocar nada. **Esperado:** 0 insertados, 1206 omitidos, 494 rechazados, mismas cuentas de tablas, ninguna fuente técnica nueva, exit code 2. Guardar salida.

### Task 5.2: Fallos controlados
- [ ] (a) Copiar `data/` a `output/data_malo/` y quitar el encabezado `Categoría` de `products.csv`: error claro, **0 filas escritas**, exit 1. (b) Renombrar un CSV en la copia: error claro antes de cargar. (c) Rollback real: crear un usuario `etl_sin_opiniones` sin permiso `EXECUTE` sobre `sp_UpsertOpinion`, ejecutar el ETL con él sobre una BD vacía y comprobar estado `Fallida`, exit 1 y conteos de las 4 tablas en 0 (los catálogos insertados antes del fallo se revirtieron). No se modifica ni la BD ni el esquema real.
- Los datos originales en `data/` no se modifican.

### Task 5.3: Suite completa
- [ ] `dotnet test OpinionesETL.sln` → todo verde; registrar el comando y el resultado en `CONTEXT.md`. Commit `test: verify repeated ETL execution`.

🛑 **PARADA 6 — Robustez.** 👤 **TÚ:** revisa los resultados de 5.1 y 5.2. **Captura 3:** segunda ejecución → `evidencias/04_segunda_ejecucion.png`. **Captura 4:** error por encabezado faltante → `evidencias/05_error_encabezado.png`.

---

## FASE 6 — Consultas de validación y evidencias

### Task 6.1: `database/04_consultas_validacion.sql`
Nueve consultas, cada una con comentario `-- Demuestra:` y resultado esperado, ejecutadas **después** de la carga:
1. Conteo por tabla (`sp_ObtenerConteos`) vs resumen. 2. Opiniones con cliente informado inexistente (vacío). 3. Opiniones con producto inexistente (vacío). 4. Opiniones con fuente inexistente (vacío). 5. `Canal+IdExterno` repetidos (vacío). 6. Totales, clasificadas y pendientes por canal (`vw_OpinionesPorCanal`). 7. Detalle con nombres de producto y cliente conservando clientes NULL (`vw_OpinionesDetalle`). 8. Compatibilidad canal/tipo de fuente (vacío). 9. FK existentes y validadas (`information_schema.REFERENTIAL_CONSTRAINTS` / `TABLE_CONSTRAINTS`).
- [ ] Ejecutar y guardar la salida real en `output/validacion.txt`. Interpretar cada resultado en `database/README.md` (vacío en tablas pobladas **sí** es prueba; en tablas vacías no). Commit `test: add SQL validation queries`.

🛑 **PARADA 7 — Evidencias.** 👤 **TÚ:** en DBeaver, ejecuta `04_consultas_validacion.sql` y toma una captura por consulta (o por grupo legible) → `evidencias/06_validacion_*.png`; captura de `SELECT * FROM vw_OpinionesDetalle LIMIT 20` y de `vw_OpinionesPorCanal`. Revisa que no aparezcan contraseñas.

---

## FASE 7 — Entrega

### Task 7.1: README, copia limpia y paquete
- [ ] `README.md`: .NET objetivo, requisitos MySQL, variables, orden de scripts, comando de ejecución, códigos de salida, cómo leer el resumen, limitaciones (494 encuestas rechazadas porque el CSV referencia clientes/productos ausentes de los catálogos; Rating→Puntaje es decisión propia; fuentes técnicas no recuperan la relación con las 100 fuentes originales).
- [ ] Copia limpia: clonar la carpeta a `output/copia_limpia/` (sin `bin/obj`, sin secretos) y correr `dotnet restore`, `dotnet build`, `dotnet run` con variables externas. Debe reproducir los números.
- [ ] Armar `EntregaCargaETL/` con `README.md`, `src/`, `tests/`, `database/`, `docs/`, `evidencias/`, `data/`. Excluir `bin/`, `obj/`, secretos, `output/`. Commit `docs: explain ETL execution`.

🛑 **PARADA FINAL.** 👤 **TÚ:** revisa el paquete, súbelo donde indique el maestro y confirma qué falta. Si el maestro pide otro nombre de carpetas (`CAPTURAS/`, `SCRIPTS/`, `src/`), lo renombro aquí.

---

## Auto-revisión
- **Cobertura del mandato:** leer/validar estructura (T3.3), formato/obligatorios/tipos (T3.4–3.5), duplicados (T3.4–3.5, T1.3), relaciones (T1.2, T3.5, T4.2), excepciones (T3.3, T4.2, T5.2), métricas y resumen (T4.3), scripts BD con SPs y vistas (T1.2–1.4), evidencias (paradas 2, 5, 6, 7), ≥5 consultas (T6.1).
- **Consistencia de tipos:** `ISesionCarga`, `ResultadoUpsert`, `Rechazo`, `MetricasArchivo`, `ResultadoValidacion<T>` y `MapasReferencia` se definen una vez (T3.1/T3.4/T3.5) y se usan con los mismos nombres después.
- **Supuestos del agente (no del usuario):** Rating→Puntaje idéntico; `FechaCarga` técnica en UTC al segundo; nombre de BD `AnalisisOpinionesClientes`; fuente de encuesta rechazada si `Fuente ≠ EncuestaInterna`.
