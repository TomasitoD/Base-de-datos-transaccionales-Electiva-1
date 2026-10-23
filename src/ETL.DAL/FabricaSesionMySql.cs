using ETL.Entities;

namespace ETL.DAL;

public sealed class FabricaSesionMySql(string cadenaConexion) : IFabricaSesion
{
    public ISesionCarga Abrir()
    {
        return new SesionCargaMySql(cadenaConexion);
    }
}
