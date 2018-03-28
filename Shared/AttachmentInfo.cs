﻿using System;

namespace NServiceBus.Attachments
#if FileShare
    .FileShare
#elif Sql
.Sql
#endif
{
    /// <summary>
    /// Information about an attachment.
    /// </summary>
    public class AttachmentInfo
    {
        /// <summary>
        /// The id of the message that the attachment was written for.
        /// </summary>
        public readonly string MessageId;
        /// <summary>
        /// The name of the attachment.
        /// </summary>
        public readonly string Name;
        /// <summary>
        /// the attachment expiry. The date after which it can be cleaned-up.
        /// </summary>
        public readonly DateTime Expiry;

        /// <summary>
        /// Initializes a new instance of <see cref="AttachmentInfo"/>.
        /// </summary>
        public AttachmentInfo(string messageId, string name, DateTime expiry)
        {
            Guard.AgainstNullOrEmpty(messageId,nameof(messageId));
            Guard.AgainstNullOrEmpty(name, nameof(name));
            MessageId = messageId;
            Name = name;
            Expiry = expiry;
        }
    }
}