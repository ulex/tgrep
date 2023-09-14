namespace core;

public record struct TrigramOffsetBlock(
  int Trigram,
  long StartOffset,
  int Length);