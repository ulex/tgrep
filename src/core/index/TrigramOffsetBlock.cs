using System.Runtime.InteropServices;

namespace core;

public record struct TrigramOffsetBlock(
  int Trigram,
  int Length)
{
  /*
  public void Write(Stream stream)
  {
    Span<byte> span = stackalloc byte[sizeof(int) + sizeof(long)];
    int trigram = Trigram;
    MemoryMarshal.Write(span[..4], ref trigram);
    long startOffset = StartOffset;
    MemoryMarshal.Write(span[4..], ref startOffset);
    stream.Write(span);
  }
*/
};