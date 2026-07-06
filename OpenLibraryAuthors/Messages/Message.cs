using System.Collections.Generic;
using OpenLibraryAuthors.Models;

namespace OpenLibraryAuthors.Messages
{
    //HTTP server -> Coordinator: korisnik trazi autora
    public record SearchAuthorRequest(string AuthorName);
    
    //Rx pipeline - AuthorActor: jedna knjiga iz API odgovora
    public record BookMessage(
        string Author,
        string Title,
        int? Year,
        string[] Languages, 
        double? Rating);

    //Coordinator AuthorActor : zatrazi trenutno stanje
    public record GetAuthorSummary(string AuthorName);

    //AuthorActor - Coordinator - Http : trenutno stanje autora
    public record AuthorSummary(
        string Author,
        List<BookInfo> Books,
        string Status
    );
    
    //Coordinator interno: dodaj autora u pracene
    public record TrackAuthor(string AuthorName);
    
    //Rx periodicni signal: vreme za osvezavanje
    public record RefreshTick;
    
    //Rx - Coordinator: zavrseno osvezavanje za autora
    public record RefreshComplete(string AuthorName);
    
    //Cache poruke
    public record CacheGet(string Key);
    public record CacheHit(AuthorSummary Result);
    public record CacheMiss;
    public record CacheSet(string Key, AuthorSummary Result);
    public record CacheInvalidate(string Key);
    
}