using System.Text.RegularExpressions;

namespace Ballware.Generic.Tenant.Data.Postgres.Internal;

static class PostgresValidator
{
    private const int MaxIdentifierLength = 63;
    private static readonly Regex ValidIdentifierRegex = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

    private static void ValidatePgIdentifier(string identifier, string paramName, string context)
    {
        if (string.IsNullOrWhiteSpace(identifier) || !ValidIdentifierRegex.IsMatch(identifier) || identifier.Length > MaxIdentifierLength)
        {
            throw new ArgumentException($"Invalid PostgreSQL {context}: '{identifier}'", paramName);
        }
    }
    
    public static void ValidateTableAndColumnIdentifier(string identifier, string paramName)
        => ValidatePgIdentifier(identifier, paramName, "identifier");
    
    public static void ValidateSchemaName(string schemaName, string paramName)
        => ValidatePgIdentifier(schemaName, paramName, "schema name");

    public static void ValidateUserName(string userName, string paramName)
        => ValidatePgIdentifier(userName, paramName, "user name");
    
    public static void ValidateFunctionName(string functionName, string paramName) =>
        ValidatePgIdentifier(functionName, paramName, "function name");

    public static void ValidateIndexName(string indexName, string paramName) =>
        ValidatePgIdentifier(indexName, paramName, "index name");
    
    public static void ValidateViewName(string viewName, string paramName) =>
        ValidatePgIdentifier(viewName, paramName, "view name");
    
    public static void ValidateDomainName(string domainName, string paramName) =>
        ValidatePgIdentifier(domainName, paramName, "domain name");
}