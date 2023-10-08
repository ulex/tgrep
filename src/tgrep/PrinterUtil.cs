namespace tgrep;

public static class PrinterUtil
{
  public static ReadOnlySpan<char> GetLine(ReadOnlySpan<char> text, int offset)
  {
    int start = text[..offset].LastIndexOfAny('\n', '\r');
    int end = text[offset..].IndexOfAny('\n', '\r');
    if (end != -1)
    {
      end += offset;
    }
    else
    {
      end = text.Length;
    }
    return text.Slice(start + 1, end - start - 1);
  }

  public static int CountNewlines(Span<char> text)
  {
    int newlineCount = 0;
    foreach (char c in text)
    {
      if (c == '\n') // todo: support only '\r' as newline character (MacOs?)
        newlineCount++;
    }
    return newlineCount;
  }
}