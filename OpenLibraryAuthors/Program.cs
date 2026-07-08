using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using OpenLibraryAuthors.Actors;
using OpenLibraryAuthors.Logging;
using OpenLibraryAuthors.Reactive;
using OpenLibraryAuthors.Server;

namespace OpenLibraryAuthors
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Logger logger = Logger.Instance;
            logger.Info("Pokretanje OpenLibrary servera...");

            using CancellationTokenSource cts = new CancellationTokenSource();

            string hocon = @"
                    akka {
                        actor {
                            default-dispatcher {
                                throughput = 10
                            }
                        }
                    }

                    author-dispatcher {
                        type = Dispatcher
                        executor = ""fork-join-executor""
                        fork-join-executor {
                            parallelism-min = 2
                            parallelism-factor = 2.0
                            parallelism-max = 8
                        }
                        throughput = 5
                    }
                    ";

            ActorSystem actorSystem = ActorSystem.Create("OpenLibrarySystem",
                Akka.Configuration.ConfigurationFactory.ParseString(hocon));

            logger.Info("ActorSystem kreiran sa custom dispatcher-om.");

            OpenLibraryService rxService = new OpenLibraryService();
            logger.Info("Rx servis kreiran.");

            IActorRef coordinator = actorSystem.ActorOf(
                Props.Create(() => new SearchCoordinatorActor(rxService)),
                "search-coordinator");

            logger.Info("SearchCoordinatorActor kreiran.");

            string prefix = "http://localhost:5050/";
            HttpServer server = new HttpServer(prefix, coordinator);

            Task serverTask = server.StartAsync(cts.Token);

            logger.Info("HTTP server pokrenut. Format zahteva: http://localhost:5050/author=Ime");
            logger.Info("Pritisnite Q za zaustavljanje servera.");


            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Q)
                {
                    logger.Info("Zaustavljanje servera...");
                    break;
                }
            }

            // Graceful shutdown
            cts.Cancel();
            rxService.Dispose();

            await CoordinatedShutdown.Get(actorSystem).Run(CoordinatedShutdown.ClrExitReason.Instance);

            try
            {
                await serverTask;
            }
            catch (OperationCanceledException)
            {
                // ocekivano pri gasenju
            }

            logger.Info("Server uspesno zaustavljen.");
        }
    }
}
