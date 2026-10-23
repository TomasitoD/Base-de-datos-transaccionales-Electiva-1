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
    DateTime Fin);

public sealed class CoordinadorCarga(IFabricaSesion fabrica, string carpetaDatos)
{
    public const string ArchivoClientes = "clients.csv";
    public const string ArchivoProductos = "products.csv";
    public const string ArchivoFuentes = "fuente_datos.csv";
    public const string ArchivoWeb = "web_reviews.csv";
    public const string ArchivoEncuestas = "surveys_part1.csv";
    public const string ArchivoSocial = "social_comments.csv";

    private readonly IFabricaSesion _fabrica = fabrica;

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
