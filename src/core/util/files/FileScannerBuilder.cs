﻿using System.Diagnostics;

namespace core.util.files;

public static class FileScannerBuilder
{
  public static IFilesScanner Build(string directory, bool useGitIgnore = true, bool sync = false)
  {
    var nativeScanner = CreateNativeScanner(directory, sync);

    GitignoreParser? parser = null;
    if (useGitIgnore)
    {
      foreach (var parent in FsUtil.Parents(directory))
      {
        var path = Path.Combine(parent, ".gitignore");
        if (File.Exists(path))
        {
          parser = new GitignoreParser(path, true);
        }
      }

      if (parser != null)
        return new GitIgnoreScanner(directory, parser, nativeScanner);
    }
    return nativeScanner;
  }

  private static IFilesScanner CreateNativeScanner(string directory, bool sync)
  {
    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
      return new WindowsFilesScanner(directory, sync);

    return new FileScanner(directory, sync);
  }

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
          Debug.WriteLine($"Git: reject {i.Path}");
#endif
          return false;
        }
#if DEBUG
        Debug.WriteLine($"Git: scan {i.Path}");
#endif
        return scanSubtree(i);
      });
    }
  }
}