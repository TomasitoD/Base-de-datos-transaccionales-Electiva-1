using ETL.BLL;

namespace ETL.Tests;

public class NormalizadorTests
{
    [Theory]
    [InlineData("C007", "7")]
    [InlineData("P016", "16")]
    [InlineData("W0001", "1")]
    [InlineData("42", "42")]
    [InlineData("  C007 ", "7")]
    [InlineData("T0000", "0")]
    public void Externo_quita_prefijo_y_ceros_a_la_izquierda(string entrada, string esperado)
    {
        Assert.Equal(esperado, Normalizador.Externo(entrada));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Externo_devuelve_null_si_esta_vacio(string? entrada)
    {
        Assert.Null(Normalizador.Externo(entrada));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("X")]
    [InlineData("C0A7")]
    public void Externo_lanza_FormatException_si_no_tiene_numero_valido(string entrada)
    {
        Assert.Throws<FormatException>(() => Normalizador.Externo(entrada));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("  ", true)]
    [InlineData("a", false)]
    public void EsVacio_detecta_nulos_y_espacios(string? entrada, bool esperado)
    {
        Assert.Equal(esperado, Normalizador.EsVacio(entrada));
    }
}
