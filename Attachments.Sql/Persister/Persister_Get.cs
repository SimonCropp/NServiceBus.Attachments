﻿using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace NServiceBus.Attachments.Sql
{
    public partial class Persister
    {
        /// <summary>
        /// Reads a byte array for an attachment.
        /// </summary>
        public virtual async Task<AttachmentBytes> GetBytes(string messageId, string name, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellation = default)
        {
            Guard.AgainstNullOrEmpty(messageId, nameof(messageId));
            Guard.AgainstNullOrEmpty(name, nameof(name));
            Guard.AgainstNull(connection, nameof(connection));
            using (var command = CreateGetDataCommand(messageId, name, connection, transaction))
            using (var reader = await command.ExecuteReaderAsync(cancellation).ConfigureAwait(false))
            {
                if (await reader.ReadAsync(cancellation).ConfigureAwait(false))
                {
                    var metadataString = reader.GetStringOrNull(1);
                    var metadata = MetadataSerializer.Deserialize(metadataString);
                    var bytes = (byte[]) reader[2];

                    return new AttachmentBytes(bytes, metadata);
                }
            }

            throw ThrowNotFound(messageId, name);
        }

        /// <summary>
        /// Returns an open stream pointing to an attachment.
        /// </summary>
        public virtual async Task<AttachmentStream> GetStream(string messageId, string name, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellation)
        {
            Guard.AgainstNullOrEmpty(messageId, nameof(messageId));
            Guard.AgainstNullOrEmpty(name, nameof(name));
            Guard.AgainstNull(connection, nameof(connection));
            SqlCommand command = null;
            SqlDataReader reader = null;
            try
            {
                command = CreateGetDataCommand(messageId, name, connection, transaction);
                reader = await command.ExecuteSequentialReader(cancellation).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellation).ConfigureAwait(false))
                {
                    reader.Dispose();
                    command.Dispose();
                    throw ThrowNotFound(messageId, name);
                }

                return InnerGetStream(reader, command);
            }
            catch (Exception)
            {
                reader?.Dispose();
                command?.Dispose();
                throw;
            }
        }

        static AttachmentStream InnerGetStream(SqlDataReader reader, SqlCommand command)
        {
            var length = reader.GetInt64(0);
            var metadataString = reader.GetStringOrNull(1);
            var sqlStream = reader.GetStream(2);
            var metadata = MetadataSerializer.Deserialize(metadataString);
            return new AttachmentStream(sqlStream, length, metadata, command, reader);
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
    Metadata,
    Data
from {fullTableName}
where
    NameLower = lower(@Name) and
    MessageIdLower = lower(@MessageId)";
            command.AddParameter("Name", name);
            command.AddParameter("MessageId", messageId);
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
    Metadata,
    Data
from {fullTableName}
where
    MessageIdLower = lower(@MessageId)";
            command.AddParameter("MessageId", messageId);
            return command;
        }
    }
}