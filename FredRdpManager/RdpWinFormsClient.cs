using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;

namespace FredRdpManager
{
  internal sealed class RdpWinFormsClient : UserControl
  {
    private AxMSTSCLib.AxMsRdpClient9NotSafeForScripting _client;

    /// <summary>Id de la connexion associée à ce client.</summary>
    public System.Guid ConnectionId { get; set; }

    /// <summary>Nom d'affichage pour l'onglet.</summary>
    public string DisplayName { get; set; }

    public bool IsConnected => _client != null && _client.Connected != 0;

    public event EventHandler ConnectedChanged;

    public RdpWinFormsClient()
    {
      Dock = DockStyle.Fill;
      BackColor = System.Drawing.Color.White;

      _client = new AxMSTSCLib.AxMsRdpClient9NotSafeForScripting();
      ((ISupportInitialize)_client).BeginInit();
      _client.Dock = DockStyle.Fill;
      Controls.Add(_client);
      ((ISupportInitialize)_client).EndInit();

      _client.OnConnected += (sender, e) => ConnectedChanged?.Invoke(this, EventArgs.Empty);
      _client.OnDisconnected += (sender, e) => ConnectedChanged?.Invoke(this, EventArgs.Empty);
      
      this.Resize += (sender, e) => UpdateDisplay();
    }

    private void UpdateDisplay()
    {
      if (!IsConnected) return;
      try {
        _client.DesktopWidth = this.Width;
        _client.DesktopHeight = this.Height;
        object ocx = _client.GetOcx();
        ocx.GetType().InvokeMember("UpdateSessionDisplaySettings", 
          System.Reflection.BindingFlags.InvokeMethod, null, ocx, null);
      } catch { }
    }

    public void Connect(RdpConnection connection)
    {
      if (connection == null) return;
      Disconnect();
      _client.DesktopWidth = Width > 100 ? Width : 1024;
      _client.DesktopHeight = Height > 100 ? Height : 768;
      _client.Server = connection.ServerName;
      _client.UserName = connection.UserName;
      if (!string.IsNullOrEmpty(connection.Domain)) _client.Domain = connection.Domain;
      
      try {
        var adv = _client.AdvancedSettings7;
        adv.EnableCredSspSupport = true;
        adv.AuthenticationLevel = 0;
        adv.SmartSizing = true;
        
        var ns = _client.GetOcx() as MSTSCLib.IMsRdpClientNonScriptable5;
        if (ns != null) ns.ClearTextPassword = connection.Password;
      } catch { }
      
      _client.Connect();
    }

    public void Disconnect()
    {
      try { if (_client.Connected != 0) _client.Disconnect(); } catch { }
    }
  }
}
