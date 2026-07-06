using Akka.Actor;
using OpenLibraryAuthors.Logging;
using OpenLibraryAuthors.Messages;
using OpenLibraryAuthors.Models;

namespace OpenLibraryAuthors.Actors;

public class AuthorActor : ReceiveActor
{
    private readonly string _authorName;
    private readonly Dictionary<string, BookInfo> _books = new();
    
    public AuthorActor(string authorName)
    {
        _authorName = authorName;

        Receive<BookMessage>(msg =>
        {
            // Strip whitespace i lowercase
            var key = msg.Title.Trim().ToLowerInvariant();


            // Ako knjiga postoji u stanju radi merge, ako ne overwrite
            if (_books.TryGetValue(key, out var existing))
            {
                var mergedRating = (existing.Rating, msg.Rating) switch
                {
                    (null, var r) => r,
                    (var r, null) => r,
                    (var r1, var r2) => (r1 + r2) / 2.0
                };

                var mergedLanguages = existing.Languages
                    .Union(msg.Languages)
                    .Distinct()
                    .ToArray();

                _books[key] = existing with
                {
                    Year = existing.Year ?? msg.Year,
                    Languages = mergedLanguages,
                    Rating = mergedRating
                };
            }
            else
            {
                _books[key] = new BookInfo(
                    msg.Title,
                    msg.Year,
                    msg.Languages,
                    msg.Rating
                );
            }
        });

        Receive<GetAuthorSummary>(_ =>
            {
                var sorted = _books.Values
                    .OrderBy(b => b.Year ?? int.MaxValue)
                    .ThenBy(b => b.Title)
                    .ToList();

                var status = sorted.Count > 0 ? "Ok" : "pending";

                Logger.Instance.Actor(
                    $"[AuthorActor: {_authorName}] GetAuthorSummary -> {sorted.Count} knjiga(e), status={status}");

                Sender.Tell(new AuthorSummary(_authorName, sorted, status));
            }
        );
    }
    
    public static Props CreateProps(string authorName) =>
        Props.Create(() => new AuthorActor(authorName)).WithDispatcher("author-dispatcher");
}