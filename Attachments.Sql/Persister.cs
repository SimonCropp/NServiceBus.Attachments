﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NServiceBus.Attachments.Sql
{
    /// <summary>
    /// Raw access to manipulating attachments outside of the context of the NServiceBus pipeline.
    /// </summary>
    public class Persister
    {
        string fullTableName;

        /// <summary>
        /// Instatiate a new instance of <see cref="Persister"/>.
        /// </summary>
        public Persister(string schema, string tableName)
        {
            Guard.AgainstNullOrEmpty(schema, nameof(schema));
            Guard.AgainstNullOrEmpty(tableName, nameof(tableName));
            fullTableName = $"[{schema}].[{tableName}]";
        }

        /// <summary>
        /// Saves <paramref name="stream"/> as an attachment.
        /// </summary>
        /// <exception cref="TaskCanceledException">If <paramref name="cancellation"/> is <see cref="CancellationToken.IsCancellationRequested"/>.</exception>
        public Task SaveStream(SqlConnection connection, SqlTransaction transaction, string messageId, string name, DateTime expiry, Stream stream, CancellationToken cancellation = default)
        {
            Guard.AgainstNull(connection, nameof(connection));
            Guard.AgainstNullOrEmpty(messageId, nameof(messageId));
            Guard.AgainstNullOrEmpty(name, nameof(name));
            Guard.AgainstNull(stream, nameof(stream));
            return Save(connection, transaction, messageId, name, expiry, stream, cancellation);
        }

        /// <summary>
        /// Saves <paramref name="bytes"/> as an attachment.
        /// </summary>
        /// <exception cref="TaskCanceledException">If <paramref name="cancellation"/> is <see cref="CancellationToken.IsCancellationRequested"/>.</exception>
        public Task SaveBytes(SqlConnection connection, SqlTransaction transaction, string messageId, string name, DateTime expiry, byte[] bytes, CancellationToken cancellation = default)
        {
            Guard.AgainstNull(connection, nameof(connection));
            Guard.AgainstNullOrEmpty(messageId, nameof(messageId));
            Guard.AgainstNullOrEmpty(name, nameof(name));
            Guard.AgainstNull(bytes, nameof(bytes));
            return Save(connection, transaction, messageId, name, expiry, bytes, cancellation);
        }

        async Task Save(SqlConnection connection, SqlTransaction transaction, string messageId, string name, DateTime expiry, object stream, CancellationToken cancellation = default)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = $@"
insert into {fullTableName}
(
    MessageId,
    Name,
    Expiry,
    Data
)
values
(
    @MessageId,
    @Name,
    @Expiry,
    @Data
)";
                var parameters = command.Parameters;
                parameters.Add("@MessageId", SqlDbType.NVarChar).Value = messageId;
                parameters.Add("@Name", SqlDbType.NVarChar).Value = name;
                parameters.Add("@Expiry", SqlDbType.DateTime2).Value = expiry;
                parameters.Add("@Data", SqlDbType.Binary, -1).Value = stream;

                // Send the data to the server asynchronously
                await command.ExecuteNonQueryAsync(cancellation).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Reads the <see cref="AttachmentMetadata"/> for all attachments.
        /// </summary>
        public IEnumerable<AttachmentMetadata> ReadAllMetadata(SqlConnection connection, SqlTransaction transaction)
        {
            Guard.AgainstNull(connection, nameof(connection));
            using (var command = GetReadMetadataCommand(connection, transaction))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    yield return new AttachmentMetadata(
                        messageId: reader.GetString(1),
                        name: reader.GetString(2),
                        expiry: reader.GetDateTime(3));
                }
            }
        }

        SqlCommand GetReadMetadataCommand(SqlConnection connection, SqlTransaction transaction)
        {
            var command = connection.CreateCommand();
            if (transaction != null)
            {
                command.Transaction = transaction;
            }

            command.CommandText = $@"
select
    Id,
    MessageId,
    Name,
    Expiry
from {fullTableName}";
            return command;
        }

        /// <summary>
        /// Deletes all attachments.
        /// </summary>
        public async Task DeleteAllAttachements(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellation = default)
        {
            Guard.AgainstNull(connection, nameof(connection));
            using (var command = connection.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Transaction = transaction;
                }

                command.CommandText = $@"delete from {fullTableName}";
                await command.ExecuteNonQueryAsync(cancellation).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Deletes attachments older than <paramref name="dateTime"/>.
        /// </summary>
        public async Task CleanupItemsOlderThan(SqlConnection connection, SqlTransaction transaction, DateTime dateTime, CancellationToken cancellation = default)
        {
            Guard.AgainstNull(connection, nameof(connection));
            using (var command = connection.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Transaction = transaction;
                }

                command.CommandText = $@"delete from {fullTableName} where expiry < @date";
                command.Parameters.AddWithValue("date", dateTime);
                await command.ExecuteNonQueryAsync(cancellation).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Copies an attachment to <paramref name="target"/>.
        /// </summary>
        public async Task CopyTo(string messageId, string name, SqlConnection connection, SqlTransaction transaction, Stream target, CancellationToken cancellation = default)
        {
            Guard.AgainstNullOrEmpty(messageId, nameof(messageId));
            Guard.AgainstNullOrEmpty(name, nameof(name));
            Guard.AgainstNull(connection, nameof(connection));
            Guard.AgainstNull(target, nameof(target));
            using (var command = CreateGetDataCommand(messageId, name, connection, transaction))
            using (var reader = await ExecuteSequentialReader(command, cancellation).ConfigureAwait(false))
            {
                if (!await reader.ReadAsync(cancellation).ConfigureAwait(false))
                {
                    throw ThrowNotFound(messageId, name);
                }

                using (var data = reader.GetStream(1))
                {
                    await data.CopyToAsync(target, 81920, cancellation).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Reads a byte array for an attachment.
        /// </summary>
        public async Task<byte[]> GetBytes(string messageId, string name, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellation = default)
        {
            Guard.AgainstNullOrEmpty(messageId, nameof(messageId));
            Guard.AgainstNullOrEmpty(name, nameof(name));
            Guard.AgainstNull(connection, nameof(connection));
            using (var command = CreateGetDataCommand(messageId, name, connection, transaction))
            using (var reader = await command.ExecuteReaderAsync(cancellation).ConfigureAwait(false))
            {
                if (await reader.ReadAsync(cancellation).ConfigureAwait(false))
                {
                    return (byte[]) reader[1];
                }
            }

            throw ThrowNotFound(messageId, name);
        }

        /// <summary>
        /// Returns an open stream pointing to an attachment.
        /// </summary>
        public async Task<Stream> GetStream(string messageId, string name, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellation)
        {
            Guard.AgainstNullOrEmpty(messageId, nameof(messageId));
            Guard.AgainstNullOrEmpty(name, nameof(name));
            Guard.AgainstNull(connection, nameof(connection));
            SqlCommand command = null;
            SqlDataReader reader = null;
            try
            {
                command = CreateGetDataCommand(messageId, name, connection, transaction);
                reader = await ExecuteSequentialReader(command, cancellation).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellation).ConfigureAwait(false))
                {
                    var length = reader.GetInt64(0);
                    var sqlStream = reader.GetStream(1);
                    return new StreamWrapper(sqlStream, length, command, reader);
                }
            }
            catch (Exception)
            {
                reader?.Dispose();
                command?.Dispose();
                throw;
            }

            reader.Dispose();
            command.Dispose();
            throw ThrowNotFound(messageId, name);
        }

        /// <summary>
        /// Processes all attachments for <paramref name="messageId"/> by passing them to <paramref name="action"/>.
        /// </summary>
        public async Task ProcessStreams(string messageId, SqlConnection connection, SqlTransaction transaction, Func<string, Stream, Task> action, CancellationToken cancellation = default)
        {
            Guard.AgainstNullOrEmpty(messageId, nameof(messageId));
            Guard.AgainstNull(connection, nameof(connection));
            Guard.AgainstNull(action, nameof(action));
            using (var command = CreateGetDatasCommand(messageId, connection, transaction))
            using (var reader = await ExecuteSequentialReader(command, cancellation).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellation).ConfigureAwait(false))
                {
                    cancellation.ThrowIfCancellationRequested();
                    var name = reader.GetString(0);
                    var length = reader.GetInt64(1);
                    using (var sqlStream = reader.GetStream(2))
                    {
                        var task = action(name, new StreamWrapper(sqlStream, length));
                        Guard.ThrowIfNullReturned(messageId,null, task);
                        await task.ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// Processes an attachment by passing it to <paramref name="action"/>.
        /// </summary>
        public async Task ProcessStream(string messageId, string name, SqlConnection connection, SqlTransaction transaction, Func<Stream, Task> action, CancellationToken cancellation = default)
        {
            Guard.AgainstNullOrEmpty(messageId, nameof(messageId));
            Guard.AgainstNullOrEmpty(name, nameof(name));
            Guard.AgainstNull(connection, nameof(connection));
            Guard.AgainstNull(action, nameof(action));
            using (var command = CreateGetDataCommand(messageId, name, connection, transaction))
            using (var reader = await ExecuteSequentialReader(command, cancellation).ConfigureAwait(false))
            {
                if (!await reader.ReadAsync(cancellation).ConfigureAwait(false))
                {
                    throw ThrowNotFound(messageId, name);
                }

                var length = reader.GetInt64(0);
                using (var sqlStream = reader.GetStream(1))
                {
                    var task = action(new StreamWrapper(sqlStream, length));
                    Guard.ThrowIfNullReturned(messageId, name, task);
                    await task.ConfigureAwait(false);
                }
            }
        }

        static Exception ThrowNotFound(string messageId, string name)
        {
            return new Exception($"Could not find attachment. MessageId:{messageId}, Name:{name}");
        }

        // The reader needs to be executed with SequentialAccess to enable network streaming
        // Otherwise ReadAsync will buffer the entire BLOB in memory which can cause scalability issues or OutOfMemoryExceptions
        static Task<SqlDataReader> ExecuteSequentialReader(SqlCommand command, CancellationToken cancellation = default)
        {
            return command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellation);
        }

        SqlCommand CreateGetDataCommand(string messageId, string name, SqlConnection connection, SqlTransaction transaction)
        {
            var command = connection.CreateCommand();
            if (transaction != null)
            {
                command.Transaction = transaction;
            }

            command.CommandText = $@"
select
    datalength(Data),
    Data
from {fullTableName}
where
    NameLower = lower(@Name) and
    MessageIdLower = lower(@MessageId)";
            var parameters = command.Parameters;
            parameters.AddWithValue("Name", name);
            parameters.AddWithValue("MessageId", messageId);
            return command;
        }

        SqlCommand CreateGetDatasCommand(string messageId, SqlConnection connection, SqlTransaction transaction)
        {
            var command = connection.CreateCommand();
            if (transaction != null)
            {
                command.Transaction = transaction;
            }

            command.CommandText = $@"
select
    Name,
    datalength(Data),
    Data
from {fullTableName}
where
    MessageIdLower = lower(@MessageId)";
            var parameters = command.Parameters;
            parameters.AddWithValue("MessageId", messageId);
            return command;
        }
    }
}