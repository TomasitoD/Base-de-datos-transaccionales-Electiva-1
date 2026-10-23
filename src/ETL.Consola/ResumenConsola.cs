using ETL.BLL;

namespace ETL.Consola;

public static class ResumenConsola
{
    public static void MostrarDiagnostico(ResultadoEjecucion resultado, int ejemplos)
    {
        Console.WriteLine("DIAGNOSTICO (sin escritura en base de datos)");
        Console.WriteLine($"{"Archivo",-22}{"Procesados",11}{"Validos",9}{"Omitidos",10}{"Rechazados",12}");
        foreach (var m in resultado.Metricas)
        {
            Console.WriteLine($"{m.Archivo,-22}{m.Procesados,11}{m.NoConfirmados,9}{m.Omitidos,10}{m.Rechazados,12}");
        }

        Console.WriteLine($"{"TOTAL",-22}{resultado.Metricas.Sum(m => m.Procesados),11}"
            + $"{resultado.Metricas.Sum(m => m.NoConfirmados),9}{resultado.Metricas.Sum(m => m.Omitidos),10}"
            + $"{resultado.Metricas.Sum(m => m.Rechazados),12}");

        MostrarEjemplos(resultado, ejemplos);
    }

    private static void MostrarEjemplos(ResultadoEjecucion resultado, int ejemplos)
    {
        Console.WriteLine();
        Console.WriteLine($"Rechazos por cantidad de motivos:");
        foreach (var grupo in resultado.Rechazos.GroupBy(r => (r.Archivo, r.Motivos.Count)).OrderBy(g => g.Key.Archivo))
        {
            Console.WriteLine($"  {grupo.Key.Archivo}: {grupo.Count()} rechazo(s) con {grupo.Key.Count} motivo(s)");
        }

        Console.WriteLine();
        Console.WriteLine($"Primeros {ejemplos} rechazos:");
        foreach (var r in resultado.Rechazos.Take(ejemplos))
        {
            Console.WriteLine($"  {r.Archivo} #{r.Ordinal} id={r.IdExterno}: {string.Join("; ", r.Motivos)}");
        }
    }
}
