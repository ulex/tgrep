namespace core;

public record Query
{
  public sealed record Or(Query A, Query B) : Query;
  public sealed record And(Query A, Query B) : Query;
  public sealed record Contains(Trigram Val) : Query;
}