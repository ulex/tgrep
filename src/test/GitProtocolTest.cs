using System.IO.Pipes;

namespace test;

/// There was an idea to use git fsmonitor on windows to detect changed files
/// Following thigs should be checked:
/// 1. is it valid to query fsdaemon with some token and do not update the token in the indexfile afterward. E.g. it
/// seems that git status is always write to the index file when there are some changes in working directory, which is
/// implicitly mean, that fsmonitor flush caches when receive a query request for specific token
/// 2. 
public class GitProtocolTest
{
  // #define LARGE_PACKET_MAX 65520
  // #define LARGE_PACKET_DATA_MAX (LARGE_PACKET_MAX - 4)
  /*byte[] packet_header(int size)
  {
    byte hex(int a)
    {
      var bytes = "0123456789abcdef"u8;
      return bytes[(a) & 15];
    }
    var buf = new byte[4];
    buf[0] = hex(size >> 12);
    buf[1] = hex(size >> 8);
    buf[2] = hex(size >> 4);
    buf[3] = hex(size);
    return buf;
  }


  [Test]
  public void Query()
  {
    using (var client = new NamedPipeClientStream(".", "C_\\code\\git\\.git\\fsmonitor--daemon.ipc", PipeDirection.InOut))
    {
      var 
    }
  }*/
  
}