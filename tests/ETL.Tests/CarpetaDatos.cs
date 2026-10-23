using System.Text;

namespace ETL.Tests;

public sealed class CarpetaDatos : IDisposable
{
    public string Ruta { get; } = Path.Combine(Path.GetTempPath(), "etl-datos-" + Guid.NewGuid().ToString("N"));

    public CarpetaDatos()
    {
        Directory.CreateDirectory(Ruta);
        Escribir("clients.csv", "IdCliente,Nombre,Email\n7,Ana,ana@mail.com\n8,Luis,\n");
        Escribir("products.csv", "IdProducto,Nombre,Categoría\n16,Mouse,Periféricos\n");
        Escribir("fuente_datos.csv", "IdFuente,TipoFuente,FechaCarga\nF001,Web,2025-04-10\n");
        Escribir("web_reviews.csv",
            "IdReview,IdCliente,IdProducto,Fecha,Comentario,Rating\n"
            + "W0001,C007,P016,2024-10-23,Bien,4\n"
            + "W0002,C008,P016,2024-10-24,Mal,9\n");
        Escribir("surveys_part1.csv",
            "IdOpinion,IdCliente,IdProducto,Fecha,Comentario,Clasificación,PuntajeSatisfacción,Fuente\n"
            + "1,8537,366,2025-07-15,Regular,Neutra,3,EncuestaInterna\n"
            + "2,7,16,2025-07-16,Bueno,Positiva,5,EncuestaInterna\n");
        Escribir("social_comments.csv",
            "IdComment,IdCliente,IdProducto,Fuente,Fecha,Comentario\n"
            + "T0001,,P016,Twitter,2025-06-15,Hola\n"
            + "T0002,C007,P016,Instagram,2025-06-16,Adios\n");
    }

    public void Escribir(string nombre, string contenido)
    {
        File.WriteAllText(Path.Combine(Ruta, nombre), contenido, new UTF8Encoding(false));
    }

    public void Dispose()
    {
        Directory.Delete(Ruta, true);
    }
}
