﻿using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using core.util;
using JetBrains.Diagnostics;
using JetBrains.Util;

namespace core;

public class IndexReader
{
  private readonly Stream _indexStream;
  private readonly Preamble _preamble;
  private readonly long _start;
  private readonly byte[] _trigramSchema;

  private int TrigramCount => (_trigramSchema.Length - 16) / 12;
  public Preamble Preamble => _preamble;

  public IndexReader(Stream indexStream)
  {
    _start = indexStream.Position;
    _indexStream = indexStream;
    _preamble = Preamble.Read(_indexStream);

    _indexStream.Position = _start + _preamble.TrigramIndexOffset;
    var trigramLength = (int)(_preamble.Length - _preamble.TrigramIndexOffset);
    _trigramSchema = new byte[trigramLength];
    _indexStream.ReadExactly(_trigramSchema, 0, trigramLength);
  }

  public IEnumerable<(Trigram trigram, IReadOnlyCollection<int> docIds)> ReadAllPostingLists()
  {
    var c = TrigramCount;
    for (int i = 0; i < c; i++)
    {
      var val = ReadTrigramBlock(i);
      yield return (val.Val, ReadDocumentIds(val, x => new int[x]));
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
    var documentsIds = EvaluateDocIds(query);
    var result = new List<DocNode>(documentsIds.Count);
    foreach (var docId in documentsIds) 
      result.Add(ReadDocNodeById(docId));
    
    ArrayPool<int>.Shared.Return(documentsIds.Array!);
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

  /// returns an array segment in rented array! it is better to return it back to SharedArrayPool
  private ArraySegment<int> EvaluateDocIds(Query query)
  {
    var pool = ArrayPool<int>.Shared;
    switch (query)
    {
      case Query.And and:
      {
        ArraySegment<int>? result = null;
        foreach (var q in and.Queries)
        {
          var ev = EvaluateDocIds(q);
          if (result == null)
          {
            result = ev;
          }
          else
          {
            result = SortedArrayUtil.Intersect(result.Value, ev);
            pool.Return(ev.Array!);
          }
        }
        
        return result ?? EmptyArray<int>.Instance;
      }
      case Query.Or or:
      {
        ArraySegment<int>? result = null;
        foreach (var q in or.Queries)
        {
          var ev = EvaluateDocIds(q);
          if (result == null)
          {
            result = ev;
          }
          else
          {
            var a = EvaluateDocIds(q);
            var b = result.Value;
            result = SortedArrayUtil.Union(a, b, pool.Rent(a.Count + b.Count));
            pool.Return(a.Array!);
            pool.Return(b.Array!);
          }
        }
        return result ?? EmptyArray<int>.Instance;
      }
      case Query.Contains cq:
        return GetDocumentsIds(cq.Val, pool.Rent);
    }

    throw new ArgumentOutOfRangeException(nameof(query));
  }

  public IReadOnlyCollection<DocNode> ContainingStr(string str, bool caseSensitive)
  {
    var runes = str.EnumerateRunes().ToArray();

    if (runes.Length == 0)
    {
      return ReadAllDocNodes();
    }
    Query? query = null;

    if (runes.Length == 1)
    {
      // TODO: case-insensitive wildcard search
      var trigrams = new List<Trigram>();
      var hash = Utils.HashCodepoint(runes[0].Value);
      foreach (var t in ReadAllTrigrams())
      {
        if (t.A == hash || t.B == hash || t.C == hash)
          trigrams.Add(t);
      }
      return EvaluateWildcard(trigrams);
    }
    else if (runes.Length == 2)
    {
      // TODO: case-insensitive wildcard search
      var trigrams = new List<Trigram>();
      var p = Utils.HashCodepoint(runes[0].Value) << 8 & Utils.HashCodepoint(runes[1].Value);
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
      query = CreateNGramQuery(caseSensitive, runes);
    }

    return Evaluate(query!);
  }


  private Query CreateNGramQuery(bool caseSensitive, Rune[] runes)
  {
    var ngrams = new HashSet<Range>();
    var c = new SparseTrigramCollector(onIterval: (start, length, hash) => ngrams.Add((int)start..((int)start + length)));
    c.Run(runes);

    var andQueries = new List<Query>();
    foreach (var ngramRange in ngrams)
    {
      andQueries.Add(CreateQuery(caseSensitive, runes.AsSpan()[ngramRange]));
    }

    for (int i = 0; i < runes.Length - 2; i++)
    {
      andQueries.Add(CreateQuery(caseSensitive, runes.AsSpan().Slice(i, 3)));
    }

    return new Query.And(andQueries);
  }


  private Query CreateQuery(bool caseSensitive, ReadOnlySpan<Rune> ngram)
  {
    if (caseSensitive)
    {
      var hash = Utils.HashTrigram(ngram);
      return new Query.Contains(new Trigram(hash));
    }
    else
    {
      var hashes = new HashSet<int>();
      foreach (var i in VaryCase(ngram))
      {
        var hash = Utils.HashTrigram(i);
        hashes.Add(hash);
      }

      var orQueries = hashes.Select(h => new Query.Contains(new Trigram(h))).ToList();
      return new Query.Or(orQueries);
    }
  }

  private IEnumerable<Rune[]> VaryCase(ReadOnlySpan<Rune> runes)
  {
    return Iterate(runes.ToArray(), 0);

    static IEnumerable<Rune[]> Iterate(Rune[] mutRunes, int pos)
    {
      if (pos >= mutRunes.Length)
      {
        yield return mutRunes;
      }
      else
      {
        var r = mutRunes[pos];
        var l = Rune.ToLowerInvariant(mutRunes[pos]);
        var u = Rune.ToUpperInvariant(mutRunes[pos]);
        foreach (var res in Iterate(mutRunes, pos + 1))
          yield return res;
        if ((mutRunes[pos] = l) != r)
          foreach (var res in Iterate(mutRunes, pos + 1))
            yield return res;
        if ((mutRunes[pos] = u) != r)
          foreach (var res in Iterate(mutRunes, pos + 1))
            yield return res;
      }
    }
  }

  private IEnumerable<Trigram> VaryCase(Rune a, Rune b, Rune c)
  {
    var cul = CultureInfo.CurrentCulture;
    return Enumerable.Range(0, 8).Select(i =>
      new Trigram(
        Utils.HashCodepoint(((i & 0b001) != 0 ? Rune.ToLower(a, cul) : Rune.ToUpper(a, cul)).Value),
        Utils.HashCodepoint(((i & 0b010) != 0 ? Rune.ToLower(b, cul) : Rune.ToUpper(b, cul)).Value),
        Utils.HashCodepoint(((i & 0b100) != 0 ? Rune.ToLower(c, cul) : Rune.ToUpper(c, cul)).Value)
    )).Distinct();
  }


  private IReadOnlyCollection<DocNode> EvaluateWildcard(List<Trigram> trigrams)
  {
    if (trigrams.Count == 0)
      return EmptyArray<DocNode>.Instance;

    var containsQueries = trigrams.Select(t => new Query.Contains(t)).ToList();
    var query = new Query.Or(containsQueries);
    return Evaluate(query);
  }

  private ArraySegment<int> GetDocumentsIds(Trigram trigram, Func<int, int[]> allocator)
  {
    var block = TryFindTrigramBlock(trigram);
    if (block.HasValue)
    {
      return ReadDocumentIds(block.Value, allocator);
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

  private ArraySegment<int> ReadDocumentIds(TrigramBlock block, Func<int, int[]> allocator)
  {
    var result = allocator(block.Length);
    
    _indexStream.Position = block.Offset + _start + _preamble.PostingListOffset;
    var bytes = ArrayPool<byte>.Shared.Rent(block.Length);
    _indexStream.ReadExactly(bytes, 0, block.Length);

    var i = 0;
    var reader = new BinaryReader(new MemoryStream(bytes, 0, block.Length), Encoding.ASCII);
    int prev = 0;
    while (reader.BaseStream.Position < block.Length)
    {
      var delta = reader.ReadUVarint();
      var val = (int)(delta + prev);
      result[i++] = val;
      Assertion.Assert(val < _preamble.DocumentsCount);
      prev = val;
    }
    ArrayPool<byte>.Shared.Return(bytes);

    return new ArraySegment<int>(result, 0, i);
  }

  public void Dump(TextWriter o, bool verbose)
  {
    _preamble.Dump(o);

    if (verbose)
    {
      o.WriteLine("## Trigram | list of document ids");
      foreach (var (trigram, postingList) in ReadAllPostingLists())
      {
        o.WriteLine($"{trigram.Val,8} | {string.Join(',', postingList)}");
      }
      o.WriteLine();
      o.WriteLine("## DocId | Path | LastWriteTime");
      foreach (var docNode in ReadAllDocNodes())
      {
        o.WriteLine($"{docNode.DocId, 5} | {docNode.Path} | {docNode.LastWriteTime}");
      }
    }
    o.WriteLine();
  }
}