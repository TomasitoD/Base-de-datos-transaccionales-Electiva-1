using ETL.Entities;

namespace ETL.Tests;

public sealed class AlmacenFalso
{
    public int SiguienteId { get; set; } = 1000;
    public Dictionary<string, (int Id, ClienteNuevo Valor)> Clientes { get; } = [];
    public Dictionary<string, (int Id, ProductoNuevo Valor)> Productos { get; } = [];
    public Dictionary<string, (int Id, FuenteNueva Valor)> Fuentes { get; } = [];
    public Dictionary<(string Canal, string IdExterno), (int Id, OpinionNueva Valor)> Opiniones { get; } = [];

    public AlmacenFalso Copiar()
    {
        var copia = new AlmacenFalso { SiguienteId = SiguienteId };
        foreach (var par in Clientes) copia.Clientes[par.Key] = par.Value;
        foreach (var par in Productos) copia.Productos[par.Key] = par.Value;
        foreach (var par in Fuentes) copia.Fuentes[par.Key] = par.Value;
        foreach (var par in Opiniones) copia.Opiniones[par.Key] = par.Value;
        return copia;
    }

    public void Reemplazar(AlmacenFalso otro)
    {
        SiguienteId = otro.SiguienteId;
        Clientes.Clear();
        Productos.Clear();
        Fuentes.Clear();
        Opiniones.Clear();
        foreach (var par in otro.Clientes) Clientes[par.Key] = par.Value;
        foreach (var par in otro.Productos) Productos[par.Key] = par.Value;
        foreach (var par in otro.Fuentes) Fuentes[par.Key] = par.Value;
        foreach (var par in otro.Opiniones) Opiniones[par.Key] = par.Value;
    }
}

public sealed class SesionFalsa(AlmacenFalso almacen, Func<string, bool>? fallarEn = null) : ISesionCarga
{
    private readonly AlmacenFalso _trabajo = almacen.Copiar();

    public bool Confirmada { get; private set; }
    public bool Revertida { get; private set; }
    public List<OpinionNueva> OpinionesRecibidas { get; } = [];

    public ResultadoUpsert UpsertCliente(ClienteNuevo c)
    {
        return Upsert(_trabajo.Clientes, c.IdExterno, c, "UpsertCliente");
    }

    public ResultadoUpsert UpsertProducto(ProductoNuevo p)
    {
        return Upsert(_trabajo.Productos, p.IdExterno, p, "UpsertProducto");
    }

    public ResultadoUpsert UpsertFuente(FuenteNueva f)
    {
        return Upsert(_trabajo.Fuentes, f.IdExterno, f, "UpsertFuente");
    }

    public FuenteNueva? BuscarFuente(string idExterno)
    {
        return _trabajo.Fuentes.TryGetValue(idExterno, out var existente) ? existente.Valor : null;
    }

    public ResultadoUpsert UpsertOpinion(OpinionNueva o)
    {
        OpinionesRecibidas.Add(o);
        return Upsert(_trabajo.Opiniones, (o.Canal, o.IdExterno), o, "UpsertOpinion");
    }

    public IReadOnlyDictionary<string, long> ObtenerConteos()
    {
        return new Dictionary<string, long>
        {
            ["Clientes"] = _trabajo.Clientes.Count,
            ["Productos"] = _trabajo.Productos.Count,
            ["FuenteDatos"] = _trabajo.Fuentes.Count,
            ["Opiniones"] = _trabajo.Opiniones.Count
        };
    }

    public void Confirmar()
    {
        almacen.Reemplazar(_trabajo);
        Confirmada = true;
    }

    public void Revertir()
    {
        Revertida = true;
    }

    public void Dispose()
    {
    }

    private ResultadoUpsert Upsert<TClave, TValor>(
        Dictionary<TClave, (int Id, TValor Valor)> tabla, TClave clave, TValor valor, string operacion)
        where TClave : notnull
    {
        if (fallarEn is not null && fallarEn(operacion))
        {
            throw new InvalidOperationException($"Fallo simulado en {operacion}");
        }

        if (tabla.TryGetValue(clave, out var existente))
        {
            var estado = EqualityComparer<TValor>.Default.Equals(existente.Valor, valor)
                ? EstadoCarga.Omitido
                : EstadoCarga.Conflicto;
            return new ResultadoUpsert(existente.Id, estado);
        }

        var id = _trabajo.SiguienteId++;
        tabla[clave] = (id, valor);
        return new ResultadoUpsert(id, EstadoCarga.Insertado);
    }
}

public sealed class FabricaFalsa(AlmacenFalso almacen, Func<string, bool>? fallarEn = null) : IFabricaSesion
{
    public List<SesionFalsa> Sesiones { get; } = [];

    public ISesionCarga Abrir()
    {
        var sesion = new SesionFalsa(almacen, fallarEn);
        Sesiones.Add(sesion);
        return sesion;
    }
}
