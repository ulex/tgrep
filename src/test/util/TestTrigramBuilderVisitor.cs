using core.util;
using core.util.files;
using JetBrains.Annotations;

namespace test.dev;

public class TestTrigramBuilderVisitor
{
  private readonly string _gitRepoPath;
  public string OutDir { get; } = "C:\\bench";
  public string RepoName { get; }

  public TestTrigramBuilderVisitor(string gitRepositoryPath)
  {
    _gitRepoPath = gitRepositoryPath;
    RepoName = Path.GetFileName(_gitRepoPath);
  }

  public string OutName(string name)
  {
    return Path.Combine(OutDir, RepoName + name);
  }

  public Task AcceptAllFiles([InstantHandle] Action<string, long, IReadOnlyCollection<int>> visitor, bool sync = false)
  {
    return FileScannerBuilder.Build(_gitRepoPath, sync).Visit(i =>
    {
      if (i.IsDirectory)
        return true;

      var trigrams = Utils.ReadTrigrams(i.Path);
      visitor(i.Path, i.ModStamp, trigrams);
      return true;
    });
  }
}

