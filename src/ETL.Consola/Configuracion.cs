namespace ETL.Consola;

public static class Configuracion
{
    public const string VariableDatos = "ETL_DATA_DIR";

    public static string LeerCarpetaDatos()
    {
        var carpeta = Environment.GetEnvironmentVariable(VariableDatos);
        if (string.IsNullOrWhiteSpace(carpeta))
        {
            throw new InvalidOperationException($"Falta la variable de entorno {VariableDatos}");
        }

        return carpeta;
    }
}
