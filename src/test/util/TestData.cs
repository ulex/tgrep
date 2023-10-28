using System.IO.Compression;
using JetBrains.Lifetimes;

namespace test.util;
using Data = (string Name, Uri Uri);

public static class TestData
{
  public static Data Vim91 = ("vim91", new Uri("https://github.com/vim/vim/archive/refs/tags/v9.0.2077.zip"));
  public static Data RipGrep = ("rg13", new Uri("https://github.com/BurntSushi/ripgrep/releases/download/13.0.0/ripgrep-13.0.0-x86_64-pc-windows-msvc.zip"));

  private static string OutDirecotry => Path.GetDirectoryName(typeof(TestData).Assembly.Location)!;

  /// Download data package and return a full path to the directory, where it was extracted.
  public static async Task<string> GetData(Data d)
  {
    var outDir = OutDirecotry;
    var expectedPath = Path.Combine(outDir, d.Name);
    if (Directory.Exists(expectedPath))
    {
      return expectedPath;
    }

    // download
    using var http = new HttpClient();
    var array = await http.GetByteArrayAsync(d.Uri);

    var tempDirectory = Path.Combine(outDir, Path.GetRandomFileName());
    Directory.CreateDirectory(tempDirectory);
    try
    {
      using (var archive = new ZipArchive(new MemoryStream(array)))
      {
        archive.ExtractToDirectory(tempDirectory);
      }

      // move only as the last step to always have consistent state in test directories
      var entries = new DirectoryInfo(tempDirectory).GetFileSystemInfos();
      if (entries is [DirectoryInfo fi])
      {
        // remove nesting when archive contains only one directory at root level
        Directory.Move(fi.FullName, expectedPath);
      }
      else
      {
        Directory.Move(tempDirectory, expectedPath);
      }
      
    }
    finally
    {
      if (Directory.Exists(tempDirectory))
        Directory.Delete(tempDirectory, true);
    }

    return expectedPath;
  }

  public static string GetTempDirectory(Lifetime lifetime)
  {
    return lifetime.Bracket(
      () => Directory.CreateDirectory(Path.Combine(OutDirecotry, Path.GetRandomFileName())).FullName + Path.DirectorySeparatorChar, 
      d => Directory.Delete(d, true));
  }
}