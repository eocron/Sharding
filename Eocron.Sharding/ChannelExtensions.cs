using System.Collections.Generic;
using System.Threading.Channels;

namespace Eocron.Sharding
{
    public static class ChannelExtensions
    {
        public static List<T> TryReadAll<T>(this ChannelReader<T> output)
        {
            var result = new List<T>();
            while (output.TryRead(out var item))
            {
                result.Add(item);
            }
            return result;
        }
    }
}
