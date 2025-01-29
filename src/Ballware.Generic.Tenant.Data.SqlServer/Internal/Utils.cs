using Ballware.Meta.Client;
using Microsoft.Data.SqlClient;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

static class Utils
{
    public static string GetConnectionString(ServiceTenant tenant)
    {
        var connectionStringBuilder = new SqlConnectionStringBuilder()
        {
            DataSource = tenant.Server,
            InitialCatalog = tenant.Catalog,
            UserID = tenant.User,
            Password = tenant.Password,
            Encrypt = true,
            PersistSecurityInfo = false,
            IntegratedSecurity = false,
        };

        return connectionStringBuilder.ConnectionString;
    }

    public static T TransferToSqlVariables<T>(T target,
        T? source, string prefix = "") where T : IDictionary<string, object>
    {
        if (source?.Count > 0)
        {
            foreach (var pair in source)
            {
                target[$"{prefix}{pair.Key}"] = pair.Value is string[] arrayValue ? $"|{string.Join("|", arrayValue)}|" : pair.Value;
            }
        }
        
        return target;
    }
}