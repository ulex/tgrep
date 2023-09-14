using System.Text.RegularExpressions;

namespace core.util;

internal static class RegexPatterns
{
  public static readonly Regex MatchEmptyRegex = new("$^", RegexOptions.Compiled);
  public static readonly Regex RangeRegex = new(@"^((?:[^\[\\]|(?:\\.))*)\[((?:[^\]\\]|(?:\\.))*)\]", RegexOptions.Compiled);
  public static readonly Regex BackslashRegex = new(@"\\(.)", RegexOptions.Compiled);
  public static readonly Regex SpecialCharactersRegex = new(@"[\-\[\]\{\}\(\)\+\.\\\^\$\|]", RegexOptions.Compiled);
  public static readonly Regex QuestionMarkRegex = new(@"\?", RegexOptions.Compiled);
  public static readonly Regex SlashDoubleAsteriksSlashRegex = new(@"\/\*\*\/", RegexOptions.Compiled);
  public static readonly Regex DoubleAsteriksSlashRegex = new(@"^\*\*\/", RegexOptions.Compiled);
  public static readonly Regex SlashDoubleAsteriksRegex = new(@"\/\*\*$", RegexOptions.Compiled);
  public static readonly Regex DoubleAsteriksRegex = new(@"\*\*", RegexOptions.Compiled);
  public static readonly Regex SlashAsteriksEndOrSlashRegex = new(@"\/\*(\/|$)", RegexOptions.Compiled);
  public static readonly Regex AsteriksRegex = new(@"\*", RegexOptions.Compiled);
  public static readonly Regex SlashRegex = new(@"\/", RegexOptions.Compiled);
}