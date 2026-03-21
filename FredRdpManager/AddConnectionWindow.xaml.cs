using System;
using System.Windows;

namespace FredRdpManager
{
  public partial class AddConnectionWindow : Window
  {
    private readonly RdpConnection _existing;

    public RdpConnection ResultConnection { get; private set; }

    public AddConnectionWindow(RdpConnection existing = null)
    {
      InitializeComponent();
      _existing = existing;
      if (existing != null)
      {
        Title = "Modifier la connexion";
        ServerTextBox.Text = existing.ServerName ?? "";
        DomainTextBox.Text = existing.Domain ?? "";
        UserTextBox.Text = existing.UserName ?? "";
        PasswordBox.Password = existing.Password ?? "";
      }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
      var server = (ServerTextBox.Text ?? "").Trim();
      if (string.IsNullOrEmpty(server))
      {
        MessageBox.Show(this, "Indiquez le nom ou l’adresse du serveur.", "Connexion RDP",
          MessageBoxButton.OK, MessageBoxImage.Warning);
        ServerTextBox.Focus();
        return;
      }

      var user = (UserTextBox.Text ?? "").Trim();
      if (string.IsNullOrEmpty(user))
      {
        MessageBox.Show(this, "Indiquez le nom d’utilisateur.", "Connexion RDP",
          MessageBoxButton.OK, MessageBoxImage.Warning);
        UserTextBox.Focus();
        return;
      }

      ResultConnection = new RdpConnection
      {
        Id = _existing != null ? _existing.Id : Guid.NewGuid(),
        ServerName = server,
        Domain = (DomainTextBox.Text ?? "").Trim(),
        UserName = user,
        Password = PasswordBox.Password ?? ""
      };

      DialogResult = true;
    }
  }
}
