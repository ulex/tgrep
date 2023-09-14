using System.Threading.Channels;
using JetBrains.Lifetimes;

namespace core;

/**
 * Same as <see cref="InMemoryIndexBuilder"/>, but limited by memory usage. In case index became too large to fit in
 * memory it will dump builded content to the disk and start a new index
 */
public class MultiIndexBulder
{
  private readonly long _maxSize;

  private readonly Stream _outputStream;
  private readonly TaskCompletionSource _finished = new();
  private readonly FlippingBuffer<InMemoryIndexBuilder> _flippingBuffer;

  public MultiIndexBulder(Stream outputStream, long maxSize = 128 * 1024 * 1024, InMemoryIndexBuilder? builder = null)
  {
    _outputStream = outputStream;
    _maxSize = maxSize;
    _flippingBuffer = new FlippingBuffer<InMemoryIndexBuilder>(new InMemoryIndexBuilder(), builder ?? new InMemoryIndexBuilder());
  }

  public static MultiIndexBulder OpenAppendOnly(Lifetime lifetime, string path)
  {
    var fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read 
      /* todo: it seems it is safe to leave file opened for shared read, because atomically write a fake preambule of last index */);
    lifetime.AddDispose(fileStream);

    return OpenAppendOnly(fileStream);
  }

  public static MultiIndexBulder OpenAppendOnly(FileStream fileStream)
  {
    var structure = MultiIndex.ReadStructure(fileStream);
    fileStream.Position = structure.LastOrDefault().Start;
    var lastIndex = structure.Count > 0 ? new IndexReader(fileStream).ToWritable() : null;

    fileStream.Position = structure.LastOrDefault().Start;
    fileStream.SetLength(fileStream.Position);
    return new MultiIndexBulder(fileStream, builder: lastIndex);
  }

  public void AddDocument(string path, long modificationUtc, IEnumerable<int> trigrams)
  {
    if (_flippingBuffer.BackUnsafe.EstimatedSize > _maxSize)
    {
      Flip(false);
    }
    _flippingBuffer.WithBack(b => b.AddDocument(path, modificationUtc, trigrams));
  }

  private void Flip(bool final)
  {
    bool flipped = final ? _flippingBuffer.Flip() : _flippingBuffer.Flip(f => f.back.EstimatedSize > _maxSize);
    if (flipped)
    {
      var task = Task.Run(() => _flippingBuffer.WithFront(b =>
      {
        b.SaveTo(_outputStream);
        b.Clear();
      }));
      if (final)
        task.ContinueWith(t =>
        {
          if (t.Exception != null)
            _finished.SetException(t.Exception);
          else
            _finished.SetResult();
        });
    }
  }

  public Task Complete()
  {
    Flip(true);
    return _finished.Task;
  }
}