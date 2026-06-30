using System.Collections.Generic;
using Akka.Actor;
using OpenLibraryAuthors.Logging;
using OpenLibraryAuthors.Messages;

namespace OpenLibraryAuthors.Actors
{
    public class CacheActor : ReceiveActor
    {
        //Glavni storage. kljuc je ime autora a vrednost kesirani AuthorSummary
        private readonly Dictionary<string, AuthorSummary> _cache = new();
        //Ista LRU strategija 
        private readonly LinkedList<string> _lruOrder = new();
        private readonly int _maxSize;
        private readonly Logger _logger = Logger.Instance;
        
        
        //Srce aktora, slanje poruka 
        public CacheActor(int maxSize= 50)
        {
            _maxSize = maxSize;

            Receive<CacheGet>(msg => HandleGet(msg));
            Receive<CacheSet>(msg => HandleSet(msg));
        }


        private void HandleGet(CacheGet msg)
        {
            //Ako ga nema saljemo Cache MISS coordinatoru
            if (!_cache.ContainsKey(msg.Key))
            {
                _logger.Cache($"MISS - {msg.Key}");
                Sender.Tell(new CacheMiss());
                return;
            }
            //Ako ga ima pomeramo ga na pocetak liste i saljemo Cache HIT
            AuthorSummary result = _cache[msg.Key];
            _lruOrder.Remove(msg.Key);
            _lruOrder.AddFirst(msg.Key);
            
            _logger.Cache($"HIT - {msg.Key}");
            Sender.Tell(new CacheHit(result));
        }

        private void HandleSet(CacheSet msg)
        {
            //Ako postoji menjamo vrednost i stavljamo ga na pocetak
            if (_cache.ContainsKey(msg.Key))
            {
                _cache[msg.Key] = msg.Result;
                _lruOrder.Remove(msg.Key);
                _lruOrder.AddFirst(msg.Key);
                return;
            }
            //Ako je pun izbacujemo najstariji
            if (_cache.Count >= _maxSize)
            {
                string oldest =  _lruOrder.Last.Value;
                _lruOrder.RemoveLast();
                _cache.Remove(oldest);
                _logger.Cache($"Evicted - {oldest}");
            }
            //Novi unos
            _cache[msg.Key] = msg.Result;
            _lruOrder.AddFirst(msg.Key);
            _logger.Cache($"SET - {msg.Key}");
            
        }
    }
}