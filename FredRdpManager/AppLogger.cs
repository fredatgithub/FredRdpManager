using System;
using System.IO;

namespace FredRdpManager
{
  /// <summary>
  /// Logger simple thread-safe : écrit dans application-yyyy-MM-dd.log
  /// dans le répertoire de l'exécutable.
  /// </summary>
  internal static class AppLogger
  {
    private static readonly object _lock = new object();

    private static string LogPath =>
      Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "application-" + DateTime.Now.ToString("yyyy-MM-dd") + ".log");

    public static void Log(string message)
    {
      Write("INFO ", message, null);
    }

    public static void LogError(string message, Exception ex = null)
    {
      Write("ERROR", message, ex);
    }

    private static void Write(string level, string message, Exception ex)
    {
      try
      {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var line = $"{timestamp} [{level}] {message}";

        if (ex != null)
        {
          line += Environment.NewLine
               + $"         Exception : {ex.GetType().FullName}: {ex.Message}"
               + Environment.NewLine
               + $"         StackTrace: {ex.StackTrace}";
        }

        lock (_lock)
        {
          File.AppendAllText(LogPath, line + Environment.NewLine);
        }
      }
      catch
      {
        // Un logger ne doit jamais propager d'exception.
      }
    }
  }
}
