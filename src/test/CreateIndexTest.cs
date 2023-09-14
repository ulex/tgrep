using System.Diagnostics;
using System.Runtime.InteropServices;
using core.util;
using JetBrains.Serialization;

namespace tests;

[TestFixture]
public class CreateIndexTest
{
  [Test]
  public unsafe void CreateIndexWithStats()
  {
    var gitdir = "C:\\work\\main";

    var tr = new TrigramBuilderVisitor(gitdir);
    var index = new InMemoryIndexBuilder();
    using var log = File.CreateText(tr.OutName(".trig.log"));

    int[] stats = new int[256 * 256 * 256];
    long totalSize = 0;
    using var aggFile = File.Create(tr.OutName(".trig.agg"));
    tr.Accept((fi, t) =>
    {
      Interlocked.Add(ref totalSize, fi.Length);
      index.AddDocument(fi.FullName, fi.LastWriteTimeUtc, t);
      using (var cookie = UnsafeWriter.NewThreadLocalWriter())
      {
        cookie.Writer.WriteInt32(t.Count);
        foreach (var trigram in t) cookie.Writer.WriteInt32(trigram);
        lock (aggFile) aggFile.Write(new ReadOnlySpan<byte>(cookie.Data, cookie.Count));
      }
    });

    // bin stats
    using (var f = File.Create(tr.OutName(".trig.sum")))
    {
      f.Write(MemoryMarshal.AsBytes(new Span<int>(stats)));
    }

    log.WriteLine($"Total size bytes: {totalSize} ({Utils.BytesToString(totalSize)})");
  }

  [TestCase("C:\\work\\main")]
  [TestCase("C:\\work\\rd")]
  public void CreateIndex(string gitdir)
  {
    var tr = new TrigramBuilderVisitor(gitdir);
    var index = new InMemoryIndexBuilder();
    tr.Accept((fi, t) => index.AddDocument(fi.FullName, fi.LastWriteTimeUtc, t));

    using var st = File.Create(tr.OutName($".idx"));
    index.SaveTo(st);
  }

  [TestCase("C:\\bench\\testwt")]
  [TestCase("C:\\work\\rd")]
  public async Task CreateAllFilesIndex(string gitdir)
  {
    var tr = new TrigramBuilderVisitor(gitdir);
    
    var indexPath = tr.OutName(".idx");
    await using var st = File.Create(indexPath);
    var index = new MultiIndexBulder(st);
    tr.AcceptAllFiles((path, lwt, t) => index.AddDocument(path, lwt, t), sync: Debugger.IsAttached);
    await index.Complete();

    Console.WriteLine($"Real size = {Utils.BytesToString(st.Length)}");
  }

  [TestCase("C:\\bench\\testwt\\Psi.Features\\test\\data\\IntegrationTests\\CrippledWebsite2\\crippled\\Default.aspx.designer.cs")]
  public void CreateIndexSIngleFIle(string path)
  {
    var builder = new InMemoryIndexBuilder();
    builder.AddDocument(path, DateTime.MaxValue, Utils.ReadTrigrams(path));
    var storage = new MemoryStream();
    builder.SaveTo(storage);
    storage.Position = 0;
    var reader = new IndexReader(storage);
    Assert.True(reader.ContainingStr("TEST")!.Count > 0);
  }
}