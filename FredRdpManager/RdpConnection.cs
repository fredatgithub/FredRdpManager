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
    public string Domain { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }

    public string DisplayName =>
      string.IsNullOrWhiteSpace(ServerName)
        ? "(sans nom)"
        : ServerName.Trim();
  }
}
