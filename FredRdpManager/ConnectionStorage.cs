using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace FredRdpManager
{
  internal static class ConnectionStorage
  {
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FredRdpManager v1");

    private static string StoragePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "connections.xml");

    public static List<RdpConnection> Load()
    {
      var path = StoragePath;
      if (!File.Exists(path))
      {
        return new List<RdpConnection>();
      }

      try
      {
        using (var stream = File.OpenRead(path))
        {
          var serializer = new XmlSerializer(typeof(List<SerializableConnection>));
          var list = serializer.Deserialize(stream) as List<SerializableConnection>;
          if (list == null)
          {
            return new List<RdpConnection>();
          }

          return list.Select(FromSerializable).Where(c => c != null).ToList();
        }
      }
      catch
      {
        return new List<RdpConnection>();
      }
    }

    public static void Save(IEnumerable<RdpConnection> connections)
    {
      var path = StoragePath;
      var dir = Path.GetDirectoryName(path);
      if (!string.IsNullOrEmpty(dir))
      {
        Directory.CreateDirectory(dir);
      }

      var list = connections.Select(ToSerializable).ToList();
      using (var stream = File.Create(path))
      {
        var serializer = new XmlSerializer(typeof(List<SerializableConnection>));
        serializer.Serialize(stream, list);
      }
    }

    private static RdpConnection FromSerializable(SerializableConnection serializableConnection)
    {
      if (serializableConnection == null || string.IsNullOrWhiteSpace(serializableConnection.ServerName))
        return null;

      Guid guid;
      if (string.IsNullOrWhiteSpace(serializableConnection.Id) || !Guid.TryParse(serializableConnection.Id, out guid))
      {
        guid = Guid.NewGuid();
      }

      string password = null;
      if (!string.IsNullOrEmpty(serializableConnection.EncryptedPasswordBase64))
      {
        try
        {
          var encrypted = Convert.FromBase64String(serializableConnection.EncryptedPasswordBase64);
          var decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
          password = Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
          password = null;
        }
      }

      var port = NormalizePort(serializableConnection.Port);

      return new RdpConnection
      {
        Id = guid,
        ServerName = serializableConnection.ServerName ?? string.Empty,
        Port = port,
        Domain = serializableConnection.Domain ?? string.Empty,
        UserName = serializableConnection.UserName ?? string.Empty,
        Password = password ?? string.Empty
      };
    }

    private static SerializableConnection ToSerializable(RdpConnection rdpConnection)
    {
      string encoded = null;
      if (!string.IsNullOrEmpty(rdpConnection.Password))
      {
        var plain = Encoding.UTF8.GetBytes(rdpConnection.Password);
        var protectedBytes = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
        encoded = Convert.ToBase64String(protectedBytes);
      }

      return new SerializableConnection
      {
        Id = rdpConnection.Id.ToString("D"),
        ServerName = rdpConnection.ServerName,
        Port = NormalizePort(rdpConnection.Port),
        Domain = rdpConnection.Domain,
        UserName = rdpConnection.UserName,
        EncryptedPasswordBase64 = encoded
      };
    }

    private static int NormalizePort(int port)
    {
      if (port <= 0 || port > 65535)
      {
        return 3389;
      }

      return port;
    }
  }
}
