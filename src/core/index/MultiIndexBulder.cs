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

  private InMemoryIndexBuilder _currentIndexBuilder;

  private readonly ReaderWriterLockSlim _rwLock = new();
  private readonly Channel<InMemoryIndexBuilder> _channel;
  private readonly Stream _outputStream;
  private readonly TaskCompletionSource _finished = new();

  public MultiIndexBulder(Stream outputStream, long maxSize = 128 * 1024 * 1024, InMemoryIndexBuilder? builder = null)
  {
    _outputStream = outputStream;
    _maxSize = maxSize;
    _currentIndexBuilder = builder ?? new InMemoryIndexBuilder();
    _channel = Channel.CreateBounded<InMemoryIndexBuilder>(new BoundedChannelOptions(2)
    {
      SingleReader = true
    });
    StartWriter();
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


  public async void StartWriter()
  {
    try
    {
      await foreach (var item in _channel.Reader.ReadAllAsync())
      {
        item.SaveTo(_outputStream);
      }
      _finished.SetResult();
    }
    catch (Exception e)
    {
      _finished.SetException(e);
    }
  }

  public void AddDocument(string path, long modificationUtc, IEnumerable<int> trigrams)
  {
    if (_currentIndexBuilder.EstimatedSize > _maxSize)
    {
      StartNewWriter(new InMemoryIndexBuilder());
    }

    _rwLock.EnterReadLock();
    try
    {
      _currentIndexBuilder.AddDocument(path, modificationUtc, trigrams);
    }
    finally
    {
      _rwLock.ExitReadLock();
    }
  }

  private void StartNewWriter(InMemoryIndexBuilder? newWriter)
  {
    _rwLock.EnterWriteLock();
    try
    {
      var old = _currentIndexBuilder;
      _currentIndexBuilder = newWriter!; // disposed, not expecting new documents to be added
      _channel.Writer.WriteAsync(old).AsTask().Wait();
    }
    finally
    {
      _rwLock.ExitWriteLock();
    }
  }

  public Task Complete()
  {
    StartNewWriter(null);
    _channel.Writer.Complete();
    return _finished.Task;
  }
}