﻿using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NServiceBus.Attachments.Sql
#if Raw
    .Raw
#endif
{
    public partial class Persister
    {
        /// <summary>
        /// Copies an attachment to <paramref name="target"/>.
        /// </summary>
        public virtual async Task CopyTo(string messageId, string name, DbConnection connection, DbTransaction? transaction, Stream target, CancellationToken cancellation = default)
        {
            Guard.AgainstNullOrEmpty(messageId, nameof(messageId));
            Guard.AgainstNullOrEmpty(name, nameof(name));
            Guard.AgainstLongAttachmentName(name);
            Guard.AgainstNull(connection, nameof(connection));
            Guard.AgainstNull(target, nameof(target));
            await using var command = CreateGetDataCommand(messageId, name, connection, transaction);
            await using var reader = await command.ExecuteSequentialReader(cancellation);
            if (!await reader.ReadAsync(cancellation))
            {
                throw ThrowNotFound(messageId, name);
            }

            await using var data = reader.GetStream(2);
            await data.CopyToAsync(target, 81920, cancellation);
        }
    }
}