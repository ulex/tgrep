using System.Diagnostics;

namespace core.util;

[DebuggerDisplay("Dir:{IsDir}, {Name}, {Size} bytes")]
public class FsItem
{
  public FsItem(string name, long size, bool isDir, long lastModified = default)
  {
    Name = name;
    Size = size;
    IsDir = isDir;
    LastModified = lastModified;
    if (lastModified == default) lastModified = long.MaxValue;
  }

  public string Name { get; private set; }
  public long Size { get; set; }
  public bool IsDir { get; private set; }
  public long LastModified { get; private set; }

  public List<FsItem>? Items { get; set; }
}