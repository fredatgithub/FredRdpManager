using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace FredRdpManager
{
  internal sealed class RdpWinFormsClient : UserControl
  {
    private AxMSTSCLib.AxMsRdpClient9NotSafeForScripting _client;

    /// <summary>Id de la connexion associée à ce client.</summary>
    public Guid ConnectionId { get; set; }

    /// <summary>Vrai si la session RDP est active.</summary>
    public bool IsConnected => _client != null && _client.Connected != 0;

    /// <summary>Déclenché dès que l'état connecté/déconnecté change.</summary>
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

      // ── Événements ActiveX ───────────────────────────────────────────────

      _client.OnConnected += (sender, e) =>
      {
        AppLogger.Log($"[RDP] OnConnected  — serveur={_client.Server}  user={_client.UserName}");
        ConnectedChanged?.Invoke(this, EventArgs.Empty);
      };

      _client.OnDisconnected += (sender, e) =>
      {
        var reason = e.discReason;
        // 0 = pas d'info / déconnexion propre  1 = fermeture locale
        // 2xxx = codes d'erreur réseau/authentification (décimal)
        AppLogger.Log($"[RDP] OnDisconnected — serveur={_client.Server}  discReason={reason}  (0=OK, 1=local, 2306=réseau, 2308=timeout, 2825=auth)");

        // Récupérer le code d'erreur étendu si disponible
        try
        {
          var adv = _client.AdvancedSettings2;
          if (adv != null)
            AppLogger.Log($"[RDP] AdvancedSettings2 disponible.");
        }
        catch (Exception ex)
        {
          AppLogger.LogError("[RDP] Impossible de lire AdvancedSettings2.", ex);
        }

        ConnectedChanged?.Invoke(this, EventArgs.Empty);
      };

      _client.OnLogonError += (sender, e) =>
      {
        // -2=timeout, -5=accès refusé, -6=compte expiré, etc.
        AppLogger.Log($"[RDP] OnLogonError  — code={e.lError}  serveur={_client.Server}");
      };

      _client.OnFatalError += (sender, e) =>
      {
        AppLogger.LogError($"[RDP] OnFatalError  — errorCode={e.errorCode}  serveur={_client.Server}");
      };
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

      var port     = connection.Port > 0 && connection.Port <= 65535 ? connection.Port : 3389;
      var domain   = (connection.Domain ?? string.Empty).Trim();
      var password = connection.Password ?? string.Empty;

      AppLogger.Log($"[RDP] Connect() — serveur={server}  port={port}  user={user}  domain={(string.IsNullOrEmpty(domain) ? "(vide)" : domain)}  password={(string.IsNullOrEmpty(password) ? "(vide)" : "***")}");

      Disconnect();

      _client.Server   = server;
      _client.UserName = user;
      if (!string.IsNullOrEmpty(domain))
        _client.Domain = domain;

      // ── AdvancedSettings : port + niveau d'authentification ────────────
      try
      {
        var adv2 = _client.AdvancedSettings2;
        if (adv2 != null)
        {
          adv2.RDPPort = port;
          adv2.ClearTextPassword = password;
          AppLogger.Log($"[RDP] AdvancedSettings2 — RDPPort={port}  ClearTextPassword défini={!string.IsNullOrEmpty(password)}");
        }

        // AuthenticationLevel est sur les versions 4+, on utilise AdvancedSettings7 ou plus
        var adv7 = _client.AdvancedSettings7;
        if (adv7 != null)
        {
          // 0 = Aucun avertissement certificat (nécessaire en mode embarqué car l'UI ActiveX ne peut pas toujours l'afficher en WPF)
          adv7.AuthenticationLevel = 0;
          // S'assurer que CredSSP est activé
          adv7.EnableCredSspSupport = true;
          AppLogger.Log($"[RDP] AdvancedSettings7 — AuthenticationLevel=0  EnableCredSspSupport=true");
        }

        // Paramètre supplémentaire pour la négociation de sécurité
        var adv8 = _client.AdvancedSettings8;
        if (adv8 != null)
        {
          adv8.NegotiateSecurityLayer = true;
          AppLogger.Log($"[RDP] AdvancedSettings8 — NegotiateSecurityLayer=true");
        }
      }
      catch (Exception ex)
      {
        AppLogger.LogError("[RDP] Erreur lors de la configuration des AdvancedSettings.", ex);
      }

      // ── IMsRdpClientNonScriptable : mot de passe pour l'auth NLA ────────
      // ClearTextPassword via AdvancedSettings2 seul ne suffit pas avec NLA.
      // L'interface NonScriptable est nécessaire pour que le mot de passe
      // soit transmis lors du handshake CredSSP/NLA.
      try
      {
        var ns5 = _client.GetOcx() as MSTSCLib.IMsRdpClientNonScriptable5;
        if (ns5 != null)
        {
          ns5.ClearTextPassword = password;
          AppLogger.Log("[RDP] ClearTextPassword défini via IMsRdpClientNonScriptable5 ✓");
        }
        else
        {
          var ns3 = _client.GetOcx() as MSTSCLib.IMsRdpClientNonScriptable3;
          if (ns3 != null)
          {
            ns3.ClearTextPassword = password;
            AppLogger.Log("[RDP] ClearTextPassword défini via IMsRdpClientNonScriptable3 ✓");
          }
          else
          {
            AppLogger.Log("[RDP] ⚠ Aucune interface IMsRdpClientNonScriptable disponible — l'auth NLA risque d'échouer.");
          }
        }
      }
      catch (Exception ex)
      {
        AppLogger.LogError("[RDP] Erreur lors de la définition du mot de passe via NonScriptable.", ex);
      }

      // ── Connexion ────────────────────────────────────────────────────────
      try
      {
        AppLogger.Log("[RDP] Appel de _client.Connect()…");
        _client.Connect();
        AppLogger.Log("[RDP] _client.Connect() retourné sans exception (connexion async en cours).");
      }
      catch (Exception ex)
      {
        AppLogger.LogError("[RDP] Exception lors de _client.Connect().", ex);
        throw;
      }
    }

    public void Disconnect()
    {
      if (_client == null)
        return;

      try
      {
        if (_client.Connected != 0)
        {
          AppLogger.Log($"[RDP] Disconnect() — serveur={_client.Server}");
          _client.Disconnect();
        }
      }
      catch (Exception ex)
      {
        AppLogger.LogError("[RDP] Erreur lors de Disconnect().", ex);
      }
    }
  }
}
