namespace OpenLibraryAuthors.Server;

public static class RequestParser
{
    // URL koa "/author=Tolkien" ili "/author=J.%20R.%20R.%20Tolkien"
    public static string ParseAuthorName(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)) return string.Empty;

        // Skini vodeću kosu crtu
        var path = rawUrl.TrimStart('/');

        // Format mora biti "author=Ime"
        const string prefix = "author=";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        var raw = path.Substring(prefix.Length);
        // Browser šalje space kao %20 — dekodiraj
        return Uri.UnescapeDataString(raw).Trim();
    }
}