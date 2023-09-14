using CommandLine;
using core;

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
  if (opts.Files.Any())
    throw new NotSupportedException("Search in specific files isn't supported yet");

  if (opts.Query != null)
  {
    var ignoreCase = opts.IgnoreCase || (opts.SmartCase && opts.Query.Any(char.IsUpper));
    var query = ignoreCase ? opts.Query.ToUpper() : opts.Query;

    using var reader = new MultiIndexReader("C:\\bench\\testwt.idx");
    foreach (var docNode in reader.ContainingStr(query))
    {
      Console.WriteLine(docNode.Path);
    }
  }

}
static int HandleParseError(IEnumerable<Error> errs)
{
  foreach (var error in errs)
  {
    Console.Error.WriteLine(error.ToString());
  }

  return 1;
}

public class Options
{
  [Option('S', "smart-case", Required = false, HelpText = "Use ignore-case until uppercase symbols appear in query")]
  public bool SmartCase { get; set; }

  [Option('i', "ignore-case", Required = false, Default = false, HelpText = "Ignore case")]
  public bool IgnoreCase { get; set; }

  [Option('v', "verbose", Required = false, Default = false, HelpText = "Verbose logging")]
  public bool Verbose { get; set; }
  
  [Option("index", Required = false, HelpText = "Specify index location")]
  public string IndexPath { get; set; }

  [Option("vimgrep", Required = false, HelpText = "Output results in vim-compatible format")]
  public bool VimGrep { get; set; }
  
  [Value(0, MetaName = "query")]
  public string? Query { get; set; }

  [Value(1, MetaName = "files")]
  public IEnumerable<string> Files { get; set; }
}

class DumpHelper
{
  /*public static int Dump(DumpOptions opts)
  {
    Console.WriteLine("Dump !!");
    return 0;
  }*/
}