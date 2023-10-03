using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using core.util;
using JetBrains.Diagnostics;
using JetBrains.Serialization;
using JetBrains.Util;

namespace core;

public class IndexReader
{
  private readonly Lazy<Dictionary<string, DocNode>> _byPathLookup;
  private readonly Stream _indexStream;
  private readonly Preamble _preamble;
  private readonly long _start;
  private byte[] _trigramSchema;

  private int TrigramCount => (_trigramSchema.Length - 16) / 12;

  public Dictionary<string, DocNode> PathLookup => _byPathLookup.Value;

  public unsafe IndexReader(Stream indexStream)
  {
    _start = indexStream.Position;
    _byPathLookup = new(CreateByPathLookup); // lazy, not always needed
    _indexStream = indexStream;
    _preamble = Preamble.Read(_indexStream);

    _indexStream.Position = _start + _preamble.TrigramIndexOffset;
    var trigramLength = (int)(_preamble.Length - _preamble.TrigramIndexOffset);
    _trigramSchema = new byte[trigramLength];
    _indexStream.Read(_trigramSchema, 0, trigramLength);
  }

  public IEnumerable<(Trigram trigram, IReadOnlyCollection<int> docIds)> ReadAllPostingLists()
  {
    var c = TrigramCount;
    for (int i = 0; i < c; i++)
    {
      var val = ReadTrigramBlock(i);
      yield return (val.Val, ReadDocumentIds(val));
    }
  }

  public InMemoryIndexBuilder ToWritable()
  {
    var builder = new InMemoryIndexBuilder(ReadAllDocNodes());
    foreach (var (t, docId) in ReadAllPostingLists())
    {
      builder.AddTrigrams(t, docId);
    }

    return builder;
  }
    
  public IReadOnlyCollection<DocNode> Evaluate(Query query)
  {
    var documentsIds = Evaluate(query, out _);
    var result = new List<DocNode>(documentsIds.Count);
    foreach (var docId in documentsIds) 
      result.Add(ReadDocNodeById(docId));

    return result;
  }

  private DocRow[] ReadAllDocRows()
  {
    var docCount = _preamble.DocumentsCount;
    var result = new DocRow[docCount];
    _indexStream.Position = _start + _preamble.DocumentsTableOffset;
    var reader = new BinaryReader(_indexStream, Encoding.UTF8, true);
    for (int i = 0; i < docCount; i++)
    {
      var offset = reader.ReadUInt32();
      var modStamp = reader.ReadInt64();  
      result[i] = new DocRow(offset, modStamp);
    }

    return result;
  }

  public unsafe DocNode[] ReadAllDocNodes()
  {
    var docRows = ReadAllDocRows();
    var result = new DocNode[docRows.Length - 1];
    _indexStream.Position = _start + _preamble.StringsOffset;
    var pool = ArrayPool<byte>.Shared;
    for (int i = 0; i < docRows.Length - 1; i++)
    {
      var row = docRows[i];
      var length = (int) (docRows[i + 1].PathOffset - row.PathOffset);
      var bytes = pool.Rent(length);

      var read = _indexStream.Read(bytes, 0, length);
      Assertion.Assert(read == length);
      fixed (void* bptr = bytes)
      {
        var path = new string((sbyte*)bptr, 0, length, Encoding.UTF8);
        result[i] = new DocNode(i, path, (long)row.ModificationStamp);
      }
      pool.Return(bytes);
    }

    return result;
  }

  private unsafe DocNode ReadDocNodeById(int docId)
  {
    if (docId > _preamble.DocumentsCount)
      throw new ArgumentOutOfRangeException(nameof(docId));
    
    _indexStream.Position = _start + _preamble.DocumentsTableOffset + DocRow.Sizeof * docId;
    var reader = new BinaryReader(_indexStream, Encoding.UTF8, true);
    var offset = reader.ReadUInt32();
    var modStamp = reader.ReadInt64();
    var length = (int) (reader.ReadUInt32() - offset); /* read next offset to obtain length*/
    var row = new DocRow(offset, modStamp);
    
    _indexStream.Position = _start + _preamble.StringsOffset + row.PathOffset;
    var bytes = reader.ReadBytes(length);
    fixed (void* bptr = bytes)
    {
      var path = new string((sbyte*)bptr, 0, bytes.Length, Encoding.UTF8);
      return new DocNode(docId, path, (long)row.ModificationStamp);
    }
  }

  public IReadOnlyCollection<int> Evaluate(Query query, out HashSet<int>? rwResult)
  {
    switch (query)
    {
      case Query.And and:
      {
        var a = Evaluate(and.A, out var rA);
        var b = Evaluate(and.B, out var rB);
        var result = rA ?? rB ?? new HashSet<int>(a);
        if (rA == null && rB != null)
          result.IntersectWith(a);
        else 
          result.IntersectWith(b);
        rwResult = result;
        return result;
      }
      case Query.Or or:
      {
        var a = Evaluate(or.A, out var rA);
        var b = Evaluate(or.B, out var rB);
        var result = rA ?? rB ?? new HashSet<int>(a);
        if (rA == null && rB != null)
          result.UnionWith(a);
        else 
          result.UnionWith(b);
        rwResult = result;
        return result;
      }
      case Query.Contains cq:
        rwResult = null;
        return GetDocumentsIds(cq.Val);
    }

    throw new ArgumentOutOfRangeException(nameof(query));
  }

  public IReadOnlyCollection<DocNode> ContainingStr(string str)
  {
    if (string.IsNullOrEmpty(str))
    {
      return ReadAllDocNodes();
    }
    Query? query = null;
    if (str.Length == 1)
    {
      var trigrams = new List<Trigram>();
      var hash = Utils.HashChar(str[0]);
      foreach (var t in ReadAllTrigrams())
      {
        if (t.A == hash || t.B == hash || t.C == hash)
          trigrams.Add(t);
      }
      return EvaluateWildcard(trigrams);
    }
    else if (str.Length == 2)
    {
      var trigrams = new List<Trigram>();
      var p = Utils.HashChar(str[0]) << 8 & Utils.HashChar(str[1]);
      foreach (var t in ReadAllTrigrams())
      {
        if (((t.Val >> 8) == p) || ((t.Val & 0x00FFFF) == p))
        {
          trigrams.Add(t);
        }
      }
      return EvaluateWildcard(trigrams);
    }
    else
    {
      for (int i = 0; i < str.Length - 2; i++)
      {
        var trigram = new Trigram(Utils.HashChar(str[i]), Utils.HashChar(str[i + 1]), Utils.HashChar(str[i + 2]));
        if (query == null)
          query = new Query.Contains(trigram);
        else
          query = new Query.And(query, new Query.Contains(trigram));
      }
    }

    return Evaluate(query!);
  }

  private IReadOnlyCollection<DocNode> EvaluateWildcard(List<Trigram> trigrams)
  {
    if (trigrams.Count == 0)
      return EmptyArray<DocNode>.Instance;

    var head = trigrams.First();
    var tail = trigrams.Skip(1);
    var query1 = tail.Aggregate<Trigram, Query>(new Query.Contains(head), (a, b) => new Query.Or(new Query.Contains(b), a));
    return Evaluate(query1);
  }

  private IReadOnlyCollection<int> GetDocumentsIds(Trigram trigram)
  {
    var block = TryFindTrigramBlock(trigram);
    if (block.HasValue)
    {
      return ReadDocumentIds(block.Value);
    }

    return EmptyArray<int>.Instance;
  }

  private TrigramBlock? TryFindTrigramBlock(Trigram trigram)
  {
    int left = 0;
    int right = TrigramCount - 1;

    while (left <= right)
    {
      int middle = left + (right - left) / 2;

      var (middleT, _) = ReadTrigram(middle);
      if (middleT == trigram)
        return ReadTrigramBlock(middle);

      if (middleT.Val < trigram.Val)
        left = middle + 1;
      else
        right = middle - 1;
    }
    return default;
  }

  private TrigramBlock ReadTrigramBlock(int index)
  {
    var val = ReadTrigram(index);
    var next = ReadTrigram(index + 1);
    return new TrigramBlock(val.Value, val.Offset, (int)(next.Offset - val.Offset));
  }

  private (Trigram Value, long Offset) ReadTrigram(int index)
  {
    var span = _trigramSchema.AsSpan(4 + index * 12, 12);
    return (new Trigram(MemoryMarshal.Read<int>(span[..4])), MemoryMarshal.Read<long>(span[4..]));
  }

  private Trigram ReadTrigramOnly(int index)
  {
    return new Trigram(MemoryMarshal.Read<int>(_trigramSchema.AsSpan(4 + index * 12, 4)));
  }

  private IEnumerable<Trigram> ReadAllTrigrams()
  {
    var c = TrigramCount;
    for (int i = 0; i < c; i++)
    {
      yield return ReadTrigram(i).Value;
    }
  }

  private IReadOnlyCollection<int> ReadDocumentIds(TrigramBlock block)
  {
    var result = new List<int>(block.Length);
    
    _indexStream.Position = block.Offset + _start + _preamble.PostingListOffset;
    var bytes = ArrayPool<byte>.Shared.Rent(block.Length);
    _indexStream.ReadExactly(bytes, 0, block.Length);

    var reader = new BinaryReader(new MemoryStream(bytes, 0, block.Length), Encoding.ASCII);
    int prev = 0;
    while (reader.BaseStream.Position < block.Length)
    {
      var delta = reader.ReadUVarint();
      var val = (int)(delta + prev);
      result.Add(val);
      Assertion.Assert(val < _preamble.DocumentsCount);
      prev = val;
    }
    ArrayPool<byte>.Shared.Return(bytes);


    return result;
  }

  private Dictionary<string, DocNode> CreateByPathLookup()
  {
    throw new NotImplementedException();
    /*var lookup = new Dictionary<string, DocNode>(_docNodes.Count);
    foreach (var docNode in _docNodes)
      lookup.Add(docNode.Path, docNode);

    return lookup;*/
  }
}