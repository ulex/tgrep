using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using core.util;
using static core.util.RegexPatterns;

public class GlobPattern
{
  private readonly Regex _checker; // todo: it can be replaced by simle glob, regex aren't needed here
  private string[] _precheckers;

  public static StringComparison StringComparison = StringComparison.Ordinal;
  private static readonly char[] SpecialChars = {'[', ']', '?'};

  public GlobPattern(string pattern)
  {
    var precheckers = new List<String>();
    var parts = pattern.Split('*');
    foreach (var part in parts)
    {
      if (string.IsNullOrEmpty(part))
        continue; ;
      if (part.IndexOfAny(SpecialChars) != -1)
        continue;
      precheckers.Add(part);
    }
    _precheckers = precheckers.ToArray();

    _checker = new Regex(PrepareRegexPattern(pattern), RegexOptions.NonBacktracking);
  }

  public bool IsMatch(string path, out int matchLength)
  {
    matchLength = -1;
    foreach (var prechecker in _precheckers)
    {
      if (!path.Contains(prechecker, StringComparison))
        return false;
    }

    var isMatch = _checker.Match(path);
    if (isMatch.Success)
      matchLength = isMatch.Length;
    return isMatch.Success;
  }

  [SuppressMessage("Major Code Smell", "S1121:Assignments should not be made from within sub-expressions")]
  private static string PrepareRegexPattern(string pattern)
  {
    // https://git-scm.com/docs/gitignore#_pattern_format
    //
    // * ...
    //
    // * If there is a separator at the beginning or middle (or both) of the pattern,
    //   then the pattern is relative to the directory level of the particular
    //   .gitignore file itself.
    //   Otherwise the pattern may also match at any level below the .gitignore level.
    //
    // * ...
    //
    // * For example, a pattern `doc/frotz/` matches `doc/frotz` directory, but
    //   not `a/doc/frotz` directory; however `frotz/` matches `frotz` and `a/frotz`
    //   that is a directory (all paths are relative from the .gitignore file).
    //
#if DEBUG
            string input = pattern;
#endif
    var reBuilder = new StringBuilder();
    bool rooted = false, directory = false;
    if (pattern.StartsWith("/"))
    {
      rooted = true;
      pattern = pattern.Substring(1);
    }

    if (pattern.EndsWith("/"))
    {
      directory = true;
      pattern = pattern.Substring(0, pattern.Length - 1);
    }

    string transpileRegexPart(string _re)
    {
      if (_re.Length == 0) return _re;
      // unescape for these will be escaped again in the subsequent `.Replace(...)`,
      // whether they were escaped before or not:
      _re = BackslashRegex.Replace(_re, "$1");
      // escape special regex characters:
      _re = SpecialCharactersRegex.Replace(_re, @"\$&");
      _re = QuestionMarkRegex.Replace(_re, "[^/]");
      _re = SlashDoubleAsteriksSlashRegex.Replace(_re, "(?:/|(?:/.+/))");
      _re = DoubleAsteriksSlashRegex.Replace(_re, "(?:|(?:.+/))");
      _re = SlashDoubleAsteriksRegex.Replace(_re, _ =>
      {
        directory = true; // `a/**` should match `a/`, `a/b/` and `a/b`, the latter by implication of matching directory `a/`
        return "(?:|(?:/.+))"; // `a/**` also accepts `a/` itself
      });
      _re = DoubleAsteriksRegex.Replace(_re, ".*");
      // `a/*` should match `a/b` and `a/b/` but NOT `a` or `a/`
      // meanwhile, `a/*/` should match `a/b/` and `a/b/c` but NOT `a` or `a/` or `a/b`
      _re = SlashAsteriksEndOrSlashRegex.Replace(_re, "/[^/]+$1");
      _re = AsteriksRegex.Replace(_re, "[^/]*");
      _re = SlashRegex.Replace(_re, @"\/");
      return _re;
    }

    // keep character ranges intact:
    Regex rangeRe = RangeRegex;
    // ^ could have used the 'y' sticky flag, but there's some trouble with infinite loops inside
    //   the matcher below then...
    for (Match match; (match = rangeRe.Match(pattern)).Success;)
    {
      if (match.Groups[1].Value.Contains('/'))
      {
        rooted = true;
        // ^ cf. man page:
        //
        //   If there is a separator at the beginning or middle (or both)
        //   of the pattern, then the pattern is relative to the directory
        //   level of the particular .gitignore file itself. Otherwise
        //   the pattern may also match at any level below the .gitignore level.
      }

      reBuilder.Append(transpileRegexPart(match.Groups[1].Value));
      reBuilder.Append('[').Append(match.Groups[2].Value).Append(']');

      pattern = pattern.Substring(match.Length);
    }

    if (!string.IsNullOrWhiteSpace(pattern))
    {
      if (pattern.Contains('/'))
      {
        rooted = true;
        // ^ cf. man page:
        //
        //   If there is a separator at the beginning or middle (or both)
        //   of the pattern, then the pattern is relative to the directory
        //   level of the particular .gitignore file itself. Otherwise
        //   the pattern may also match at any level below the .gitignore level.
      }

      reBuilder.Append(transpileRegexPart(pattern));
    }

    // prep regexes assuming we'll always prefix the check string with a '/':
    reBuilder.Preappend(rooted ? @"^\/" : @"\/");
    // cf spec:
    //
    //   If there is a separator at the end of the pattern then the pattern
    //   will only match directories, otherwise the pattern can match
    //   **both files and directories**.                   (emphasis mine)
    // if `directory`: match the directory itself and anything within
    // otherwise: match the file itself, or, when it is a directory, match the directory and anything within
    reBuilder.Append(directory ? @"\/" : @"(?:$|\/)");

    // regex validation diagnostics: better to check if the part is valid
    // then to discover it's gone haywire in the big conglomerate at the end.

    return reBuilder.ToString();
  }
}

// https://github.com/Guiorgy/GitignoreParserNet
namespace core.util
{
  public sealed class GitignoreParser
  {
    public sealed class RulesSet
    {
      public readonly GlobPattern[] Positives;
      public readonly GlobPattern[] Negatives;

      // public readonly AhoCorasickStateMachine? IntrestingPartsMachine;

      public RulesSet(IReadOnlyCollection<string> positive, IReadOnlyCollection<string> negative)
      {
        Positives = positive
          .Select(s => new GlobPattern(s))
          .ToArray();
        Negatives = negative
          .Select(s => new GlobPattern(s))
          .ToArray();

        // IntrestingPartsMachine = new AhoCorasickStateMachine(Positives.Concat(Negatives).Select(g => g));
      }
    }

    private readonly RulesSet _rules;
    
    public GitignoreParser(string content)
    {
      _rules = Parse(content);
    }

    public GitignoreParser(string path, bool ignoreGitDirectory)
    {
      string content = File.ReadAllText(path, Encoding.UTF8) + (ignoreGitDirectory ? (Environment.NewLine + ".git/") : "");
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

    static List<string> ListFiles(DirectoryInfo directory, string rootPath = "")
    {
      if (rootPath.Length == 0)
        rootPath = directory.FullName;

      List<string> files = new()
      {
        directory.FullName.Substring(rootPath.Length) + '/'
      };
      foreach (FileInfo file in directory.GetFiles())
        files.Add(file.FullName.Substring(rootPath.Length + 1));

      foreach (DirectoryInfo subDir in directory.GetDirectories())
        files.AddRange(ListFiles(subDir, rootPath));

      return files;
    }


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