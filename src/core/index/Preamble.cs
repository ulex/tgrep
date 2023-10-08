using System.Runtime.InteropServices;
using core.util;

namespace core;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Preamble
{
  private static readonly byte[] magic = "IDX_ULEX"u8.ToArray();
  public static int Sizeof = Marshal.SizeOf<Preamble>() + magic.Length;
  
  public int FormatVersion = 1;

  public long StringsOffset;
  public long DocumentsTableOffset;
  public long TrigramIndexOffset;
  public long Length;

  /** including ending EOF document   */
  public readonly int DocumentsCount => (int)(TrigramIndexOffset - DocumentsTableOffset) / DocRow.Sizeof;

  public long PostingListOffset => Sizeof;

  public Preamble(int formatVersion, long stringsOffset, long trigramIndexOffset, long documentsTableOffset, long length)
  {
    FormatVersion = formatVersion;
    StringsOffset = stringsOffset;
    TrigramIndexOffset = trigramIndexOffset;
    DocumentsTableOffset = documentsTableOffset;
    Length = length;
  }

  public unsafe void WriteTo(Stream writer)
  {
    Span<byte> buffer = stackalloc byte[Marshal.SizeOf(typeof(Preamble))];
    MemoryMarshal.Write(buffer, ref this);
      
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


  public override string ToString()
  {
    return $"Documents: {DocumentsCount}, sizes: Preamble: {Utils.BytesToString(Sizeof)}, Posting lists: {Utils.BytesToString(StringsOffset - Sizeof)}, File table: {Utils.BytesToString(TrigramIndexOffset - StringsOffset)}, Trigram index: {Utils.BytesToString(Length - TrigramIndexOffset)}";
  }
}