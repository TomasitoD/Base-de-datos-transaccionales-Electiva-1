using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CsvHelper;
using ETL.Entities;

namespace ETL.BLL;

public static class Reporte
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ConstruirTexto(ResultadoEjecucion resultado)
    {
        var texto = new StringBuilder();
        texto.AppendLine("ETL de Opiniones - Resumen de ejecucion");
        texto.AppendLine($"Estado: {resultado.Estado}");
        texto.AppendLine($"Inicio (UTC): {resultado.Inicio:yyyy-MM-dd HH:mm:ss}");
        texto.AppendLine($"Fin (UTC): {resultado.Fin:yyyy-MM-dd HH:mm:ss}");
        if (resultado.Error is not null)
        {
            texto.AppendLine($"Error: {resultado.Error}");
        }

        texto.AppendLine();
        AgregarTabla(texto, resultado.Metricas, true);
        texto.AppendLine();
        texto.AppendLine("Fuentes tecnicas (aparte):");
        AgregarTabla(texto, resultado.FuentesTecnicas, false);
        AgregarConteos(texto, resultado);
        texto.AppendLine();
        texto.AppendLine($"Rechazos: {resultado.Rechazos.Count}");
        return texto.ToString();
    }

    public static IReadOnlyList<string> Escribir(ResultadoEjecucion resultado, string carpeta)
    {
        Directory.CreateDirectory(carpeta);
        var marca = resultado.Fin.ToUniversalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var rutaTexto = Path.Combine(carpeta, $"reporte-{marca}.txt");
        var rutaJson = Path.Combine(carpeta, $"reporte-{marca}.json");
        var rutaRechazados = Path.Combine(carpeta, $"rechazados-{marca}.csv");

        File.WriteAllText(rutaTexto, ConstruirTexto(resultado), new UTF8Encoding(false));
        File.WriteAllText(rutaJson, JsonSerializer.Serialize(resultado, OpcionesJson), new UTF8Encoding(false));
        EscribirRechazados(resultado.Rechazos, rutaRechazados);
        return [rutaTexto, rutaJson, rutaRechazados];
    }

    private static void EscribirRechazados(IReadOnlyList<Rechazo> rechazos, string ruta)
    {
        using var escritor = new StreamWriter(ruta, false, new UTF8Encoding(false));
        using var csv = new CsvWriter(escritor, CultureInfo.InvariantCulture);
        foreach (var encabezado in new[] { "Archivo", "Ordinal", "IdExterno", "Motivos" })
        {
            csv.WriteField(encabezado);
        }

        csv.NextRecord();
        foreach (var rechazo in rechazos)
        {
            csv.WriteField(rechazo.Archivo);
            csv.WriteField(rechazo.Ordinal.ToString(CultureInfo.InvariantCulture));
            csv.WriteField(rechazo.IdExterno);
            csv.WriteField(string.Join(" | ", rechazo.Motivos));
            csv.NextRecord();
        }
    }

    private static void AgregarTabla(StringBuilder texto, IReadOnlyList<MetricasArchivo> metricas, bool conTotal)
    {
        texto.AppendLine($"{"Archivo",-22}{"Procesados",11}{"Insertados",11}{"Omitidos",10}{"Rechazados",11}{"NoConfirmados",14}");
        foreach (var m in metricas)
        {
            texto.AppendLine(Fila(m.Archivo, m.Procesados, m.Insertados, m.Omitidos, m.Rechazados, m.NoConfirmados));
        }

        if (conTotal)
        {
            texto.AppendLine(Fila("TOTAL", metricas.Sum(m => m.Procesados), metricas.Sum(m => m.Insertados),
                metricas.Sum(m => m.Omitidos), metricas.Sum(m => m.Rechazados), metricas.Sum(m => m.NoConfirmados)));
        }
    }

    private static string Fila(string nombre, int procesados, int insertados, int omitidos, int rechazados, int noConfirmados)
    {
        return $"{nombre,-22}{procesados,11}{insertados,11}{omitidos,10}{rechazados,11}{noConfirmados,14}";
    }

    private static void AgregarConteos(StringBuilder texto, ResultadoEjecucion resultado)
    {
        if (resultado.ConteosFinales.Count == 0)
        {
            return;
        }

        texto.AppendLine();
        texto.AppendLine("Conteos en la base de datos:");
        foreach (var conteo in resultado.ConteosFinales)
        {
            texto.AppendLine($"  {conteo.Key,-14}{conteo.Value,6}");
        }
    }
}
