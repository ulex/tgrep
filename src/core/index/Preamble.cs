using System.Runtime.InteropServices;
using core.util;

namespace core;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Preamble
{
  private static readonly byte[] magic = "IDX_ULEX"u8.ToArray();
  public static int Sizeof = Marshal.SizeOf<Preamble>() + magic.Length;
  
  public readonly int FormatVersion = 1;

  public readonly long StringsOffset;
  public readonly long DocumentsTableOffset;
  public readonly long TrigramIndexOffset;

  public readonly long Length;

  /** including ending EOF document   */
  public int DocumentsCount => (int)(TrigramIndexOffset - DocumentsTableOffset) / DocRow.Sizeof;

  public long PostingListOffset => Sizeof;

  public Preamble(int formatVersion, long stringsOffset, long trigramIndexOffset, long documentsTableOffset, long length)
  {
    FormatVersion = formatVersion;
    StringsOffset = stringsOffset;
    DocumentsTableOffset = documentsTableOffset;
    TrigramIndexOffset = trigramIndexOffset;
    Length = length;
  }

  public unsafe void WriteTo(Stream writer)
  {
    var copy = this;
    Span<byte> buffer = stackalloc byte[Marshal.SizeOf(typeof(Preamble))];
    MemoryMarshal.Write(buffer, ref copy);
      
    writer.Write(magic);
    writer.Write(buffer);
  }

  public static unsafe Preamble Read(Stream stream)
  {
    var sig = new byte[8];
    stream.ReadExactly(sig);
    if (!sig.SequenceEqual(magic))
      throw new InvalidOperationException("Invalid signature");

    byte[] buffer = new byte[Marshal.SizeOf(typeof(Preamble))];
    stream.ReadExactly(buffer);
    fixed (byte* b = buffer)
      return Marshal.PtrToStructure<Preamble>((IntPtr)b);
  }

  public Stats GetStats() => new(DocumentsCount, 
    PostingListsLength: StringsOffset - PostingListOffset, 
    StringsLength: DocumentsTableOffset - StringsOffset, 
    FileTableLength: TrigramIndexOffset - DocumentsTableOffset, 
    NgramTableLength: Length - TrigramIndexOffset);

  public override string ToString()
  {
    return GetStats().ToString();
  }

  public void Dump(TextWriter textWriter)
  {
    textWriter.WriteLine(ToString());
  }

  public record struct Stats(int DocumentCount, long PostingListsLength, long StringsLength, long FileTableLength, long NgramTableLength)
  {
    public static Stats Default => default;

    public long LPreamble => Preamble.Sizeof;
    public long Length => LPreamble + PostingListsLength + StringsLength + FileTableLength + NgramTableLength;
    
    public override string ToString()
    {
      long totalSize = Length;
      string FmtSize(long size) => $"{Utils.BytesToString(size)} ({size * 100 / totalSize}%)";
      return $"""
              Total size: {Utils.BytesToString(Length)}, contains {DocumentCount} documents
                Posting lists: {FmtSize(PostingListsLength)},
                Strings:       {FmtSize(StringsLength)},
                File table:    {FmtSize(FileTableLength)},
                Ngrams table:  {FmtSize(NgramTableLength)}
              """;
    }

    public static Stats operator +(Stats a, Stats b) => new(
      a.DocumentCount + b.DocumentCount,
      a.PostingListsLength + b.PostingListsLength,
      a.StringsLength + b.StringsLength,
      a.FileTableLength + b.FileTableLength,
      a.NgramTableLength + b.NgramTableLength);
  }
}