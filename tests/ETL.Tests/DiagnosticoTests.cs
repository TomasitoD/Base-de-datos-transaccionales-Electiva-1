using System.Text;
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

    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "etl-diagnostico-" + Guid.NewGuid().ToString("N"));

    public DiagnosticoTests()
    {
        Directory.CreateDirectory(_carpeta);
        Escribir("clients.csv", "IdCliente,Nombre,Email\n7,Ana,ana@mail.com\n8,Luis,\n");
        Escribir("products.csv", "IdProducto,Nombre,Categoría\n16,Mouse,Periféricos\n");
        Escribir("fuente_datos.csv", "IdFuente,TipoFuente,FechaCarga\nF001,Web,2025-04-10\n");
        Escribir("web_reviews.csv", "IdReview,IdCliente,IdProducto,Fecha,Comentario,Rating\nW0001,C007,P016,2024-10-23,Bien,4\n");
        Escribir("surveys_part1.csv",
            "IdOpinion,IdCliente,IdProducto,Fecha,Comentario,Clasificación,PuntajeSatisfacción,Fuente\n"
            + "1,8537,366,2025-07-15,Regular,Neutra,3,EncuestaInterna\n"
            + "2,7,16,2025-07-16,Bueno,Positiva,5,EncuestaInterna\n");
        Escribir("social_comments.csv", "IdComment,IdCliente,IdProducto,Fuente,Fecha,Comentario\nT0001,,P016,Twitter,2025-06-15,Hola\n");
    }

    public void Dispose()
    {
        Directory.Delete(_carpeta, true);
    }

    private void Escribir(string nombre, string contenido)
    {
        File.WriteAllText(Path.Combine(_carpeta, nombre), contenido, new UTF8Encoding(false));
    }

    [Fact]
    public void Diagnosticar_cuenta_validos_y_rechazos_por_archivo_sin_abrir_sesion()
    {
        var resultado = new CoordinadorCarga(new FabricaProhibida(), _carpeta).Diagnosticar();

        var porArchivo = resultado.Metricas.ToDictionary(m => m.Archivo);
        Assert.Equal(2, porArchivo["clients.csv"].NoConfirmados);
        Assert.Equal(1, porArchivo["products.csv"].NoConfirmados);
        Assert.Equal(1, porArchivo["fuente_datos.csv"].NoConfirmados);
        Assert.Equal(1, porArchivo["web_reviews.csv"].NoConfirmados);
        Assert.Equal(2, porArchivo["surveys_part1.csv"].Procesados);
        Assert.Equal(1, porArchivo["surveys_part1.csv"].NoConfirmados);
        Assert.Equal(1, porArchivo["surveys_part1.csv"].Rechazados);
        Assert.Equal(1, porArchivo["social_comments.csv"].NoConfirmados);
        Assert.All(resultado.Metricas, m => Assert.Equal(0, m.Insertados));
        Assert.Equal(EstadoEjecucion.CompletadaConRechazos, resultado.Estado);
        var rechazo = Assert.Single(resultado.Rechazos);
        Assert.Equal(2, rechazo.Motivos.Count);
    }

    [Fact]
    public void Diagnosticar_con_archivo_faltante_lanza_ArchivoNoEncontradoException()
    {
        File.Delete(Path.Combine(_carpeta, "products.csv"));

        Assert.Throws<ArchivoNoEncontradoException>(
            () => new CoordinadorCarga(new FabricaProhibida(), _carpeta).Diagnosticar());
    }
}
