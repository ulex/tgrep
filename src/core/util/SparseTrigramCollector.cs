using System.Text;

namespace core.util;

public class SparseTrigramCollector
{
  private const int WordMask = 0x7;
  private const int WordSize = WordMask + 1;

  private long _consumedBytes;
  private readonly int[] _codePoints = new int[WordSize];
  private readonly int[] _fn = new int[WordSize];

  private readonly Action<int>? _onHash;
  private readonly IntervalDelegate? _onIterval;
  public delegate void IntervalDelegate(long start, int length, int hash);

  public SparseTrigramCollector(Action<int> onHash = null, IntervalDelegate? onIterval = null)
  {
    _onHash = onHash;
    _onIterval = onIterval;
  }

  public void Feed(int codePoint)
  {
    var i = (int) _consumedBytes & WordMask;

    if (_consumedBytes >= WordSize)
    {
      int lv = _fn[i]; // left value
      int mv = _fn[(i + 1) & WordMask]; // max value in the middle interval
      for (int j = i + 2; j < WordSize + i - 1; j++)
      {
        int rv = _fn[j & WordMask]; // right value
        if (mv < lv)
        {
          if (rv > mv)
          {
            Add(i, (j + 1) & WordMask);
          }
        }
        else
        {
          break; // having max value greater or equals 
        }

        if (rv > mv) mv = rv;
      }
    }

    _codePoints[i] = codePoint;
    _fn[(i - 1) & WordMask] = DigramWeight.F(_codePoints[(i - 1) & WordMask], codePoint);
    _consumedBytes++;
  }

  /// startI endI are included
  private void Add(int startI, int endI)
  {
    uint hash = 0;
    for (int i = startI; i != endI; i = (i + 1) & WordMask)
    {
      hash = Utils.MixHashes(hash, (uint)_codePoints[i]);
    }
    hash = Utils.MixHashes(hash, (uint)_codePoints[endI]);
    
    var finalHash = (int)(hash >> 8);

    _onHash?.Invoke(finalHash);

    if (_onIterval != null)
    {
      long start = _consumedBytes - WordSize;
      int length = endI - startI + 1;
      if (length <= 0) length += WordSize;
      _onIterval(start, length, finalHash);
    }
  }

  public void Run(IEnumerable<Rune> runes)
  {
    Reset();
    foreach (var rune in runes)
    {
      Feed(rune.Value);
    }
    Finish();
  }

  private void Reset()
  {
    // Array.Fill(_codePoints, 0);
    Array.Fill(_fn, 0);
    _consumedBytes = 0;
  }

  public void Finish()
  {
    for (int i = 0; i < WordSize; i++)
    {
      Feed(0);
    }
  }
}