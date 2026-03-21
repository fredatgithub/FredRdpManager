using System;
using System.IO;
using System.Windows;
using System.Xml.Serialization;

namespace FredRdpManager
{
  internal static class WindowLayoutStorage
  {
    private static string StoragePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window-layout.xml");

    public static void TryApply(Window window)
    {
      if (window == null)
      {
        return;
      }

      var path = StoragePath;
      if (!File.Exists(path))
      {
        return;
      }

      SerializableWindowLayout data;
      try
      {
        using (var stream = File.OpenRead(path))
        {
          var serializer = new XmlSerializer(typeof(SerializableWindowLayout));
          data = serializer.Deserialize(stream) as SerializableWindowLayout;
        }
      }
      catch
      {
        return;
      }

      if (data == null || data.Width <= 0 || data.Height <= 0)
      {
        return;
      }

      var left = data.Left;
      var top = data.Top;
      var width = data.Width;
      var height = data.Height;

      ClampToVirtualScreen(ref left, ref top, ref width, ref height, window.MinWidth, window.MinHeight);

      window.WindowStartupLocation = WindowStartupLocation.Manual;
      window.Width = width;
      window.Height = height;
      window.Left = left;
      window.Top = top;

      var state = ParseWindowState(data.State);
      if (state == WindowState.Maximized)
      {
        window.WindowState = WindowState.Maximized;
      }
    }

    public static void Save(Window window)
    {
      if (window == null)
      {
        return;
      }

      Rect bounds;
      if (window.WindowState == WindowState.Normal)
        bounds = new Rect(window.Left, window.Top, window.Width, window.Height);
      else
      {
        bounds = window.RestoreBounds;
        if (bounds.IsEmpty)
        {
          bounds = new Rect(window.Left, window.Top, window.Width, window.Height);
        }
      }

      var stateToSave = window.WindowState == WindowState.Minimized ? WindowState.Normal : window.WindowState;

      var data = new SerializableWindowLayout
      {
        Width = bounds.Width,
        Height = bounds.Height,
        Left = bounds.Left,
        Top = bounds.Top,
        State = stateToSave.ToString()
      };

      var path = StoragePath;
      var dir = Path.GetDirectoryName(path);
      if (!string.IsNullOrEmpty(dir))
      {
        Directory.CreateDirectory(dir);
      }

      try
      {
        using (var stream = File.Create(path))
        {
          var serializer = new XmlSerializer(typeof(SerializableWindowLayout));
          serializer.Serialize(stream, data);
        }
      }
      catch
      {
        // ignore write errors (droits, disque plein, etc.)
      }
    }

    private static WindowState ParseWindowState(string s)
    {
      if (string.IsNullOrWhiteSpace(s))
      {
        return WindowState.Normal;
      }

      try
      {
        return (WindowState)Enum.Parse(typeof(WindowState), s, true);
      }
      catch
      {
        return WindowState.Normal;
      }
    }

    private static void ClampToVirtualScreen(ref double left, ref double top, ref double width, ref double height, double minWidth, double minHeight)
    {
      var vl = SystemParameters.VirtualScreenLeft;
      var vt = SystemParameters.VirtualScreenTop;
      var vw = SystemParameters.VirtualScreenWidth;
      var vh = SystemParameters.VirtualScreenHeight;

      if (width < minWidth)
      {
        width = minWidth;
      }

      if (height < minHeight)
      {
        height = minHeight;
      }

      if (width > vw)
      {
        width = vw;
      }

      if (height > vh)
      {
        height = vh;
      }

      if (left < vl)
      {
        left = vl;
      }

      if (top < vt)
      {
        top = vt;
      }

      if (left + width > vl + vw)
      {
        left = vl + vw - width;
      }

      if (top + height > vt + vh)
      {
        top = vt + vh - height;
      }
    }
  }

  public sealed class SerializableWindowLayout
  {
    public double Width { get; set; }
    public double Height { get; set; }
    public double Left { get; set; }
    public double Top { get; set; }
    public string State { get; set; }
  }
}
