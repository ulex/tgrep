namespace core.util.files;

public class GitIgnoreScanner : IFilesScanner
{
  private readonly string _rootDirectory;
  private readonly GitignoreParser _parser;
  private readonly IFilesScanner _nativeScanner;

  public GitIgnoreScanner(string rootDirectory, GitignoreParser parser, IFilesScanner nativeScanner)
  {
    _rootDirectory = rootDirectory;
    _parser = parser;
    _nativeScanner = nativeScanner;
  }

  public Task Visit(Predicate<ScanItem> scanSubtree)
  {
    return _nativeScanner.Visit(i =>
    {
      if (_parser.Denies(FsUtil.TryMakeRelative(_rootDirectory, i.Path)))
      {
#if DEBUG
        System.Diagnostics.Debug.WriteLine($"Git: reject {i.Path}");
#endif
        return false;
      }
#if DEBUG
      System.Diagnostics.Debug.WriteLine($"Git: scan {i.Path}");
#endif
      return scanSubtree(i);
    });
  }
}