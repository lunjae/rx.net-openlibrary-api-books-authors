using OpenLibraryAuthors.Messages;

namespace OpenLibraryAuthors.Response;


public static class ResponseBuilder
{
    public static string BuildAuthorPage(AuthorSummary summary)
    {
        var booksHtml = summary.Books.Count == 0
            ? "<tr><td colspan='4' style='text-align:center;color:#888'>Podaci se učitavaju, pokušajte ponovo za nekoliko sekundi...</td></tr>"
            : string.Join("\n", summary.Books.Select(b =>
            {
                var year = b.Year?.ToString() ?? "—";
                var langs = b.Languages.Length > 0 ? string.Join(", ", b.Languages) : "—";
                var rating = b.Rating.HasValue ? $"{b.Rating.Value:F2} ⭐" : "—";
                return $"""
                    <tr>
                        <td>{Escape(b.Title)}</td>
                        <td>{year}</td>
                        <td>{Escape(langs)}</td>
                        <td>{rating}</td>
                    </tr>
                """;
            }));

        // Ako podaci jos nisu stigli, stranica se sama osvezava svakih 5s
        var autoRefresh = summary.Status != "Ok"
            ? "<meta http-equiv='refresh' content='5'>"
            : "";

        return $$"""
            <!DOCTYPE html>
            <html lang="sr">
            <head>
                <meta charset="UTF-8">
                {{autoRefresh}}
                <title>OpenLibrary — {{Escape(summary.Author)}}</title>
                <style>
                    body { font-family: Segoe UI, sans-serif; max-width: 1000px; margin: 40px auto; padding: 0 20px; background: #f5f5f5; }
                    h1 { color: #2c3e50; }
                    .status { display: inline-block; padding: 4px 10px; border-radius: 12px;
                               background: {{(summary.Status == "Ok" ? "#27ae60" : "#e67e22")}}; color: white; font-size: 0.85em; }
                    table { width: 100%; border-collapse: collapse; background: white; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,.1); }
                    th { background: #2c3e50; color: white; padding: 12px 16px; text-align: left; }
                    td { padding: 10px 16px; border-bottom: 1px solid #eee; }
                    tr:last-child td { border-bottom: none; }
                    tr:hover td { background: #f0f4f8; }
                    .meta { color: #666; margin-bottom: 20px; font-size: 0.9em; }
                </style>
            </head>
            <body>
                <h1> {{Escape(summary.Author)}} <span class="status">{{summary.Status}}</span></h1>
                <p class="meta">Ukupno knjiga: <strong>{{summary.Books.Count}}</strong></p>
                <table>
                    <thead>
                        <tr>
                            <th>Naslov</th>
                            <th>Godina</th>
                            <th>Jezici</th>
                            <th>Rejting</th>
                        </tr>
                    </thead>
                    <tbody>
                        {{booksHtml}}
                    </tbody>
                </table>
                <p style="color:#999;font-size:0.8em;margin-top:20px">
                    Podaci se osvežavaju svakih 30 sekundi via Rx.NET → Akka.NET
                </p>
            </body>
            </html>
            """;
    }

    public static string BuildErrorPage(string message) => $"""
        <!DOCTYPE html>
        <html><head><meta charset="UTF-8"><title>Greška</title></head>
        <body style="font-family:sans-serif;max-width:600px;margin:40px auto">
        <h2>⚠️ Greška</h2><p>{Escape(message)}</p>
        <p><a href="/">Nazad</a></p>
        </body></html>
        """;

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}