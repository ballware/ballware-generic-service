namespace Ballware.Generic.Tenant.Data;

public struct GenericExport
{
    public string FileName { get; set; }
    public string MediaType { get; set; }
    public byte[] Data { get; set; }
}