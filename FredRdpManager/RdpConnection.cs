using System;

namespace FredRdpManager
{
  /// <summary>
  /// Représente une connexion RDP en mémoire.
  /// </summary>
  public sealed class RdpConnection
  {
    public Guid Id { get; set; }
    public string ServerName { get; set; }
    /// <summary>Port TCP RDP (3389 par défaut).</summary>
    public int Port { get; set; } = 3389;
    public string Domain { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }

    public string DisplayName
    {
      get
      {
        if (string.IsNullOrWhiteSpace(ServerName))
          return "(sans nom)";
        var host = ServerName.Trim();
        if (Port > 0 && Port != 3389)
          return host + " : " + Port;
        return host;
      }
    }
  }
}
