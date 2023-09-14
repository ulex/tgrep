﻿using CommandLine;
using core.util;
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
  if (opts.Files.Any())
    throw new HackathonException("HKTN: Search in specific files isn't supported yet");

  if (opts.Query != null)
  {
    var ignoreCase = opts.IgnoreCase || (opts.SmartCase && !opts.Query.Any(char.IsUpper));
    var query = ignoreCase ? opts.Query.ToUpper() : opts.Query;

    using var def = new LifetimeDefinition();
    var currentDirectory = Directory.GetCurrentDirectory();
    var printer = new VimgrepPrinter(currentDirectory);
    var cmd = new QueryCommand(def.Lifetime, IndexLocationHelper.GetIndexPath(opts, currentDirectory), printer, searchInFiles: !opts.OnlyOutputFiles, ignoreCase);
    if (opts.IndexOnly)
    {
      cmd.PrintIndexOnly(query, printer);
    }
    else
    {
      cmd.Start(query, printer, useGitIgnore: !opts.SearchAllFiles);
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