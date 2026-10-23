namespace ETL.Entities;

public enum EstadoCarga { Insertado, Omitido, Conflicto }

public readonly record struct ResultadoUpsert(long Id, EstadoCarga Estado);

public sealed record ClienteNuevo(string IdExterno, string Nombre, string? Email);

public sealed record ProductoNuevo(string IdExterno, string Nombre, string Categoria);

public sealed record FuenteNueva(string IdExterno, string TipoFuente, DateTime FechaCarga);

public sealed record OpinionNueva(
    string Canal,
    string IdExterno,
    int? IdCliente,
    int IdProducto,
    int IdFuente,
    DateTime Fecha,
    string Comentario,
    string? Clasificacion,
    byte? PuntajeSatisfaccion,
    string? IdClienteOrigen,
    string IdProductoOrigen);

public sealed record Rechazo(string Archivo, int Ordinal, string IdExterno, IReadOnlyList<string> Motivos);

public sealed record MetricasArchivo(
    string Archivo,
    int Procesados,
    int Insertados,
    int Omitidos,
    int Rechazados,
    int NoConfirmados);
