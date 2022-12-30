﻿using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Eocron.Sharding.Configuration
{
    public sealed class NewLineSerializer : IStreamWriterSerializer<string>
    {
        public async Task SerializeToAsync(StreamWriter writer, IEnumerable<string> items, CancellationToken ct)
        {
            foreach (var item in items) await writer.WriteLineAsync(item).ConfigureAwait(false);
        }
    }
}