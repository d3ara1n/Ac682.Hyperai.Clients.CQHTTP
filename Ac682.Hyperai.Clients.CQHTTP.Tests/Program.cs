using System;
using Hyperai.Events;
using Hyperai.Messages;
using Hyperai.Messages.ConcreteModels;
using Hyperai.Relations;

namespace Ac682.Hyperai.Clients.CQHTTP.Tests
{
    internal class Program
    {
        private static void Main(string[] cargs)
        {
            var options = new CQClientOptions
            {
                AccessToken = "NOACCESSTOKEN",
                Host = "192.168.1.110",
                HttpPort = 6259,
                WebSocketPort = 6260
            };
            var client = new CQClient(options);
            client.Connect();
            var builder = new MessageChainBuilder();
            builder.AddPoke(PokeType.SixSixSix);
            builder.AddPlain("我上线了!");
            builder.AddImage(new Uri("https://i.loli.net/2020/09/19/YsNtV3iEj6DUenp.jpg"));
            var chain = builder.Build();
            var evt = new GroupMessageEventArgs
            {
                Message = chain,
                Group = new Group {Identity = 594429092}
            };
            client.On(new DefaultEventHandler<GroupMessageEventArgs>(client, (_, args) =>
            {
                Console.WriteLine($"{args.Group.Name}({args.Group.Identity})=>{args.Message}");
                if (args.Group.Identity == 594429092) client.SendAsync(args).Wait();
            }));
            client.Listen();
            client.Disconnect();
        }
    }
}