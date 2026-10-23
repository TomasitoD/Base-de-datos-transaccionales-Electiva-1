namespace ETL.Consola;

public sealed record ConfiguracionEtl(string CarpetaDatos, string CarpetaReportes, string CadenaConexion);

public static class Configuracion
{
    public const string VariableDatos = "ETL_DATA_DIR";
    public const string VariableReportes = "ETL_REPORT_DIR";
    public const string VariableConexion = "ETL_CONNECTION_STRING";

    public static ConfiguracionEtl Leer()
    {
        var datos = Leer(VariableDatos);
        var reportes = Leer(VariableReportes);
        var conexion = Leer(VariableConexion);
        var faltantes = new[] { (VariableDatos, datos), (VariableReportes, reportes), (VariableConexion, conexion) }
            .Where(v => string.IsNullOrWhiteSpace(v.Item2))
            .Select(v => v.Item1)
            .ToList();
        if (faltantes.Count > 0)
        {
            throw new InvalidOperationException($"Faltan variables de entorno: {string.Join(", ", faltantes)}");
        }

        return new ConfiguracionEtl(datos!, reportes!, conexion!);
    }

    public static string LeerCarpetaDatos()
    {
        var carpeta = Leer(VariableDatos);
        if (string.IsNullOrWhiteSpace(carpeta))
        {
            throw new InvalidOperationException($"Falta la variable de entorno {VariableDatos}");
        }

        return carpeta;
    }

    private static string? Leer(string variable)
    {
        return Environment.GetEnvironmentVariable(variable);
    }
}
