﻿using System.Diagnostics;
using Eocron.Sharding.Configuration;

namespace Eocron.Sharding.TestCommon
{
    public sealed class NewLineProcessInputOutputHandlerFactory : IProcessInputOutputHandlerFactory<string, string, string>
    {
        public IProcessInputOutputHandler<string, string, string> CreateHandler(Process process)
        {
            return new NewLineProcessInputOutputHandler(process);
        }
    }
}