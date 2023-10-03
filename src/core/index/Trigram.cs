namespace core;

/// todo: what is performance overhead of wrapped int?
public readonly record struct Trigram(int Val)
{
  public Trigram(byte a, byte b, byte c) : this(a << 16 | b << 8 | c)
  {
  }

  public byte A => (byte)(Val >> 16 & 0x0000FF);
  public byte B => (byte)(Val >> 8 & 0x0000FF);
  public byte C => (byte)(Val & 0x0000FF);

  private sealed class ValEqualityComparer : IEqualityComparer<Trigram>
  {
    public bool Equals(Trigram x, Trigram y)
    {
      return x.Val == y.Val;
    }

    public int GetHashCode(Trigram obj)
    {
      return obj.Val;
    }
  }

  public static IEqualityComparer<Trigram> ValComparer { get; } = new ValEqualityComparer();
}
