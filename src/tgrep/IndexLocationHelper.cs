using core.util;

namespace tgrep;

public class IndexLocationHelper
{
  public static string GetIndexPath(Options opts, string directory)
  {
    var indexPath = Environment.GetEnvironmentVariable("TGREP_INDEX_PATH");
    if (indexPath == null)
      indexPath = opts.IndexPath;
    if (indexPath == null)
    {
      var indexDirectory = GetDefaultIndexDirectory();
      if (!Directory.Exists(indexDirectory))
        Directory.CreateDirectory(indexDirectory);

      indexPath = FindExistingIndex(directory);
    }

    return indexPath;
  }

  private static string FindExistingIndex(string origin)
  {
    var directory = origin;
    while (!string.IsNullOrEmpty(directory))
    {
      var directoryInfo = new DirectoryInfo(directory);

      var indexFileName = GetIndexFilePath(directory);
      if (File.Exists(indexFileName))
        return indexFileName;
      
      directory = directoryInfo.Parent?.FullName;
    }

    return GetIndexFilePath(origin);
  }

  private static string GetIndexFilePath(string directory)
  {
    directory = directory.TrimEnd(Path.DirectorySeparatorChar);
    var fileName = Path.GetFileName(directory) + "." + StableHash.String(directory).ToString("x8");

    return Path.Combine(GetDefaultIndexDirectory(), fileName);
  }

  public static string GetDefaultIndexDirectory()
  {
    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".tgrep");
  }
}