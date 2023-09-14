using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using core;
using core.util;
using JetBrains.Lifetimes;

namespace tgrep;

public class QueryCommand
{
  private readonly VimgrepPrinter _printer;
  private readonly bool _searchInFiles;
  private readonly bool _ignoreCase;

  private readonly MultiIndex _multiIndex;
  private readonly string _directory;

  public QueryCommand(Lifetime lifetime, string indexPath, VimgrepPrinter printer, bool searchInFiles, bool ignoreCase)
  {
    _printer = printer;
    _searchInFiles = searchInFiles;
    _ignoreCase = ignoreCase;
    _directory = Directory.GetCurrentDirectory();
    if (!File.Exists(indexPath))
    {
      var watch = Stopwatch.StartNew();
      Console.Error.WriteLine($"Indexing directory... {_directory}");
      Console.Error.WriteLine($"     index location : {indexPath}");
      int n = 0;
      using (var writer = File.OpenWrite(indexPath))
      {
        var multiIndexBulder = new MultiIndexBulder(writer);

        FastFilesVisitor.VisitFiles(_directory, (parent, item) =>
        {
          var fpath = Path.Combine(parent, item.Name);
          if (fpath == indexPath) // ignore index file itself
            return;
          var trigrams = Utils.ReadTrigrams(fpath);
          Interlocked.Increment(ref n);
          multiIndexBulder.AddDocument(fpath, item.LastModified, trigrams);
        });

        multiIndexBulder.Complete().Wait();
      }
      Console.Error.WriteLine($"completed in {watch.Elapsed}, indexed {n} files");
    }

    _multiIndex = new MultiIndex(lifetime, indexPath);
  }

  public void Start(string query, VimgrepPrinter printer, bool useGitIgnore)
  {
    if (useGitIgnore)
    {
      foreach (var parent in FsUtil.Parents(_directory))
      {
        var path = Path.Combine(parent, ".gitignore");
        if (File.Exists(path))
        {
          var parser = new GitignoreParser(path, true);
        }
      }
    }

    var state = _multiIndex.CreateIndexStateForQuery(query);
    FastFilesVisitor.VisitFiles(_directory, (parent, item) =>
    {
      var path = Path.Combine(parent, item.Name);
      var search = state.DoesItMakeAnySenseToSearchInFile(path, item.LastModified);
      if (search == false)
        return;

      if (!_searchInFiles)
      {
        _printer.PrintFile(path);
      }
      else
      {
        SearchInFile(query, path, printer);
      }
    });
  }

  public void PrintIndexOnly(string query, VimgrepPrinter printer)
  {
    foreach (var docNode in _multiIndex.ContainingStr(query))
    {
      if (!_searchInFiles)
      {
        _printer.PrintFile(docNode.Path);
      }
      else
      {
        if (File.Exists(docNode.Path))
        {
          SearchInFile(query, docNode.Path, printer);
        }
      }
    }
  }

  private void SearchInFile(string query, string path, VimgrepPrinter printer)
  {
    using var fileStream = File.OpenRead(path);
    if (fileStream.Length > int.MaxValue)
      throw new HackathonException("HKTN: File greater than 2gb are not supported at the moment");
    
    var reader = new StreamReader(fileStream);
    var buffer = ArrayPool<char>.Shared.Rent((int)fileStream.Length);

    int totalRead = 0;
    int read = 0;
    do
    {
      var slice = buffer.AsSpan()[totalRead..];
      read = reader.ReadBlock(slice);
      if (slice.Slice(0, read).IndexOf('\0') != -1)
      {
        return; // binary file!
      }
      totalRead += read;
    } while (read > 0);

    var bufferSpan = buffer.AsSpan(0, totalRead);

    string? bufStr = null;
    if (_ignoreCase)
      bufStr = new string(bufferSpan);

    int newLine = 0;
    int offset = 0;

    while (true)
    {
      int matchIndex;
      if (bufStr != null)
      {
        matchIndex = bufStr.IndexOf(query, offset, StringComparison.OrdinalIgnoreCase);
      }
      else
      {
        matchIndex = bufferSpan[offset..].IndexOf(query);
        offset += matchIndex;
      }

      if (matchIndex == -1)
        break;

      
      printer.Print(path, offset, bufferSpan);

      offset += query.Length;
    }
    ArrayPool<char>.Shared.Return(buffer);
  }
}