using core.util;

namespace test;

public class SparseTrigramTest
{
  [TestCase("await tr.AcceptAllFiles((path, lwt, t) => index.AddDocument(path, lwt, t),")]
  [TestCase("CreateMissingNamespaces")]
  public void SplitToTrigram(string input)
  {
    var vals = new List<string>();
    for (int i = 1; i < input.Length; i++)
    {
      var val = DigramWeight.F(input[i - 1], input[i]);
      vals.Add($"{input[i-1]}{input[i]}\t{val}");
    }

    foreach (var val in vals)
    {
      Console.WriteLine(val);
    }
  }

  [TestCase("await tr.AcceptAllFiles((path, lwt, t) => index.AddDocument(path, lwt, t),")]
  [TestCase("CreateMissingNamespaces")]
  public void SparseCollectorTest(string input)
  {
    var col = new SparseTrigramCollector();
    foreach (var c in input)
    {
      col.Feed(c);
    }
    col.Finish();
    Console.WriteLine($"ngram count: {col.Trigrams.Count}");
    Console.WriteLine($"3gram count: {input.Length - 2}");
  }
}