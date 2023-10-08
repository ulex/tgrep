namespace core.util;

public static class SortedArrayUtil
{
  /// Merges two sorted arrays and writes results to output array.
  /// The size of output array should be at least a.Count + b.Count
  public static ArraySegment<int> Union(ArraySegment<int> a, ArraySegment<int> b, int[] output)
  {
    int l1 = a.Count;
    int l2 = b.Count;

    int ia = 0, ib = 0, l = 0;
    while (ia < l1 && ib < l2)
    {
      int toAdd;
      if (a[ia] < b[ib])
      {
        toAdd = a[ia];
        ia++;
      }
      else if (b[ib] < a[ia])
      {
        toAdd = b[ib];
        ib++;
      }
      else
      {
        toAdd = a[ia];
        ia++;
        ib++;
      }

      if (l == 0 || toAdd != output[l - 1])
      {
        output[l++] = toAdd;
      }
    }

    // remaining
    while (ia < l1)
      output[l++] = a[ia++];
    while (ib < l2)
      output[l++] = b[ib++];

    return new ArraySegment<int>(output, 0, l);
  }

  /// Intersects two sorted array segments.
  ///
  /// This method does not allocate new array and always modify first argument
  public static ArraySegment<int> Intersect(ArraySegment<int> a, ArraySegment<int> b)
  {
    int l1 = a.Count;
    int l2 = b.Count;
    int ia = 0, ib = 0, l = 0;
    var resultArray = a.Array!;

    while (ia < l1 && ib < l2)
    {
      int s = Math.Sign(a[ia] - b[ib]);
      if (s == 1)
      {
        ib++;
      }
      else if (s == -1)
      {
        ia++;
      }
      else
      {
        resultArray[l++] = a[ia];
        ia++;
        ib++;
      }
    }

    return new ArraySegment<int>(resultArray, 0, l);
  }
}