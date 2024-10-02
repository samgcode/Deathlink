using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Deathlink.Infrastructure
{
  public class BidirectionalDictionary<TPrimary, TSecondary> : IEnumerable<KeyValuePair<TPrimary, TSecondary>>
  {
    private Dictionary<TPrimary, TSecondary> _forward = new Dictionary<TPrimary, TSecondary>();
    private Dictionary<TSecondary, TPrimary> _reverse = new Dictionary<TSecondary, TPrimary>();
    public Indexer<TPrimary, TSecondary> Forward { get; private set; }
    public Indexer<TSecondary, TPrimary> Reverse { get; private set; }

    public BidirectionalDictionary()
    {
      Forward = new Indexer<TPrimary, TSecondary>(_forward);
      Reverse = new Indexer<TSecondary, TPrimary>(_reverse);
    }

    public int Count
    {
      get
      {
        return _forward.Count;
      }
    }

    public bool Contains(TPrimary tp) { return _forward.ContainsKey(tp); }
    public bool Contains(TSecondary ts) { return _reverse.ContainsKey(ts); }
    public bool Contains(TPrimary tp, TSecondary ts)
    {
      return _forward.ContainsKey(tp) && _forward[tp].Equals(ts);
    }

    public void Add(TPrimary tp, TSecondary ts)
    {
      Remove(tp);
      Remove(ts);
      _forward.Add(tp, ts);
      _reverse.Add(ts, tp);
    }

    /// <summary>
    /// Removes an element from the dictionary based on the primary type
    /// </summary>
    /// <param name="tp">Element to remove</param>
    /// <returns></returns>
    public bool Remove(TPrimary tp)
    {
      if (!_forward.ContainsKey(tp)) return false;
      TSecondary ts = _forward[tp];
      if (_forward.Remove(tp))
      {
        Remove(ts);
        return true;
      }
      return false;
    }

    /// <summary>
    /// Removes an element from the dictionary based on the secondary type
    /// </summary>
    /// <param name="ts"></param>
    /// <returns></returns>
    public bool Remove(TSecondary ts)
    {
      if (!_reverse.ContainsKey(ts)) return false;
      TPrimary tp = _reverse[ts];
      if (_reverse.Remove(ts))
      {
        Remove(tp);
        return true;
      }
      return false;
    }

    public void Clear()
    {
      _forward.Clear();
      _reverse.Clear();
    }

    IEnumerator IEnumerable.GetEnumerator()
        => ((IEnumerable)Forward).GetEnumerator();
    IEnumerator<KeyValuePair<TPrimary, TSecondary>> IEnumerable<KeyValuePair<TPrimary, TSecondary>>.GetEnumerator()
        => ((IEnumerable<KeyValuePair<TPrimary, TSecondary>>)Forward).GetEnumerator();

    public class Indexer<T1, T2> : IEnumerable<KeyValuePair<T1, T2>>
    {
      private Dictionary<T1, T2> _dictionary;
      public Indexer(Dictionary<T1, T2> dictionary)
      {
        _dictionary = dictionary;
      }
      public T2 this[T1 index]
      {
        get { return _dictionary[index]; }
        set { _dictionary[index] = value; }
      }

      IEnumerator IEnumerable.GetEnumerator()
          => ((IEnumerable)_dictionary).GetEnumerator();
      public IEnumerator<KeyValuePair<T1, T2>> GetEnumerator()
          => ((IEnumerable<KeyValuePair<T1, T2>>)_dictionary).GetEnumerator();
    }

  }
}
