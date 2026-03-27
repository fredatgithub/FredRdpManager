using System;
using System.ComponentModel;

namespace FredRdpManager
{
  /// <summary>
  /// ViewModel d'une connexion : expose IsConnected pour la pastille colorée dans la liste.
  /// </summary>
  public sealed class ConnectionViewModel : INotifyPropertyChanged
  {
    private bool _isConnected;

    public RdpConnection Connection { get; }

    public string DisplayName => Connection.DisplayName;

    public bool IsConnected
    {
      get => _isConnected;
      set
      {
        if (_isConnected == value)
          return;
        _isConnected = value;
        OnPropertyChanged(nameof(IsConnected));
      }
    }

    public ConnectionViewModel(RdpConnection connection)
    {
      Connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}
