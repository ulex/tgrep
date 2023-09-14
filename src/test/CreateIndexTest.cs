using System.Diagnostics;
using System.Runtime.InteropServices;
using core.util;
using JetBrains.Lifetimes;
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
      index.AddDocument(fi.FullName, fi.LastWriteTime.ToFileTime(), t);
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
    tr.Accept((fi, t) => index.AddDocument(fi.FullName, fi.LastWriteTime.ToFileTime(), t));

    using var st = File.Create(tr.OutName($".idx"));
    index.SaveTo(st);
  }

  [TestCase("C:\\bench\\testwt")]
  [TestCase("C:\\work\\rd")]
  public async Task CreateAllFilesIndex(string dir)
  {
    await IndexDirectory(dir);
  }

  private static async Task<string> IndexDirectory(string dir)
  {
    var tr = new TrigramBuilderVisitor(dir);
    
    var indexPath = tr.OutName(".idx");
    await using var stream = File.Create(indexPath);
    var index = new MultiIndexBulder(stream);
    tr.AcceptAllFiles((path, lwt, t) => index.AddDocument(path, lwt, t),
#if DEBUG
      sync: true
      #else
      sync: false
#endif
      );
    await index.Complete();
    
    Console.WriteLine($"Real size = {Utils.BytesToString(stream.Length)}");

    return indexPath;
  }

  [TestCase("C:\\bench\\testwt\\Psi.Features\\test\\data\\IntegrationTests\\CrippledWebsite2\\crippled\\Default.aspx.designer.cs")]
  public void CreateIndexSIngleFIle(string path)
  {
    var builder = new InMemoryIndexBuilder();
    builder.AddDocument(path, long.MaxValue, Utils.ReadTrigrams(path));
    var storage = new MemoryStream();
    builder.SaveTo(storage);
    storage.Position = 0;
    var reader = new IndexReader(storage);
    Assert.True(reader.ContainingStr("TEST")!.Count > 0);
  }

  [TestCase("C:\\work\\rd")]
  public async Task TestAppendOnlyIndex(string path)
  {
    var indexPath = await IndexDirectory(path);
    Console.WriteLine($"File size after writing = {Utils.BytesToString(new FileInfo(indexPath).Length)}");
    var origPath = indexPath + ".orig";
    File.Copy(indexPath, origPath, true);
    using (var def = new LifetimeDefinition())
    {
      var multiIndexBulder = MultiIndexBulder.OpenAppendOnly(def.Lifetime, indexPath);
      await multiIndexBulder.Complete();
    }

    FileAssert.AreEqual(origPath, indexPath);
  }


  [TestCase("C:\\work\\rd")]
  public async Task TestQuery(string path)
  {
    var indexPath = await IndexDirectory(path);
    using (var def = new LifetimeDefinition())
    {
      var i = new MultiIndex(def.Lifetime, "C:\\Users\\sa\\AppData\\Roaming\\.tgrep\\rd.bb9bb23d");
      var indexState = i.CreateIndexStateForQuery("DotPeek");
    }
  }
}