namespace ETL.BLL;

public static class Normalizador
{
    public static string? Externo(string? codigo)
    {
        if (EsVacio(codigo))
        {
            return null;
        }

        var digitos = new string(codigo!.Trim().SkipWhile(char.IsAsciiLetter).ToArray());
        if (digitos.Length == 0 || !digitos.All(char.IsAsciiDigit))
        {
            throw new FormatException($"Codigo externo invalido: '{codigo}'");
        }

        var sinCeros = digitos.TrimStart('0');
        return sinCeros.Length == 0 ? "0" : sinCeros;
    }

    public static bool EsVacio(string? texto)
    {
        return string.IsNullOrWhiteSpace(texto);
    }
}
