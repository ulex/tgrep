namespace core.util.files;

public class FileScanner : IFilesScanner
{
  private readonly string _directory;
  private readonly bool _sync;
  private readonly TaskFactory _taskFactory;

  public FileScanner(string directory, bool sync)
  {
    _directory = directory;
    _sync = sync;
    _taskFactory = new TaskFactory(new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, Environment.ProcessorCount).ConcurrentScheduler);
  }

  public Task Visit(Predicate<ScanItem> scanSubtree)
  {
    return TraverseDirectory(new DirectoryInfo(_directory), scanSubtree);
  }

  private Task TraverseDirectory(DirectoryInfo directoryInfo, Predicate<ScanItem> scanSubtree)
  {
    var tasks = new List<Task>();
    try
    {
      var infos = directoryInfo.GetFileSystemInfos();

      foreach (var file in infos)
      {
        var dir = file as DirectoryInfo;
        var scan = scanSubtree(new ScanItem(file.LastAccessTime.ToFileTime(), dir != null, file.FullName));
        if (!scan)
          continue;

        if (dir != null)
        {
          tasks.Add(RunTask(() => TraverseDirectory(dir, scanSubtree)));

        }
      }
    }
    catch (Exception e)
    {
      Console.Error.WriteLine($"Error: {e.Message}");
    }

    return Task.WhenAll(tasks);
  }

  private Task RunTask(Action function)
  {
    if (_sync)
    {
      function();
      return Task.CompletedTask;
    }
    return _taskFactory.StartNew(function);
  }
}