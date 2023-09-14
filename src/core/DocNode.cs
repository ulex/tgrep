using System.Runtime.InteropServices;

namespace core;

public record struct DocNode(
  int DocId, 
  string Path, 
  long LastWriteTime);

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public record struct DocRow(
  UInt32 PathOffset,
  Int64 ModificationStamp
)
{
  public static int Sizeof = Marshal.SizeOf<DocRow>();
}