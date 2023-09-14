namespace core.util;

public class FastFilesVisitory
{
  [ThreadStatic]
  private static DirectoryScanner? _scanner;

  public static void VisitFiles(string dir, Action<string, FsItem> fileConsumer, bool sync = false)
  {
    var directory = dir.TrimEnd('/', '\\');
    var item = new FsItem(Path.GetFileName(directory), 0, true);
    ScanRecursive(item, Path.GetDirectoryName(directory) + "\\", fileConsumer, sync);
  }

  private static void ScanRecursive(FsItem item, string parentPath, Action<string, FsItem> fileConsumer, bool sync = false)
  {
    var scanObject = parentPath + item.Name;
    if (scanObject[^1] != Path.DirectorySeparatorChar) scanObject += Path.DirectorySeparatorChar;

    _scanner ??= new DirectoryScanner(true);

    item.Items = _scanner.Scan(scanObject);
    if (item.Items == null)
      return; //Access to directory denied

    var tasks = new List<Task>();
    for (var i = item.Items.Count - 1; i >= 0; i--)
    {
      var child = item.Items[i];
      if (child.IsDir)
      {
        if (sync)
          ScanRecursive(child, scanObject, fileConsumer, sync);
        else
          tasks.Add(Task.Run(() => ScanRecursive(child, scanObject, fileConsumer, sync)));
      }
      else
      {
        fileConsumer(scanObject, child);
      }

      item.Size += child.Size;
    }
    if (tasks.Count > 0)
      Task.WaitAll(tasks.ToArray());
  }
}