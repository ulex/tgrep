using core.util;
using DotNet.Globbing;

namespace test;

public class Globs
{
  [Test]
  public void TestGlobDotnet()
  {
    var gitignore = new GitignoreParser("c:\\code\\git\\.gitignore", true);
    Assert.True(gitignore.Denies("/.git/abc"));
  }

  [Test]
  public void TestGlob2()
  {
    var gitignore = Glob.Parse("/**/.git/**");
    Assert.True(gitignore.IsMatch("/.git/index"));
  }
}