using System.Runtime.InteropServices;
using System.Text;
using core.util;
using JetBrains.Diagnostics;

namespace core;

/**
The file with index may contains one or many indexes in the following format:
1. [32] Preamble.
   [8] Magic 8 bytes word "IDX_ULEX".
   [4] Int32 file format version
   [8] UInt64 strings table offset
   [8] UInt64 documents offset
   [8] UInt64 Trigram Index offset
   [8] UInt64 Total length of the current trigram block
2. Posting lists
   Sequence of delta-encoded VarUInts32 of documents. Trigrams boundaries are defined later in the trigram
   index offssets part.
3. Metadata. (the contract is that metadata always fits in memory)
3.1 Strings table
   just binary blob of sequentially written paths,
   a single-byte zero, to check for index corruption
3.2 Documents table
    UInt32 Documents count = dc
    *dc documents. The docId is definied based on the position of record in this table.
      [4] UInt32 offset to path in strings table. UInt32.MaxValue = file is removed
      [8] UInt64 file modification stamp
    last fake document with modificationStamp = 0xFEEDFEEDFEEDFEED and Offset to the single-byte-zero in strings table
3.3 Trigram Index. A schema for postings  list.
   VarInt32 block count = bc. negative and greater than 0xFF00 values ares used to detect corrupted index.
   *bc blocks:
      VarInt32 delta-encoded trigram
      VarInt32 Length of block
   a single-byte zero, to check for index corruption
*/
public class InMemoryIndexBuilder
{
  private readonly List<int>?[] _index = new List<int>[256 * 256 * 256];
  private readonly List<DocNode> _documents;

  // ReSharper disable once InconsistentlySynchronizedField
  public long EstimatedSize => _totalTrigrams * 2 + _documents.Count * 64;

  private long _totalTrigrams = 0;

  public InMemoryIndexBuilder()
  {
    _documents = new List<DocNode>();
  }

  public InMemoryIndexBuilder(IEnumerable<DocNode> documents)
  {
    _documents = new List<DocNode>(documents);
    EnsureDocumentsSorted();
  }

  /// thread-safe
  public void AddDocument(string path, long modificationUtc, IEnumerable<int> trigrams)
  {
    int docId;
    lock (_documents)
    {
      docId = _documents.Count;
      _documents.Add(new DocNode(docId, path, modificationUtc));
    }

    var total = 0l;
    foreach (var trigram in trigrams)
    {
      var list = _index[trigram];
      if (list == null)
      {
        Interlocked.CompareExchange(ref _index[trigram], new List<int>(), null);
        list = _index[trigram]!;
      }

      total++;
      lock (list) list.Add(docId);
    }

    Interlocked.Add(ref _totalTrigrams, total);
  }

  /// thread-safe
  public void AddTrigrams(Trigram trigram, IReadOnlyCollection<int> docIds)
  {
    var list = _index[trigram.Val];
    if (list == null)
    {
      Interlocked.CompareExchange(ref _index[trigram.Val], new List<int>(), null);
      list = _index[trigram.Val]!;
    }

    lock (list)
    {
      list.AddRange(docIds);
#if DEBUG
      lock (_documents)
      {
        foreach (var docId in docIds)
        {
          Assertion.Assert(docId < _documents.Count);
        }
      }
#endif
    }
    Interlocked.Add(ref _totalTrigrams, docIds.Count);
  }

  public void MarkAsRemoved(int documentId)
  {
    lock (_documents)
    {
      CollectionsMarshal.AsSpan(_documents)[documentId].DocId = -1;
    }
  }

  public void SaveTo(Stream stream)
  {
    if (stream.Position != 0)
      stream = new HackPositionStream(stream);
    lock (_documents)
    {
      EnsureDocumentsSorted();

      // documents can be removed, make copy and lookup
      var sortedDocs = _documents;

      var lookup =  new int[_documents.Count];
      int holes = 0;
      for (int i = 0; i < sortedDocs.Count; i++)
      {
        var docId = _documents[i].DocId;
        if (docId == -1)
        {
          lookup[i] = -1;
          holes++;
        }
        else
        {
          lookup[i] = docId - holes;
        }
      }

      var writer = new BinaryWriter(stream);
      // write fake preamble just to reserve length
      default(Preamble).WriteTo(writer);

      var offsetBlocks = new List<TrigramOffsetBlock>(0xFF00);
      // writing inverted documents index itself
      for (var i = 0; i < _index.Length; i++)
      {
        var docIds = _index[i];
        if (docIds != null)
        {
          docIds.Sort();
          long startOffset = writer.BaseStream.Position;
          int prev = 0;
          foreach (var docId in docIds)
          {
            if (docId == -1)
              continue;
            var newDocId = lookup[docId];
            Assertion.Assert(newDocId < _documents.Count);
    
            writer.WriteVarint(newDocId - prev);
            prev = newDocId;
          }

          int length = (int)(writer.BaseStream.Position - startOffset);
          offsetBlocks.Add(new TrigramOffsetBlock(i, startOffset, length));
        }
      }

      // Writing metadata part
      long stringsTableOffset = writer.BaseStream.Position;

      // Document paths, id + stamps
      var docTableOffset = WriteDocuments(writer, sortedDocs);

      // Trigram address table
      long trigramIndexOffset = writer.BaseStream.Position;
      writer.WriteVarint(offsetBlocks.Count);
      int prevT = 0;
      foreach (var ob in offsetBlocks)
      {
        writer.WriteVarint(ob.Trigram - prevT);
        writer.WriteVarint(ob.Length);
        prevT = ob.Trigram;
      }
      writer.Write((byte)0);


      var totalLength = writer.BaseStream.Position;
      writer.BaseStream.Position = 0;
      new Preamble(1, stringsTableOffset, trigramIndexOffset, docTableOffset, totalLength).WriteTo(writer);
      writer.BaseStream.Position = totalLength;
    }
  }

  private void EnsureDocumentsSorted()
  {
    lock (_documents)
    {
      for (int i = 0; i < _documents.Count; i++)
      {
        var docId = _documents[i].DocId;
        if (docId != i && docId != -1)
          throw new InvalidOperationException("Documents aren't sorted by DocId");
      }
    }
  }

  /// <summary>
  /// Writes strings blob and documents table
  /// </summary>
  private long WriteDocuments(BinaryWriter writer, List<DocNode> documents)
  {
    var start = writer.BaseStream.Position;
    long prev = start;
    var docRows = new List<DocRow>(documents.Count);
    foreach (var file in documents)
    {
      var bytes = Encoding.UTF8.GetBytes(file.Path);
      writer.Write(bytes);

      docRows.Add(new DocRow((uint)(prev - start), file.LastWriteTime));
      prev = writer.BaseStream.Position;
    }

    // fake document
    docRows.Add(new DocRow((uint)(prev - start), 0x0EEDFEEDFEEDFEED));

    long documentTableOffset = writer.BaseStream.Position;
    foreach (var docRow in docRows)
    {
      writer.Write(docRow.PathOffset);
      writer.Write(docRow.ModificationStamp);
    }
    writer.WriteVarint(0);

    return documentTableOffset;
  }

  public void Clear()
  {
    lock (_documents)
    {
      _documents.Clear();
      Array.Clear(_index);
      _totalTrigrams = 0;
    }
  }

  public void SaveTo(string filepath)
  {
    using (var st = File.Create(filepath))
      SaveTo(st);
  }
}

/**
 * Wraps a stream and maps position to
 * TODO: [HACK]
 */
public class HackPositionStream : Stream
{
  private readonly Stream _stream;
  private readonly long _offset;

  public HackPositionStream(Stream stream)
  {
    _stream = stream;
    _offset = stream.Position;
  }

  public override void Flush()
  {
    _stream.Flush();
  }

  public override int Read(byte[] buffer, int offset, int count)
  {
    return _stream.Read(buffer, offset, count);
  }

  public override long Seek(long offset, SeekOrigin origin)
  {
    switch (origin)
    {
      case SeekOrigin.Begin:
        return _stream.Seek(offset + _offset, origin);
      case SeekOrigin.Current:
      case SeekOrigin.End:
        return _stream.Seek(offset, origin);
    }
    throw new InvalidOperationException();
  }

  public override void SetLength(long value)
  {
    throw new NotSupportedException();
  }

  public override void Write(byte[] buffer, int offset, int count)
  {
    _stream.Write(buffer, offset, count);
  }

  public override bool CanRead => _stream.CanRead;

  public override bool CanSeek => _stream.CanSeek;

  public override bool CanWrite => _stream.CanWrite;

  public override long Length => _stream.Length;

  public override long Position
  {
    get => _stream.Position - _offset;
    set => _stream.Position = value + _offset;
  }
}