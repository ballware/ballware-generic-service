using System.Security.Cryptography;
using System.Text;

namespace Ballware.Generic.Tenant.Data.Commons.Utils;

public static class CommonPasswordGenerator
{
    private const string AllowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";

    public static string GenerateTenantPassword(int length = 20)
    {
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));

        var data = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(data);

        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            char c = AllowedChars[data[i] % AllowedChars.Length];
            sb.Append(c);
        }

        return sb.ToString();
    }
}