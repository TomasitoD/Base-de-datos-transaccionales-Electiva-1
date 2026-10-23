namespace ETL.Entities;

public interface ISesionCarga : IDisposable
{
    ResultadoUpsert UpsertCliente(ClienteNuevo c);
    ResultadoUpsert UpsertProducto(ProductoNuevo p);
    ResultadoUpsert UpsertFuente(FuenteNueva f);
    FuenteNueva? BuscarFuente(string idExterno);
    ResultadoUpsert UpsertOpinion(OpinionNueva o);
    IReadOnlyDictionary<string, long> ObtenerConteos();
    void Confirmar();
    void Revertir();
}

public interface IFabricaSesion
{
    ISesionCarga Abrir();
}
