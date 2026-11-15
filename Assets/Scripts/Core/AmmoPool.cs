using System;
using System.Collections.Generic;
using System.Linq;

namespace Core
{
    public class AmmoPool<TKey>
    {
        private readonly Dictionary<TKey, int> _counts = new Dictionary<TKey, int>();
        public event Action<TKey, int> OnChanged;

        public void Set(TKey key, int count)
        {
            _counts[key] = count;

            OnChanged?.Invoke(key, count);
        }

        public int Get(TKey key)
        {
            return _counts.GetValueOrDefault(key, 0);
        }

        public bool Has(TKey key)
        {
            return _counts.TryGetValue(key, out int count) && count > 0;
        }

        public int Consume(TKey key)
        {
            if (!Has(key)) return 0;   // unknown key or already empty

            int newValue = --_counts[key];
            
            OnChanged?.Invoke(key, newValue);
            
            return newValue;
        }   // decrement, never below 0, raises OnChanged
        
        public int TotalRemaining()
        {
            return _counts.Sum(pair => pair.Value);
        }

        public void CopyFrom(IReadOnlyList<KeyValuePair<TKey,int>> entries)
        {
            _counts.Clear();
            foreach (var entry in entries)
            {
                _counts[entry.Key] = entry.Value;
            }
        }
    }
}