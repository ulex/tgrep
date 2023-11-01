using System.Diagnostics;
using System.Text;

namespace test.util;

public record ProcessRunInfo(
  string StdOut,
  string StdErr,
  int ExitCode);

public static class TestProcess
{
  public static ProcessRunInfo Run(string exe, string args, string workingDirectory, IDictionary<string, string>? env = null)
  {
    var startInfo = new ProcessStartInfo(exe, args)
    {
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      WorkingDirectory = workingDirectory,
      CreateNoWindow = false,
    };
    if (env != null)
    {
      foreach (var (k, val) in env) 
        startInfo.EnvironmentVariables[k] = val;
    }

    StringBuilder stdout = new();
    StringBuilder stderr = new();


    var process = new Process();
    process.OutputDataReceived += (_, eventArgs) => { if (eventArgs.Data != null) lock(stdout) stdout.AppendLine(eventArgs.Data); };
    process.ErrorDataReceived += (_, eventArgs) => { if (eventArgs.Data != null) lock(stderr) stderr.AppendLine(eventArgs.Data); };
    process.StartInfo = startInfo;
    process.Start();

    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    Task.WaitAny(process.WaitForExitAsync(), Task.Delay(TimeSpan.FromSeconds(10)));
    if (!process.HasExited)
    {
      process.Kill();
      Assert.Fail("Process hasn't exited in predefined timeout(out: {0}, err: {1})", stdout, stderr);
    }

    return new ProcessRunInfo(stdout.ToString(), stderr.ToString(), process.ExitCode);
  }

  public static ProcessRunInfo AssertExitCode(this ProcessRunInfo self)
  {
    Assert.That(self.ExitCode, Is.EqualTo(0), $"Proces exited with non-zero code");
    return self;
  }

  public static string SortLines(string input)
  {
    // var list = input.Split('\r', '\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    var list = input.Split(new[]{'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    list.Sort();
    return string.Join(Environment.NewLine, list);
  }
}