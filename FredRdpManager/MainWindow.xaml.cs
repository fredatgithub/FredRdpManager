using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FredRdpManager
{
  public partial class MainWindow : Window
  {
    private readonly ObservableCollection<RdpConnection> _connections;
    private RdpWinFormsClient _rdpClient;

    public MainWindow()
    {
      InitializeComponent();
      SourceInitialized += MainWindow_OnSourceInitialized;
      _connections = new ObservableCollection<RdpConnection>(ConnectionStorage.Load());
      ConnectionsList.ItemsSource = _connections;
      ConnectionsList.SelectionChanged += ConnectionsList_OnSelectionChanged;
      UpdateDetail();
    }

    private void MainWindow_OnSourceInitialized(object sender, EventArgs e)
    {
      SourceInitialized -= MainWindow_OnSourceInitialized;
      WindowLayoutStorage.TryApply(this);
    }

    private void MainWindow_OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
      try
      {
        if (_rdpClient != null)
          _rdpClient.Disconnect();
      }
      catch
      {
        // ignore
      }

      WindowLayoutStorage.Save(this);
      PersistConnections();
    }

    private void PersistConnections()
    {
      ConnectionStorage.Save(_connections);
    }

    private void ConnectionsList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      UpdateDetail();
    }

    private void UpdateDetail()
    {
      var connectionList = ConnectionsList.SelectedItem as RdpConnection;
      if (connectionList == null)
      {
        DetailText.Text = string.Empty;
        return;
      }

      var domain = string.IsNullOrWhiteSpace(connectionList.Domain) ? "—" : connectionList.Domain.Trim();
      var user = string.IsNullOrWhiteSpace(connectionList.UserName) ? "—" : connectionList.UserName.Trim();
      var port = connectionList.Port > 0 ? connectionList.Port : 3389;
      DetailText.Text = "Serveur : " + connectionList.ServerName.Trim() + Environment.NewLine
                        + "Port RDP : " + port + Environment.NewLine
                        + "Domaine : " + domain + Environment.NewLine
                        + "Utilisateur : " + user;
    }

    private void AddButton_OnClick(object sender, RoutedEventArgs e)
    {
      var dialog = new AddConnectionWindow { Owner = this };
      if (dialog.ShowDialog() != true || dialog.ResultConnection == null)
        return;

      _connections.Add(dialog.ResultConnection);
      ConnectionsList.SelectedItem = dialog.ResultConnection;
      PersistConnections();
    }

    private void EditButton_OnClick(object sender, RoutedEventArgs e)
    {
      var current = ConnectionsList.SelectedItem as RdpConnection;
      if (current == null)
      {
        MessageBox.Show(this, "Sélectionnez une connexion à modifier.", "Fred RDP Manager",
          MessageBoxButton.OK, MessageBoxImage.Information);
        return;
      }

      var dialog = new AddConnectionWindow(current) { Owner = this };
      if (dialog.ShowDialog() != true || dialog.ResultConnection == null)
        return;

      var ix = _connections.IndexOf(current);
      if (ix < 0)
      {
        return;
      }

      _connections[ix] = dialog.ResultConnection;
      ConnectionsList.SelectedItem = dialog.ResultConnection;
    }

    private void RemoveButton_OnClick(object sender, RoutedEventArgs e)
    {
      var current = ConnectionsList.SelectedItem as RdpConnection;
      if (current == null)
      {
        MessageBox.Show(this, "Sélectionnez une connexion à supprimer.", "Fred RDP Manager",
          MessageBoxButton.OK, MessageBoxImage.Information);
        return;
      }

      var response = MessageBox.Show(this,
        "Supprimer la connexion « " + current.DisplayName + " » ?",
        "Fred RDP Manager",
        MessageBoxButton.YesNo,
        MessageBoxImage.Question);
      if (response != MessageBoxResult.Yes)
      {
        return;
      }

      _connections.Remove(current);
      UpdateDetail();
      PersistConnections();
    }

    private void ConnectButton_OnClick(object sender, RoutedEventArgs e)
    {
      ConnectSelected();
    }

    private void ConnectionsList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      if (e.ChangedButton != MouseButton.Left)
      {
        return;
      }

      var element = e.OriginalSource as DependencyObject;
      if (element != null && ItemsControl.ContainerFromElement(ConnectionsList, element) != null)
      {
        ConnectSelected();
      }
    }

    private void ConnectSelected()
    {
      var connectionList = ConnectionsList.SelectedItem as RdpConnection;
      if (connectionList == null)
      {
        MessageBox.Show(this, "Sélectionnez une connexion.", "Fred RDP Manager",
          MessageBoxButton.OK, MessageBoxImage.Information);
        return;
      }

      try
      {
        EnsureRdpHostCreated();
        _rdpClient.Connect(connectionList);
      }
      catch (Exception exception)
      {
        MessageBox.Show(this, exception.Message, "Connexion RDP",
          MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private void EnsureRdpHostCreated()
    {
      if (_rdpClient != null)
        return;

      _rdpClient = new RdpWinFormsClient();
      RdpHost.Child = _rdpClient;
    }
  }
}
