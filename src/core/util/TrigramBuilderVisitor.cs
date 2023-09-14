using core.util.files;
using JetBrains.Annotations;

namespace core.util;

public class TrigramBuilderVisitor
{
  private readonly string _gitRepoPath;
  public string OutDir { get; } = "C:\\bench";
  public string RepoName { get; }

  public TrigramBuilderVisitor(string gitRepositoryPath)
  {
    _gitRepoPath = gitRepositoryPath;
    RepoName = Path.GetFileName(_gitRepoPath);
  }

  public string OutName(string name)
  {
    return Path.Combine(OutDir, RepoName + name);
  }

  public void AcceptAllFiles([InstantHandle] Action<string, long, IReadOnlyCollection<int>> visitor, bool sync = false)
  {
    FileScannerBuilder.Build(sync).Visit(_gitRepoPath, i => 
    {
      var trigrams = Utils.ReadTrigrams(i.Path);
      visitor(i.Path, i.ModStamp, trigrams);
      return true;
    });
  }
}

