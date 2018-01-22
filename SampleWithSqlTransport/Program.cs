﻿using System;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Attachments;
using NServiceBus.Features;

class Program
{
    static async Task Main()
    {
        var configuration = new EndpointConfiguration("AttachmentsSample");
        configuration.UsePersistence<LearningPersistence>();
        configuration.DisableFeature<TimeoutManager>();
        configuration.DisableFeature<MessageDrivenSubscriptions>();
        configuration.EnableInstallers();
        var transport = configuration.UseTransport<SqlServerTransport>();
        transport.Transactions(TransportTransactionMode.ReceiveOnly);
        transport.ConnectionString(@"Data Source=.\SQLExpress;Database=NServiceBusAttachments; Integrated Security=True;Max Pool Size=100");
        configuration.AuditProcessedMessagesTo("audit");
        var attachments = configuration.EnableAttachments();
        attachments.UseSqlTransportContext();
        var endpoint = await Endpoint.Start(configuration);
        await SendMessage(endpoint);
        Console.WriteLine("Press any key to stop program");
        Console.Read();
        await endpoint.Stop();
    }

    static SqlConnection BuildSqlConnection()
    {
        return new SqlConnection(@"Data Source=.\SQLExpress;Database=NServiceBusAttachments; Integrated Security=True;Max Pool Size=100");
    }
    static async Task SendMessage(IEndpointInstance endpoint)
    {
        var sendOptions = new SendOptions();
        sendOptions.RouteToThisEndpoint();
        var attachments = sendOptions.OutgoingAttachments();
        attachments.Add(
            name: "foo",
            stream: () =>
            {
                var stream = new MemoryStream();
                var streamWriter = new StreamWriter(stream);
                streamWriter.Write("sdflgkndkjfgn");
                return stream;
            },
            timeToKeep: before => TimeSpan.FromDays(1));

        await endpoint.Send(new MyMessage(), sendOptions);
    }
}