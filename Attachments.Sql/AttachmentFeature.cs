﻿using System;
using NServiceBus;
using NServiceBus.Features;
using NServiceBus.ObjectBuilder;
using NServiceBus.Attachments.Sql;

class AttachmentFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        var settings = context.Settings.Get<AttachmentSettings>();

        var connectionFactory = settings.ConnectionFactory;
        var pipeline = context.Pipeline;
        var persister = new Persister(settings.Schema, settings.TableName, false);
        pipeline.Register(new ReceiveRegistration(connectionFactory, persister));
        pipeline.Register(new SendRegistration(connectionFactory, persister, settings.TimeToKeep, settings.UseMars));
        if (settings.RunCleanTask)
        {
            context.RegisterStartupTask(builder => CreateCleaner(settings, persister, builder));
        }
    }

    static Cleaner CreateCleaner(AttachmentSettings settings, IPersister persister, IBuilder builder)
    {
        return new Cleaner(async token =>
            {
                using (var connection = await settings.ConnectionFactory().ConfigureAwait(false))
                {
                    await persister.CleanupItemsOlderThan(connection, null, DateTime.UtcNow, token).ConfigureAwait(false);
                }
            },
            criticalError: builder.Build<CriticalError>().Raise,
            frequencyToRunCleanup: TimeSpan.FromHours(1),
            timer: new AsyncTimer());
    }
}