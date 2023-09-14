﻿using System.Diagnostics;
using core.util;
using JetBrains.Lifetimes;

namespace tests;

public class ReadIndexTest
{
  // [TestCase("C:\\work\\rd")]
  // [TestCase("C:\\work\\maiin")]
  [TestCase("C:\\bench\\testwt")]
  public void QueryIndex(string gitdir)
  {
    var sw = Stopwatch.StartNew();
    var tr = new TrigramBuilderVisitor(gitdir);
    using var def = new LifetimeDefinition();
    var index = new MultiIndex(def.Lifetime, tr.OutName(".idx"));
    sw.Stop();
    Console.WriteLine($"Read index: {sw.ElapsedMilliseconds:D}ms");

    sw.Restart();
    var documents = index.ContainingStr("TestFixture");
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