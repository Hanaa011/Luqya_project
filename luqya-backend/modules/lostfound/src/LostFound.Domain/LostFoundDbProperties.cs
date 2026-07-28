namespace LostFound;

public static class LostFoundDbProperties
{
    public static string DbTablePrefix { get; set; } = "LostFound";

    public static string? DbSchema { get; set; } = null;

    public const string ConnectionStringName = "LostFound";
}
