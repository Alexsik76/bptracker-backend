using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace BpTracker.Api.Tests.Infrastructure;

public static partial class HttpClientExtensions
{
    /// <summary>Sets the session cookie for all subsequent requests from this client.</summary>
    public static HttpClient AuthAs(this HttpClient client, string sessionToken)
    {
        client.DefaultRequestHeaders.Add("Cookie", $"__Host-session={sessionToken}");
        return client;
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body)
        => client.PostAsync(url, JsonContent.Create(body));

    public static Task<HttpResponseMessage> PatchJsonAsync<T>(this HttpClient client, string url, T body)
        => client.PatchAsync(url, JsonContent.Create(body));

    /// <summary>Extracts the raw session token value from a Set-Cookie response header.</summary>
    public static string? ExtractSessionToken(this HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
            return null;

        foreach (var cookie in cookies)
        {
            if (cookie.StartsWith("__Host-session=", StringComparison.Ordinal))
                return cookie["__Host-session=".Length..].Split(';')[0].Trim();
        }
        return null;
    }

    /// <summary>Parses the magic link token from the email body URL (/login?token=HEX).</summary>
    public static string ExtractMagicToken(this CapturedEmail email)
    {
        var match = MagicTokenPattern().Match(email.Body);
        if (!match.Success)
            throw new InvalidOperationException($"Magic token not found in email body:\n{email.Body}");
        return match.Groups[1].Value;
    }

    [GeneratedRegex(@"\?token=([A-Fa-f0-9]+)")]
    private static partial Regex MagicTokenPattern();
}
