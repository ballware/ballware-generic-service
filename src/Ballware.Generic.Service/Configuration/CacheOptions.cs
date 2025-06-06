namespace Ballware.Generic.Service.Configuration;

public class CacheOptions : Ballware.Generic.Caching.Configuration.CacheOptions
{
    public string RedisConfiguration { get; set; } = string.Empty;
    public string RedisInstanceName { get; set; } = "ballware.generic:";
}