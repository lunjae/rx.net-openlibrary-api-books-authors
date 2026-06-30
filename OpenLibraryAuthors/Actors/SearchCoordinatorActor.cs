using System.Collections.Generic;
using Akka.Actor;
using OpenLibraryAuthors.Logging;
using OpenLibraryAuthors.Messages;
using OpenLibraryAuthors.Reactive;

namespace OpenLibraryAuthors.Actors
{
    public class SearchCoordinatorActor : ReceiveActor
    {
        //referenca na aktora/ adresa preko koje mu saljemo poruke
        private readonly IActorRef _cacheActor;
        
        private readonly OpenLibraryService _rxService;
        
        //Mapa (imaautora, ref na njegov actor)
        private readonly Dictionary<string, IActorRef> _authorActors = new();
        
        //Lista autora koje pratimo
        private readonly HashSet<string> _trackedAuthors = new();
        
        private readonly Logger _logger = Logger.Instance;
     
        
        public SearchCoordinatorActor(IActorRef cacheActor, OpenLibraryService rxService)
        {
            _cacheActor = cacheActor;
            _rxService = rxService;

            Receive<SearchAuthorRequest>(msg => HandleSearch(msg));
            Receive<BookMessage>(msg => HandleBookMessage(msg));
            
        }

        private void HandleSearch(SearchAuthorRequest msg)
        {
            string author = msg.AuthorName;
            IActorRef originalSender = Sender;

            // 1) Pitaj cache prvo
            _cacheActor.Ask<object>(new CacheGet(author), TimeSpan.FromSeconds(2))
                .ContinueWith(t =>
                {
                    if (t.Result is CacheHit hit)
                    {
                        _logger.Cache($"[Coordinator] Cache hit za {author}");
                        originalSender.Tell(hit.Result);
                        return;
                    }

                    // 2) Miss — kreiraj/registruj autora i vrati stanje
                    IActorRef authorActor = GetOrCreateAuthorActor(author);
                    if (_trackedAuthors.Add(author))
                        _rxService.Track(author, authorActor);

                    authorActor.Ask<AuthorSummary>(new GetAuthorSummary(author))
                        .ContinueWith(summaryTask =>
                        {
                            var summary = summaryTask.Result;
                            if (summary.Status == "Ok")
                                _cacheActor.Tell(new CacheSet(author, summary));
                            originalSender.Tell(summary);
                        });
                });
        }
        
        private IActorRef GetOrCreateAuthorActor(string author)
        {
            if (_authorActors.ContainsKey(author))
                return _authorActors[author];

            IActorRef actor = Context.ActorOf(
                AuthorActor.CreateProps(author),
                $"author-{SanitizeName(author)}");

            _authorActors[author] = actor;
            _rxService.Track(author, actor);
            _logger.Actor($"[Coordinator] Kreiran AuthorActor za: {author}");
            return actor;
        }

        private void HandleBookMessage(BookMessage msg)
        {
            // Rx emituje knjigu sa imenom autora — prosledi samo njegovom AuthorActor-u
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
