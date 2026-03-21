using System;

namespace FredRdpManager
{
  /// <summary>
  /// DTO pour la sérialisation (mot de passe chiffré DPAPI).
  /// </summary>
  public sealed class SerializableConnection
  {
    public string Id { get; set; }
    public string ServerName { get; set; }
    public string Domain { get; set; }
    public string UserName { get; set; }
    public string EncryptedPasswordBase64 { get; set; }
  }
}
