using System.Diagnostics;
using System.Text;

namespace test.util;

public record ProcessRunInfo(
  string StdOut, 
  string StdErr, 
  int ExitCode);

public static class TestProcess
{
  public static async Task<ProcessRunInfo> RunAsync(string exe, string args, string workingDirectory, IDictionary<string, string>? env = null)
  {
    var startInfo = new ProcessStartInfo(exe, args)
    {
      RedirectStandardError = true,
      RedirectStandardOutput = true,
      UseShellExecute = false,
      WorkingDirectory = workingDirectory
    };
    if (env != null)
    {
      foreach (var (k, val) in env) 
        startInfo.EnvironmentVariables[k] = val;
    }

    StringBuilder stdout = new();
    StringBuilder stderr = new();

    var process = new Process()
    {
      StartInfo = startInfo
    };
    if (process == null) 
      throw new InvalidOperationException("Unable to start process");
    process.ErrorDataReceived += (_, eventArgs) => { if (eventArgs.Data != null) lock(stderr) stderr.Append(eventArgs.Data); };
    process.OutputDataReceived += (_, eventArgs) => { if (eventArgs.Data != null) lock(stdout) stdout.Append(eventArgs.Data); };
    
    process.Start();

    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    await process.WaitForExitAsync();
    return new ProcessRunInfo(stdout.ToString(), stderr.ToString(), process.ExitCode);
  }

  public static ProcessRunInfo AssertExitCode(this ProcessRunInfo self)
  {
    Assert.AreEqual(0, self.ExitCode, "Proces exited with non-zero code");
    return self;
  }
}