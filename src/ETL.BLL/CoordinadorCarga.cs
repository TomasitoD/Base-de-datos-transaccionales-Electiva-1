using ETL.Entities;

namespace ETL.BLL;

public enum EstadoEjecucion { Completada, CompletadaConRechazos, Fallida }

public sealed record ResultadoEjecucion(
    EstadoEjecucion Estado,
    IReadOnlyList<MetricasArchivo> Metricas,
    IReadOnlyList<Rechazo> Rechazos,
    IReadOnlyList<MetricasArchivo> FuentesTecnicas,
    IReadOnlyDictionary<string, long> ConteosFinales,
    DateTime Inicio,
    DateTime Fin,
    string? Error = null);

public sealed class CoordinadorCarga(IFabricaSesion fabrica, string carpetaDatos, Func<DateTime>? reloj = null)
{
    public const string ArchivoClientes = "clients.csv";
    public const string ArchivoProductos = "products.csv";
    public const string ArchivoFuentes = "fuente_datos.csv";
    public const string ArchivoWeb = "web_reviews.csv";
    public const string ArchivoEncuestas = "surveys_part1.csv";
    public const string ArchivoSocial = "social_comments.csv";
    public const string NombreFuentesTecnicas = "fuentes_tecnicas";

    private readonly IFabricaSesion _fabrica = fabrica;
    private readonly Func<DateTime> _reloj = reloj ?? (() => DateTime.UtcNow);

    private static readonly string[] EncabezadosClientes = ["IdCliente", "Nombre", "Email"];
    private static readonly string[] EncabezadosProductos = ["IdProducto", "Nombre", "Categoría"];
    private static readonly string[] EncabezadosFuentes = ["IdFuente", "TipoFuente", "FechaCarga"];
    private static readonly string[] EncabezadosWeb = ["IdReview", "IdCliente", "IdProducto", "Fecha", "Comentario", "Rating"];
    private static readonly string[] EncabezadosEncuestas =
        ["IdOpinion", "IdCliente", "IdProducto", "Fecha", "Comentario", "Clasificación", "PuntajeSatisfacción", "Fuente"];
    private static readonly string[] EncabezadosSocial = ["IdComment", "IdCliente", "IdProducto", "Fuente", "Fecha", "Comentario"];

    private sealed record ArchivosLeidos(
        IReadOnlyList<FilaCsv> Clientes,
        IReadOnlyList<FilaCsv> Productos,
        IReadOnlyList<FilaCsv> Fuentes,
        IReadOnlyList<FilaCsv> Web,
        IReadOnlyList<FilaCsv> Encuestas,
        IReadOnlyList<FilaCsv> Social);

    public ResultadoEjecucion Ejecutar()
    {
        var inicio = DateTime.UtcNow;
        var archivos = LeerArchivos();

        ISesionCarga? sesion = null;
        try
        {
            sesion = _fabrica.Abrir();
            var resultado = Cargar(sesion, archivos, inicio);
            sesion.Confirmar();
            return resultado;
        }
        catch (Exception ex)
        {
            Revertir(sesion);
            return CrearFallida(archivos, inicio, ex);
        }
        finally
        {
            sesion?.Dispose();
        }
    }

    private ResultadoEjecucion Cargar(ISesionCarga sesion, ArchivosLeidos archivos, DateTime inicio)
    {
        var rechazos = new List<Rechazo>();
        var metricas = new List<MetricasArchivo>();

        var mapaClientes = new Dictionary<string, int>(StringComparer.Ordinal);
        var mapaProductos = new Dictionary<string, int>(StringComparer.Ordinal);
        var mapaFuentes = new Dictionary<string, int>(StringComparer.Ordinal);

        metricas.Add(CargarCatalogo(ArchivoClientes, archivos.Clientes.Count,
            ValidadorCatalogos.Clientes(ArchivoClientes, archivos.Clientes),
            c => c.IdExterno, sesion.UpsertCliente, "cliente", mapaClientes, rechazos));
        metricas.Add(CargarCatalogo(ArchivoProductos, archivos.Productos.Count,
            ValidadorCatalogos.Productos(ArchivoProductos, archivos.Productos),
            p => p.IdExterno, sesion.UpsertProducto, "producto", mapaProductos, rechazos));
        metricas.Add(CargarCatalogo(ArchivoFuentes, archivos.Fuentes.Count,
            ValidadorCatalogos.Fuentes(ArchivoFuentes, archivos.Fuentes),
            f => f.IdExterno, sesion.UpsertFuente, "fuente", new Dictionary<string, int>(), rechazos));
        var tecnicas = CargarFuentesTecnicas(sesion, mapaFuentes, rechazos);

        var mapas = new MapasReferencia(mapaClientes, mapaProductos, mapaFuentes);
        metricas.Add(CargarOpiniones(ArchivoWeb, archivos.Web.Count,
            ValidadorOpiniones.Web(ArchivoWeb, archivos.Web, mapas), sesion, rechazos));
        metricas.Add(CargarOpiniones(ArchivoEncuestas, archivos.Encuestas.Count,
            ValidadorOpiniones.Encuestas(ArchivoEncuestas, archivos.Encuestas, mapas), sesion, rechazos));
        metricas.Add(CargarOpiniones(ArchivoSocial, archivos.Social.Count,
            ValidadorOpiniones.Social(ArchivoSocial, archivos.Social, mapas), sesion, rechazos));

        var estado = rechazos.Count == 0 ? EstadoEjecucion.Completada : EstadoEjecucion.CompletadaConRechazos;
        return new ResultadoEjecucion(estado, metricas, rechazos, [tecnicas], sesion.ObtenerConteos(), inicio, DateTime.UtcNow);
    }

    private static MetricasArchivo CargarCatalogo<T>(
        string archivo,
        int procesados,
        ResultadoValidacion<T> validacion,
        Func<T, string> idDe,
        Func<T, ResultadoUpsert> upsert,
        string entidad,
        Dictionary<string, int> mapa,
        List<Rechazo> rechazos)
    {
        rechazos.AddRange(validacion.Rechazos);
        var insertados = 0;
        var omitidos = validacion.Omitidos;
        var rechazados = validacion.Rechazos.Count;

        foreach (var valido in validacion.Validos)
        {
            var id = idDe(valido.Valor);
            var resultado = upsert(valido.Valor);
            switch (resultado.Estado)
            {
                case EstadoCarga.Insertado:
                    insertados++;
                    mapa[id] = (int)resultado.Id;
                    break;
                case EstadoCarga.Omitido:
                    omitidos++;
                    mapa[id] = (int)resultado.Id;
                    break;
                default:
                    rechazados++;
                    rechazos.Add(new Rechazo(archivo, valido.Ordinal, id,
                        [$"{entidad} {id} ya existe con contenido distinto"]));
                    break;
            }
        }

        return new MetricasArchivo(archivo, procesados, insertados, omitidos, rechazados, 0);
    }

    private MetricasArchivo CargarFuentesTecnicas(
        ISesionCarga sesion, Dictionary<string, int> mapaFuentes, List<Rechazo> rechazos)
    {
        var insertados = 0;
        var omitidos = 0;
        var rechazados = 0;
        var ordinal = 0;

        foreach (var (idExterno, tipoFuente) in FuentesTecnicas.Todas)
        {
            ordinal++;
            var fecha = sesion.BuscarFuente(idExterno)?.FechaCarga ?? TruncarASegundos(_reloj());
            var resultado = sesion.UpsertFuente(new FuenteNueva(idExterno, tipoFuente, fecha));
            switch (resultado.Estado)
            {
                case EstadoCarga.Insertado:
                    insertados++;
                    mapaFuentes[idExterno] = (int)resultado.Id;
                    break;
                case EstadoCarga.Omitido:
                    omitidos++;
                    mapaFuentes[idExterno] = (int)resultado.Id;
                    break;
                default:
                    rechazados++;
                    rechazos.Add(new Rechazo(NombreFuentesTecnicas, ordinal, idExterno,
                        [$"fuente {idExterno} ya existe con contenido distinto"]));
                    break;
            }
        }

        return new MetricasArchivo(NombreFuentesTecnicas, FuentesTecnicas.Todas.Count, insertados, omitidos, rechazados, 0);
    }

    private static MetricasArchivo CargarOpiniones(
        string archivo,
        int procesados,
        ResultadoValidacion<OpinionNueva> validacion,
        ISesionCarga sesion,
        List<Rechazo> rechazos)
    {
        rechazos.AddRange(validacion.Rechazos);
        var insertados = 0;
        var omitidos = validacion.Omitidos;
        var rechazados = validacion.Rechazos.Count;

        foreach (var valido in validacion.Validos)
        {
            var resultado = sesion.UpsertOpinion(valido.Valor);
            switch (resultado.Estado)
            {
                case EstadoCarga.Insertado:
                    insertados++;
                    break;
                case EstadoCarga.Omitido:
                    omitidos++;
                    break;
                default:
                    rechazados++;
                    rechazos.Add(new Rechazo(archivo, valido.Ordinal, valido.Valor.IdExterno,
                        [$"{valido.Valor.Canal} {valido.Valor.IdExterno} ya existe con contenido distinto"]));
                    break;
            }
        }

        return new MetricasArchivo(archivo, procesados, insertados, omitidos, rechazados, 0);
    }

    private static void Revertir(ISesionCarga? sesion)
    {
        try
        {
            sesion?.Revertir();
        }
        catch (Exception)
        {
        }
    }

    private static ResultadoEjecucion CrearFallida(ArchivosLeidos archivos, DateTime inicio, Exception ex)
    {
        var metricas = new List<MetricasArchivo>
        {
            SinConfirmar(ArchivoClientes, archivos.Clientes.Count),
            SinConfirmar(ArchivoProductos, archivos.Productos.Count),
            SinConfirmar(ArchivoFuentes, archivos.Fuentes.Count),
            SinConfirmar(ArchivoWeb, archivos.Web.Count),
            SinConfirmar(ArchivoEncuestas, archivos.Encuestas.Count),
            SinConfirmar(ArchivoSocial, archivos.Social.Count)
        };
        var tecnicas = SinConfirmar(NombreFuentesTecnicas, FuentesTecnicas.Todas.Count);
        return new ResultadoEjecucion(EstadoEjecucion.Fallida, metricas, [], [tecnicas],
            new Dictionary<string, long>(), inicio, DateTime.UtcNow, ex.Message);
    }

    private static MetricasArchivo SinConfirmar(string archivo, int procesados)
    {
        return new MetricasArchivo(archivo, procesados, 0, 0, 0, procesados);
    }

    private static DateTime TruncarASegundos(DateTime fecha)
    {
        return new DateTime(fecha.Ticks - fecha.Ticks % TimeSpan.TicksPerSecond);
    }

    public ResultadoEjecucion Diagnosticar()
    {
        var inicio = DateTime.UtcNow;
        var archivos = LeerArchivos();

        var clientes = ValidadorCatalogos.Clientes(ArchivoClientes, archivos.Clientes);
        var productos = ValidadorCatalogos.Productos(ArchivoProductos, archivos.Productos);
        var fuentes = ValidadorCatalogos.Fuentes(ArchivoFuentes, archivos.Fuentes);
        var mapas = new MapasReferencia(
            SimularIds(clientes.Validos.Select(v => v.Valor.IdExterno)),
            SimularIds(productos.Validos.Select(v => v.Valor.IdExterno)),
            SimularIds(FuentesTecnicas.Todas.Select(f => f.IdExterno)));

        var web = ValidadorOpiniones.Web(ArchivoWeb, archivos.Web, mapas);
        var encuestas = ValidadorOpiniones.Encuestas(ArchivoEncuestas, archivos.Encuestas, mapas);
        var social = ValidadorOpiniones.Social(ArchivoSocial, archivos.Social, mapas);

        var metricas = new List<MetricasArchivo>
        {
            Resumir(ArchivoClientes, archivos.Clientes.Count, clientes),
            Resumir(ArchivoProductos, archivos.Productos.Count, productos),
            Resumir(ArchivoFuentes, archivos.Fuentes.Count, fuentes),
            Resumir(ArchivoWeb, archivos.Web.Count, web),
            Resumir(ArchivoEncuestas, archivos.Encuestas.Count, encuestas),
            Resumir(ArchivoSocial, archivos.Social.Count, social)
        };
        var rechazos = clientes.Rechazos.Concat(productos.Rechazos).Concat(fuentes.Rechazos)
            .Concat(web.Rechazos).Concat(encuestas.Rechazos).Concat(social.Rechazos).ToList();

        var estado = rechazos.Count == 0 ? EstadoEjecucion.Completada : EstadoEjecucion.CompletadaConRechazos;
        return new ResultadoEjecucion(estado, metricas, rechazos, [], new Dictionary<string, long>(), inicio, DateTime.UtcNow);
    }

    private ArchivosLeidos LeerArchivos()
    {
        return new ArchivosLeidos(
            Leer(ArchivoClientes, EncabezadosClientes),
            Leer(ArchivoProductos, EncabezadosProductos),
            Leer(ArchivoFuentes, EncabezadosFuentes),
            Leer(ArchivoWeb, EncabezadosWeb),
            Leer(ArchivoEncuestas, EncabezadosEncuestas),
            Leer(ArchivoSocial, EncabezadosSocial));
    }

    private IReadOnlyList<FilaCsv> Leer(string archivo, string[] encabezados)
    {
        return LectorCsv.Leer(Path.Combine(carpetaDatos, archivo), encabezados);
    }

    private static Dictionary<string, int> SimularIds(IEnumerable<string> idsExternos)
    {
        var mapa = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var id in idsExternos)
        {
            mapa[id] = mapa.Count + 1;
        }

        return mapa;
    }

    private static MetricasArchivo Resumir<T>(string archivo, int procesados, ResultadoValidacion<T> r)
    {
        return new MetricasArchivo(archivo, procesados, 0, r.Omitidos, r.Rechazos.Count, r.Validos.Count);
    }
}
