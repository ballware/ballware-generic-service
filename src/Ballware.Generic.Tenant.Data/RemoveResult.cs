namespace Ballware.Generic.Tenant.Data;

public struct RemoveResult
{
    public bool Result { get; set; }
    public IEnumerable<string> Messages { get; set; }
}