namespace core;

public class MergeIndex
{
  public static void SaveTo(Stream stream, IReadOnlyCollection<IndexReader> indicies)
  {
    if (stream.Position != 0)
      stream = new HackPositionStream(stream);

    var writer = new BinaryWriter(stream);

    // write fake preamble just to reserve length
    default(Preamble).WriteTo(writer);

    foreach (var index in indicies)
    {
      
    }
  }
}