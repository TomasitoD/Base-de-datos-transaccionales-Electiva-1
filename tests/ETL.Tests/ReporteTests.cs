using System.Text.Json;
using ETL.BLL;
using ETL.Entities;

namespace ETL.Tests;

public class ReporteTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "etl-reporte-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_carpeta))
        {
            Directory.Delete(_carpeta, true);
        }
    }

    private static ResultadoEjecucion Resultado(EstadoEjecucion estado = EstadoEjecucion.CompletadaConRechazos, string? error = null)
    {
        var inicio = new DateTime(2026, 10, 23, 20, 0, 0, DateTimeKind.Utc);
        return new ResultadoEjecucion(
            estado,
            [
                new MetricasArchivo("clients.csv", 500, 500, 0, 0, 0),
                new MetricasArchivo("surveys_part1.csv", 500, 6, 0, 494, 0)
            ],
            [
                new Rechazo("surveys_part1.csv", 1, "1", ["cliente 8537 no existe", "producto 366 no existe"]),
                new Rechazo("surveys_part1.csv", 2, "2", ["motivo con, coma y \"comillas\""])
            ],
            [new MetricasArchivo("fuentes_tecnicas", 5, 5, 0, 0, 0)],
            new Dictionary<string, long> { ["Clientes"] = 500, ["Opiniones"] = 6 },
            inicio,
            inicio.AddSeconds(3),
            error);
    }

    [Fact]
    public void Texto_incluye_estado_metricas_totales_fuentes_tecnicas_y_conteos()
    {
        var texto = Reporte.ConstruirTexto(Resultado());

        Assert.Contains("CompletadaConRechazos", texto);
        Assert.Contains("clients.csv", texto);
        Assert.Contains("surveys_part1.csv", texto);
        Assert.Contains("TOTAL", texto);
        Assert.Contains("fuentes_tecnicas", texto);
        Assert.Contains("Clientes", texto);
        Assert.Contains("Rechazos: 2", texto);
    }

    [Fact]
    public void Texto_de_ejecucion_fallida_muestra_el_error_y_no_afirma_conteos()
    {
        var fallida = Resultado(EstadoEjecucion.Fallida, "Fallo simulado") with
        {
            ConteosFinales = new Dictionary<string, long>()
        };

        var texto = Reporte.ConstruirTexto(fallida);

        Assert.Contains("Fallida", texto);
        Assert.Contains("Fallo simulado", texto);
        Assert.DoesNotContain("Conteos en la base de datos", texto);
    }

    [Fact]
    public void Escribir_crea_txt_json_y_csv_de_rechazados_en_la_carpeta()
    {
        var rutas = Reporte.Escribir(Resultado(), _carpeta);

        Assert.Equal(3, rutas.Count);
        Assert.All(rutas, ruta => Assert.True(File.Exists(ruta)));
        Assert.Contains(rutas, r => Path.GetFileName(r).StartsWith("reporte-") && r.EndsWith(".txt"));
        Assert.Contains(rutas, r => Path.GetFileName(r).StartsWith("reporte-") && r.EndsWith(".json"));
        Assert.Contains(rutas, r => Path.GetFileName(r).StartsWith("rechazados-") && r.EndsWith(".csv"));
    }

    [Fact]
    public void Json_es_valido_y_conserva_estado_y_metricas()
    {
        var rutas = Reporte.Escribir(Resultado(), _carpeta);

        using var documento = JsonDocument.Parse(File.ReadAllText(rutas.Single(r => r.EndsWith(".json"))));
        var raiz = documento.RootElement;
        Assert.Equal("CompletadaConRechazos", raiz.GetProperty("Estado").GetString());
        Assert.Equal(2, raiz.GetProperty("Metricas").GetArrayLength());
        Assert.Equal(494, raiz.GetProperty("Metricas")[1].GetProperty("Rechazados").GetInt32());
    }

    [Fact]
    public void Csv_de_rechazados_tiene_encabezado_y_escapa_comas_y_comillas()
    {
        var rutas = Reporte.Escribir(Resultado(), _carpeta);

        var lineas = File.ReadAllLines(rutas.Single(r => r.EndsWith(".csv")));
        Assert.Equal("Archivo,Ordinal,IdExterno,Motivos", lineas[0]);
        Assert.Equal("surveys_part1.csv,1,1,cliente 8537 no existe | producto 366 no existe", lineas[1]);
        Assert.Equal("surveys_part1.csv,2,2,\"motivo con, coma y \"\"comillas\"\"\"", lineas[2]);
    }

    [Fact]
    public void Escribir_crea_la_carpeta_si_no_existe()
    {
        Assert.False(Directory.Exists(_carpeta));

        Reporte.Escribir(Resultado(), _carpeta);

        Assert.True(Directory.Exists(_carpeta));
    }
}
