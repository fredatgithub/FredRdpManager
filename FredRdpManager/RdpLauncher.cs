using System;
using System.Diagnostics;
using System.IO;

namespace FredRdpManager
{
  internal static class RdpLauncher
  {
    /// <summary>
    /// Lance mstsc avec les identifiants via cmdkey (TERMSRV).
    /// </summary>
    public static void Connect(RdpConnection rdpConnection)
    {
      if (rdpConnection == null)
      {
        throw new ArgumentNullException(nameof(rdpConnection));
      }

      var server = (rdpConnection.ServerName ?? "").Trim();
      if (string.IsNullOrEmpty(server))
      {
        throw new InvalidOperationException("Le nom du serveur est requis.");
      }

      var port = rdpConnection.Port > 0 && rdpConnection.Port <= 65535 ? rdpConnection.Port : 3389;
      var address = BuildRdpAddress(server, port);

      var user = BuildUserPrincipal(rdpConnection.Domain, rdpConnection.UserName);
      if (string.IsNullOrEmpty(user))
      {
        throw new InvalidOperationException("Le nom d'utilisateur est requis.");
      }

      var pass = rdpConnection.Password ?? string.Empty;

      var target = "TERMSRV/" + address;
      RunHidden("cmdkey.exe", "/generic:" + QuoteArg(target) + " /user:" + QuoteArg(user) + " /pass:" + QuoteArg(pass));

      try
      {
        var mstsc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "mstsc.exe");
        if (!File.Exists(mstsc))
        {
          mstsc = "mstsc.exe";
        }

        Process.Start(new ProcessStartInfo
        {
          FileName = mstsc,
          Arguments = "/v:" + QuoteArg(address),
          UseShellExecute = true
        });
      }
      catch
      {
        try
        {
          RunHidden("cmdkey.exe", "/delete:" + QuoteArg(target));
        }
        catch
        {
          // ignore cleanup failure
        }
        throw;
      }
    }

    /// <summary>
    /// Adresse pour mstsc et TERMSRV : « hôte » ou « hôte:port » si le port n’est pas 3389.
    /// </summary>
    private static string BuildRdpAddress(string host, int port)
    {
      if (port == 3389)
      {
        return host;
      }

      return host + ":" + port;
    }

    private static string BuildUserPrincipal(string domain, string userName)
    {
      var user = (userName ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(user))
      {
        return null;
      }

      var theDomain = (domain ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(theDomain))
      {
        return user;
      }

      if (user.Contains("\\") || user.Contains("@"))
      {
        return user;
      }

      return theDomain + "\\" + user;
    }

    private static string QuoteArg(string theString)
    {
      if (string.IsNullOrEmpty(theString))
      {
        return "\"\"";
      }

      if (theString.IndexOfAny(new[] { ' ', '\t' }) < 0)
      {
        return theString;
      }

      return "\"" + theString.Replace("\"", "\\\"") + "\"";
    }

    private static void RunHidden(string fileName, string arguments)
    {
      using (var process = Process.Start(new ProcessStartInfo
      {
        FileName = fileName,
        Arguments = arguments,
        UseShellExecute = false,
        CreateNoWindow = true,
        WindowStyle = ProcessWindowStyle.Hidden
      }))
      {
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
          throw new InvalidOperationException("La commande a échoué : " + fileName + " (code " + process.ExitCode + ").");
        }
      }
    }
  }
}
