namespace core.util.files;

public static class FileScannerBuilder
{
  public static IFilesScanner Build(bool sync = false)
  {
    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
      return new WindowsFilesScanner(sync);

    return new FileScanner(sync);
  }
}