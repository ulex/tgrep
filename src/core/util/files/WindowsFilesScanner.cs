using JetBrains.Annotations;

namespace core.util.files;

public class WindowsFilesScanner : IFilesScanner
{
  [ThreadStatic]
  private static DirectoryScanner? _scanner;

  private readonly TaskFactory _taskFactory;

  public WindowsFilesScanner(bool sync = false)
  {
    _taskFactory = new TaskFactory(new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, Environment.ProcessorCount).ConcurrentScheduler);
  }

  public Task Visit(string dir, Predicate<ScanItem> scanSubtree)
  {
    var directory = dir.TrimEnd('/', '\\');
    var item = new FsItem(Path.GetFileName(directory), 0, true);
    return ScanRecursive(item, Path.GetDirectoryName(directory) + "\\", scanSubtree);
  }

  [MustUseReturnValue]
  private Task ScanRecursive(FsItem item, string parentPath, Predicate<ScanItem> scanSubtree, bool sync = false)
  {
    var scanObject = parentPath + item.Name;
    if (scanObject[^1] != Path.DirectorySeparatorChar) scanObject += Path.DirectorySeparatorChar;

    _scanner ??= new DirectoryScanner(true);

    item.Items = _scanner.Scan(scanObject);
    if (item.Items == null)
      return Task.CompletedTask; //Access to directory denied

    var tasks = new List<Task>();
    for (var i = item.Items.Count - 1; i >= 0; i--)
    {
      var child = item.Items[i];
      
      var scanWeiter = scanSubtree(new ScanItem(child.LastModified, child.IsDir, Path.Combine(scanObject, child.Name)));
      if (!scanWeiter)
        return Task.CompletedTask;

      if (child.IsDir)
      {
        if (sync)
          ScanRecursive(child, scanObject, scanSubtree, sync).Wait();
        else
        {
          tasks.Add(RunTask(() => ScanRecursive(child, scanObject, scanSubtree, sync).Wait()));
        }
      }
    }
    return Task.WhenAll(tasks.ToArray());
  }

  private Task RunTask(Action function)
  {
    return _taskFactory.StartNew(function);
  }
}