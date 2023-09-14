namespace core.util.files;


public class WindowsFilesScanner : IFilesScanner
{
  private readonly string _directory;

  [ThreadStatic]
  private static DirectoryScanner? _scanner;

  private readonly TaskFactory _taskFactory;

  public WindowsFilesScanner(string directory, bool sync = false)
  {
    _directory = directory;
    var concurrentScheduler = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, Environment.ProcessorCount).ConcurrentScheduler;
    _taskFactory = new TaskFactory(CancellationToken.None, TaskCreationOptions.AttachedToParent, TaskContinuationOptions.ExecuteSynchronously, concurrentScheduler);
  }

  public Task Visit(Predicate<ScanItem> scanSubtree)
  {
    var directory = _directory.TrimEnd('/', '\\');
    var item = new FsItem(Path.GetFileName(directory), 0, true);
    return ScanRecursive(item, Path.GetDirectoryName(directory) + "\\", scanSubtree);
  }

  private async Task ScanRecursive(FsItem item, string parentPath, Predicate<ScanItem> scanSubtree, bool sync = false)
  {
    if (!sync)
      await RunTask(() => { }); // HACK: FORCE YIELD to scheduler

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
      
      var scanWeiter = scanSubtree(new ScanItem(child.LastModified, child.IsDir, Path.Combine(scanObject, child.Name)));
      if (!scanWeiter)
      {
        continue;
      }

      if (child.IsDir)
      {
        if (sync)
          ScanRecursive(child, scanObject, scanSubtree, sync).Wait();
        else
        {
          /* attached to parent */
          tasks.Add(ScanRecursive(child, scanObject, scanSubtree, sync));
        }
      }
    }
    await Task.WhenAll(tasks.ToArray());
  }

  private Task RunTask(Action function)
  {
    return _taskFactory.StartNew(function);
  }
}