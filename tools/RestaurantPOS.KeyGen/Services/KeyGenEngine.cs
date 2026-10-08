using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RestaurantPOS.KeyGen.Services;

public static class KeyGenEngine
{
    // Master Private Key (PKCS#8 Base64) - KEPT ONLY IN DEV KEYGEN TOOL
    private const string MasterPrivateKeyB64 = "MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQDEP2dz5MYC0XZN/SkNppXyMSI8AxlH7Z4jRlU2lZCy8+0T4PFrMn+dJq5as64xuAtR/sY2Ux/rnb+G/RsQB4+EdsVfNl1NIe8uzC4Q2UNViWK9/YdDgCzTdLDiNCJbT3duZwuOEf89jpkkHYilWKkdU+btHM93JTosnPGhNxvhSqrsUTWG0batKBYHnJ6rfBKugzHf3uW1kE4fXFjpn7pYb304tLSkoX+vaE33CXBc5wXmoIFFwvuN5he7GHP/tqQ/bb2sh4W78Y5mgv+4vRNuOWgi30jPz5JUQfGQqW7t0ZVs96Ykc39H5+AuVhSxfqHxX/MLRbRiYyKvmvfpsYvNAgMBAAECggEAH8SnyZICH9iqjwtpRuVtpowfn5lc0VD85XbwOmrRxrZH7YzfZ9v/3n+xf8tQaooBgx98FLU2GJ0rsP1uERU4PVhAsR4K0n6oymmRF3ckOEeKLXlKGTo5h21/aM7JtQBTNBMIQtJVADECR19kjGei0LWRT1b3mrDzDt/FGbTjFYAIvihzCse672Vr9RiLsjTWFY44bLGP5fdtD/RCyTjbmunWJzEsh47e0AZ7Nk1XPKlA+FniagJx+aGCRCUrMyvOmiZT3SS28+6PAf7g/4fOC0Khlvpui9WPMCcVNHyg6oNlWbLWOxWg7C/pFs8jdc7EGPR0uGXwcSZHsZDhjQ8UfQKBgQD8r4kF2WDC66PUhlJgQdX/R7TQZrTfh/v6y2wakHW0hJjp+BXvsp2gl0sCYtfSUaiADTeucFmZcDDF1nISskQCcX1a8G1iprzDoNwZgHzIal3wpigcfF7hxxhOhpoxYKE/rzqXIcZDKocpYrV1gvXVpxtCRlzEj7TTGDIDBIaHRwKBgQDG0lyuI8IiRsGinh0f905IPOJcevlpPrRGBdgxFso0c/s6To9IxsmvGerI0jhJ2ku7KbgZCBz2ZVcFQ2Am8mdFprqM4RqwkXc5hHpzfmEGMYUbguR0DzJcMsNvjidWSCTgUfF4wIOumBXOczU8Gnn79Fm9dx8dEMBKuIb42qHGSwKBgCmp79ftDc1V+T9znmWMtXJQKnoqVSx+SYgCvqJqq13Gd0gsxcEuHFt8Vrvf/GILYNMsWsACemOYYhJc15ZJmnkkqVyzQ/X+NCW1glUaIHkTQMYLP5Xi0+o6X8Gi5A28Nxo/FFQTF5O/kWW7htwHae1+jjoRS/6FC3W7CZQBzQTFAoGBALHTRIGcCe6OSnIf6PzGPIXvrqX4d0rigmp+n7aII6J2eaTUzuLQqWKrU4r6Os4TKNjln3sD/qOCUCqs8DrlY+iwDvoh+7Ug5fnTu5HA3xajA+JvV9VWIjzESnhkNFq/e/wGTmsqBJ0L+bUE5gAzzhbDneAPwwxEBzjNgirhoP+xAoGAHCWJzLzzOOpu4gehr6iylRNgbXurNUARDiaxzBCtfefuPlwaF9g19R14fTxgbm40GrozpzFCRYcjfwFxwR34XmU2GLbwmUSSP9S+hkchypQSBbHc5x26u3XOeFCHaLNxS1TwWoGWawxnU0J3ilB6ZNOh4pHLlyLC332X+TvR/K0=";

    public class KeyPayload
    {
        public string MachineCode { get; set; } = string.Empty;
        public string StoreCode { get; set; } = string.Empty;
        public string Plan { get; set; } = string.Empty;
        public long IssuedUtcTicks { get; set; }
        public long ExpiresUtcTicks { get; set; }
    }

    public static string GenerateKey(string machineCode, string storeCode, string planType, int customDays = 0)
    {
        var cleanMachine = machineCode.Trim().ToUpperInvariant();
        var cleanStore = string.IsNullOrWhiteSpace(storeCode) ? "DEFAULT" : storeCode.Trim().ToUpperInvariant();
        var nowUtc = DateTime.UtcNow;

        DateTime expiresUtc;
        if (planType == "FullLifetime")
        {
            expiresUtc = new DateTime(2099, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        }
        else if (planType == "FullYearly")
        {
            expiresUtc = nowUtc.AddYears(1);
        }
        else
        {
            var days = customDays > 0 ? customDays : 30;
            expiresUtc = nowUtc.AddDays(days);
        }

        var payload = new KeyPayload
        {
            MachineCode = cleanMachine,
            StoreCode = cleanStore,
            Plan = planType,
            IssuedUtcTicks = nowUtc.Ticks,
            ExpiresUtcTicks = expiresUtc.Ticks
        };

        var json = JsonSerializer.Serialize(payload);
        var payloadBytes = Encoding.UTF8.GetBytes(json);

        // Sign with RSA Private Key
        using var rsa = RSA.Create();
        var privBytes = Convert.FromBase64String(MasterPrivateKeyB64);
        rsa.ImportPkcs8PrivateKey(privBytes, out _);

        var signatureBytes = rsa.SignData(payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var b64Payload = Convert.ToBase64String(payloadBytes);
        var b64Signature = Convert.ToBase64String(signatureBytes);

        return $"ACT-{b64Payload}.{b64Signature}";
    }

    public static KeyPayload? InspectKey(string keyString)
    {
        try
        {
            var clean = keyString.Trim();
            if (!clean.StartsWith("ACT-", StringComparison.OrdinalIgnoreCase)) return null;

            var raw = clean.Substring(4);
            var parts = raw.Split('.');
            if (parts.Length != 2) return null;

            var payloadBytes = Convert.FromBase64String(parts[0]);
            var json = Encoding.UTF8.GetString(payloadBytes);
            return JsonSerializer.Deserialize<KeyPayload>(json);
        }
        catch
        {
            return null;
        }
    }
}
