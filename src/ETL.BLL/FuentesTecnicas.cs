namespace ETL.BLL;

public static class FuentesTecnicas
{
    public const string Web = "ETL:WEB:WEB_REVIEWS";
    public const string Encuesta = "ETL:CSV:ENCUESTA_INTERNA";
    public const string Instagram = "ETL:SOCIAL:INSTAGRAM";
    public const string Twitter = "ETL:SOCIAL:TWITTER";
    public const string Facebook = "ETL:SOCIAL:FACEBOOK";

    public static IReadOnlyList<(string IdExterno, string TipoFuente)> Todas { get; } =
    [
        (Web, "Web"),
        (Encuesta, "CSV"),
        (Instagram, "Red Social"),
        (Twitter, "Red Social"),
        (Facebook, "Red Social")
    ];
}
