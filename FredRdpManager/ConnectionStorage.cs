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

    private static string StoragePath =>
      Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "connections.xml");

    public static List<RdpConnection> Load()
    {
      var path = StoragePath;
      if (!File.Exists(path))
        return new List<RdpConnection>();

      try
      {
        using (var stream = File.OpenRead(path))
        {
          var serializer = new XmlSerializer(typeof(List<SerializableConnection>));
          var list = serializer.Deserialize(stream) as List<SerializableConnection>;
          if (list == null)
            return new List<RdpConnection>();

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
        Directory.CreateDirectory(dir);

      var list = connections.Select(ToSerializable).ToList();
      using (var stream = File.Create(path))
      {
        var serializer = new XmlSerializer(typeof(List<SerializableConnection>));
        serializer.Serialize(stream, list);
      }
    }

    private static RdpConnection FromSerializable(SerializableConnection s)
    {
      if (s == null || string.IsNullOrWhiteSpace(s.ServerName))
        return null;

      Guid id;
      if (string.IsNullOrWhiteSpace(s.Id) || !Guid.TryParse(s.Id, out id))
        id = Guid.NewGuid();

      string password = null;
      if (!string.IsNullOrEmpty(s.EncryptedPasswordBase64))
      {
        try
        {
          var encrypted = Convert.FromBase64String(s.EncryptedPasswordBase64);
          var decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
          password = Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
          password = null;
        }
      }

      return new RdpConnection
      {
        Id = id,
        ServerName = s.ServerName ?? "",
        Domain = s.Domain ?? "",
        UserName = s.UserName ?? "",
        Password = password ?? ""
      };
    }

    private static SerializableConnection ToSerializable(RdpConnection c)
    {
      string enc = null;
      if (!string.IsNullOrEmpty(c.Password))
      {
        var plain = Encoding.UTF8.GetBytes(c.Password);
        var protectedBytes = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
        enc = Convert.ToBase64String(protectedBytes);
      }

      return new SerializableConnection
      {
        Id = c.Id.ToString("D"),
        ServerName = c.ServerName,
        Domain = c.Domain,
        UserName = c.UserName,
        EncryptedPasswordBase64 = enc
      };
    }
  }
}
