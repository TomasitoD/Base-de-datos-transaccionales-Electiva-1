using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace ETL.BLL;

public sealed record FilaCsv(int Ordinal, IReadOnlyDictionary<string, string> Campos);

public class ArchivoNoEncontradoException(string ruta)
    : Exception($"No se encontro el archivo '{ruta}'")
{
    public string Ruta { get; } = ruta;
}

public class EncabezadoInvalidoException(string archivo, IReadOnlyList<string> faltantes)
    : Exception($"El archivo '{archivo}' no tiene los encabezados requeridos: {string.Join(", ", faltantes)}")
{
    public string Archivo { get; } = archivo;
    public IReadOnlyList<string> Faltantes { get; } = faltantes;
}

public class ArchivoMalFormadoException(string archivo, string detalle)
    : Exception($"El archivo '{archivo}' esta mal formado: {detalle}")
{
    public string Archivo { get; } = archivo;
}

public static class LectorCsv
{
    public static IReadOnlyList<FilaCsv> Leer(string ruta, string[] encabezadosRequeridos)
    {
        if (!File.Exists(ruta))
        {
            throw new ArchivoNoEncontradoException(ruta);
        }

        var archivo = Path.GetFileName(ruta);
        try
        {
            using var lector = new StreamReader(ruta, new UTF8Encoding(false), true);
            using var csv = new CsvReader(lector, CrearConfiguracion(archivo));
            var encabezados = LeerEncabezados(csv, archivo, encabezadosRequeridos);
            return LeerFilas(csv, archivo, encabezados);
        }
        catch (CsvHelperException ex)
        {
            throw new ArchivoMalFormadoException(archivo, ex.Message);
        }
    }

    private static CsvConfiguration CrearConfiguracion(string archivo)
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            DetectDelimiter = false,
            Delimiter = ",",
            PrepareHeaderForMatch = argumentos => argumentos.Header.Trim(),
            BadDataFound = argumentos =>
                throw new ArchivoMalFormadoException(archivo, $"datos mal formados cerca de: {argumentos.RawRecord.Trim()}")
        };
    }

    private static string[] LeerEncabezados(CsvReader csv, string archivo, string[] requeridos)
    {
        if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is null)
        {
            throw new EncabezadoInvalidoException(archivo, requeridos);
        }

        var encabezados = csv.HeaderRecord.Select(e => e.Trim()).ToArray();
        if (encabezados.Distinct(StringComparer.Ordinal).Count() != encabezados.Length)
        {
            throw new ArchivoMalFormadoException(archivo, "hay encabezados repetidos");
        }

        var faltantes = requeridos.Where(r => !encabezados.Contains(r, StringComparer.Ordinal)).ToList();
        if (faltantes.Count > 0)
        {
            throw new EncabezadoInvalidoException(archivo, faltantes);
        }

        return encabezados;
    }

    private static List<FilaCsv> LeerFilas(CsvReader csv, string archivo, string[] encabezados)
    {
        var filas = new List<FilaCsv>();
        while (csv.Read())
        {
            var ordinal = filas.Count + 1;
            if (csv.Parser.Count != encabezados.Length)
            {
                throw new ArchivoMalFormadoException(
                    archivo,
                    $"el registro {ordinal} tiene {csv.Parser.Count} columnas y se esperaban {encabezados.Length}");
            }

            var campos = new Dictionary<string, string>(encabezados.Length, StringComparer.Ordinal);
            for (var i = 0; i < encabezados.Length; i++)
            {
                campos[encabezados[i]] = csv.GetField(i) ?? string.Empty;
            }

            filas.Add(new FilaCsv(ordinal, campos));
        }

        return filas;
    }
}
