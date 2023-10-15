using System.IO.MemoryMappedFiles;
using JetBrains.Lifetimes;
using static core.IndexState;

namespace core;

public class MultiIndex
{
  private readonly List<IndexReader> _indices = new();
  
  public int ICount => _indices.Count;

  public MultiIndex(Lifetime lt, string path)
  {
    var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    lt.AddDispose(fileStream);
    var mmapLength = fileStream.Length;

    var mmap = MemoryMappedFile.CreateFromFile(fileStream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, false);
    lt.AddDispose(mmap);

    Stream mStream = mmap.CreateViewStream(0, mmapLength, MemoryMappedFileAccess.Read);
    lt.AddDispose(mStream);

    while (mStream.Position < mStream.Length)
    {
      _indices.Add(new IndexReader(mStream));
    }
  }

  public static IReadOnlyCollection<IndexRange> ReadStructure(Stream stream)
  {
    var ranges = new List<IndexRange>();
    while (stream.Position < stream.Length)
    {
      var startPosition = stream.Position;
      var preamble = Preamble.Read(stream);
      ranges.Add(new IndexRange(startPosition, preamble.Length, preamble));
      stream.Position = startPosition + preamble.Length;
    }

    return ranges;
  }
  public IEnumerable<DocNode> ContainingStr(string str, bool caseSensitive)
  {
    foreach (IndexReader ind in _indices)
    foreach (DocNode node in ind.ContainingStr(str, caseSensitive)!)
      yield return node;
  }

  /// sorry, hackathon naming scheme
  public IndexState CreateIndexStateForQuery(string query, bool caseSensitive)
  {
    var allNodes = GetAllNodes();
    var queryNodes = new HashSet<PathAndStamp>(ContainingStr(query, caseSensitive).Select(PathAndStamp.FromDocNode));
    return new IndexState(allNodes, queryNodes);
  }

  public HashSet<PathAndStamp> GetAllNodes()
  {
    return new HashSet<PathAndStamp>(_indices.SelectMany(i => i.ReadAllDocNodes()).Select(PathAndStamp.FromDocNode));
  }
  
  public IEnumerable<string> ReadPaths()
  {
    return new HashSet<string>(_indices.SelectMany(i => i.ReadAllDocNodes()).Select(n => n.Path), StringComparer.Ordinal);
  }

  public record struct IndexRange(long Start, long Length, Preamble Head);
}

public class IndexState
{
  public record struct PathAndStamp(string Path, long ModStamp)
  {
    public readonly bool Equals(PathAndStamp other) => string.Equals(Path, other.Path, StringComparison.Ordinal) && ModStamp.Equals(other.ModStamp);
    public readonly override int GetHashCode() => HashCode.Combine(Path, ModStamp);
    public static PathAndStamp FromDocNode(DocNode n) => new(n.Path, n.LastWriteTime);
  };
  public HashSet<PathAndStamp> AllNodes;
  public HashSet<PathAndStamp> QueryNodes;

  public IndexState(HashSet<PathAndStamp> allNodes, HashSet<PathAndStamp> queryNodes)
  {
    AllNodes = allNodes;
    QueryNodes = queryNodes;
  }

  public bool? DoesItMakeAnySenseToSearchInFile(string path, long modStamp)
  {
    var pathAndStamp = new PathAndStamp(path, modStamp);
    if (QueryNodes.Contains(pathAndStamp))
      return true;
    if (AllNodes.Contains(pathAndStamp))
      return false;
    return default;
  }
}