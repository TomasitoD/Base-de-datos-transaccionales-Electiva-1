using System.Data;
using ETL.Entities;
using MySqlConnector;

namespace ETL.DAL;

public sealed class SesionCargaMySql : ISesionCarga
{
    private const int TamanoComentario = 16777215;

    private readonly MySqlConnection _conexion;
    private readonly MySqlTransaction _transaccion;
    private bool _finalizada;

    public SesionCargaMySql(string cadenaConexion)
    {
        _conexion = new MySqlConnection(cadenaConexion);
        try
        {
            _conexion.Open();
            _transaccion = _conexion.BeginTransaction();
        }
        catch
        {
            _conexion.Dispose();
            throw;
        }
    }

    public ResultadoUpsert UpsertCliente(ClienteNuevo c)
    {
        using var comando = CrearComando("sp_UpsertCliente");
        Entrada(comando, "pIdExterno", MySqlDbType.VarChar, 50, c.IdExterno);
        Entrada(comando, "pNombre", MySqlDbType.VarChar, 120, c.Nombre);
        Entrada(comando, "pEmail", MySqlDbType.VarChar, 254, c.Email);
        return EjecutarUpsert(comando, "pIdCliente", MySqlDbType.Int32);
    }

    public ResultadoUpsert UpsertProducto(ProductoNuevo p)
    {
        using var comando = CrearComando("sp_UpsertProducto");
        Entrada(comando, "pIdExterno", MySqlDbType.VarChar, 50, p.IdExterno);
        Entrada(comando, "pNombre", MySqlDbType.VarChar, 150, p.Nombre);
        Entrada(comando, "pCategoria", MySqlDbType.VarChar, 100, p.Categoria);
        return EjecutarUpsert(comando, "pIdProducto", MySqlDbType.Int32);
    }

    public ResultadoUpsert UpsertFuente(FuenteNueva f)
    {
        using var comando = CrearComando("sp_UpsertFuente");
        Entrada(comando, "pIdExterno", MySqlDbType.VarChar, 100, f.IdExterno);
        Entrada(comando, "pTipoFuente", MySqlDbType.VarChar, 50, f.TipoFuente);
        Entrada(comando, "pFechaCarga", MySqlDbType.DateTime, 0, f.FechaCarga);
        return EjecutarUpsert(comando, "pIdFuente", MySqlDbType.Int32);
    }

    public FuenteNueva? BuscarFuente(string idExterno)
    {
        using var comando = CrearComando("sp_BuscarFuente");
        Entrada(comando, "pIdExterno", MySqlDbType.VarChar, 100, idExterno);
        using var lector = comando.ExecuteReader();
        if (!lector.Read())
        {
            return null;
        }

        return new FuenteNueva(idExterno, lector.GetString("TipoFuente"), lector.GetDateTime("FechaCarga"));
    }

    public ResultadoUpsert UpsertOpinion(OpinionNueva o)
    {
        using var comando = CrearComando("sp_UpsertOpinion");
        Entrada(comando, "pCanal", MySqlDbType.VarChar, 20, o.Canal);
        Entrada(comando, "pIdExterno", MySqlDbType.VarChar, 50, o.IdExterno);
        Entrada(comando, "pIdCliente", MySqlDbType.Int32, 0, o.IdCliente);
        Entrada(comando, "pIdProducto", MySqlDbType.Int32, 0, o.IdProducto);
        Entrada(comando, "pIdFuente", MySqlDbType.Int32, 0, o.IdFuente);
        Entrada(comando, "pFecha", MySqlDbType.DateTime, 0, o.Fecha);
        Entrada(comando, "pComentario", MySqlDbType.MediumText, TamanoComentario, o.Comentario);
        Entrada(comando, "pClasificacion", MySqlDbType.VarChar, 10, o.Clasificacion);
        Entrada(comando, "pPuntaje", MySqlDbType.UByte, 0, o.PuntajeSatisfaccion);
        Entrada(comando, "pIdClienteOrigen", MySqlDbType.VarChar, 50, o.IdClienteOrigen);
        Entrada(comando, "pIdProductoOrigen", MySqlDbType.VarChar, 50, o.IdProductoOrigen);
        return EjecutarUpsert(comando, "pIdOpinion", MySqlDbType.Int64);
    }

    public IReadOnlyDictionary<string, long> ObtenerConteos()
    {
        using var comando = CrearComando("sp_ObtenerConteos");
        using var lector = comando.ExecuteReader();
        var conteos = new Dictionary<string, long>(StringComparer.Ordinal);
        while (lector.Read())
        {
            conteos[lector.GetString("Tabla")] = lector.GetInt64("Total");
        }

        return conteos;
    }

    public void Confirmar()
    {
        _transaccion.Commit();
        _finalizada = true;
    }

    public void Revertir()
    {
        if (_finalizada)
        {
            return;
        }

        _finalizada = true;
        _transaccion.Rollback();
    }

    public void Dispose()
    {
        if (!_finalizada)
        {
            TryRevertir();
        }

        _transaccion.Dispose();
        _conexion.Dispose();
    }

    private void TryRevertir()
    {
        try
        {
            _transaccion.Rollback();
        }
        catch (MySqlException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private MySqlCommand CrearComando(string procedimiento)
    {
        return new MySqlCommand(procedimiento, _conexion, _transaccion) { CommandType = CommandType.StoredProcedure };
    }

    private static void Entrada(MySqlCommand comando, string nombre, MySqlDbType tipo, int tamano, object? valor)
    {
        var parametro = tamano > 0 ? comando.Parameters.Add(nombre, tipo, tamano) : comando.Parameters.Add(nombre, tipo);
        parametro.Value = valor ?? DBNull.Value;
    }

    private static ResultadoUpsert EjecutarUpsert(MySqlCommand comando, string nombreId, MySqlDbType tipoId)
    {
        var id = comando.Parameters.Add(nombreId, tipoId);
        id.Direction = ParameterDirection.Output;
        var estado = comando.Parameters.Add("pEstado", MySqlDbType.VarChar, 10);
        estado.Direction = ParameterDirection.Output;

        comando.ExecuteNonQuery();

        return new ResultadoUpsert(Convert.ToInt64(id.Value), Enum.Parse<EstadoCarga>((string)estado.Value!));
    }
}
