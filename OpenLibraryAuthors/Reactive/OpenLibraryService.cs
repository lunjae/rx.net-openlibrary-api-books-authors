using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Net.Http.Json;
using System.Reactive.Concurrency;
using Akka.Actor;
using OpenLibraryAuthors.Logging;
using OpenLibraryAuthors.Messages;
using OpenLibraryAuthors.Models;

namespace OpenLibraryAuthors.Reactive;

/* Rx subscribe vraca IDisposable, dugme za gasenje pipline-a
Ukoliko se ne sacuva referenca i ne pozove se Dispose() 
pipline nastavlja da radi cak i kada nam servis ne treba*/
    
public class OpenLibraryService : IDisposable
{
    private readonly HttpClient _http = new();
    private readonly ConcurrentDictionary<string, byte> _trackedAuthors = new();
    private readonly ConcurrentDictionary<string, IActorRef> _authorActors = new();
    private readonly IDisposable _subscription;

    public OpenLibraryService(TimeSpan? interval = null)
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("OpenLibraryActors/1.0");
        
        _subscription = Observable
            .Interval(interval ?? TimeSpan.FromSeconds(30))
            //Za svaki tick uzmi snapshot sbih pracenih autora
            .SelectMany(_ => _authorActors.Keys.ToObservable())
            .SelectMany(author => FetchBooksObservable(author))
            .Where(item => !string.IsNullOrWhiteSpace(item.book.Title))
            .Select(item => new BookMessage(
                item.author,
                item.book.Title!,
                item.book.FirstPublishYear,
                item.book.Languages ?? [],
                item.book.RatingsAverage
            ))
            .ObserveOn(TaskPoolScheduler.Default)// Rx scheduler
            .Subscribe(msg =>
                {
                    if (_authorActors.TryGetValue(msg.Author, out var actor))
                        actor.Tell(msg);
                },
                ex => Logger.Instance.Error($"[Rx] Greška u pipeline-u: {ex.Message}")
            );
    }
    //Coordinator ovo zove kad dodje novi autor
    public void Track(string author, IActorRef authorActor)
    {
        if (_authorActors.TryAdd(author, authorActor))
        {
            Logger.Instance.Rx($"[Rx] Pocinje pracenje autora: {author}");
            // Odmah pokreni prvi fetch da korisnik ne čeka 30s
            _ = TriggerImmediateFetchAsync(author, authorActor);
        }
    }
    private async Task TriggerImmediateFetchAsync(string author, IActorRef actor)
    {
        try
        {
            var books = await FetchBooksAsync(author);
            foreach (var b in books.Where(b => !string.IsNullOrWhiteSpace(b.Title)))
            {
                actor.Tell(new BookMessage(
                    author, b.Title!, b.FirstPublishYear,
                    b.Languages ?? [], b.RatingsAverage));
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[Rx] Inicijalni fetch '{author}': {ex.Message}");
        }
    }

    private IObservable<(string author, OpenLibraryDoc book)> FetchBooksObservable(string author)
    {
        return Observable
            .FromAsync(() => FetchBooksAsync(author))
            .SelectMany(books => books.Select(b => (author, b)))
            .Catch<(string, OpenLibraryDoc), Exception>(ex =>
            {
                Logger.Instance.Rx($"[Rx] Greška pri pozivu API za '{author}' : {ex.Message}");
                return Observable.Empty<(string, OpenLibraryDoc)>();
            });
    }

    private async Task<List<OpenLibraryDoc>> FetchBooksAsync(string author)
    {
        var url = $"https://openlibrary.org/search.json?author={Uri.EscapeDataString(author)}&limit=50&fields=title,first_publish_year,languages,ratings_average";
        Logger.Instance.Rx($"[Rx] Pozivam API za autora: {author}");

        var response = await _http.GetFromJsonAsync<OpenLibrarySearchResponse>(url);
        return response?.Docs ?? [];
    }

    public void Dispose()
    {
        _subscription.Dispose();
        _http.Dispose();
    }
}