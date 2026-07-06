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
    private readonly ConcurrentDictionary<string, IActorRef> _authorActors = new();
    private readonly IActorRef _cacheActor;
    private readonly IDisposable _subscription;

    public OpenLibraryService(IActorRef cacheActor, TimeSpan? interval = null)
    {
        _cacheActor = cacheActor;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("OpenLibraryActors/1.0");

        _subscription = Observable
            .Interval(interval ?? TimeSpan.FromSeconds(30))
            .SelectMany(_ => _authorActors.Keys.ToObservable())
            .SelectMany(author => FetchBooksObservable(author)
                .Where(item => !string.IsNullOrWhiteSpace(item.book.Title))
                .Select(item => new BookMessage(
                    item.author,
                    item.book.Title!,
                    item.book.FirstPublishYear,
                    item.book.Languages ?? [],
                    item.book.RatingsAverage
                ))
                .ToList()
                .Do(lista =>
                {
                    Logger.Instance.Rx($"[Rx] Osvezavanje '{author}': {lista.Count} knjiga.");
                    // Novi podaci stizu — stari kesiran odgovor vise nije validan
                    _cacheActor.Tell(new CacheInvalidate(author));
                })
                .SelectMany(lista => lista)
            )
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(
                msg =>
                {
                    if (_authorActors.TryGetValue(msg.Author, out var actor))
                        actor.Tell(msg);
                },
                ex => Logger.Instance.Error($"[Rx] Greška u pipeline-u: {ex.Message}")
            );
    }
    // Coordinator ovo zove kad dodje novi autor.
    // Vraca Task inicijalnog fetcha - koordinator ceka na njega pre nego
    // sto pita actora za stanje, da korisnik ne dobije prazan odgovor.
    public Task<bool> Track(string author, IActorRef authorActor)
    {
        if (_authorActors.TryAdd(author, authorActor))
        {
            Logger.Instance.Rx($"[Rx] Pocinje pracenje autora: {author}");
            return TriggerImmediateFetchAsync(author, authorActor)
                .ContinueWith(_ => true);
        }
        return Task.FromResult(false);
    }
    private async Task TriggerImmediateFetchAsync(string author, IActorRef actor)
    {
        try
        {
            var books = await FetchBooksAsync(author);
            var valid = books.Where(b => !string.IsNullOrWhiteSpace(b.Title)).ToList();
            foreach (var b in valid)
                actor.Tell(new BookMessage(author, b.Title!, b.FirstPublishYear, b.Languages ?? [], b.RatingsAverage));
            var unique = valid.Select(b => b.Title!.Trim().ToLowerInvariant()).Distinct().Count();
            Logger.Instance.Rx($"[Rx] Inicijalni fetch '{author}': {valid.Count} knjiga sa API-ja, {unique} jedinstvenih naslova.");
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
        var url = $"https://openlibrary.org/search.json?author={Uri.EscapeDataString(author)}&limit=50&fields=title,first_publish_year,language,ratings_average";
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