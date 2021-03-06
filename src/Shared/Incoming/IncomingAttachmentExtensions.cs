﻿using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NServiceBus.Attachments
#if FileShare
    .FileShare
#endif
#if Sql
    .Sql
#endif
#if Raw
    .Raw
#endif
{
    /// <summary>
    /// Extensions for <see cref="IMessageAttachments"/>.
    /// </summary>
    public static class IncomingAttachmentExtensions
    {
        /// <summary>
        /// Copies all attachments for the current message to <paramref name="directory"/>.
        /// </summary>
        public static Task CopyToDirectory(
            this IMessageAttachments attachments,
            string directory,
            string? nameForDefault = default,
            CancellationToken cancellation = default)
        {
            Guard.AgainstNullOrEmpty(directory, nameof(directory));
            Guard.AgainstEmpty(nameForDefault, nameof(nameForDefault));
            Directory.CreateDirectory(directory);

            return attachments.ProcessStreams(
                async stream =>
                {
                    var name = stream.Name;
                    if (name == "default" && nameForDefault != null)
                    {
                        name = nameForDefault;
                    }

                    name = name.TrimStart('\\', '/');
                    var file = Path.Combine(directory, name);
                    var fileDirectory = Path.GetDirectoryName(file)!;
                    Directory.CreateDirectory(fileDirectory);
                    File.Delete(file);
                    using var fileStream = File.Create(file);
                    await stream.CopyToAsync(fileStream, 4096, cancellation);
                },
                cancellation);
        }
    }
}