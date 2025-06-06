namespace Ballware.Generic.Data.Ef.Configuration;

public sealed class StorageOptions
{
    public bool AutoMigrations { get; set; } = false;
    public bool EnableCaching { get; set; } = false;
}