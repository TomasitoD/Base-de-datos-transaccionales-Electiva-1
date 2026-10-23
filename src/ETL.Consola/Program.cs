using ETL.BLL;
using ETL.Consola;

return Ejecutar(args);

static int Ejecutar(string[] args)
{
    try
    {
        if (args.Contains("--diagnostico"))
        {
            return Diagnosticar();
        }

        Console.Error.WriteLine("Opcion no soportada. Use --diagnostico.");
        return 1;
    }
    catch (Exception ex) when (ex is InvalidOperationException or ArchivoNoEncontradoException
        or EncabezadoInvalidoException or ArchivoMalFormadoException)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static int Diagnosticar()
{
    var coordinador = new CoordinadorCarga(null!, Configuracion.LeerCarpetaDatos());
    var resultado = coordinador.Diagnosticar();
    ResumenConsola.MostrarDiagnostico(resultado, 5);
    return resultado.Estado == EstadoEjecucion.Completada ? 0 : 2;
}
