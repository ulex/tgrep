namespace core.util.files;

public static class FsUtil
{
  /// return all parent directories including directory itself
  public static IEnumerable<string> Parents(string origin)
  {
    string? directory = origin;
    yield return directory;
    
    while (!string.IsNullOrEmpty(directory))
    {
      var directoryInfo = new DirectoryInfo(directory);
      yield return directory;
      directory = directoryInfo.Parent?.FullName;
    }
  }

  public static string TryMakeRelative(string directory, string path)
  {
    if (path.StartsWith(directory))
      path = path[directory.Length..];
    return path;
  }
}