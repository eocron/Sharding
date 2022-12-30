using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Eocron.Sharding.DataStructures
{
    public sealed class PriorityDictionaryThreadSafetyGuard<TKey, TPriority, TElement> : IPriorityDictionary<TKey, TPriority, TElement>
    {
        private readonly IPriorityDictionary<TKey, TPriority, TElement> _inner;
        private readonly object _sync;

        public PriorityDictionaryThreadSafetyGuard(IPriorityDictionary<TKey, TPriority, TElement> inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _sync = new object();
        }

        public void Enqueue(TKey key, TPriority priority, TElement element)
        {
            lock (_sync)
            {
                _inner.Enqueue(key, priority, element);
            }
        }

        public bool TryUpdatePriority(TKey key, TPriority priority)
        {
            lock (_sync)
            {
                return _inner.TryUpdatePriority(key, priority);
            }
        }

        public bool TryDequeue(out TKey key, out TElement element)
        {
            lock (_sync)
            {
                return _inner.TryDequeue(out key, out element);
            }
        }

        public bool TryRemoveByKey(TKey key, out TElement element)
        {
            lock (_sync)
            {
                return _inner.TryRemoveByKey(key, out element);
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                _inner.Clear();
            }
        }

        public IEnumerator<KeyValuePair<TKey, TElement>> GetEnumerator()
        {
            lock (_sync)
            {
                return _inner.ToList().GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}