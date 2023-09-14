namespace core;

public class FlippingBuffer<T>
{
  private T _front;
  private T _back;

  private readonly ReaderWriterLockSlim _rwLock = new();

  public FlippingBuffer(T front, T back)
  {
    _front = front;
    _back = back;
  }

  public T BackUnsafe => _back;
  public T FrontUnsafe => _front;

  public void WithFront(Action<T> d)
  {
    _rwLock.EnterReadLock();
    try
    {
      d(_front);
    }
    finally
    {
      _rwLock.ExitReadLock();
    }
  }

  public void WithBack(Action<T> d)
  {
    _rwLock.EnterReadLock();
    try
    {
      d(_back);
    }
    finally
    {
      _rwLock.ExitReadLock();
    }
  }

  public bool Flip() => Flip(_ => true);

  public bool Flip(Predicate<(T front, T back)> check)
  {
    _rwLock.EnterWriteLock();
    try
    {
      if (check((_front, _back)))
      {
        (_back, _front) = (_front, _back);
        return true;
      }
    }
    finally
    {
      _rwLock.ExitWriteLock();
    }

    return false;
  }
}