﻿using System.Buffers;
using System.Diagnostics;
using core;
using core.util;
using core.util.files;
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
    var watch = Stopwatch.StartNew();
    if (!File.Exists(indexPath))
    {
      Console.Error.WriteLine($"Indexing directory... {_directory}");
      Console.Error.WriteLine($"     index location : {indexPath}");
      int n = 0;
      using (var writer = File.OpenWrite(indexPath))
      {
        var multiIndexBulder = new MultiIndexBulder(writer);

        var visitTask = FileScannerBuilder.Build(_directory).Visit(
          i =>
          {
            if (i.Path == indexPath) // ignore index file itself
              return true;

            if (i.IsDirectory)
              return true;

            try
            {
              var trigrams = Utils.ReadTrigrams(i.Path);
              Interlocked.Increment(ref n);
              multiIndexBulder.AddDocument(i.Path, i.ModStamp, trigrams);
            }
            catch (Exception exception)
            {
              Console.Error.WriteLine(exception.Message);
            }

            return true;
          });
        visitTask.Wait();

        multiIndexBulder.Complete().Wait();
      }

      Console.Error.WriteLine($"completed in {watch.Elapsed}, indexed {n} files");
    }

    var indexLdef = lifetime.CreateNested();
    try
    {
      _multiIndex = new MultiIndex(indexLdef.Lifetime, indexPath);
    }
    catch (Exception)
    {
      indexLdef.Terminate();
      File.Delete(indexPath);
      throw;
    }
    printer.ReportIndexOpen(watch.Elapsed, _multiIndex.ICount);
  }

  public void Start(string query, VimgrepPrinter printer, bool useGitIgnore)
  {
    var state = _multiIndex.CreateIndexStateForQuery(query, !_ignoreCase);
    FileScannerBuilder.Build(_directory, useGitIgnore: useGitIgnore).Visit(i =>
    {
      if (i.IsDirectory)
        return true;

      try
      {
        var search = state.DoesItMakeAnySenseToSearchInFile(i.Path, i.ModStamp);
        if (search == false)
          return true;

        if (!_searchInFiles)
        {
          _printer.PrintFile(i.Path);
        }
        else
        {
          SearchInFile(query, i.Path, printer);
        }
      }
      catch (Exception e)
      {
        Console.Error.WriteLine(e.Message);
      }

      return true;
    }).Wait();
  }

  public void PrintIndexOnly(string query, VimgrepPrinter printer)
  {
    var sw = Stopwatch.StartNew();
    var docs = _multiIndex.ContainingStr(query, !_ignoreCase).ToList();
    printer.ReportQueryIndexTime(sw.Elapsed);
    
    sw.Restart();
    docs
      .AsParallel()
      .ForAll(docNode =>
      {
        try
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
        catch (Exception e)
        {
          Console.Error.WriteLine(e.Message);
        }
      });
    printer.ReportStats(docs.Count, sw.Elapsed);
  }

  private void SearchInFile(string query, string path, VimgrepPrinter printer)
  {
    using var fileStream = File.OpenRead(path);
    if (fileStream.Length > int.MaxValue)
      throw new HackathonException("HKTN: Files greater than 2gb are not supported at the moment");

    var reader = new StreamReader(fileStream);
    
    var buffer = ArrayPool<char>.Shared.Rent((int)fileStream.Length);
    using var x = new LifetimeDefinition();
    x.Lifetime.OnTermination(() => ArrayPool<char>.Shared.Return(buffer));

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

    var offsets = new List<int>();
    while (true)
    {
      int matchIndex;
      if (bufStr != null) // if ignore case
      {
        matchIndex = bufStr.IndexOf(query, offset, StringComparison.OrdinalIgnoreCase);
        offset = matchIndex;
      }
      else // case-sensitive search
      {
        matchIndex = bufferSpan[offset..].IndexOf(query);
        offset += matchIndex;
      }

      if (matchIndex == -1)
        break;

      offsets.Add(offset);

      offset += query.Length;
    }
    if (offsets.Count > 0)
      printer.Print(path, offsets, bufferSpan);
  }
}