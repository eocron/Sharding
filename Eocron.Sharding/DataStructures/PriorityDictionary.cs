using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Eocron.Sharding.DataStructures
{
    public sealed class PriorityDictionary<TKey, TPriority, TElement> : IPriorityDictionary<TKey, TPriority, TElement>
        where TKey : IEquatable<TKey>
        where TPriority : IComparable<TPriority>
    {
        public PriorityDictionary(IEqualityComparer<TKey> keyComparer = null,
            IComparer<TPriority> priorityComparer = null)
        {
            _priorityComparer = priorityComparer ?? Comparer<TPriority>.Default;
            _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
            _priorityToSetIndex = new SortedDictionary<TPriority, Dictionary<TKey, TElement>>(_priorityComparer);
            _keyToPriorityIndex = new Dictionary<TKey, TPriority>(_keyComparer);
        }

        public void Clear()
        {
            _keyToPriorityIndex.Clear();
            _priorityToSetIndex.Clear();
        }

        public void Enqueue(TKey key, TPriority priority, TElement element)
        {
            _keyToPriorityIndex.Add(key, priority);
            if (!_priorityToSetIndex.TryGetValue(priority, out var tmp))
            {
                tmp = new Dictionary<TKey, TElement>(_keyComparer);
                _priorityToSetIndex[priority] = tmp;
            }

            tmp.Add(key, element);
        }

        public IEnumerator<KeyValuePair<TKey, TElement>> GetEnumerator()
        {
            return GetAll().GetEnumerator();
        }

        public bool TryDequeue(out TKey key, out TElement element)
        {
            key = default;
            element = default;
            if (_priorityToSetIndex.Count == 0)
                return false;

            var priorityPair = _priorityToSetIndex.First();
            var setPair = priorityPair.Value.First();
            priorityPair.Value.Remove(setPair.Key);
            if (priorityPair.Value.Count == 0) _priorityToSetIndex.Remove(priorityPair.Key);
            _keyToPriorityIndex.Remove(setPair.Key);
            key = setPair.Key;
            element = setPair.Value;
            return true;
        }

        public bool TryRemoveByKey(TKey key, out TElement element)
        {
            element = default;
            if (!_keyToPriorityIndex.TryGetValue(key, out var oldPriority)) return false;
            RemoveByOldPriority(key, oldPriority, out element);
            return true;
        }

        public bool TryUpdatePriority(TKey key, TPriority priority)
        {
            if (!_keyToPriorityIndex.TryGetValue(key, out var oldPriority)) return false;

            if (_priorityComparer.Compare(oldPriority, priority) == 0)
                return false;
            RemoveByOldPriority(key, oldPriority, out var element);
            Enqueue(key, priority, element);
            return true;
        }

        private IEnumerable<KeyValuePair<TKey, TElement>> GetAll()
        {
            foreach (var kv in _priorityToSetIndex)
            foreach (var element in kv.Value)
                yield return element;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private void RemoveByOldPriority(TKey key, TPriority oldPriority, out TElement element)
        {
            _keyToPriorityIndex.Remove(key);
            var set = _priorityToSetIndex[oldPriority];
            element = set[key];
            set.Remove(key);
            if (set.Count == 0) _priorityToSetIndex.Remove(oldPriority);
        }

        public int Count => _keyToPriorityIndex.Count;
        private readonly Dictionary<TKey, TPriority> _keyToPriorityIndex;
        private readonly IComparer<TPriority> _priorityComparer;
        private readonly IEqualityComparer<TKey> _keyComparer;
        private readonly SortedDictionary<TPriority, Dictionary<TKey, TElement>> _priorityToSetIndex;
    }
}