using ETL.BLL;
using ETL.Consola;
using ETL.DAL;

return Ejecutar(args);

static int Ejecutar(string[] args)
{
    try
    {
        return args.Contains("--diagnostico") ? Diagnosticar() : Cargar();
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
    var cadena = Environment.GetEnvironmentVariable(Configuracion.VariableConexion) ?? string.Empty;
    var coordinador = new CoordinadorCarga(new FabricaSesionMySql(cadena), Configuracion.LeerCarpetaDatos());
    var resultado = coordinador.Diagnosticar();
    ResumenConsola.MostrarDiagnostico(resultado, 5);
    return resultado.Estado == EstadoEjecucion.Completada ? 0 : 2;
}

static int Cargar()
{
    var configuracion = Configuracion.Leer();
    var coordinador = new CoordinadorCarga(new FabricaSesionMySql(configuracion.CadenaConexion), configuracion.CarpetaDatos);
    var resultado = coordinador.Ejecutar();

    IReadOnlyList<string> rutas;
    try
    {
        rutas = Reporte.Escribir(resultado, configuracion.CarpetaReportes);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        ResumenConsola.Mostrar(resultado, []);
        Console.Error.WriteLine(resultado.Estado == EstadoEjecucion.Fallida
            ? $"Falló el reporte: {ex.Message}"
            : $"Datos confirmados; falló el reporte: {ex.Message}");
        return 1;
    }

    ResumenConsola.Mostrar(resultado, rutas);
    return resultado.Estado switch
    {
        EstadoEjecucion.Completada => 0,
        EstadoEjecucion.CompletadaConRechazos => 2,
        _ => 1
    };
}
