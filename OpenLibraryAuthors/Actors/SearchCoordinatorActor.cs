using System.Collections.Generic;
using Akka.Actor;
using Microsoft.VisualBasic;
using OpenLibraryAuthors.Logging;
using OpenLibraryAuthors.Messages;

namespace OpenLibraryAuthors.Actors
{
    public class SearchCoordinatorActor : ReceiveActor
    {
        //referenca na aktora/ adresa preko koje mu saljemo poruke
        private readonly IActorRef _cacheActor;
        //Mapa (imaautora, ref na njegov actor)
        private readonly Dictionary<string, IActorRef> _authorActors = new();
        //Lista autora koje pratimo
        private readonly HashSet<string> _trackedAuthors = new();
        
        private readonly Logger _logger = Logger.Instance;
     
        
        public SearchCoordinatorActor(IActorRef cacheActor)
        {
            _cacheActor = cacheActor;

            Receive<SearchAuthorRequest>(msg => HandleSearch(msg));
            Receive<BookMessage>(msg => HandleBookMessage(msg));
            
        }

        private void HandleSearch(SearchAuthorRequest msg)
        {
            string author = msg.AuthorName;
            _logger.Actor($"[Coordinator] Zahtev za autora: {author}");
            
            //Dodaj autora u pracene ako vec nije 
            if (!_trackedAuthors.Contains(author))
            {
                _trackedAuthors.Add(author);
                _logger.Actor($"[Coordinator] Novi praceni autor: {author}");
            }
            
            //Pronadji ili kreiraj AuthorActor za ovog autora
            IActorRef authorActor = GetOrCreateAuthorActor(author);
            
            //Zapamti ko je trazio (HTTP server) da mu vratimo odgovor
            IActorRef originalSender = Sender;
            
            //Pitaj AuthroActor-a za trenutno stanje 
            authorActor.Ask<AuthorSummary>(new GetAuthorSummary(author))
                .PipeTo(originalSender);
        }
        
        private IActorRef GetOrCreateAuthorActor(string author)
        {
            if (_authorActors.ContainsKey(author))
                return _authorActors[author];

            IActorRef actor = Context.ActorOf(
                Props.Create(() => new AuthorActor(author)),
                $"author-{SanitizeName(author)}");

            _authorActors[author] = actor;
            _logger.Actor($"[Coordinator] Kreiran AuthorActor za: {author}");
            return actor;
        }

        private void HandleBookMessage(BookMessage msg)
        {
            // Rx emituje knjigu sa imenom autora — prosledi samo njegovom AuthorActor-u
            IActorRef authorActor = GetOrCreateAuthorActor(msg.Author);
            authorActor.Forward(msg);
        }

        private string SanitizeName(string name)
        {
            return name.Replace(" ", "_").Replace(".", "_").Replace("&", "_");
        }
        
    }
}
