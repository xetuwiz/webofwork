namespace WebOfWork.Api.Configuration;

public sealed class DatabaseOptions
{
    public const string Section = "ConnectionStrings";

    public string Postgres { get; init; } = string.Empty;
    public string Redis    { get; init; } = string.Empty;
}
