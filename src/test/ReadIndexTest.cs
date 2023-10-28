using System.Diagnostics;
using JetBrains.Lifetimes;
using test;

namespace tests;

public class ReadIndexTest
{
  // [TestCase("C:\\work\\rd")]
  // [TestCase("C:\\work\\maiin")]
  [TestCase("C:\\bench\\testwt")]
  public void QueryIndex(string gitdir)
  {
    var sw = Stopwatch.StartNew();
    var tr = new TestTrigramBuilderVisitor(gitdir);
    using var def = new LifetimeDefinition();
    var index = MultiIndex.OpenMmap(def.Lifetime, tr.OutName(".idx"));
    sw.Stop();
    Console.WriteLine($"Read index: {sw.ElapsedMilliseconds:D}ms");

    sw.Restart();
    var documents = index.ContainingStr("TestFixture", true);
    sw.Stop();
    Console.WriteLine($"Query index: {sw.ElapsedMilliseconds:D}ms");
    foreach (var docNode in documents)
    {
      Console.WriteLine(docNode.Path);
    }
  }

  [Test]
  public void METHOD()
  {
    var memoryStream = new MemoryStream("dddf"u8.ToArray().Concat(new byte[] { 0 }).ToArray());
    var readToEnd = new StreamReader(memoryStream).ReadToEnd();
  }
}