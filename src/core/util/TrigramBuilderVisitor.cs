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

  public void Accept([InstantHandle] Action<FileInfo, IReadOnlyCollection<int>> visitor)
  {
    Parallel.ForEach(Utils.VisitFiles(_gitRepoPath), fileInfo =>
    {
      if (fileInfo.Exists)
      {
        var trigrams = Utils.ReadTrigrams(fileInfo.FullName);
        visitor(fileInfo, trigrams);
      }
    });
  }

  public void AcceptAllFiles([InstantHandle] Action<string, DateTime, IReadOnlyCollection<int>> visitor, bool sync = false)
  {
    FastFilesVisitory.VisitFiles(_gitRepoPath, (parent, item) =>
    {
      var path = Path.Combine(parent, item.Name);
      var trigrams = Utils.ReadTrigrams(path);
      visitor(path, item.LastModified, trigrams);
    }, sync);
  }
}

