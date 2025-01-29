namespace Ballware.Generic.Service.Configuration;

public class CorsOptions
{
    public string AllowedOrigins { get; set; } = "";
    public string AllowedMethods { get; set; } = "";
    public string AllowedHeaders { get; set; } = "";
}