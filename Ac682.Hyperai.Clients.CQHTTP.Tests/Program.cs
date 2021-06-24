using System;
using System.IO;
using System.Linq;
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
            client.On(new DefaultEventHandler<GroupMessageEventArgs>(client, (c, args) =>
            {
                Console.WriteLine($"{args.Group.Name}({args.Group.Identity})=>{args.Message}");
                args.Group.Identity = 594429092;
                c.SendAsync(args).Wait();
                string msg = (args.Message.FirstOrDefault(x => x is Plain) as Plain)?.Text;
                Console.WriteLine(msg);
                if (msg == "disconnect")
                {
                    c.Disconnect();
                }
            }));
            client.Listen();
            client.Dispose();
        }
    }
}