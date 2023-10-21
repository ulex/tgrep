using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using DotNet.Globbing;

public class GlobPattern
{
  private readonly string _pattern;
  private readonly Glob? _checker;

  private readonly string[] _precheckers = Array.Empty<string>();
  private readonly Predicate<string>? _fastChecker;

  public static StringComparison StringComparison = StringComparison.Ordinal;
  private static readonly char[] SpecialCharsPlusSep = {'[', ']', '?', '*', '/'};
  private static readonly char[] SpecialChars = {'[', ']', '?', '*'};

  public GlobPattern(string pattern)
  {
    _pattern = pattern;

    if (pattern.StartsWith('*') && pattern.IndexOfAny(SpecialCharsPlusSep, 1) == -1)
    {
      _fastChecker = s => s.EndsWith(pattern, StringComparison.OrdinalIgnoreCase);
      return;
    }

    if (pattern[0] == '/' && pattern.TrimEnd('*').IndexOfAny(SpecialChars) == -1)
    {
      _fastChecker = s => s.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);
      return;
    }

    if (!pattern.StartsWith('/'))
    {
      pattern = "/**/" + pattern;
    }
    if (pattern.StartsWith('/'))
    {
      pattern += "**";
    }

    var precheckers = new List<string>();
    var parts = pattern.Split('*', '/', '\\');
    foreach (var part in parts)
    {
      if (string.IsNullOrEmpty(part))
        continue; ;
      if (part.IndexOfAny(SpecialCharsPlusSep) != -1)
        continue;
      if (part.Length >= 3)
        precheckers.Add(part);
    }
    _precheckers = precheckers.ToArray();
    _checker = Glob.Parse(pattern);
  }

  public bool IsMatch(string path, out int matchLength)
  {
    matchLength = _pattern.Length;

    if (_fastChecker != null)
      return _fastChecker(path);

    foreach (var prechecker in _precheckers)
    {
      if (!path.Contains(prechecker, StringComparison))
        return false;
    }
    var isMatch = _checker!.IsMatch(path);
    return isMatch;
  }
}

// https://github.com/Guiorgy/GitignoreParserNet, Apache License
namespace core.util
{
  public sealed class GitignoreParser
  {
    public sealed class RulesSet
    {
      public readonly GlobPattern[] Positives;
      public readonly GlobPattern[] Negatives;

      public RulesSet(IReadOnlyCollection<string> positive, IReadOnlyCollection<string> negative)
      {
        Positives = positive
          .Select(s => new GlobPattern(s))
          .ToArray();
        Negatives = negative
          .Select(s => new GlobPattern(s))
          .ToArray();
      }
    }

    private readonly RulesSet _rules;
    
    public GitignoreParser(string content)
    {
      _rules = Parse(content);
    }

    public GitignoreParser(string path, bool ignoreGitDirectory)
    {
      string content = File.ReadAllText(path, Encoding.UTF8) + (ignoreGitDirectory ? (Environment.NewLine + "/.git/") : "");
      var globalExclude = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gitignore");
      if (File.Exists(globalExclude))
      {
        content = content + Environment.NewLine + File.ReadAllText(globalExclude, Encoding.UTF8);
      }

      _rules = Parse(content);
    }

    public static RulesSet Parse(string content)
    {
      (List<string> positive, List<string> negative) = content
        .Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
        .Select(line => line.Trim())
        .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
        .Aggregate<string, (List<string>, List<string>), (List<string>, List<string>)>(
          (new List<string>(), new List<string>()),
          ((List<string> positive, List<string> negative) lists, string line) =>
          {
            if (line.StartsWith("!"))
              lists.negative.Add(line.Substring(1));
            else
              lists.positive.Add(line);
            return (lists.positive, lists.negative);
          },
          ((List<string> positive, List<string> negative) lists) => lists
        );

      return new RulesSet(positive, negative);
    }

    private static RegexOptions RegexOptions => RegexOptions.Compiled;


    /// <summary>
    /// Notes:
    /// - you MUST postfix a input directory with '/' to ensure the gitignore
    ///   rules can be applied conform spec.
    /// - you MAY prefix a input directory with '/' when that directory is
    ///   'rooted' in the same directory as the compiled .gitignore spec file.
    /// </summary>
    /// <returns>
    /// TRUE when the given `input` path FAILS the gitignore filters,
    /// i.e. when the given input path is ACCEPTED.
    /// </returns>
    public bool Denies(string input)
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        input = input.Replace('\\', '/');

      if (!input.StartsWith("/"))
        input = "/" + input;

      // var acceptTest = _rules.NegativeMachine?.Match(input) ?? false;
      // var denyTest = _rules.PositiveMachine?.Match(input) ?? false;
      // var returnVal = acceptTest || !denyTest;

      // See the test/fixtures/gitignore.manpage.txt near line 680 (grep for "uber-nasty"):
      // to resolve chained rules which reject, then accept, we need to establish
      // the precedence of both accept and reject parts of the compiled gitignore by
      // comparing match lengths.
      // Since the generated consolidated regexes are lazy, we must loop through all lines' regexes instead:
      // if (acceptTest && denyTest)
      {
        // See the test/fixtures/gitignore.manpage.txt near line 680 (grep for "uber-nasty"):
        // to resolve chained rules which reject, then accept, we need to establish
        // the precedence of both accept and reject parts of the compiled gitignore by
        // comparing match lengths.
        // Since the generated regexes are all set up to be GREEDY, we can use the
        // consolidated regex for this, instead of having to loop through all lines' regexes:
        int acceptLength = 0, denyLength = 0;
        foreach (var g in _rules.Negatives)
        {
          var m = g.IsMatch(input, out var matchLength);
          if (m && acceptLength < matchLength)
          {
            acceptLength = matchLength;
          }
        }

        foreach (var g in _rules.Positives)
        {
          var m = g.IsMatch(input, out var matchLegnth);
          if (m && denyLength < matchLegnth)
          {
            denyLength = matchLegnth;
          }
        }

        return acceptLength < denyLength;
      }

      //return returnVal;
    }
  }
}