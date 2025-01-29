namespace Ballware.Generic.Service.Configuration;

public class SwaggerOptions
{
    public bool EnableClient { get; set; } = false;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RequiredScopes { get; set; } = "openid genericApi";
}