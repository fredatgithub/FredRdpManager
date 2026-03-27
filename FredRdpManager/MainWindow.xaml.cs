using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FredRdpManager
{
  public partial class MainWindow : Window
  {
    // Un ViewModel par connexion enregistrée
    private readonly ObservableCollection<ConnectionViewModel> _viewModels;

    // Clients RDP actifs, indexés par l'Id de la connexion
    private readonly Dictionary<Guid, RdpWinFormsClient> _activeClients =
        new Dictionary<Guid, RdpWinFormsClient>();

    // Id de la connexion actuellement affichée dans RdpHost
    private Guid _currentConnectionId = Guid.Empty;

    public MainWindow()
    {
      InitializeComponent();
      SourceInitialized += MainWindow_OnSourceInitialized;

      AppLogger.Log("=== Application démarrée ===");
      AppLogger.Log($"Répertoire : {AppDomain.CurrentDomain.BaseDirectory}");

      _viewModels = new ObservableCollection<ConnectionViewModel>(
          ConnectionStorage.Load().Select(c => new ConnectionViewModel(c)));

      AppLogger.Log($"Connexions chargées : {_viewModels.Count}");

      ConnectionsList.ItemsSource = _viewModels;
      ConnectionsList.SelectionChanged += ConnectionsList_OnSelectionChanged;
      UpdateDetail();
    }

    // ── Initialisation / fermeture ──────────────────────────────────────────

    private void MainWindow_OnSourceInitialized(object sender, EventArgs e)
    {
      SourceInitialized -= MainWindow_OnSourceInitialized;
      WindowLayoutStorage.TryApply(this);
    }

    private void MainWindow_OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
      AppLogger.Log($"=== Fermeture — {_activeClients.Count} client(s) actif(s) à déconnecter ===");

      // Déconnecter tous les clients actifs
      foreach (var client in _activeClients.Values)
      {
        try { client.Disconnect(); }
        catch (Exception ex) { AppLogger.LogError("Erreur lors de la déconnexion à la fermeture.", ex); }
      }

      WindowLayoutStorage.Save(this);
      PersistConnections();
      AppLogger.Log("=== Application fermée ===");
    }

    // ── Persistance ─────────────────────────────────────────────────────────

    private void PersistConnections()
    {
      ConnectionStorage.Save(_viewModels.Select(vm => vm.Connection));
    }

    // ── Sélection dans la liste ─────────────────────────────────────────────

    private void ConnectionsList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      // Simple clic : met à jour les détails textuels uniquement (pas de connexion automatique)
      UpdateDetail();
    }

    private void UpdateDetail()
    {
      var vm = ConnectionsList.SelectedItem as ConnectionViewModel;
      if (vm == null)
      {
        DetailText.Text = string.Empty;
        return;
      }

      var c      = vm.Connection;
      var domain = string.IsNullOrWhiteSpace(c.Domain) ? "—" : c.Domain.Trim();
      var user   = string.IsNullOrWhiteSpace(c.UserName) ? "—" : c.UserName.Trim();
      var port   = c.Port > 0 ? c.Port : 3389;

      DetailText.Text = "Serveur : "     + c.ServerName.Trim() + Environment.NewLine
                      + "Port RDP : "    + port                + Environment.NewLine
                      + "Domaine : "     + domain              + Environment.NewLine
                      + "Utilisateur : " + user;
    }

    // ── Double-clic : afficher ou ouvrir ────────────────────────────────────

    private void ConnectionsList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      if (e.ChangedButton != MouseButton.Left)
        return;

      var element = e.OriginalSource as DependencyObject;
      if (element == null || ItemsControl.ContainerFromElement(ConnectionsList, element) == null)
        return;

      var vm = ConnectionsList.SelectedItem as ConnectionViewModel;
      if (vm == null)
        return;

      var id = vm.Connection.Id;

      if (_activeClients.TryGetValue(id, out var existingClient))
      {
        // Connexion déjà ouverte → afficher dans le panneau de droite
        AppLogger.Log($"[UI] Double-clic sur '{vm.DisplayName}' — session déjà active, bascule affichage.");
        ShowClient(existingClient);
      }
      else
      {
        // Nouvelle connexion → créer et connecter
        AppLogger.Log($"[UI] Double-clic sur '{vm.DisplayName}' — lancement d'une nouvelle session.");
        LaunchConnection(vm);
      }
    }

    // ── Boutons ─────────────────────────────────────────────────────────────

    private void AddButton_OnClick(object sender, RoutedEventArgs e)
    {
      var dialog = new AddConnectionWindow { Owner = this };
      if (dialog.ShowDialog() != true || dialog.ResultConnection == null)
        return;

      var vm = new ConnectionViewModel(dialog.ResultConnection);
      _viewModels.Add(vm);
      ConnectionsList.SelectedItem = vm;
      PersistConnections();
    }

    private void EditButton_OnClick(object sender, RoutedEventArgs e)
    {
      var vm = ConnectionsList.SelectedItem as ConnectionViewModel;
      if (vm == null)
      {
        MessageBox.Show(this, "Sélectionnez une connexion à modifier.", "Fred RDP Manager",
            MessageBoxButton.OK, MessageBoxImage.Information);
        return;
      }

      var dialog = new AddConnectionWindow(vm.Connection) { Owner = this };
      if (dialog.ShowDialog() != true || dialog.ResultConnection == null)
        return;

      var ix = _viewModels.IndexOf(vm);
      if (ix < 0)
        return;

      var updated = new ConnectionViewModel(dialog.ResultConnection)
      {
        IsConnected = vm.IsConnected   // conserver l'état visuel si le client tourne encore
      };
      _viewModels[ix] = updated;
      ConnectionsList.SelectedItem = updated;
    }

    private void RemoveButton_OnClick(object sender, RoutedEventArgs e)
    {
      var vm = ConnectionsList.SelectedItem as ConnectionViewModel;
      if (vm == null)
      {
        MessageBox.Show(this, "Sélectionnez une connexion à supprimer.", "Fred RDP Manager",
            MessageBoxButton.OK, MessageBoxImage.Information);
        return;
      }

      var response = MessageBox.Show(this,
          "Supprimer la connexion « " + vm.DisplayName + " » ?",
          "Fred RDP Manager",
          MessageBoxButton.YesNo,
          MessageBoxImage.Question);
      if (response != MessageBoxResult.Yes)
        return;

      // Déconnecter si actif
      if (_activeClients.TryGetValue(vm.Connection.Id, out var client))
      {
        try { client.Disconnect(); }
        catch { /* ignore */ }
        _activeClients.Remove(vm.Connection.Id);
        if (_currentConnectionId == vm.Connection.Id)
        {
          RdpHost.Child = null;
          _currentConnectionId = Guid.Empty;
        }
      }

      _viewModels.Remove(vm);
      UpdateDetail();
      PersistConnections();
    }

    private void ConnectButton_OnClick(object sender, RoutedEventArgs e)
    {
      var vm = ConnectionsList.SelectedItem as ConnectionViewModel;

      if (vm == null)
      {
        MessageBox.Show(this, "Sélectionnez une connexion.", "Fred RDP Manager",
            MessageBoxButton.OK, MessageBoxImage.Information);
        return;
      }

      // Si déjà connecté, juste afficher ; sinon créer une nouvelle session
      if (_activeClients.TryGetValue(vm.Connection.Id, out var existing))
        ShowClient(existing);
      else
        LaunchConnection(vm);
    }

    // ── Logique de connexion ─────────────────────────────────────────────────

    private void LaunchConnection(ConnectionViewModel vm)
    {
      AppLogger.Log($"[UI] LaunchConnection — '{vm.DisplayName}'  Id={vm.Connection.Id}");

      try
      {
        var client = new RdpWinFormsClient { ConnectionId = vm.Connection.Id };

        client.ConnectedChanged += (s, ev) =>
        {
          // Toujours sur le thread UI (événement ActiveX)
          var wasConnected = vm.IsConnected;
          vm.IsConnected = client.IsConnected;

          AppLogger.Log($"[UI] ConnectedChanged — '{vm.DisplayName}'  IsConnected={client.IsConnected}  (était={wasConnected})");

          if (!client.IsConnected)
          {
            _activeClients.Remove(client.ConnectionId);
            AppLogger.Log($"[UI] Client retiré du dictionnaire — Id={client.ConnectionId}");

            // Si c'était la fenêtre affichée, on vide le panneau
            if (_currentConnectionId == client.ConnectionId)
            {
              RdpHost.Child = null;
              _currentConnectionId = Guid.Empty;
            }
          }
        };

        _activeClients[vm.Connection.Id] = client;
        AppLogger.Log($"[UI] Client ajouté au dictionnaire — Id={vm.Connection.Id}  total actifs={_activeClients.Count}");

        client.Connect(vm.Connection);
        ShowClient(client);
        AppLogger.Log($"[UI] ShowClient() appelé — connexion async démarrée.");
      }
      catch (Exception ex)
      {
        AppLogger.LogError($"[UI] Exception dans LaunchConnection pour '{vm.DisplayName}'.", ex);
        MessageBox.Show(this, ex.Message, "Connexion RDP",
            MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private void ShowClient(RdpWinFormsClient client)
    {
      AppLogger.Log($"[UI] ShowClient — Id={client.ConnectionId}  (précédent={_currentConnectionId})");
      _currentConnectionId = client.ConnectionId;
      RdpHost.Child = client;
    }
  }
}
