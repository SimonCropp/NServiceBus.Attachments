﻿using System;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Attachments.Sql;

class MessageAttachmentsFromConnection : IMessageAttachments
{
    SqlConnection connection;
    string messageId;
    IPersister persister;

    public MessageAttachmentsFromConnection(SqlConnection connection, string messageId, IPersister persister)
    {
        this.connection = connection;
        this.messageId = messageId;
        this.persister = persister;
    }

    public Task CopyTo(Stream target, CancellationToken cancellation = default)
    {
        return persister.CopyTo(messageId, "default", connection, null, target, cancellation);
    }

    public Task CopyTo(string name, Stream target, CancellationToken cancellation = default)
    {
        return persister.CopyTo(messageId, name, connection, null, target, cancellation);
    }

    public Task ProcessStream(Func<AttachmentStream, Task> action, CancellationToken cancellation = default)
    {
        return persister.ProcessStream(messageId, "default", connection, null, action, cancellation);
    }

    public Task ProcessStream(string name, Func<AttachmentStream, Task> action, CancellationToken cancellation = default)
    {
        return persister.ProcessStream(messageId, name, connection, null, action, cancellation);
    }

    public Task ProcessStreams(Func<string, AttachmentStream, Task> action, CancellationToken cancellation = default)
    {
        return persister.ProcessStreams(messageId, connection, null, action, cancellation);
    }

    public Task<AttachmentBytes> GetBytes(CancellationToken cancellation = default)
    {
        return persister.GetBytes(messageId, "default", connection, null, cancellation);
    }

    public Task<AttachmentBytes> GetBytes(string name, CancellationToken cancellation = default)
    {
        return persister.GetBytes(messageId, name, connection, null, cancellation);
    }

    public Task<AttachmentStream> GetStream(CancellationToken cancellation = default)
    {
        return persister.GetStream(messageId, "default", connection, null, cancellation);
    }

    public Task<AttachmentStream> GetStream(string name, CancellationToken cancellation = default)
    {
        return persister.GetStream(messageId, name, connection, null, cancellation);
    }

    public Task CopyToForMessage(string messageId, Stream target, CancellationToken cancellation = default)
    {
        return persister.CopyTo(messageId, "default", connection, null, target, cancellation);
    }

    public Task CopyToForMessage(string messageId, string name, Stream target, CancellationToken cancellation = default)
    {
        return persister.CopyTo(messageId, name, connection, null, target, cancellation);
    }

    public Task ProcessStreamForMessage(string messageId, Func<AttachmentStream, Task> action, CancellationToken cancellation = default)
    {
        return persister.ProcessStream(messageId, "default", connection, null, action, cancellation);
    }

    public Task ProcessStreamForMessage(string messageId, string name, Func<AttachmentStream, Task> action, CancellationToken cancellation = default)
    {
        return persister.ProcessStream(messageId, name, connection, null, action, cancellation);
    }

    public Task ProcessStreamsForMessage(string messageId, Func<string, AttachmentStream, Task> action, CancellationToken cancellation = default)
    {
        return persister.ProcessStreams(messageId, connection, null, action, cancellation);
    }

    public async Task<AttachmentBytes> GetBytesForMessage(string messageId, CancellationToken cancellation = default)
    {
        return await persister.GetBytes(messageId, "default", connection, null, cancellation);
    }

    public Task<AttachmentBytes> GetBytesForMessage(string messageId, string name, CancellationToken cancellation = default)
    {
        return persister.GetBytes(messageId, name, connection, null, cancellation);
    }

    public Task<AttachmentStream> GetStreamForMessage(string messageId, CancellationToken cancellation = default)
    {
        return persister.GetStream(messageId, "default", connection, null, cancellation);
    }

    public Task<AttachmentStream> GetStreamForMessage(string messageId, string name, CancellationToken cancellation = default)
    {
        return persister.GetStream(messageId, name, connection, null, cancellation);
    }
}