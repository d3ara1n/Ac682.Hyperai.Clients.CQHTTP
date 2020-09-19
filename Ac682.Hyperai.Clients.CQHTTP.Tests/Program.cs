using Hyperai.Events;
using Hyperai.Messages;
using Hyperai.Messages.ConcreteModels;
using Hyperai.Relations;
using System;

namespace Ac682.Hyperai.Clients.CQHTTP.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            CQClientOptions options = new CQClientOptions()
            {
                AccessToken = "NOACCESSTOKEN",
                Host = "192.168.1.110",
                HttpPort = 6259,
                WebSocketPort = 6260
            };
            CQClient client = new CQClient(options);
            client.Connect();
            MessageChainBuilder builder = new MessageChainBuilder();
            builder.AddPoke(PokeType.SixSixSix);
            builder.AddPlain("我上线了!");
            builder.AddImage(new Uri("https://i.loli.net/2020/09/19/YsNtV3iEj6DUenp.jpg"));
            MessageChain chain = builder.Build();
            GroupMessageEventArgs evt = new GroupMessageEventArgs()
            {
                Message = chain,
                Group = new Group() { Identity = 594429092 },
            };
            client.On(new DefaultEventHandler<GroupMessageEventArgs>(client, (_, args) =>
            {
                Console.WriteLine($"{args.Group.Name}({args.Group.Identity})=>{args.Message}");
                if(args.Group.Identity == 594429092)
                {
                    client.SendAsync(args).Wait();
                }
            }));
            client.Listen();
            client.Disconnect();
        }
    }
}
