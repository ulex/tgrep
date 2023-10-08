using System.Globalization;
using System.Text;
using core.util.files;

namespace tgrep;

public class VimgrepPrinter
{
  private readonly bool _verbose;
  private readonly string _directory;

  public VimgrepPrinter(string directory, bool verbose)
  {
    _verbose = verbose;
    _directory = directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
  }

  public void Print(string path, IReadOnlyCollection<int> offsets, Span<char> content)
  {
    path = FsUtil.TryMakeRelative(_directory, path);

    var output = new StringBuilder();
    foreach (var offset in offsets)
    {
      var before = content[..offset];
      int line = PrinterUtil.CountNewlines(before) + 1;
      int col = before.LastIndexOfAny('\r', '\n');
      if (col != -1)
        col = before.Length - col;
      else
        col = 0;

      var context = PrinterUtil.GetLine(content, offset);
      output.AppendLine($"{path}:{line.ToString(CultureInfo.InvariantCulture)}:{col}:{context}");
    }

    if (output.Length > 0)
      Console.WriteLine(output.ToString());
  }

  public void PrintFile(string path)
  {
    Console.WriteLine(FsUtil.TryMakeRelative(_directory, path));
  }

  public void ReportStats(int total, TimeSpan elapsed)
  {
    if (_verbose)
      Console.Error.WriteLine($"Search through {total} files in {elapsed.TotalMilliseconds:N0}ms");
  }

  public void ReportQueryIndexTime(TimeSpan elapsed)
  {
    if (_verbose)
      Console.Error.WriteLine($"Query index time: {elapsed.TotalMilliseconds:N0}ms");
  }

  public void ReportIndexOpen(TimeSpan elapsed, int multiIndexCount)
  {
    if (_verbose)
      Console.Error.WriteLine($"Index opened in {elapsed.TotalMilliseconds:N0}ms, ICount = {multiIndexCount}");
  }
}