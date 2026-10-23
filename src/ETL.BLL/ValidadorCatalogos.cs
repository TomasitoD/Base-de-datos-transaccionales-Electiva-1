using System.Text.RegularExpressions;
using ETL.Entities;

namespace ETL.BLL;

public sealed record Validado<T>(int Ordinal, T Valor);

public sealed record ResultadoValidacion<T>(
    IReadOnlyList<Validado<T>> Validos,
    IReadOnlyList<Rechazo> Rechazos,
    int Omitidos);

public static partial class ValidadorCatalogos
{
    private static readonly string[] TiposFuente = ["Web", "CSV", "Red Social"];

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex PatronEmail();

    public static ResultadoValidacion<ClienteNuevo> Clientes(string archivo, IReadOnlyList<FilaCsv> filas)
    {
        return ProcesadorFilas.Procesar<ClienteNuevo>(archivo, filas, EvaluarCliente, c => c.IdExterno);
    }

    public static ResultadoValidacion<ProductoNuevo> Productos(string archivo, IReadOnlyList<FilaCsv> filas)
    {
        return ProcesadorFilas.Procesar<ProductoNuevo>(archivo, filas, EvaluarProducto, p => p.IdExterno);
    }

    public static ResultadoValidacion<FuenteNueva> Fuentes(string archivo, IReadOnlyList<FilaCsv> filas)
    {
        return ProcesadorFilas.Procesar<FuenteNueva>(archivo, filas, EvaluarFuente, f => f.IdExterno);
    }

    private static (string, ClienteNuevo?) EvaluarCliente(FilaCsv fila, List<string> motivos)
    {
        var id = Campos.IdExterno(fila, "IdCliente", motivos);
        var nombre = Campos.Obligatorio(fila, "Nombre", "Nombre", 120, motivos);
        var email = Campos.Texto(fila, "Email");
        if (email.Length > 0 && (email.Length > 254 || !PatronEmail().IsMatch(email)))
        {
            motivos.Add($"Email inválido: '{email}'");
        }

        return (id, motivos.Count == 0 ? new ClienteNuevo(id, nombre, email.Length == 0 ? null : email) : null);
    }

    private static (string, ProductoNuevo?) EvaluarProducto(FilaCsv fila, List<string> motivos)
    {
        var id = Campos.IdExterno(fila, "IdProducto", motivos);
        var nombre = Campos.Obligatorio(fila, "Nombre", "Nombre", 150, motivos);
        var categoria = Campos.Obligatorio(fila, "Categoría", "Categoria", 100, motivos);
        return (id, motivos.Count == 0 ? new ProductoNuevo(id, nombre, categoria) : null);
    }

    private static (string, FuenteNueva?) EvaluarFuente(FilaCsv fila, List<string> motivos)
    {
        var id = Campos.Obligatorio(fila, "IdFuente", "IdFuente", 100, motivos);
        var tipo = Campos.Obligatorio(fila, "TipoFuente", "TipoFuente", 50, motivos);
        if (tipo.Length > 0 && !TiposFuente.Contains(tipo, StringComparer.Ordinal))
        {
            motivos.Add($"TipoFuente no permitido: '{tipo}'");
        }

        var fecha = Campos.Fecha(fila, "FechaCarga", motivos);
        return (id, motivos.Count == 0 ? new FuenteNueva(id, tipo, fecha) : null);
    }
}
