using System.Buffers;
using System.Runtime.CompilerServices;
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

    var reader = new StreamReader(path);
    var pool = ArrayPool<char>.Shared;
    var chars = pool.Rent(4096);
      int c;
    while ((c = reader.ReadBlock(chars, 0, chars.Length)) > 0)
    {
      if (!m.Feed(chars.AsSpan(0, c)))
      {
        m.Trigrams.Clear();
        break;
      }
    }
    pool.Return(chars);

    return m.Trigrams;
  }

  public struct TrigramCollector
  {
    public readonly HashSet<int> Trigrams = new(); // todo: sparse set

    private long _position = 0;
    private int _ti = 0;
    private int _tc = 0;

    public TrigramCollector()
    {
    }

    /// <summary>
    /// feed chars. In case of zero (=probably binary file) returns false
    /// </summary>
    [MustUseReturnValue]
    public bool Feed(Span<char> r)
    {
      if (r.Length > 2 && _position == 0)
      {
        _tc = HashChar(r[0]) << 8;
        _tc |= HashChar(r[1]);
        
        _ti = HashChar(char.ToUpperInvariant(r[0])) << 8;
        _ti |= HashChar(char.ToUpperInvariant(r[1]));
      }

      for (int i = _position == 0 ? 2 : 0; i < r.Length; i++)
      {
        _tc = (_tc << 8 | HashChar(r[i])) & 0x00FFFFFF;
        _ti = (_ti << 8 | HashChar(char.ToUpperInvariant(r[i]))) & 0x00FFFFFF;
        if (Trigrams.Add(_tc) | Trigrams.Add(_ti)) // NOTE: Intentially use non-lazy OR to evaluate both branches in any case
        {
          if (r[i] == 0)
          {
            return false;
          }
        }
      }

      _position += r.Length;
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
  public static byte HashChar(char b)
  {
    return (byte)(b & 0xFF ^ (b >> 8));
  }
}