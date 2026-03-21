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
      var c = ConnectionsList.SelectedItem as RdpConnection;
      if (c == null)
      {
        DetailText.Text = "";
        return;
      }

      var domain = string.IsNullOrWhiteSpace(c.Domain) ? "—" : c.Domain.Trim();
      var user = string.IsNullOrWhiteSpace(c.UserName) ? "—" : c.UserName.Trim();
      var port = c.Port > 0 ? c.Port : 3389;
      DetailText.Text = "Serveur : " + c.ServerName.Trim() + Environment.NewLine
                        + "Port RDP : " + port + Environment.NewLine
                        + "Domaine : " + domain + Environment.NewLine
                        + "Utilisateur : " + user;
    }

    private void AddButton_OnClick(object sender, RoutedEventArgs e)
    {
      var dlg = new AddConnectionWindow { Owner = this };
      if (dlg.ShowDialog() != true || dlg.ResultConnection == null)
        return;

      _connections.Add(dlg.ResultConnection);
      ConnectionsList.SelectedItem = dlg.ResultConnection;
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

      var dlg = new AddConnectionWindow(current) { Owner = this };
      if (dlg.ShowDialog() != true || dlg.ResultConnection == null)
        return;

      var ix = _connections.IndexOf(current);
      if (ix < 0)
        return;
      _connections[ix] = dlg.ResultConnection;
      ConnectionsList.SelectedItem = dlg.ResultConnection;
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

      var r = MessageBox.Show(this,
        "Supprimer la connexion « " + current.DisplayName + " » ?",
        "Fred RDP Manager",
        MessageBoxButton.YesNo,
        MessageBoxImage.Question);
      if (r != MessageBoxResult.Yes)
        return;

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
        return;
      var el = e.OriginalSource as DependencyObject;
      if (el != null && ItemsControl.ContainerFromElement(ConnectionsList, el) != null)
        ConnectSelected();
    }

    private void ConnectSelected()
    {
      var c = ConnectionsList.SelectedItem as RdpConnection;
      if (c == null)
      {
        MessageBox.Show(this, "Sélectionnez une connexion.", "Fred RDP Manager",
          MessageBoxButton.OK, MessageBoxImage.Information);
        return;
      }

      try
      {
        RdpLauncher.Connect(c);
      }
      catch (Exception ex)
      {
        MessageBox.Show(this, ex.Message, "Connexion RDP",
          MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }
  }
}
