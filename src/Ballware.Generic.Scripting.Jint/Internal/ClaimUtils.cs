namespace Ballware.Generic.Scripting.Jint.Internal;

public static class ClaimUtils
{
    public static string? GetClaim(Dictionary<string, object> claims, string claim)
    {
        if (!claims.ContainsKey(claim))
        {
            return null;
        }

        if (claims[claim] is string[] strings)
        {
            return strings.FirstOrDefault();
        }
        else
        {
            return (claims[claim] as string);
        }
    }

    public static bool HasClaim(Dictionary<string, object> claims, string claim, string value)
    {
        if (!claims.ContainsKey(claim))
        {
            return false;
        }

        if (claims[claim] is string[] strings)
        {
            return strings.Contains(value);
        }
         
        return claims[claim].Equals(value);
    }

    public static bool HasAnyClaim(Dictionary<string, object> claims, string claim, string valuePrefix)
    {
        if (!claims.ContainsKey(claim))
        {
            return false;
        }

        if (claims[claim] is string[] strings)
        {
            return strings.Any(r => r.StartsWith(valuePrefix));
        }
         
        return (claims[claim] as string)?.StartsWith(valuePrefix) ?? false;
    }
}