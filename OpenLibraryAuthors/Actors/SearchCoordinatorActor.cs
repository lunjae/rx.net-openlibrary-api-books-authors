using System.Collections.Generic;
using Akka.Actor;
using OpenLibraryAuthors.Logging;
using OpenLibraryAuthors.Messages;
using OpenLibraryAuthors.Reactive;

namespace OpenLibraryAuthors.Actors
{
    public class SearchCoordinatorActor : ReceiveActor
    {
        private readonly OpenLibraryService _rxService;
        private readonly Dictionary<string, IActorRef> _authorActors = new();
        private readonly HashSet<string> _trackedAuthors = new();
        private readonly Logger _logger = Logger.Instance;

        // Interni tipovi poruka za PipeTo tok - nikad ne izlaze iz ovog aktora
        private sealed record FetchDone(string Author, IActorRef OriginalSender, IActorRef AuthorActor);
        private sealed record SummaryResponse(string Author, IActorRef OriginalSender, AuthorSummary Summary);

        public SearchCoordinatorActor(OpenLibraryService rxService)
        {
            _rxService = rxService;

            Receive<SearchAuthorRequest>(msg => HandleSearch(msg));
            Receive<BookMessage>(msg => HandleBookMessage(msg));
            Receive<FetchDone>(msg => HandleFetchDone(msg));
            Receive<SummaryResponse>(msg => HandleSummaryResponse(msg));
        }

        private void HandleSearch(SearchAuthorRequest msg)
        {
            string author = msg.AuthorName;
            IActorRef originalSender = Sender;

            _logger.Actor($"[Coordinator] Zahtev za autora: {author}");

            IActorRef authorActor = GetOrCreateAuthorActor(author);

            if (_trackedAuthors.Add(author))
            {
                // Nov autor - sacekaj inicijalni fetch da se zavrsi pa tek onda
                // pitaj actora, inace bi dobili prazan odgovor (pending).
                // WaitAsync(8s): ako API odgovori u roku, korisnik odmah vidi knjige.
                // Ako ne (spor API), FetchDone stize kroz failure handler, actor vraca
                // pending stranicu, a Rx interval osvezava u pozadini.
                _logger.Actor($"[Coordinator] Novi praceni autor: {author}");
                _rxService.Track(author, authorActor)
                    .WaitAsync(TimeSpan.FromSeconds(8))
                    .PipeTo(Self,
                        success: _ => new FetchDone(author, originalSender, authorActor),
                        failure: _ => new FetchDone(author, originalSender, authorActor));
            }
            else
            {
                // Vec pracen - podaci vec postoje u actoru, pitaj odmah
                authorActor.Ask<AuthorSummary>(new GetAuthorSummary(author))
                    .PipeTo(Self,
                        success: s => new SummaryResponse(author, originalSender, s),
                        failure: _ => new SummaryResponse(author, originalSender,
                            new AuthorSummary(author, new(), "error")));
            }
        }

        private void HandleFetchDone(FetchDone msg)
        {
            // Inicijalni fetch je gotov - sada aktor ima podatke, pitaj ga
            msg.AuthorActor.Ask<AuthorSummary>(new GetAuthorSummary(msg.Author))
                .PipeTo(Self,
                    success: s => new SummaryResponse(msg.Author, msg.OriginalSender, s),
                    failure: _ => new SummaryResponse(msg.Author, msg.OriginalSender,
                        new AuthorSummary(msg.Author, new(), "error")));
        }

        private void HandleSummaryResponse(SummaryResponse msg)
        {
            msg.OriginalSender.Tell(msg.Summary);
        }

        private IActorRef GetOrCreateAuthorActor(string author)
        {
            if (_authorActors.ContainsKey(author))
                return _authorActors[author];

            IActorRef actor = Context.ActorOf(
                AuthorActor.CreateProps(author),
                $"author-{SanitizeName(author)}");

            _authorActors[author] = actor;
            _logger.Actor($"[Coordinator] Kreiran AuthorActor za: {author}");
            return actor;
        }

        private void HandleBookMessage(BookMessage msg)
        {
            IActorRef authorActor = GetOrCreateAuthorActor(msg.Author);
            authorActor.Forward(msg);
        }

        private static string SanitizeName(string name)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in name)
                sb.Append(char.IsLetterOrDigit(c) && c < 128 ? c : '_');
            return sb.ToString().Trim('_');
        }
    }
}
