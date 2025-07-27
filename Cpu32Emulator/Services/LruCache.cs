using System;
using System.Collections.Generic;
using System.Linq;

namespace Cpu32Emulator.Services
{
    /// <summary>
    /// Simple LRU (Least Recently Used) cache implementation
    /// </summary>
    public class LruCache<TKey, TValue> where TKey : notnull
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache;
        private readonly LinkedList<CacheItem> _lruList;

        public LruCache(int capacity)
        {
            _capacity = capacity;
            _cache = new Dictionary<TKey, LinkedListNode<CacheItem>>();
            _lruList = new LinkedList<CacheItem>();
        }

        public int Count => _cache.Count;
        public int Capacity => _capacity;

        /// <summary>
        /// Gets a value from the cache and marks it as recently used
        /// </summary>
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                value = node.Value.Value;
                
                // Move to front (most recently used)
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                
                return true;
            }

            value = default!;
            return false;
        }

        /// <summary>
        /// Adds or updates a value in the cache
        /// </summary>
        public void Set(TKey key, TValue value)
        {
            if (_cache.TryGetValue(key, out var existingNode))
            {
                // Update existing value and move to front
                existingNode.Value.Value = value;
                _lruList.Remove(existingNode);
                _lruList.AddFirst(existingNode);
            }
            else
            {
                // Add new value
                if (_cache.Count >= _capacity)
                {
                    // Remove least recently used item
                    var lru = _lruList.Last!;
                    _lruList.RemoveLast();
                    _cache.Remove(lru.Value.Key);
                    
                    // Invoke eviction callback if the value supports it
                    if (lru.Value.Value is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }

                // Add new item at front
                var newItem = new CacheItem { Key = key, Value = value };
                var newNode = _lruList.AddFirst(newItem);
                _cache[key] = newNode;
            }
        }

        /// <summary>
        /// Removes a value from the cache
        /// </summary>
        public bool Remove(TKey key)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                _lruList.Remove(node);
                _cache.Remove(key);
                
                if (node.Value.Value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Clears all items from the cache
        /// </summary>
        public void Clear()
        {
            // Dispose all disposable values
            foreach (var node in _lruList)
            {
                if (node.Value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            
            _cache.Clear();
            _lruList.Clear();
        }

        /// <summary>
        /// Gets all keys in the cache, ordered from most to least recently used
        /// </summary>
        public IEnumerable<TKey> GetKeys()
        {
            return _lruList.Select(item => item.Key);
        }

        private class CacheItem
        {
            public TKey Key { get; set; } = default!;
            public TValue Value { get; set; } = default!;
        }
    }
}
