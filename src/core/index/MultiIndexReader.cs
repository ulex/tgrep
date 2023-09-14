using System.IO.MemoryMappedFiles;

namespace core;

public class MultiIndexReader : IDisposable, IAsyncDisposable
{
  private readonly MemoryMappedViewStream _wholeFile;
  private readonly List<IndexReader> _indices = new();

  public MultiIndexReader(string path)
  {
    var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    var mmapLength = fileStream.Length;
    var mmap = MemoryMappedFile.CreateFromFile(fileStream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, false);
    _wholeFile = mmap.CreateViewStream(0, mmapLength, MemoryMappedFileAccess.Read);
    while (_wholeFile.Position < _wholeFile.Length)
    {
      _indices.Add(new IndexReader(_wholeFile));
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


  public void Dispose()
  {
    _wholeFile.Dispose();
  }

  public async ValueTask DisposeAsync()
  {
    await _wholeFile.DisposeAsync();
  }

  public IEnumerable<DocNode> ContainingStr(string str)
  {
    foreach (IndexReader i in _indices)
    foreach (DocNode node in i.ContainingStr(str)!)
      yield return node;
  }

  public record struct IndexRange(long Start, long Length, Preamble Head);
}