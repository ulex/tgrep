using core.util;
using core.util.files;

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

    return indexPath ?? GetDefaultIndexDirectory();
  }

  public static string? FindExistingIndex(string origin)
  {
    foreach (var directory in FsUtil.Parents(origin))
    {
      var indexFileName = GetIndexFilePath(directory);
      if (File.Exists(indexFileName))
        return indexFileName;
    }

    return null;
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