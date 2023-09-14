namespace core.util;

public static class StableHash
{
  public static int String(string text)
  {
    unchecked
    {
      int hash = 37;
      foreach (char c in text)
      {
        hash = hash * 31 + c;
      }
      return hash;
    }
  }
}