using ETL.BLL;
using ETL.Entities;

namespace ETL.Tests;

public class CoordinadorCargaTests : IDisposable
{
    private static readonly DateTime Ahora = new(2026, 10, 23, 20, 15, 42, 789);
    private static readonly DateTime AhoraTruncado = new(2026, 10, 23, 20, 15, 42);

    private readonly CarpetaDatos _datos = new();
    private readonly AlmacenFalso _almacen = new();

    public void Dispose()
    {
        _datos.Dispose();
    }

    private CoordinadorCarga Crear(FabricaFalsa fabrica, DateTime? ahora = null)
    {
        return new CoordinadorCarga(fabrica, _datos.Ruta, () => ahora ?? Ahora);
    }

    private static MetricasArchivo De(ResultadoEjecucion r, string archivo)
    {
        return r.Metricas.Single(m => m.Archivo == archivo);
    }

    private static void AssertIdentidad(MetricasArchivo m)
    {
        Assert.Equal(m.Procesados, m.Insertados + m.Omitidos + m.Rechazados + m.NoConfirmados);
    }

    [Fact]
    public void Carga_normal_cumple_la_identidad_de_metricas_en_cada_archivo()
    {
        var fabrica = new FabricaFalsa(_almacen);

        var r = Crear(fabrica).Ejecutar();

        Assert.Equal(EstadoEjecucion.CompletadaConRechazos, r.Estado);
        Assert.Equal(6, r.Metricas.Count);
        Assert.All(r.Metricas, AssertIdentidad);
        Assert.Equal(2, De(r, "clients.csv").Insertados);
        Assert.Equal(1, De(r, "products.csv").Insertados);
        Assert.Equal(1, De(r, "fuente_datos.csv").Insertados);
        Assert.Equal(1, De(r, "web_reviews.csv").Insertados);
        Assert.Equal(1, De(r, "web_reviews.csv").Rechazados);
        Assert.Equal(1, De(r, "surveys_part1.csv").Insertados);
        Assert.Equal(1, De(r, "surveys_part1.csv").Rechazados);
        Assert.Equal(2, De(r, "social_comments.csv").Insertados);
        Assert.Equal(2, r.Rechazos.Count);
        Assert.True(fabrica.Sesiones.Single().Confirmada);
        Assert.False(fabrica.Sesiones.Single().Revertida);
    }

    [Fact]
    public void Fuentes_tecnicas_se_informan_aparte_y_no_entran_en_las_metricas_de_archivos()
    {
        var r = Crear(new FabricaFalsa(_almacen)).Ejecutar();

        var tecnicas = Assert.Single(r.FuentesTecnicas);
        Assert.Equal(5, tecnicas.Procesados);
        Assert.Equal(5, tecnicas.Insertados);
        AssertIdentidad(tecnicas);
        Assert.Equal(6, r.Metricas.Count);
        Assert.Equal(2, r.ConteosFinales["Clientes"]);
        Assert.Equal(1, r.ConteosFinales["Productos"]);
        Assert.Equal(6, r.ConteosFinales["FuenteDatos"]);
        Assert.Equal(4, r.ConteosFinales["Opiniones"]);
    }

    [Fact]
    public void Fuente_tecnica_nueva_usa_la_hora_actual_truncada_a_segundos()
    {
        Crear(new FabricaFalsa(_almacen)).Ejecutar();

        Assert.Equal(AhoraTruncado, _almacen.Fuentes[FuentesTecnicas.Web].Valor.FechaCarga);
    }

    [Fact]
    public void Las_opiniones_usan_ids_internos_de_la_sesion_y_no_los_externos()
    {
        var fabrica = new FabricaFalsa(_almacen);

        Crear(fabrica).Ejecutar();

        var web = fabrica.Sesiones.Single().OpinionesRecibidas.Single(o => o.Canal == "Web");
        Assert.Equal(_almacen.Clientes["7"].Id, web.IdCliente);
        Assert.NotEqual(7, web.IdCliente);
        Assert.Equal(_almacen.Productos["16"].Id, web.IdProducto);
        Assert.Equal(_almacen.Fuentes[FuentesTecnicas.Web].Id, web.IdFuente);
        Assert.Equal("C007", web.IdClienteOrigen);
    }

    [Fact]
    public void Segunda_ejecucion_no_inserta_omite_todo_y_reutiliza_la_fecha_de_las_fuentes_tecnicas()
    {
        Crear(new FabricaFalsa(_almacen)).Ejecutar();

        var r = Crear(new FabricaFalsa(_almacen), Ahora.AddHours(3)).Ejecutar();

        Assert.Equal(0, r.Metricas.Sum(m => m.Insertados));
        Assert.Equal(8, r.Metricas.Sum(m => m.Omitidos));
        Assert.Equal(2, r.Metricas.Sum(m => m.Rechazados));
        Assert.All(r.Metricas, AssertIdentidad);
        var tecnicas = Assert.Single(r.FuentesTecnicas);
        Assert.Equal(0, tecnicas.Insertados);
        Assert.Equal(5, tecnicas.Omitidos);
        Assert.Equal(0, tecnicas.Rechazados);
        Assert.Equal(AhoraTruncado, _almacen.Fuentes[FuentesTecnicas.Web].Valor.FechaCarga);
        Assert.Equal(EstadoEjecucion.CompletadaConRechazos, r.Estado);
    }

    [Fact]
    public void Fallo_tecnico_revierte_no_confirma_y_deja_todo_como_no_confirmado()
    {
        var fabrica = new FabricaFalsa(_almacen, operacion => operacion == "UpsertOpinion");

        var r = Crear(fabrica).Ejecutar();

        var sesion = fabrica.Sesiones.Single();
        Assert.True(sesion.Revertida);
        Assert.False(sesion.Confirmada);
        Assert.Equal(EstadoEjecucion.Fallida, r.Estado);
        Assert.All(r.Metricas, m =>
        {
            Assert.True(m.NoConfirmados > 0);
            Assert.Equal(m.Procesados, m.NoConfirmados);
            AssertIdentidad(m);
        });
        Assert.Empty(_almacen.Clientes);
        Assert.Empty(_almacen.Opiniones);
    }

    [Fact]
    public void Catalogo_en_conflicto_rechaza_a_sus_dependientes_sin_asociarlos_a_otra_version()
    {
        var existente = new ClienteNuevo("7", "Otro nombre", "ana@mail.com");
        _almacen.Clientes["7"] = (555, existente);
        var fabrica = new FabricaFalsa(_almacen);

        var r = Crear(fabrica).Ejecutar();

        var clientes = De(r, "clients.csv");
        Assert.Equal(1, clientes.Insertados);
        Assert.Equal(1, clientes.Rechazados);
        Assert.DoesNotContain(fabrica.Sesiones.Single().OpinionesRecibidas, o => o.IdCliente == 555);
        Assert.Equal(0, De(r, "web_reviews.csv").Insertados);
        Assert.Contains(r.Rechazos, x => x.Archivo == "web_reviews.csv" && x.IdExterno == "W0001"
            && x.Motivos.Contains("cliente C007 no existe"));
        Assert.All(r.Metricas, AssertIdentidad);
    }

    [Fact]
    public void Opinion_existente_con_contenido_distinto_se_rechaza_como_conflicto()
    {
        _almacen.Opiniones[("Web", "W0001")] = (777, new OpinionNueva(
            "Web", "W0001", 1, 1, 1, new DateTime(2024, 10, 23), "Otro comentario", null, (byte)4, "C007", "P016"));

        var r = Crear(new FabricaFalsa(_almacen)).Ejecutar();

        var web = De(r, "web_reviews.csv");
        Assert.Equal(0, web.Insertados);
        Assert.Equal(2, web.Rechazados);
        AssertIdentidad(web);
        Assert.Contains(r.Rechazos, x => x.IdExterno == "W0001" && x.Motivos.Any(m => m.Contains("contenido distinto")));
    }

    [Fact]
    public void Archivo_faltante_falla_antes_de_abrir_sesion()
    {
        File.Delete(Path.Combine(_datos.Ruta, "products.csv"));
        var fabrica = new FabricaFalsa(_almacen);

        Assert.Throws<ArchivoNoEncontradoException>(() => Crear(fabrica).Ejecutar());

        Assert.Empty(fabrica.Sesiones);
    }

    [Fact]
    public void Encabezado_faltante_falla_antes_de_abrir_sesion()
    {
        _datos.Escribir("products.csv", "IdProducto,Nombre,Categoria\n16,Mouse,Perifericos\n");
        var fabrica = new FabricaFalsa(_almacen);

        var ex = Assert.Throws<EncabezadoInvalidoException>(() => Crear(fabrica).Ejecutar());

        Assert.Equal(["Categoría"], ex.Faltantes);
        Assert.Empty(fabrica.Sesiones);
    }
}
