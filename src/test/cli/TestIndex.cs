using test.util;
using tgrep;

namespace test.cli;

[TestFixture]
public class TestIndex : TestBase
{
  private string _rgExe = null!;
  private string _dataDirectory = null!;
  private string _tgExe = null!;

  [OneTimeSetUp]
  public async Task OneTimeSetup()
  {
    var rgDirectory = await TestData.GetData(TestData.RipGrep);
    _rgExe = Path.Combine(rgDirectory, "rg.exe");
    _tgExe = Path.ChangeExtension(typeof(Options).Assembly.Location, "exe");
    _dataDirectory = await TestData.GetData(TestData.Vim91);
  }

  [SetUp]
  public void SetIndexLocation()
  {
    Environment.SetEnvironmentVariable(IndexLocationHelper.TgrepIndexPathEnvVar, TestData.GetTempDirectory(TestLifetime) + "index");
  }

  [TestCase("helphelp")]
  [TestCase("gitignore")]
  public async Task TestSmoke(string query)
  {
    var rg = TestProcess.SortLines(TestProcess.Run(_rgExe, $" --no-config --no-ignore-global --no-ignore-parent --hidden --vimgrep {query}", _dataDirectory).AssertExitCode().StdOut);
    var tg = TestProcess.SortLines(TestProcess.Run(_tgExe, $"--vimgrep {query}", _dataDirectory).AssertExitCode().StdOut);
    Assert.That(tg, Is.EqualTo(rg));
    var tgIndexed = TestProcess.SortLines(TestProcess.Run(_tgExe, $"--vimgrep {query}", _dataDirectory).AssertExitCode().StdOut);
    Assert.That(tgIndexed, Is.EqualTo(rg));
  }


}