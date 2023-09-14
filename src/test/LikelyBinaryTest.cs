using core.util;

namespace tests;

[TestFixture]
public unsafe class LikelyBinaryTest
{
  [Test]
  public void LikelyBinaryFiles()
  {
    var gitdir = "C:\\bench\\testwt";
    var outdir = "C:\\bench\\";

    var outFilename = Path.GetFileName(gitdir);
    using var f = File.CreateText(Path.Combine(outdir, outFilename + ".trig.binaryfiles.log"));
    FastFilesVisitory.VisitFiles(gitdir, (dir, fsItem) =>
    {
      /*
      var path = Path.Combine(dir, fsItem.Name);
      if (BinaryFileDetector.ReadAllBytesIfNotBinary(path))
      {
        lock (f)
        {
          f.WriteLine($"{path} ({Utils.BytesToString(fsItem.Size)})");
        }
      }
    */
    });
  }
}