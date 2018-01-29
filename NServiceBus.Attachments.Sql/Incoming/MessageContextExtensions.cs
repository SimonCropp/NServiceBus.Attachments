﻿using NServiceBus.Attachments;

namespace NServiceBus
{
    //todo: throw if attachments not enabled
    public static partial class MessageContextExtensions
    {
        public static IMessageAttachments IncomingAttachments(this IMessageHandlerContext context)
        {
            return IncomingAttachments(context, context.MessageId);
        }

        public static IMessageAttachment IncomingAttachment(this IMessageHandlerContext context)
        {
            if (context.Extensions.TryGet<IMessageAttachment>(out var attachment))
            {
                return attachment;
            }
            var incomingAttachments = context.IncomingAttachments();
            return new MessageAttachment(incomingAttachments);
        }

        public static IMessageAttachments AttachmentsForMessage(this IMessageHandlerContext context, string messageId)
        {
            return IncomingAttachments(context, messageId);
        }

        public static IMessageAttachment AttachmentForMessage(this IMessageHandlerContext context, string messageId)
        {
            var contextBag = context.Extensions;
            //TODO: how to mock multiple calls to diff messageId
            if (contextBag.TryGet<IMessageAttachment>(out var attachment))
            {
                return attachment;
            }
            var incomingAttachments = context.AttachmentsForMessage(messageId);
            return new MessageAttachment(incomingAttachments);
        }

        static IMessageAttachments IncomingAttachments(IMessageHandlerContext context, string messageId)
        {
            Guard.AgainstNull(context, "context");
            Guard.AgainstNullOrEmpty(messageId, "messageId");
            var contextBag = context.Extensions;
            if (contextBag.TryGet<IMessageAttachments>(out var attachments))
            {
                return attachments;
            }
            var state = contextBag.Get<AttachmentReceiveState>();
            return new MessageAttachments(state.ConnectionFactory, messageId, state.Persister);
        }
    }
}