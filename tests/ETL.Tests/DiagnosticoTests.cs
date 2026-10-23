using ETL.BLL;
using ETL.Entities;

namespace ETL.Tests;

public class DiagnosticoTests : IDisposable
{
    private sealed class FabricaProhibida : IFabricaSesion
    {
        public ISesionCarga Abrir()
        {
            throw new InvalidOperationException("El diagnostico no debe abrir sesion");
        }
    }

    private readonly CarpetaDatos _datos = new();

    public void Dispose()
    {
        _datos.Dispose();
    }

    [Fact]
    public void Diagnosticar_cuenta_validos_y_rechazos_por_archivo_sin_abrir_sesion()
    {
        var resultado = new CoordinadorCarga(new FabricaProhibida(), _datos.Ruta).Diagnosticar();

        var porArchivo = resultado.Metricas.ToDictionary(m => m.Archivo);
        Assert.Equal(2, porArchivo["clients.csv"].NoConfirmados);
        Assert.Equal(1, porArchivo["products.csv"].NoConfirmados);
        Assert.Equal(1, porArchivo["fuente_datos.csv"].NoConfirmados);
        Assert.Equal(1, porArchivo["web_reviews.csv"].NoConfirmados);
        Assert.Equal(2, porArchivo["surveys_part1.csv"].Procesados);
        Assert.Equal(1, porArchivo["surveys_part1.csv"].NoConfirmados);
        Assert.Equal(1, porArchivo["surveys_part1.csv"].Rechazados);
        Assert.Equal(2, porArchivo["social_comments.csv"].NoConfirmados);
        Assert.All(resultado.Metricas, m => Assert.Equal(0, m.Insertados));
        Assert.Equal(EstadoEjecucion.CompletadaConRechazos, resultado.Estado);
        Assert.Equal(2, resultado.Rechazos.Count);
        Assert.Equal(1, porArchivo["web_reviews.csv"].Rechazados);
        Assert.Equal(2, resultado.Rechazos.Single(r => r.Archivo == "surveys_part1.csv").Motivos.Count);
    }

    [Fact]
    public void Diagnosticar_con_archivo_faltante_lanza_ArchivoNoEncontradoException()
    {
        File.Delete(Path.Combine(_datos.Ruta, "products.csv"));

        Assert.Throws<ArchivoNoEncontradoException>(
            () => new CoordinadorCarga(new FabricaProhibida(), _datos.Ruta).Diagnosticar());
    }
}
