using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using OpenLibraryAuthors.Logging;
using OpenLibraryAuthors.Messages;
using OpenLibraryAuthors.Response;

namespace OpenLibraryAuthors.Server
{
    public class HttpServer
    {
        private readonly HttpListener _listener;
        private readonly IActorRef _coordinator;
        private readonly string _prefix;
        private readonly Logger _logger = Logger.Instance;

        public HttpServer(string prefix, IActorRef coordinator)
        {
            _prefix = prefix;
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _coordinator = coordinator;
        }
        
        public async Task StartAsync(CancellationToken ct)
        {
            _listener.Start();
            ct.Register(() => _listener.Stop());
            _logger.Info($"[HttpServer] Server pokrenut na {_prefix}");

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        HttpListenerContext context = await Task.Run(() => _listener.GetContext(), ct);
                        _ = HandleRequestAsync(context);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (HttpListenerException e)
                    {
                        if (!ct.IsCancellationRequested)
                            _logger.Error($"[HttpServer] Greska: {e.Message}");
                    }
                }
            }
            finally
            {
                _listener.Stop();
                _logger.Info("[HttpServer] Server zaustavljen.");
            }
        }
        
        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                string rawUrl = context.Request.Url?.AbsolutePath ?? "/";
                string author = RequestParser.ParseAuthorName(rawUrl);

                if (string.IsNullOrEmpty(author))
                {
                    _logger.Warn("[HttpServer] Neispravan zahtev.");
                    SendResponse(context, "<html><body><h2>Neispravan zahtev. Format: /author=Ime</h2></body></html>");
                    return;
                }

                _logger.Info($"[HttpServer] Zahtev za autora: {author}");

                // Pošalji poruku koordinatoru i čekaj odgovor (Ask pattern)
                AuthorSummary summary = await _coordinator.Ask<AuthorSummary>(
                    new SearchAuthorRequest(author),
                    TimeSpan.FromSeconds(30));

                string html = ResponseBuilder.BuildAuthorPage(summary);
                SendResponse(context, html);

                _logger.Info($"[HttpServer] Zahtev obrađen: {author}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[HttpServer] Greška: {ex.Message}");
                try
                {
                    SendResponse(context, "<html><body><h2>Greška na serveru.</h2></body></html>");
                }
                catch { }
            }
        }

        private void SendResponse(HttpListenerContext context, string html)
        {
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(html);
                context.Response.ContentType = "text/html; charset=utf-8" ;
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
            }
            catch (Exception e)
            {
                _logger.Error($"[HttpServer] Greska pri slanju: {e.Message}");
            }
        }
    }
}