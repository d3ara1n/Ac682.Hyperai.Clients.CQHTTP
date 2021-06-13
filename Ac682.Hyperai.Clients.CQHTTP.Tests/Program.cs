using System;
using System.IO;
using Hyperai.Events;
using Hyperai.Messages;
using Hyperai.Messages.ConcreteModels;
using Hyperai.Messages.ConcreteModels.ImageSources;
using Hyperai.Relations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ac682.Hyperai.Clients.CQHTTP.Tests
{
    internal class Program
    {
        private static void Main(string[] cargs)
        {
            var options = new CQClientOptions
            {
                AccessToken = "NOACCESSTOKEN",
                Host = "qiv.dowob.vip",
                HttpPort = 6259,
                WebSocketPort = 6260
            };
            var client = new CQClient(options, NullLoggerFactory.Instance);
            client.Connect();
            client.On(new DefaultEventHandler<GroupMessageEventArgs>(client, (_, args) =>
            {
                Console.WriteLine($"{args.Group.Name}({args.Group.Identity})=>{args.Message}");
                args.Group.Identity = 594429092;
                client.SendAsync(args).Wait();
            }));
            client.Listen();
            client.Disconnect();
        }
    }
}