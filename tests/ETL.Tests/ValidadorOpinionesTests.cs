using ETL.BLL;

namespace ETL.Tests;

public class ValidadorOpinionesTests
{
    private static readonly MapasReferencia Mapas = new(
        new Dictionary<string, int> { ["7"] = 101, ["19"] = 102 },
        new Dictionary<string, int> { ["16"] = 201, ["3"] = 202 },
        new Dictionary<string, int>
        {
            ["ETL:WEB:WEB_REVIEWS"] = 301,
            ["ETL:CSV:ENCUESTA_INTERNA"] = 302,
            ["ETL:SOCIAL:INSTAGRAM"] = 303,
            ["ETL:SOCIAL:TWITTER"] = 304,
            ["ETL:SOCIAL:FACEBOOK"] = 305
        });

    private static FilaCsv Fila(int ordinal, params (string Campo, string Valor)[] campos)
    {
        return new FilaCsv(ordinal, campos.ToDictionary(c => c.Campo, c => c.Valor));
    }

    private static FilaCsv Web(int ordinal, string id = "W0001", string cliente = "C007", string producto = "P016",
        string fecha = "2024-10-23", string comentario = "Muy bueno", string rating = "4")
    {
        return Fila(ordinal, ("IdReview", id), ("IdCliente", cliente), ("IdProducto", producto),
            ("Fecha", fecha), ("Comentario", comentario), ("Rating", rating));
    }

    private static FilaCsv Encuesta(int ordinal, string id = "1", string cliente = "7", string producto = "16",
        string fecha = "2025-07-15", string comentario = "Esta bien", string clasificacion = "Neutra",
        string puntaje = "3", string fuente = "EncuestaInterna")
    {
        return Fila(ordinal, ("IdOpinion", id), ("IdCliente", cliente), ("IdProducto", producto),
            ("Fecha", fecha), ("Comentario", comentario), ("Clasificación", clasificacion),
            ("PuntajeSatisfacción", puntaje), ("Fuente", fuente));
    }

    private static FilaCsv Social(int ordinal, string id = "T0001", string cliente = "C019", string producto = "P003",
        string fuente = "Instagram", string fecha = "2025-06-15", string comentario = "Regular")
    {
        return Fila(ordinal, ("IdComment", id), ("IdCliente", cliente), ("IdProducto", producto),
            ("Fuente", fuente), ("Fecha", fecha), ("Comentario", comentario));
    }

    [Fact]
    public void Web_valida_convierte_rating_en_puntaje_y_deja_clasificacion_null()
    {
        var r = ValidadorOpiniones.Web("web_reviews.csv", [Web(1)], Mapas);

        var o = Assert.Single(r.Validos).Valor;
        Assert.Equal("Web", o.Canal);
        Assert.Equal("W0001", o.IdExterno);
        Assert.Equal(101, o.IdCliente);
        Assert.Equal(201, o.IdProducto);
        Assert.Equal(301, o.IdFuente);
        Assert.Equal(new DateTime(2024, 10, 23), o.Fecha);
        Assert.Equal((byte)4, o.PuntajeSatisfaccion);
        Assert.Null(o.Clasificacion);
        Assert.Equal("C007", o.IdClienteOrigen);
        Assert.Equal("P016", o.IdProductoOrigen);
    }

    [Theory]
    [InlineData("6")]
    [InlineData("0")]
    [InlineData("abc")]
    [InlineData("")]
    public void Web_con_rating_fuera_de_rango_o_invalido_se_rechaza(string rating)
    {
        var r = ValidadorOpiniones.Web("web_reviews.csv", [Web(1, rating: rating)], Mapas);

        var rechazo = Assert.Single(r.Rechazos);
        Assert.Contains(rechazo.Motivos, m => m.Contains("Rating"));
        Assert.Empty(r.Validos);
    }

    [Fact]
    public void Web_con_cliente_vacio_se_rechaza()
    {
        var r = ValidadorOpiniones.Web("web_reviews.csv", [Web(1, cliente: "")], Mapas);

        var rechazo = Assert.Single(r.Rechazos);
        Assert.Contains(rechazo.Motivos, m => m.Contains("IdCliente"));
    }

    [Fact]
    public void Web_con_fecha_invalida_o_comentario_vacio_se_rechaza_con_ambos_motivos()
    {
        var r = ValidadorOpiniones.Web("web_reviews.csv", [Web(1, fecha: "2024-02-30", comentario: " ")], Mapas);

        var rechazo = Assert.Single(r.Rechazos);
        Assert.Equal(2, rechazo.Motivos.Count);
    }

    [Fact]
    public void Encuesta_con_cliente_y_producto_ausentes_es_un_solo_rechazo_con_dos_motivos()
    {
        var r = ValidadorOpiniones.Encuestas("surveys_part1.csv", [Encuesta(1, cliente: "8537", producto: "366")], Mapas);

        Assert.Empty(r.Validos);
        var rechazo = Assert.Single(r.Rechazos);
        Assert.Equal(2, rechazo.Motivos.Count);
        Assert.Contains("cliente 8537 no existe", rechazo.Motivos);
        Assert.Contains("producto 366 no existe", rechazo.Motivos);
    }

    [Fact]
    public void Encuesta_valida_conserva_clasificacion_y_puntaje()
    {
        var r = ValidadorOpiniones.Encuestas("surveys_part1.csv", [Encuesta(1, clasificacion: "Positiva", puntaje: "5")], Mapas);

        var o = Assert.Single(r.Validos).Valor;
        Assert.Equal("Encuesta", o.Canal);
        Assert.Equal("Positiva", o.Clasificacion);
        Assert.Equal((byte)5, o.PuntajeSatisfaccion);
        Assert.Equal(302, o.IdFuente);
        Assert.Equal("7", o.IdClienteOrigen);
        Assert.Equal("16", o.IdProductoOrigen);
    }

    [Theory]
    [InlineData("positiva", "3")]
    [InlineData("Excelente", "3")]
    [InlineData("", "3")]
    [InlineData("Neutra", "0")]
    [InlineData("Neutra", "6")]
    [InlineData("Neutra", "")]
    public void Encuesta_con_clasificacion_o_puntaje_invalido_se_rechaza(string clasificacion, string puntaje)
    {
        var r = ValidadorOpiniones.Encuestas("surveys_part1.csv",
            [Encuesta(1, clasificacion: clasificacion, puntaje: puntaje)], Mapas);

        Assert.Single(r.Rechazos);
        Assert.Empty(r.Validos);
    }

    [Fact]
    public void Encuesta_con_fuente_distinta_de_EncuestaInterna_se_rechaza()
    {
        var r = ValidadorOpiniones.Encuestas("surveys_part1.csv", [Encuesta(1, fuente: "Otra")], Mapas);

        var rechazo = Assert.Single(r.Rechazos);
        Assert.Contains(rechazo.Motivos, m => m.Contains("Fuente"));
    }

    [Fact]
    public void Social_con_cliente_vacio_es_valida_con_cliente_null()
    {
        var r = ValidadorOpiniones.Social("social_comments.csv", [Social(1, cliente: "")], Mapas);

        var o = Assert.Single(r.Validos).Valor;
        Assert.Null(o.IdCliente);
        Assert.Null(o.IdClienteOrigen);
        Assert.Equal("RedSocial", o.Canal);
        Assert.Null(o.PuntajeSatisfaccion);
        Assert.Null(o.Clasificacion);
        Assert.Equal(303, o.IdFuente);
    }

    [Fact]
    public void Social_con_cliente_informado_inexistente_se_rechaza_y_no_se_convierte_en_null()
    {
        var r = ValidadorOpiniones.Social("social_comments.csv", [Social(1, cliente: "C999")], Mapas);

        Assert.Empty(r.Validos);
        var rechazo = Assert.Single(r.Rechazos);
        Assert.Equal(["cliente C999 no existe"], rechazo.Motivos);
    }

    [Fact]
    public void Social_con_fuente_no_permitida_se_rechaza()
    {
        var r = ValidadorOpiniones.Social("social_comments.csv", [Social(1, fuente: "TikTok")], Mapas);

        var rechazo = Assert.Single(r.Rechazos);
        Assert.Contains(rechazo.Motivos, m => m.Contains("Fuente"));
    }

    [Theory]
    [InlineData("Instagram", 303)]
    [InlineData("Twitter", 304)]
    [InlineData("Facebook", 305)]
    public void Social_asigna_la_fuente_tecnica_de_su_red(string fuente, int esperado)
    {
        var r = ValidadorOpiniones.Social("social_comments.csv", [Social(1, fuente: fuente)], Mapas);

        Assert.Equal(esperado, Assert.Single(r.Validos).Valor.IdFuente);
    }

    [Fact]
    public void Social_con_producto_vacio_se_rechaza()
    {
        var r = ValidadorOpiniones.Social("social_comments.csv", [Social(1, producto: "")], Mapas);

        var rechazo = Assert.Single(r.Rechazos);
        Assert.Contains(rechazo.Motivos, m => m.Contains("IdProducto"));
    }

    [Fact]
    public void Id_repetido_identico_se_cuenta_como_omitido()
    {
        var r = ValidadorOpiniones.Web("web_reviews.csv", [Web(1), Web(2)], Mapas);

        Assert.Single(r.Validos);
        Assert.Empty(r.Rechazos);
        Assert.Equal(1, r.Omitidos);
    }

    [Fact]
    public void Id_repetido_con_contenido_distinto_es_conflicto()
    {
        var r = ValidadorOpiniones.Web("web_reviews.csv", [Web(1), Web(2, comentario: "Otro")], Mapas);

        Assert.Single(r.Validos);
        var rechazo = Assert.Single(r.Rechazos);
        Assert.Equal(["conflicto"], rechazo.Motivos);
        Assert.Equal(2, rechazo.Ordinal);
    }

    [Fact]
    public void Rechazo_conserva_archivo_ordinal_e_id_externo()
    {
        var r = ValidadorOpiniones.Web("web_reviews.csv", [Web(1), Web(5, id: "W0005", rating: "9")], Mapas);

        var rechazo = Assert.Single(r.Rechazos);
        Assert.Equal("web_reviews.csv", rechazo.Archivo);
        Assert.Equal(5, rechazo.Ordinal);
        Assert.Equal("W0005", rechazo.IdExterno);
    }
}
