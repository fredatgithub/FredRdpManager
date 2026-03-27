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

    // Clients RDP actifs, liés au TabControl
    private readonly ObservableCollection<RdpWinFormsClient> _activeSessionsCollection =
        new ObservableCollection<RdpWinFormsClient>();

    // Dictionnaire pour recherche rapide par Id
    private readonly Dictionary<Guid, RdpWinFormsClient> _activeClients =
        new Dictionary<Guid, RdpWinFormsClient>();

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
      
      SessionsTabControl.ItemsSource = _activeSessionsCollection;
      
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
      AppLogger.Log($"=== Fermeture — {_activeSessionsCollection.Count} client(s) actif(s) à déconnecter ===");

      foreach (var client in _activeSessionsCollection)
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

    // ── Gestion des Onglets ─────────────────────────────────────────────────

    private void SessionsTabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (SessionsTabControl.SelectedItem is RdpWinFormsClient client)
      {
         ShowClient(client);
         
         // Synchroniser la liste de gauche
         var vm = _viewModels.FirstOrDefault(x => x.Connection.Id == client.ConnectionId);
         if (vm != null && ConnectionsList.SelectedItem != vm)
         {
             ConnectionsList.SelectedItem = vm;
         }
      }
      else
      {
         RdpContainer.Visibility = Visibility.Collapsed;
         ActiveRdpHost.Child = null;
      }
    }

    private void CloseTabButton_OnClick(object sender, RoutedEventArgs e)
    {
      if (sender is Button btn && btn.Tag is RdpWinFormsClient client)
      {
         AppLogger.Log($"[UI] Fermeture onglet — Id={client.ConnectionId}");
         client.Disconnect();
         _activeSessionsCollection.Remove(client);
         _activeClients.Remove(client.ConnectionId);
         
         var vm = _viewModels.FirstOrDefault(x => x.Connection.Id == client.ConnectionId);
         if (vm != null) vm.IsConnected = false;
      }
    }

    // ── Sélection dans la liste ─────────────────────────────────────────────

    private void ConnectionsList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      UpdateDetail();
      
      // Si la connexion sélectionnée est déjà active, basculer sur l'onglet
      if (ConnectionsList.SelectedItem is ConnectionViewModel vm)
      {
          var active = _activeSessionsCollection.FirstOrDefault(x => x.ConnectionId == vm.Connection.Id);
          if (active != null && SessionsTabControl.SelectedItem != active)
          {
              SessionsTabControl.SelectedItem = active;
          }
      }
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
        AppLogger.Log($"[UI] Double-clic sur '{vm.DisplayName}' — session déjà active, bascule onglet.");
        SessionsTabControl.SelectedItem = _activeSessionsCollection.FirstOrDefault(x => x.ConnectionId == id);
      }
      else
      {
        AppLogger.Log($"[UI] Double-clic sur '{vm.DisplayName}' — lancement nouvelle session.");
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
      if (vm == null) return;

      var dialog = new AddConnectionWindow(vm.Connection) { Owner = this };
      if (dialog.ShowDialog() != true || dialog.ResultConnection == null)
        return;

      var ix = _viewModels.IndexOf(vm);
      if (ix < 0) return;

      var updated = new ConnectionViewModel(dialog.ResultConnection) { IsConnected = vm.IsConnected };
      _viewModels[ix] = updated;
      ConnectionsList.SelectedItem = updated;
    }

    private void RemoveButton_OnClick(object sender, RoutedEventArgs e)
    {
      var vm = ConnectionsList.SelectedItem as ConnectionViewModel;
      if (vm == null) return;

      var response = MessageBox.Show(this, "Supprimer la connexion « " + vm.DisplayName + " » ?", "Fred RDP Manager", MessageBoxButton.YesNo, MessageBoxImage.Question);
      if (response != MessageBoxResult.Yes) return;

      if (_activeClients.TryGetValue(vm.Connection.Id, out var client))
      {
        client.Disconnect();
        _activeSessionsCollection.Remove(client);
        _activeClients.Remove(vm.Connection.Id);
      }

      _viewModels.Remove(vm);
      UpdateDetail();
      PersistConnections();
    }

    private void ConnectButton_OnClick(object sender, RoutedEventArgs e)
    {
      var vm = ConnectionsList.SelectedItem as ConnectionViewModel;
      if (vm == null) return;

      if (_activeClients.TryGetValue(vm.Connection.Id, out var existing))
        SessionsTabControl.SelectedItem = _activeSessionsCollection.FirstOrDefault(x => x.ConnectionId == vm.Connection.Id);
      else
        LaunchConnection(vm);
    }

    // ── Logique de connexion ─────────────────────────────────────────────────

    private void LaunchConnection(ConnectionViewModel vm)
    {
      AppLogger.Log($"[UI] LaunchConnection — '{vm.DisplayName}'");

      try
      {
        var client = new RdpWinFormsClient { 
            ConnectionId = vm.Connection.Id,
            DisplayName = vm.DisplayName 
        };

        client.ConnectedChanged += (s, ev) =>
        {
          vm.IsConnected = client.IsConnected;
          if (!client.IsConnected)
          {
             // On ne ferme pas l'onglet automatiquement pour permettre à l'utilisateur de lire l'erreur si besoin
             // Mais on pourrait le faire ici si souhaité
          }
        };

        _activeClients[vm.Connection.Id] = client;
        _activeSessionsCollection.Add(client);
        
        SessionsTabControl.SelectedItem = client; 
        client.Connect(vm.Connection);
      }
      catch (Exception ex)
      {
        AppLogger.LogError($"[UI] Exception dans LaunchConnection pour '{vm.DisplayName}'.", ex);
        MessageBox.Show(this, ex.Message, "Connexion RDP", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private void ShowClient(RdpWinFormsClient client)
    {
      AppLogger.Log($"[UI] ShowClient — Id={client.ConnectionId}");
      RdpContainer.Visibility = Visibility.Visible;
      
      // Pour éviter les clignotements, on ne réassigne que si nécessaire
      if (ActiveRdpHost.Child != client)
      {
          ActiveRdpHost.Child = client;
      }
    }
  }
}
