namespace Ballware.Generic.Tenant.Data;

public class TenantPlaceholderOptions
{
    public bool ReplaceTenantId { get; private set; }
    public bool ReplaceClaims { get; private set; }

    private TenantPlaceholderOptions()
    {
    }

    public static TenantPlaceholderOptions Create()
    {
        return new TenantPlaceholderOptions();
    }

    public TenantPlaceholderOptions WithReplaceTenantId()
    {
        ReplaceTenantId = true;

        return this;
    }

    public TenantPlaceholderOptions WithReplaceClaims()
    {
        ReplaceClaims = true;

        return this;
    }
}