﻿using System;
using System.IO;
using System.Threading.Tasks;

namespace NServiceBus.Attachments
{
    public interface IIncomingAttachments
    {
        Task CopyTo(string name, Stream target);
        Task ProcessStream(string name, Func<Stream, Task> action);
        Task ProcessStreams(Func<string, Stream, Task> action);
        Task<byte[]> GetBytes(string name);
    }
}