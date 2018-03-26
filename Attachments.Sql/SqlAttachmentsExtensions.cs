﻿using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using NServiceBus.Attachments.Sql;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Settings;

namespace NServiceBus
{
    /// <summary>
    /// Extensions to control what messages are audited.
    /// </summary>
    public static class SqlAttachmentsExtensions
    {
        /// <summary>
        /// Enable SQL attachments for this endpoint.
        /// </summary>
        public static AttachmentSettings EnableAttachments(
            this EndpointConfiguration configuration,
            Func<Task<SqlConnection>> connectionFactory,
            GetTimeToKeep timeToKeep)
        {
            Guard.AgainstNull(configuration, nameof(configuration));
            Guard.AgainstNull(timeToKeep, nameof(timeToKeep));
            Guard.AgainstNull(connectionFactory, nameof(connectionFactory));
            var settings = configuration.GetSettings();
            var attachments = new AttachmentSettings(connectionFactory, timeToKeep);
            return SetAttachments(configuration, settings, attachments);
        }

        /// <summary>
        /// Enable SQL attachments for this endpoint.
        /// </summary>
        public static AttachmentSettings EnableAttachments(
            this EndpointConfiguration configuration,
            string connection,
            GetTimeToKeep timeToKeep)
        {
            Guard.AgainstNull(configuration, nameof(configuration));
            Guard.AgainstNull(timeToKeep, nameof(timeToKeep));
            Guard.AgainstNullOrEmpty(connection, nameof(connection));
            var settings = configuration.GetSettings();
            var attachments = new AttachmentSettings(connection, timeToKeep);
            return SetAttachments(configuration, settings, attachments);
        }

        static AttachmentSettings SetAttachments(EndpointConfiguration configuration, SettingsHolder settings, AttachmentSettings attachments)
        {
            settings.Set<AttachmentSettings>(attachments);
            configuration.EnableFeature<AttachmentFeature>();
            configuration.DisableFeature<AttachmentsUsedWhenNotEnabledFeature>();
            return attachments;
        }
    }
}