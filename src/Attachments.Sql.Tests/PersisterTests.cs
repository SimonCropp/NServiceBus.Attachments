﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Attachments.Sql;
using Xunit;
using Xunit.Abstractions;

public class PersisterTests :
    XunitApprovalBase
{
    DateTime defaultTestDate = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc);
    Dictionary<string, string> metadata = new Dictionary<string, string> { { "key", "value" } };
    Persister persister;

    static PersisterTests()
    {
        DbSetup.Setup();
    }

    public PersisterTests(ITestOutputHelper output) :
        base(output)
    {
        persister = new Persister("MessageAttachments");
    }

    [Fact]
    public async Task CopyTo()
    {
        await using var connection = Connection.OpenConnection();
        await Installer.CreateTable(connection, "MessageAttachments");
        await persister.DeleteAllAttachments(connection, null);
        await persister.SaveStream(connection, null, "theMessageId", "theName", defaultTestDate, GetStream());
        var memoryStream = new MemoryStream();
        await persister.CopyTo("theMessageId", "theName", connection, null, memoryStream);

        memoryStream.Position = 0;
        Assert.Equal(5, memoryStream.GetBuffer()[0]);
    }

    [Fact]
    public async Task GetBytes()
    {
        await using var connection = Connection.OpenConnection();
        await Installer.CreateTable(connection, "MessageAttachments");
        await persister.DeleteAllAttachments(connection, null);
        await persister.SaveStream(connection, null, "theMessageId", "theName", defaultTestDate, GetStream(), metadata);
        byte[] bytes = await persister.GetBytes("theMessageId", "theName", connection, null);
        Assert.Equal(5, bytes[0]);
    }

    [Fact]
    public async Task CaseInsensitiveRead()
    {
        await using var connection = Connection.OpenConnection();
        await Installer.CreateTable(connection, "MessageAttachments");
        await persister.DeleteAllAttachments(connection, null);
        await persister.SaveStream(connection, null, "theMessageId", "theName", defaultTestDate, GetStream());
        byte[] bytes = await persister.GetBytes("themeSsageid", "Thename", connection, null);
        Assert.Equal(5, bytes[0]);
    }

    [Fact]
    public async Task LongName()
    {
        await using var connection = Connection.OpenConnection();
        await Installer.CreateTable(connection, "MessageAttachments");
        await persister.DeleteAllAttachments(connection, null);
        var name = new string('a',255);
        await persister.SaveStream(connection, null, "theMessageId", name, defaultTestDate, GetStream());
        byte[] bytes = await persister.GetBytes("theMessageId", name, connection, null);
        Assert.Equal(5, bytes[0]);
    }

    [Fact]
    public async Task ProcessStream()
    {
        await using var connection = Connection.OpenConnection();
        await Installer.CreateTable(connection, "MessageAttachments");
        await persister.DeleteAllAttachments(connection, null);
        var count = 0;
        await persister.SaveStream(connection, null, "theMessageId", "theName", defaultTestDate, GetStream(), metadata);
        await persister.ProcessStream("theMessageId", "theName", connection, null,
            action: stream =>
            {
                count++;
                var array = ToBytes(stream);
                Assert.Equal(5, array[0]);
                return Task.CompletedTask;
            });
        Assert.Equal(1, count);
    }
    [Fact]
    public async Task ProcessStreamMultiple()
    {
        await using var connection = Connection.OpenConnection();
        await Installer.CreateTable(connection, "MessageAttachments");
        await persister.DeleteAllAttachments(connection, null);
        var count = 0;
        var saves = new List<Task>();
        for (var i = 0; i < 10; i++)
        {
            saves.Add(persister.SaveStream(connection, null, "theMessageId", $"theName{i}", defaultTestDate, GetStream(), metadata));
        }

        await Task.WhenAll(saves);

        var reads = new List<Task>();
        for (var i = 0; i < 10; i++)
        {
            reads.Add(persister.ProcessStream("theMessageId", $"theName{i}", connection, null,
                action: stream =>
                {
                    Interlocked.Increment(ref count);
                    var array = ToBytes(stream);
                    Assert.Equal(5, array[0]);
                    return Task.CompletedTask;
                }));
        }

        await Task.WhenAll(reads);
        Assert.Equal(10, count);
    }

    [Fact]
    public async Task ProcessStreams()
    {
        await using var connection = Connection.OpenConnection();
        await Installer.CreateTable(connection, "MessageAttachments");
        await persister.DeleteAllAttachments(connection, null);
        var count = 0;
        await persister.SaveStream(connection, null, "theMessageId", "theName1", defaultTestDate, GetStream(1), metadata);
        await persister.SaveStream(connection, null, "theMessageId", "theName2", defaultTestDate, GetStream(2), metadata);
        await persister.ProcessStreams("theMessageId", connection, null,
            action: stream =>
            {
                count++;
                var array = ToBytes(stream);
                if (count == 1)
                {
                    Assert.Equal(1, array[0]);
                    Assert.Equal("theName1", stream.Name);
                }

                if (count == 2)
                {
                    Assert.Equal(2, array[0]);
                    Assert.Equal("theName2", stream.Name);
                }

                return Task.CompletedTask;
            });
        Assert.Equal(2, count);
    }

    static byte[] ToBytes(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    [Fact]
    public async Task SaveStream()
    {
        await using var connection = Connection.OpenConnection();
        await Installer.CreateTable(connection, "MessageAttachments");
        await persister.DeleteAllAttachments(connection, null);
        await persister.SaveStream(connection, null, "theMessageId", "theName", defaultTestDate, GetStream(), metadata);
        var result = await persister.ReadAllInfo(connection, null);
        ObjectApprover.Verify(result);
    }

    [Fact]
    public async Task SaveBytes()
    {
        await using var connection = Connection.OpenConnection();
        await Installer.CreateTable(connection, "MessageAttachments");
        await persister.DeleteAllAttachments(connection, null);
        await persister.SaveBytes(connection, null, "theMessageId", "theName", defaultTestDate, new byte[] {1}, metadata);
        var result = await  persister.ReadAllInfo(connection, null);
        Assert.NotNull(result);
        ObjectApprover.Verify(result);
    }

    [Fact]
    public async Task SaveString()
    {
        await using var connection = Connection.OpenConnection();
        await Installer.CreateTable(connection, "MessageAttachments");
        await persister.DeleteAllAttachments(connection, null);
        await persister.SaveString(connection, null, "theMessageId", "theName", defaultTestDate, "foo", metadata);
        var result = await persister.ReadAllInfo(connection, null);
        Assert.NotNull(result);
        ObjectApprover.Verify(result);
    }

    [Fact]
    public async Task DuplicateAll()
    {
        await using var connection = Connection.OpenConnection();
        await Installer.CreateTable(connection, "MessageAttachments");
        await persister.DeleteAllAttachments(connection, null);
        await persister.SaveBytes(connection, null, "theSourceMessageId", "theName1", defaultTestDate, new byte[] {1}, metadata);
        await persister.SaveBytes(connection, null, "theSourceMessageId", "theName2", defaultTestDate, new byte[] {1}, metadata);
        await persister.Duplicate("theSourceMessageId", connection, null, "theTargetMessageId");
        var result = await persister.ReadAllInfo(connection, null);
        ObjectApprover.Verify(result);
    }

    [Fact]
    public async Task Duplicate()
    {
        await using var connection = Connection.OpenConnection();
        await Installer.CreateTable(connection, "MessageAttachments");
        await persister.DeleteAllAttachments(connection, null);
        await persister.SaveBytes(connection, null, "theSourceMessageId", "theName1", defaultTestDate, new byte[] {1}, metadata);
        await persister.SaveBytes(connection, null, "theSourceMessageId", "theName2", defaultTestDate, new byte[] {1}, metadata);
        await persister.Duplicate("theSourceMessageId", "theName1", connection, null, "theTargetMessageId");
        var result = await persister.ReadAllInfo(connection, null);
        ObjectApprover.Verify(result);
    }

    [Fact]
    public async Task DuplicateWithRename()
    {
        await using var connection = Connection.OpenConnection();
        await Installer.CreateTable(connection, "MessageAttachments");
        await persister.DeleteAllAttachments(connection, null);
        await persister.SaveBytes(connection, null, "theSourceMessageId", "theName1", defaultTestDate, new byte[] {1}, metadata);
        await persister.Duplicate("theSourceMessageId", "theName1", connection, null, "theTargetMessageId","theName2");
        var result = await persister.ReadAllInfo(connection, null);
        ObjectApprover.Verify(result);
    }

    [Fact]
    public async Task ReadAllMessageInfo()
    {
        await using var connection = Connection.OpenConnection();
        await Installer.CreateTable(connection, "MessageAttachments");
        await persister.DeleteAllAttachments(connection, null);
        await persister.SaveBytes(connection, null, "theMessageId", "theName1", defaultTestDate, new byte[] {1}, metadata);
        await persister.SaveBytes(connection, null, "theMessageId", "theName2", defaultTestDate, new byte[] {1}, metadata);
        var list = new List<AttachmentInfo>();
        await persister.ReadAllMessageInfo(connection, null, "theMessageId",
            info =>
            {
                list.Add(info);
                return Task.CompletedTask;
            });
        Assert.NotNull(list);
        ObjectApprover.Verify(list);
    }

    [Fact]
    public async Task CleanupItemsOlderThan()
    {
        await using var connection = Connection.OpenConnection();
        await Installer.CreateTable(connection, "MessageAttachments");
        await persister.DeleteAllAttachments(connection, null);
        await persister.SaveStream(connection, null, "theMessageId1", "theName", defaultTestDate, GetStream());
        await persister.SaveStream(connection, null, "theMessageId2", "theName", defaultTestDate.AddYears(2), GetStream());
        await persister.CleanupItemsOlderThan(connection, null, new DateTime(2001, 1, 1, 1, 1, 1));
        var result = await persister.ReadAllInfo(connection, null);
        Assert.NotNull(result);
        ObjectApprover.Verify(result);
    }

    static Stream GetStream(byte content = 5)
    {
        var stream = new MemoryStream();
        stream.WriteByte(content);
        stream.Position = 0;
        return stream;
    }
}