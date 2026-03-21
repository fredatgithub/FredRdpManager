using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace FredRdpManager
{
  internal static class RdpLauncher
  {
    /// <summary>
    /// Lance mstsc avec les identifiants via cmdkey (TERMSRV).
    /// </summary>
    public static void Connect(RdpConnection c)
    {
      if (c == null)
        throw new ArgumentNullException(nameof(c));

      var server = (c.ServerName ?? "").Trim();
      if (string.IsNullOrEmpty(server))
        throw new InvalidOperationException("Le nom du serveur est requis.");

      var user = BuildUserPrincipal(c.Domain, c.UserName);
      if (string.IsNullOrEmpty(user))
        throw new InvalidOperationException("Le nom d'utilisateur est requis.");

      var pass = c.Password ?? "";

      var target = "TERMSRV/" + server;
      RunHidden("cmdkey.exe", "/generic:" + QuoteArg(target) + " /user:" + QuoteArg(user) + " /pass:" + QuoteArg(pass));

      try
      {
        var mstsc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "mstsc.exe");
        if (!File.Exists(mstsc))
          mstsc = "mstsc.exe";
        Process.Start(new ProcessStartInfo
        {
          FileName = mstsc,
          Arguments = "/v:" + QuoteArg(server),
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

    private static string BuildUserPrincipal(string domain, string userName)
    {
      var u = (userName ?? "").Trim();
      if (string.IsNullOrEmpty(u))
        return null;
      var d = (domain ?? "").Trim();
      if (string.IsNullOrEmpty(d))
        return u;
      if (u.Contains("\\") || u.Contains("@"))
        return u;
      return d + "\\" + u;
    }

    private static string QuoteArg(string s)
    {
      if (string.IsNullOrEmpty(s))
        return "\"\"";
      if (s.IndexOfAny(new[] { ' ', '\t' }) < 0)
        return s;
      return "\"" + s.Replace("\"", "\\\"") + "\"";
    }

    private static void RunHidden(string fileName, string arguments)
    {
      using (var p = Process.Start(new ProcessStartInfo
      {
        FileName = fileName,
        Arguments = arguments,
        UseShellExecute = false,
        CreateNoWindow = true,
        WindowStyle = ProcessWindowStyle.Hidden
      }))
      {
        p.WaitForExit();
        if (p.ExitCode != 0)
          throw new InvalidOperationException(
            "La commande a échoué : " + fileName + " (code " + p.ExitCode + ").");
      }
    }
  }
}
