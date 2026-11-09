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
            return _counts[key];
        }

        public bool Has(TKey key)
        {
            return _counts.ContainsKey(key);
        }

        public int Consume(TKey key)
        {
            if (_counts[key] <= 0)
            {
                return 0;
            }

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