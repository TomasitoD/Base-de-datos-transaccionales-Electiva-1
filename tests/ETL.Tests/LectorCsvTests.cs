using System.Text;
using ETL.BLL;

namespace ETL.Tests;

public class LectorCsvTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "etl-tests-" + Guid.NewGuid().ToString("N"));

    public LectorCsvTests()
    {
        Directory.CreateDirectory(_carpeta);
    }

    public void Dispose()
    {
        Directory.Delete(_carpeta, true);
    }

    private string Crear(string contenido, bool conBom = false)
    {
        var ruta = Path.Combine(_carpeta, Guid.NewGuid().ToString("N") + ".csv");
        File.WriteAllText(ruta, contenido, new UTF8Encoding(conBom));
        return ruta;
    }

    [Fact]
    public void Comentario_con_comas_comillas_y_salto_de_linea_es_una_sola_fila()
    {
        var ruta = Crear("IdReview,Comentario,Rating\nW0001,\"hola, \"\"mundo\"\"\nlinea 2\",4\nW0002,otro,5\n");

        var filas = LectorCsv.Leer(ruta, ["IdReview", "Comentario", "Rating"]);

        Assert.Equal(2, filas.Count);
        Assert.Equal(1, filas[0].Ordinal);
        Assert.Equal("W0001", filas[0].Campos["IdReview"]);
        Assert.Equal("hola, \"mundo\"\nlinea 2", filas[0].Campos["Comentario"]);
        Assert.Equal("4", filas[0].Campos["Rating"]);
        Assert.Equal(2, filas[1].Ordinal);
    }

    [Fact]
    public void Archivo_con_BOM_y_encabezados_con_tilde_se_lee_por_nombre()
    {
        var ruta = Crear("IdProducto,Nombre,Categoría\nP001,Mouse,Periféricos\n", conBom: true);

        var filas = LectorCsv.Leer(ruta, ["IdProducto", "Nombre", "Categoría"]);

        Assert.Single(filas);
        Assert.Equal("P001", filas[0].Campos["IdProducto"]);
        Assert.Equal("Periféricos", filas[0].Campos["Categoría"]);
    }

    [Fact]
    public void Falta_un_encabezado_lanza_EncabezadoInvalidoException_con_el_faltante()
    {
        var ruta = Crear("IdProducto,Nombre,Categoria\nP001,Mouse,Perifericos\n");

        var ex = Assert.Throws<EncabezadoInvalidoException>(
            () => LectorCsv.Leer(ruta, ["IdProducto", "Nombre", "Categoría"]));

        Assert.Equal(["Categoría"], ex.Faltantes);
    }

    [Fact]
    public void Ruta_inexistente_lanza_ArchivoNoEncontradoException()
    {
        var ruta = Path.Combine(_carpeta, "no-existe.csv");

        Assert.Throws<ArchivoNoEncontradoException>(() => LectorCsv.Leer(ruta, ["IdProducto"]));
    }

    [Fact]
    public void Fila_con_menos_columnas_lanza_ArchivoMalFormadoException()
    {
        var ruta = Crear("IdProducto,Nombre,Categoria\nP001,Mouse\n");

        Assert.Throws<ArchivoMalFormadoException>(
            () => LectorCsv.Leer(ruta, ["IdProducto", "Nombre", "Categoria"]));
    }

    [Fact]
    public void Fila_con_mas_columnas_lanza_ArchivoMalFormadoException()
    {
        var ruta = Crear("IdProducto,Nombre\nP001,Mouse,sobra\n");

        Assert.Throws<ArchivoMalFormadoException>(() => LectorCsv.Leer(ruta, ["IdProducto", "Nombre"]));
    }

    [Fact]
    public void Comillas_sin_cerrar_lanzan_ArchivoMalFormadoException()
    {
        var ruta = Crear("IdProducto,Nombre\nP001,\"Mouse\nP002,Teclado\n");

        Assert.Throws<ArchivoMalFormadoException>(() => LectorCsv.Leer(ruta, ["IdProducto", "Nombre"]));
    }
}
