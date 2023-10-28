using Microsoft.VisualStudio.TestPlatform.TestHost;
using test.util;
using tgrep;

namespace test.cli;

[TestFixture]
public class TestIndex : TestBase
{
  private string _rgExe = null!;
  private string _dataDirectory = null!;
  private string _tgExe;

  [OneTimeSetUp]
  public async Task OneTimeSetup()
  {
    var rgDirectory = await TestData.GetData(TestData.RipGrep);
    _rgExe = Path.Combine(rgDirectory, "rg.exe");
    _tgExe = typeof(Program).Assembly.Location;
    _dataDirectory = await TestData.GetData(TestData.Vim91);
  }

  [SetUp]
  public void SetIndexLocation()
  {
    Environment.SetEnvironmentVariable(IndexLocationHelper.TgrepIndexPathEnvVar, TestData.GetTempDirectory(TestLifetime) + "index");
  }

  [Test]
  public async Task TestSmoke()
  {
    var rg = (await TestProcess.RunAsync(_rgExe, "--vimgrep helphelp", _dataDirectory)).AssertExitCode().StdOut;
    var tg = (await TestProcess.RunAsync(_tgExe, "--vimgrep helphelp", _dataDirectory)).AssertExitCode().StdOut;
    Assert.AreEqual(rg, tg);

    var tgIndexed = (await TestProcess.RunAsync(_tgExe, "--vim-grep helphelp", _dataDirectory)).AssertExitCode().StdOut;
    Assert.AreEqual(rg, tgIndexed);
  }
}