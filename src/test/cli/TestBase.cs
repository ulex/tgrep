using JetBrains.Lifetimes;

namespace test.cli;

public class TestBase
{
  private readonly LifetimeDefinition _fixtureLifetime;
  private readonly SequentialLifetimes _testLifetimes;
  private Lifetime _testLifetime;

  public Lifetime FixtureLifetime => _fixtureLifetime.Lifetime;
  public Lifetime TestLifetime => _testLifetime;

  public TestBase()
  {
    _fixtureLifetime = new LifetimeDefinition();
    _testLifetimes = new SequentialLifetimes(_fixtureLifetime.Lifetime);
  }

  [SetUp]
  public void Setup()
  {
    _testLifetime = _testLifetimes.Next();
  }

  [TearDown]
  public void Teardown()
  {
    _testLifetimes.TerminateCurrent();
  }

  [OneTimeTearDown]
  public void OneTimeTearDown() => _fixtureLifetime.Terminate();
}