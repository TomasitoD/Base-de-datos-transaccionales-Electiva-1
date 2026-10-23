using ETL.BLL;

namespace ETL.Tests;

public class ValidadorCatalogosTests
{
    private static FilaCsv Fila(int ordinal, params (string Campo, string Valor)[] campos)
    {
        return new FilaCsv(ordinal, campos.ToDictionary(c => c.Campo, c => c.Valor));
    }

    private static FilaCsv Cliente(int ordinal, string id, string nombre, string email)
    {
        return Fila(ordinal, ("IdCliente", id), ("Nombre", nombre), ("Email", email));
    }

    private static FilaCsv Producto(int ordinal, string id, string nombre, string categoria)
    {
        return Fila(ordinal, ("IdProducto", id), ("Nombre", nombre), ("Categoría", categoria));
    }

    private static FilaCsv Fuente(int ordinal, string id, string tipo, string fecha)
    {
        return Fila(ordinal, ("IdFuente", id), ("TipoFuente", tipo), ("FechaCarga", fecha));
    }

    [Fact]
    public void Cliente_valido_se_acepta_con_id_externo_normalizado()
    {
        var r = ValidadorCatalogos.Clientes("clients.csv", [Cliente(1, "007", "Ana", "ana@mail.com")]);

        var valido = Assert.Single(r.Validos);
        Assert.Equal(1, valido.Ordinal);
        Assert.Equal("7", valido.Valor.IdExterno);
        Assert.Equal("Ana", valido.Valor.Nombre);
        Assert.Equal("ana@mail.com", valido.Valor.Email);
        Assert.Empty(r.Rechazos);
    }

    [Fact]
    public void Cliente_con_email_vacio_es_valido_con_email_null()
    {
        var r = ValidadorCatalogos.Clientes("clients.csv", [Cliente(1, "1", "Ana", "")]);

        Assert.Null(Assert.Single(r.Validos).Valor.Email);
    }

    [Fact]
    public void Cliente_con_nombre_vacio_se_rechaza_con_motivo()
    {
        var r = ValidadorCatalogos.Clientes("clients.csv", [Cliente(3, "1", "  ", "a@b.co")]);

        Assert.Empty(r.Validos);
        var rechazo = Assert.Single(r.Rechazos);
        Assert.Equal("clients.csv", rechazo.Archivo);
        Assert.Equal(3, rechazo.Ordinal);
        Assert.Equal("1", rechazo.IdExterno);
        Assert.Contains(rechazo.Motivos, m => m.Contains("Nombre"));
    }

    [Fact]
    public void Cliente_con_email_incompleto_se_rechaza()
    {
        var r = ValidadorCatalogos.Clientes("clients.csv", [Cliente(1, "1", "Ana", "a@")]);

        var rechazo = Assert.Single(r.Rechazos);
        Assert.Contains(rechazo.Motivos, m => m.Contains("Email"));
    }

    [Fact]
    public void Cliente_con_nombre_mas_largo_que_el_esquema_se_rechaza()
    {
        var r = ValidadorCatalogos.Clientes("clients.csv", [Cliente(1, "1", new string('a', 121), "")]);

        Assert.Single(r.Rechazos);
    }

    [Fact]
    public void Cliente_con_id_invalido_se_rechaza()
    {
        var r = ValidadorCatalogos.Clientes("clients.csv", [Cliente(1, "abc", "Ana", "")]);

        var rechazo = Assert.Single(r.Rechazos);
        Assert.Contains(rechazo.Motivos, m => m.Contains("IdCliente"));
    }

    [Fact]
    public void Id_repetido_identico_se_cuenta_como_omitido()
    {
        var r = ValidadorCatalogos.Clientes("clients.csv",
            [Cliente(1, "1", "Ana", "a@b.co"), Cliente(2, "1", "Ana", "a@b.co")]);

        Assert.Single(r.Validos);
        Assert.Empty(r.Rechazos);
        Assert.Equal(1, r.Omitidos);
    }

    [Fact]
    public void Id_repetido_con_contenido_distinto_se_rechaza_como_conflicto()
    {
        var r = ValidadorCatalogos.Clientes("clients.csv",
            [Cliente(1, "1", "Ana", "a@b.co"), Cliente(2, "1", "Otra", "a@b.co")]);

        var valido = Assert.Single(r.Validos);
        Assert.Equal(1, valido.Ordinal);
        var rechazo = Assert.Single(r.Rechazos);
        Assert.Equal(2, rechazo.Ordinal);
        Assert.Contains("conflicto", rechazo.Motivos);
        Assert.Equal(0, r.Omitidos);
    }

    [Fact]
    public void Producto_valido_usa_la_categoria_con_tilde()
    {
        var r = ValidadorCatalogos.Productos("products.csv", [Producto(1, "16", "Mouse", "Periféricos")]);

        var valido = Assert.Single(r.Validos);
        Assert.Equal("16", valido.Valor.IdExterno);
        Assert.Equal("Periféricos", valido.Valor.Categoria);
    }

    [Fact]
    public void Producto_sin_categoria_se_rechaza()
    {
        var r = ValidadorCatalogos.Productos("products.csv", [Producto(1, "16", "Mouse", "")]);

        var rechazo = Assert.Single(r.Rechazos);
        Assert.Contains(rechazo.Motivos, m => m.Contains("Categoria"));
    }

    [Fact]
    public void Fuente_valida_conserva_el_id_tal_cual_y_la_fecha()
    {
        var r = ValidadorCatalogos.Fuentes("fuente_datos.csv", [Fuente(1, "F001", "Web", "2025-04-10")]);

        var valido = Assert.Single(r.Validos);
        Assert.Equal("F001", valido.Valor.IdExterno);
        Assert.Equal(new DateTime(2025, 4, 10), valido.Valor.FechaCarga);
    }

    [Fact]
    public void Fuente_con_fecha_imposible_se_rechaza()
    {
        var r = ValidadorCatalogos.Fuentes("fuente_datos.csv", [Fuente(1, "F001", "Web", "2025-13-40")]);

        var rechazo = Assert.Single(r.Rechazos);
        Assert.Contains(rechazo.Motivos, m => m.Contains("FechaCarga"));
    }

    [Fact]
    public void Fuente_con_fecha_en_otro_formato_se_rechaza()
    {
        var r = ValidadorCatalogos.Fuentes("fuente_datos.csv", [Fuente(1, "F001", "Web", "10/04/2025")]);

        Assert.Single(r.Rechazos);
    }

    [Fact]
    public void Fuente_con_tipo_desconocido_se_rechaza()
    {
        var r = ValidadorCatalogos.Fuentes("fuente_datos.csv", [Fuente(1, "F001", "Radio", "2025-04-10")]);

        var rechazo = Assert.Single(r.Rechazos);
        Assert.Contains(rechazo.Motivos, m => m.Contains("TipoFuente"));
    }

    [Theory]
    [InlineData("Web")]
    [InlineData("CSV")]
    [InlineData("Red Social")]
    public void Fuente_acepta_los_tres_tipos_permitidos(string tipo)
    {
        var r = ValidadorCatalogos.Fuentes("fuente_datos.csv", [Fuente(1, "F001", tipo, "2025-04-10")]);

        Assert.Single(r.Validos);
    }
}
