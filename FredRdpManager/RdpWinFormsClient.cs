using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace FredRdpManager
{
  internal sealed class RdpWinFormsClient : UserControl
  {
    private AxMSTSCLib.AxMsRdpClient9NotSafeForScripting _client;

    public RdpWinFormsClient()
    {
      Dock = DockStyle.Fill;
      BackColor = System.Drawing.Color.White;

      _client = new AxMSTSCLib.AxMsRdpClient9NotSafeForScripting();
      ((ISupportInitialize)_client).BeginInit();
      _client.Dock = DockStyle.Fill;
      Controls.Add(_client);
      ((ISupportInitialize)_client).EndInit();
    }

    public void Connect(RdpConnection connection)
    {
      if (connection == null)
        throw new ArgumentNullException(nameof(connection));

      var server = (connection.ServerName ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(server))
        throw new InvalidOperationException("Le nom du serveur est requis.");

      var user = (connection.UserName ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(user))
        throw new InvalidOperationException("Le nom d'utilisateur est requis.");

      var port = connection.Port > 0 && connection.Port <= 65535 ? connection.Port : 3389;
      var domain = (connection.Domain ?? string.Empty).Trim();
      var password = connection.Password ?? string.Empty;

      Disconnect();

      _client.Server = server;
      _client.UserName = user;
      if (!string.IsNullOrEmpty(domain))
        _client.Domain = domain;

      try
      {
        // Port RDP (selon version, l’API peut être exposée via AdvancedSettings)
        var adv2 = _client.AdvancedSettings2;
        if (adv2 != null)
        {
          adv2.RDPPort = port;
          adv2.ClearTextPassword = password;
        }
      }
      catch
      {
        // Si la propriété n’est pas disponible selon la version, on tentera quand même la connexion.
      }

      _client.Connect();
    }

    public void Disconnect()
    {
      if (_client == null)
        return;

      try
      {
        if (_client.Connected != 0)
          _client.Disconnect();
      }
      catch
      {
        // ignore
      }
    }
  }
}

