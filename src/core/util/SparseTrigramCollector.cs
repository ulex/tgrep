using System.Text;

namespace core.util;

public struct SparseTrigramCollector
{
  public readonly HashSet<int> Trigrams = new(); // todo: sparse set
  
  private const int WordSize = 8;

  private const int WordMask = 0x7;

  private int[] codePoints = new int[WordSize];
  private int[] fn = new int[WordSize];

#if DEBUG
  private readonly string FN_AS_STR => new(codePoints.Select(i => (char)i).ToArray());
#endif

  private long consumedBytes = 0;

  public SparseTrigramCollector()
  {
  }

  public void Feed(int codePoint)
  {
    var i = (int) consumedBytes & WordMask;

    if (consumedBytes >= WordSize)
    {
      int lv = fn[i]; // left value
      int mv = fn[(i + 1) & WordMask]; // max value in the middle interval
      for (int j = i + 2; j < WordSize + i - 1; j++)
      {
        int rv = fn[j & WordMask]; // right value
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

    codePoints[i] = codePoint;
    fn[(i - 1) & WordMask] = DigramWeight.F(codePoints[(i - 1) & WordMask], codePoint);
    consumedBytes++;
  }

  /// startI endI are included
  private void Add(int startI, int endI)
  {
    uint hash = 0;
    for (int i = startI; i != endI; i = (i + 1) & WordMask)
    {
      hash = MixHashes(hash, (uint)codePoints[i]);
    }
    hash = MixHashes(hash, (uint)codePoints[endI]);

#if DEBUG
    if (System.Diagnostics.Debugger.IsAttached)
    {
      var builder = new StringBuilder();
      for (int i = startI; i != endI; i = (i + 1) & WordMask)
      {
        builder.Append(new Rune(codePoints[i]).ToString());
      }
      builder.Append(new Rune(codePoints[endI]).ToString());
      var resultingStr = builder.ToString();
    }
#endif

    Trigrams.Add((int)(hash >> WordSize));
  }

  private static uint murmur_32_scramble(uint k) {
    k *= 0xcc9e2d51;
    k = (k << 15) | (k >> 17);
    k *= 0x1b873593;
    return k;
  }

  private static uint MixHashes(uint hash1, uint hash2)
  {
    unchecked
    {
      const uint m = 0x5bd1e995;
      const int r = 24;
      hash2 *= m;
      hash2 ^= hash2 >> r;
      hash2 *= m;

      hash1 *= m;
      hash1 ^= hash2;

      return hash1;
    }
  }

  public void Finish()
  {
    for (int i = 0; i < WordSize; i++)
    {
      Feed(0);
    }
  }
}