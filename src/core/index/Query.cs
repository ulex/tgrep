namespace core;

public abstract record Query
{
  public sealed record Or(IEnumerable<Query> Queries) : Query;
  public sealed record And(IEnumerable<Query> Queries) : Query;
  public sealed record Contains(Trigram Val) : Query;
}