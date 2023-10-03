namespace core;

public record struct TrigramBlock(
  Trigram Val,
  long Offset /* relative to posting lists start (e.g. can be 0)*/,
  int Length);