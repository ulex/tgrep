using CommandLine;

public class Options
{
  [Option('S', "smart-case", Required = false, HelpText = "Ignore-case until uppercase symbols appear in query (default)")]
  public bool SmartCase { get; set; }

  [Option('i', "ignore-case", Required = false, HelpText = "Ignore case (overrides smart-case)")]
  public bool IgnoreCase { get; set; }

  [Option('s', "case-sensitive", Required = false, HelpText = "Case sensitive (overrides ignore-case, smart-case)")]
  public bool CaseSensitive { get; set; }

  [Option('v', "verbose", Required = false, HelpText = "Verbose logging")]
  public bool Verbose { get; set; }
  
  [Option('f', "files", Required = false, HelpText = "Only output files")]
  public bool OnlyOutputFiles { get; set; }

  [Option('a', "all", Required = false, HelpText = "Search all files, don't use gitignore")]
  public bool SearchAllFiles { get; set; }

  [Option("index-only", Required = false, HelpText = "Don't walk directories, only return files from index")]
  public bool IndexOnly { get; set; }

  [Option("index", Required = false, HelpText = "Override index location")]
  public string? IndexPath { get; set; }

  [Option("vimgrep", Hidden = true /*it is the only option at the moment*/, Required = false, HelpText = "Output results in vim-compatible format")]
  public bool VimGrep { get; set; }
  
  [Option('F', Required = false, Default = true, Hidden = true, HelpText = "Fixed strings only (the only supported option)")]
  public bool FixedStrings { get; set; }

  [Option("dump", Required = false, Hidden = true, HelpText = "Dump index (internal option)")]
  public bool Dump { get; set; }

  [Value(0, MetaName = "query")]
  public string? Query { get; set; }

  [Value(1, MetaName = "files")]
  public IEnumerable<string>? Files { get; set; }
}