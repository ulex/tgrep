using System.Globalization;
using core.util.files;

namespace tgrep;

public class VimgrepPrinter
{
  private readonly string _directory;

  public VimgrepPrinter(string directory)
  {
    _directory = directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
  }

  public void Print(string path, int offset, Span<char> content)
  {
    path = FsUtil.TryMakeRelative(_directory, path);

    var before = content[..offset];
    int line = CountNewlines(before) + 1;
    int col = before.LastIndexOfAny('\r', '\n');
    if (col != -1)
      col = before.Length - col;
    else
      col = 0;

    var context = GetLine(content, offset);
    Console.WriteLine($"{path}:{line.ToString(CultureInfo.InvariantCulture)}:{col}:{context}");
  }

  static ReadOnlySpan<char> GetLine(ReadOnlySpan<char> text, int offset)
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

  public void PrintFile(string path)
  {
    Console.WriteLine(FsUtil.TryMakeRelative(_directory, path));
  }

  private static int CountNewlines(Span<char> text)
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