using CommandLine;
using core.util;
using core.util.files;
using JetBrains.Lifetimes;
using tgrep;

var parserResult = Parser.Default.ParseArguments<Options>(args);

if (parserResult is Parsed<Options>)
{
  try
  {
    RunOptions(parserResult.Value);
  }
  catch (Exception e)
  {
    if (parserResult.Value.Verbose)
    {
      throw;
    }

    Console.Error.WriteLine(e.Message);
    return 1;
  }
}
else
{
  return HandleParseError(parserResult.Errors);
}

return parserResult.Value != null ? 0 : 1;

static void RunOptions(Options opts)
{
  // TODO: streamwriter with buffer!
  if (opts.Files != null && opts.Files.Any())
    throw new HackathonException("HKTN: Search in specific files isn't supported yet");
  
  var currentDirectory = Directory.GetCurrentDirectory();
  var printer = new VimgrepPrinter(currentDirectory, opts.Verbose);

  if (!opts.IndexOnly && opts.OnlyOutputFiles)
  {
    ListFiles(currentDirectory, printer, !opts.SearchAllFiles);
    return;
  }

  using var def = new LifetimeDefinition();
  var indexPath = IndexLocationHelper.GetIndexPath(opts, currentDirectory);

  var cmd = new QueryCommand(def.Lifetime, indexPath, printer, searchInFiles: !opts.OnlyOutputFiles);

  if (opts.Dump)
  {
    cmd.Dump(Console.Out, opts.Verbose);
    return;
  }

  if (opts.Query != null)
  {
    var caseSensitive = opts.CaseSensitive || (!opts.IgnoreCase && !opts.Query.Any(char.IsUpper));
    if (opts.IndexOnly)
    {
      cmd.SearchIndexOnly(opts.Query, printer, !caseSensitive);
    }
    else
    {
      cmd.Search(opts.Query, printer, useGitIgnore: !opts.SearchAllFiles, ignoreCase: !caseSensitive);
    }
  }
  else if (opts.OnlyOutputFiles && opts.IndexOnly)
  {
    cmd.ListFilesIndexOnly(printer);
  }
}

static void ListFiles(string directory, VimgrepPrinter printer, bool useGitIgnore)
{
  FileScannerBuilder
    .Build(directory, useGitIgnore: useGitIgnore)
    .Visit(i =>
    {
      if (i.IsDirectory)
        return true;
      
      printer.PrintFile(i.Path);

      return true;
    }).Wait();
}


static int HandleParseError(IEnumerable<Error> errs)
{
  foreach (var error in errs)
  {
    Console.Error.WriteLine(error.ToString());
  }

  return 1;
}