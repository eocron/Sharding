using System.Collections.Generic;

namespace Eocron.Sharding.DataStructures
{
    public interface IPriorityDictionary<TKey, in TPriority, TElement>: IEnumerable<KeyValuePair<TKey, TElement>>
    {
        void Enqueue(TKey key, TPriority priority, TElement element);

        /// <summary>
        /// Updates priority by element key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="priority"></param>
        /// <returns>True - if priority updated; False - if key not found or priority not changed</returns>
        bool TryUpdatePriority(TKey key, TPriority priority);

        /// <summary>
        /// Return first priority element
        /// </summary>
        /// <param name="key"></param>
        /// <param name="element"></param>
        /// <returns>True - if element is returned</returns>
        bool TryDequeue(out TKey key, out TElement element);

        /// <summary>
        /// Removes element by key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="element"></param>
        /// <returns></returns>
        bool TryRemoveByKey(TKey key, out TElement element);

        void Clear();
    }
}