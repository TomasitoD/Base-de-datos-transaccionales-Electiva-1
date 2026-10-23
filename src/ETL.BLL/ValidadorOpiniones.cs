using System.Globalization;
using ETL.Entities;

namespace ETL.BLL;

public sealed record MapasReferencia(
    IReadOnlyDictionary<string, int> Clientes,
    IReadOnlyDictionary<string, int> Productos,
    IReadOnlyDictionary<string, int> Fuentes);

public static class ValidadorOpiniones
{
    private static readonly string[] Clasificaciones = ["Positiva", "Neutra", "Negativa"];
    private static readonly string[] RedesPermitidas = ["Instagram", "Twitter", "Facebook"];

    private sealed record Referencias(
        int? IdCliente,
        int IdProducto,
        string? IdClienteOrigen,
        string IdProductoOrigen);

    public static ResultadoValidacion<OpinionNueva> Web(string archivo, IReadOnlyList<FilaCsv> filas, MapasReferencia m)
    {
        return ProcesadorFilas.Procesar<OpinionNueva>(archivo, filas, (f, mot) => EvaluarWeb(f, mot, m), o => o.IdExterno);
    }

    public static ResultadoValidacion<OpinionNueva> Encuestas(string archivo, IReadOnlyList<FilaCsv> filas, MapasReferencia m)
    {
        return ProcesadorFilas.Procesar<OpinionNueva>(archivo, filas, (f, mot) => EvaluarEncuesta(f, mot, m), o => o.IdExterno);
    }

    public static ResultadoValidacion<OpinionNueva> Social(string archivo, IReadOnlyList<FilaCsv> filas, MapasReferencia m)
    {
        return ProcesadorFilas.Procesar<OpinionNueva>(archivo, filas, (f, mot) => EvaluarSocial(f, mot, m), o => o.IdExterno);
    }

    private static (string, OpinionNueva?) EvaluarWeb(FilaCsv fila, List<string> motivos, MapasReferencia m)
    {
        var id = Campos.Obligatorio(fila, "IdReview", "IdReview", 50, motivos);
        var refs = ResolverReferencias(fila, m, false, motivos);
        var idFuente = ResolverFuente("ETL:WEB:WEB_REVIEWS", m, motivos);
        var fecha = Campos.Fecha(fila, "Fecha", motivos);
        var comentario = Campos.Obligatorio(fila, "Comentario", "Comentario", int.MaxValue, motivos);
        var rating = LeerEntero(fila, "Rating", 1, 5, motivos);
        if (motivos.Count > 0)
        {
            return (id, null);
        }

        return (id, new OpinionNueva("Web", id, refs.IdCliente, refs.IdProducto, idFuente, fecha, comentario,
            null, (byte)rating, refs.IdClienteOrigen, refs.IdProductoOrigen));
    }

    private static (string, OpinionNueva?) EvaluarEncuesta(FilaCsv fila, List<string> motivos, MapasReferencia m)
    {
        var id = Campos.Obligatorio(fila, "IdOpinion", "IdOpinion", 50, motivos);
        var refs = ResolverReferencias(fila, m, false, motivos);
        var fuente = Campos.Texto(fila, "Fuente");
        if (fuente != "EncuestaInterna")
        {
            motivos.Add($"Fuente no permitida para encuestas: '{fuente}'");
        }

        var idFuente = ResolverFuente("ETL:CSV:ENCUESTA_INTERNA", m, motivos);
        var fecha = Campos.Fecha(fila, "Fecha", motivos);
        var comentario = Campos.Obligatorio(fila, "Comentario", "Comentario", int.MaxValue, motivos);
        var clasificacion = Campos.Texto(fila, "Clasificación");
        if (!Clasificaciones.Contains(clasificacion, StringComparer.Ordinal))
        {
            motivos.Add($"Clasificación inválida: '{clasificacion}'");
        }

        var puntaje = LeerEntero(fila, "PuntajeSatisfacción", 1, 5, motivos);
        if (motivos.Count > 0)
        {
            return (id, null);
        }

        return (id, new OpinionNueva("Encuesta", id, refs.IdCliente, refs.IdProducto, idFuente, fecha, comentario,
            clasificacion, (byte)puntaje, refs.IdClienteOrigen, refs.IdProductoOrigen));
    }

    private static (string, OpinionNueva?) EvaluarSocial(FilaCsv fila, List<string> motivos, MapasReferencia m)
    {
        var id = Campos.Obligatorio(fila, "IdComment", "IdComment", 50, motivos);
        var refs = ResolverReferencias(fila, m, true, motivos);
        var red = Campos.Texto(fila, "Fuente");
        var idFuente = 0;
        if (RedesPermitidas.Contains(red, StringComparer.Ordinal))
        {
            idFuente = ResolverFuente("ETL:SOCIAL:" + red.ToUpperInvariant(), m, motivos);
        }
        else
        {
            motivos.Add($"Fuente no permitida para redes sociales: '{red}'");
        }

        var fecha = Campos.Fecha(fila, "Fecha", motivos);
        var comentario = Campos.Obligatorio(fila, "Comentario", "Comentario", int.MaxValue, motivos);
        if (motivos.Count > 0)
        {
            return (id, null);
        }

        return (id, new OpinionNueva("RedSocial", id, refs.IdCliente, refs.IdProducto, idFuente, fecha, comentario,
            null, null, refs.IdClienteOrigen, refs.IdProductoOrigen));
    }

    private static Referencias ResolverReferencias(
        FilaCsv fila, MapasReferencia m, bool clienteOpcional, List<string> motivos)
    {
        var codigoCliente = Campos.Texto(fila, "IdCliente");
        var codigoProducto = Campos.Texto(fila, "IdProducto");
        int? idCliente = null;

        if (codigoCliente.Length == 0)
        {
            if (!clienteOpcional)
            {
                motivos.Add("IdCliente vacío");
            }
        }
        else
        {
            idCliente = BuscarReferencia(codigoCliente, "IdCliente", "cliente", m.Clientes, motivos);
        }

        var idProducto = 0;
        if (codigoProducto.Length == 0)
        {
            motivos.Add("IdProducto vacío");
        }
        else
        {
            idProducto = BuscarReferencia(codigoProducto, "IdProducto", "producto", m.Productos, motivos) ?? 0;
        }

        return new Referencias(idCliente, idProducto, codigoCliente.Length == 0 ? null : codigoCliente, codigoProducto);
    }

    private static int? BuscarReferencia(
        string codigo, string columna, string entidad, IReadOnlyDictionary<string, int> mapa, List<string> motivos)
    {
        string? externo;
        try
        {
            externo = Normalizador.Externo(codigo);
        }
        catch (FormatException)
        {
            motivos.Add($"{columna} inválido: '{codigo}'");
            return null;
        }

        if (externo is not null && mapa.TryGetValue(externo, out var id))
        {
            return id;
        }

        motivos.Add($"{entidad} {codigo} no existe");
        return null;
    }

    private static int ResolverFuente(string idExterno, MapasReferencia m, List<string> motivos)
    {
        if (m.Fuentes.TryGetValue(idExterno, out var id))
        {
            return id;
        }

        motivos.Add($"fuente {idExterno} no existe");
        return 0;
    }

    private static int LeerEntero(FilaCsv fila, string columna, int minimo, int maximo, List<string> motivos)
    {
        var texto = Campos.Texto(fila, columna);
        if (int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor)
            && valor >= minimo && valor <= maximo)
        {
            return valor;
        }

        motivos.Add($"{columna} debe ser un entero entre {minimo} y {maximo}: '{texto}'");
        return 0;
    }
}
