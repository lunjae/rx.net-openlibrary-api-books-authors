using System;
using System.IO;

namespace OpenLibraryAuthors.Logging
{
    public class Logger
    {
        static readonly Logger _instance = new Logger();
        private readonly object _lock = new object();
        private readonly string _logFilePath;

        private Logger()
        {
            _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server.log");
        }

        public static Logger Instance => _instance;

        public void Log(string level, string message)
        {
            string entry = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}";

            lock (_lock)
            {
                Console.WriteLine(entry);
                File.AppendAllText(_logFilePath, entry + Environment.NewLine);
            }
        }

        public void Info(string message)  => Log("INFO ", message);
        public void Warn(string message)  => Log("WARN ", message);
        public void Error(string message) => Log("ERROR", message);
        public void Cache(string message) => Log("CACHE", message);
        public void Rx(string message)    => Log("RX   ", message);
        public void Actor(string message) => Log("ACTOR", message);
    }
}