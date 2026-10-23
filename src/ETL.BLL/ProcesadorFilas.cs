using System.Globalization;
using ETL.Entities;

namespace ETL.BLL;

internal static class ProcesadorFilas
{
    public static ResultadoValidacion<T> Procesar<T>(
        string archivo,
        IReadOnlyList<FilaCsv> filas,
        Func<FilaCsv, List<string>, (string IdExterno, T? Valor)> evaluar,
        Func<T, string> clave) where T : class
    {
        var validos = new List<Validado<T>>();
        var rechazos = new List<Rechazo>();
        var vistos = new Dictionary<string, T>(StringComparer.Ordinal);
        var omitidos = 0;

        foreach (var fila in filas)
        {
            var motivos = new List<string>();
            var (idExterno, valor) = evaluar(fila, motivos);
            if (motivos.Count > 0 || valor is null)
            {
                rechazos.Add(new Rechazo(archivo, fila.Ordinal, idExterno, motivos));
                continue;
            }

            var k = clave(valor);
            if (!vistos.TryGetValue(k, out var previo))
            {
                vistos[k] = valor;
                validos.Add(new Validado<T>(fila.Ordinal, valor));
            }
            else if (previo.Equals(valor))
            {
                omitidos++;
            }
            else
            {
                rechazos.Add(new Rechazo(archivo, fila.Ordinal, idExterno, ["conflicto"]));
            }
        }

        return new ResultadoValidacion<T>(validos, rechazos, omitidos);
    }
}

internal static class Campos
{
    public static string Texto(FilaCsv fila, string columna)
    {
        return fila.Campos.TryGetValue(columna, out var valor) ? valor.Trim() : string.Empty;
    }

    public static string Obligatorio(FilaCsv fila, string columna, string etiqueta, int maximo, List<string> motivos)
    {
        var texto = Texto(fila, columna);
        if (texto.Length == 0)
        {
            motivos.Add($"{etiqueta} vacío");
        }
        else if (texto.Length > maximo)
        {
            motivos.Add($"{etiqueta} excede {maximo} caracteres");
        }

        return texto;
    }

    public static string IdExterno(FilaCsv fila, string columna, List<string> motivos)
    {
        var texto = Texto(fila, columna);
        try
        {
            var normalizado = Normalizador.Externo(texto);
            if (normalizado is null)
            {
                motivos.Add($"{columna} vacío");
                return texto;
            }

            return normalizado;
        }
        catch (FormatException)
        {
            motivos.Add($"{columna} inválido: '{texto}'");
            return texto;
        }
    }

    public static DateTime Fecha(FilaCsv fila, string columna, List<string> motivos)
    {
        var texto = Texto(fila, columna);
        if (DateTime.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
        {
            return fecha;
        }

        motivos.Add($"{columna} inválida, se espera yyyy-MM-dd: '{texto}'");
        return default;
    }
}
