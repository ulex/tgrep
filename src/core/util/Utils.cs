using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using JetBrains.Annotations;
using JetBrains.Serialization;

namespace core.util;

public static class Utils
{
  public static string BytesToString(long byteCount)
  {
    string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
    if (byteCount == 0)
      return "0" + suf[0];
    long bytes = Math.Abs(byteCount);
    int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
    double num = Math.Round(bytes / Math.Pow(1024, place), 1);
    return (Math.Sign(byteCount) * num).ToString() + suf[place];
  }

  public static int CommonPrefixLength(string x, string y)
  {
    int len = 0;
    for (; len < Math.Min(x.Length, y.Length); len++)
    {
      if (x[len] != y[len])
        break;
    }
    return len;
  }

  public static IReadOnlyCollection<int> ReadTrigrams(string path, bool ignoreBinaryFiles = true)
  {
    var m = new TrigramCollector();

    const int fileBufSize = 4096;
    const int bufSize =  + 4;
    var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, fileBufSize);
    var reader = new StreamReader(fileStream);
    reader.Peek(); // detect file encoding

    // possible optimization, todo: proper bom handle
    // if (Equals(reader.CurrentEncoding, Encoding.UTF8))
    // {
    //   fileStream.Position = 0;
    //   while (Rune.DecodeFromUtf16(span, out Rune rune, out int charsConsumed) == OperationStatus.Done)
    //   {
    //     if (Rune.IsLetterOrDigit(rune))
    //     { break; }
    //     span = span[charsConsumed..];
    //   }
    // }
    var fileContent = reader.ReadToEnd();// todo: traffic/memory usage
    foreach (var rune in fileContent.EnumerateRunes())
    {
      if (!m.Feed(rune.Value))
      {
        m.Trigrams.Clear();
        break;
      }
    }

    return m.Trigrams;
  }

  public struct TrigramCollector
  {
    public readonly HashSet<int> Trigrams = new(); // todo: sparse set

    private long _position = 0;
    private int _tc = 0;

    public TrigramCollector()
    {
    }

    /// <summary>
    /// feed unicode code points. In case of zero (=probably binary file) returns false
    /// </summary>
    [MustUseReturnValue]
    public bool Feed(int codePoint)
    {
      if (_position > 2)
      {
        _tc = (_tc << 8 | HashChar(codePoint)) & 0x00FFFFFF;
        if (Trigrams.Add(_tc))
        {
          if (codePoint == 0)
          {
            return false;
          }
        }
      }
      else if (_position == 0)
      {
        _tc |= HashChar(codePoint);
      }
      else if (_position == 1)
      {
        _tc = HashChar(codePoint) << 8;
      }

      _position++;
      return true;
    }
  }


  public static int ReadZigZagVarint(this BinaryReader reader)
  {
    int raw = ReadVarint(reader);
    int result = raw >> 1 ^ -(raw & 1);
    return result;
  }

  public static uint ReadUVarint(this BinaryReader reader) => unchecked((uint)ReadVarint(reader));
  
  public static int ReadVarint(this BinaryReader reader)
  {
    int result = 0;
    int shift = 0;
    while (true)
    {
      byte b = reader.ReadByte();
      result |= (b & 0x7F) << shift;
      if ((b & 0x80) == 0)
        break;
      shift += 7;
    }

    return result;
  }
  public static uint ReadUVarint(this UnsafeReader reader) => unchecked((uint)ReadVarint(reader));
  
  public static int ReadVarint(this UnsafeReader reader)
  {
    int result = 0;
    int shift = 0;
    while (true)
    {
      byte b = reader.ReadByte();
      result |= (b & 0x7F) << shift;
      if ((b & 0x80) == 0)
        break;
      shift += 7;
    }

    return result;
  }

  public static void WriteVarint(this BinaryWriter writer, int value)
  {
    while (value >= 0x80)
    {
      writer.Write((byte)(value | 0x80));
      value >>= 7;
    }

    writer.Write((byte)value);
  }

  public static void WriteZigZagVarint(this BinaryWriter writer, int value)
  {
    int zigZagEncoded = ((value << 1) ^ (value >> 31));
    WriteVarint(writer, zigZagEncoded);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
  public static byte HashChar(int codePoint)
  {
    // todo: proper hash function
    return unchecked((byte)(codePoint ^ (codePoint >> 8) ^ (codePoint >> 16)));
  }
}