using System;
using System.Linq;
using Ac682.Hyperai.Clients.CQHTTP.ConcreteMessages;
using Hyperai.Messages;
using Hyperai.Messages.ConcreteModels;
using Hyperai.Messages.ConcreteModels.FileSources;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ac682.Hyperai.Clients.CQHTTP.Serialization
{
    public class MessageChainParser : IMessageChainParser
    {
        public MessageChain Parse(string text)
        {
            var builder = new MessageChainBuilder();
            var array = JsonConvert.DeserializeObject<JArray>(text, Shared.SerializerSettings);
            foreach (var jToken in array)
            {
                var obj = (JObject)jToken;
                var data = (JObject)obj["data"];
                MessageElement element = obj.Value<string>("type") switch
                {
                    "text" => new Plain(data!.Value<string>("text")),
                    "face" => new Face(data!.Value<int>("id")),
                    "image" => data!.Value<string>("type") == "flash"
                        ? new Flash(data!.Value<string>("file"), new UrlSource(new Uri(data.Value<string>("url") ?? $"http://gchat.qpic.cn/gchatpic_new/0/0-0-{data.Value<string>("file")!.Replace(".image", "").ToUpper()}/0?term=0", UriKind.Absolute)))
                        : new Image(data!.Value<string>("file"), new UrlSource(new Uri(data.Value<string>("url") ?? $"http://gchat.qpic.cn/gchatpic_new/0/0-0-{data.Value<string>("file")!.Replace(".image", "").ToUpper()}/0?term=0", UriKind.Absolute)))
                        ,
                    "at" => data!.Value<string>("qq") == "all"
                        ? new AtAll()
                        : new At(long.Parse(data!.Value<string>("qq") ?? string.Empty)),
                    "reply" => new Quote(long.Parse(data!.Value<string>("id") ?? string.Empty)),
                    "poke" => new Poke(GetPoke(data!.Value<string>("type"))),
                    "xml" => new XmlContent(data!.Value<string>("data")),
                    "json" => new JsonContent(data!.Value<string>("data")),
                    "music" => new Music(data.Value<string>("type") switch
                        {
                            "qq" => Music.MusicSource.QqMusic,
                            "163" => Music.MusicSource.Music163,
                            "xm" => Music.MusicSource.XiaMi
                        },data.Value<string>("id")),
                    "forward" => new ForwardFetch(data.Value<string>("id")),
                    "node" when data.ContainsKey("id") => new NodeFetch(long.Parse(data.Value<string>("id") ?? "0")), // try get the node by api
                    "node" when data.ContainsKey("uin") => new Node(data.Value<long>("uin"), data.Value<string>("name"), Parse(data["content"].ToString())),
                    "voice" => new Voice(new UrlSource(new Uri(data.Value<string>("url") ?? data.Value<string>("file"), UriKind.Absolute))),
                    "video" => new Video(new UrlSource(new Uri(data.Value<string>("url") ?? data.Value<string>("file"), UriKind.Absolute))),
                    
                    _ => new Unknown(obj.ToString())
                };
                builder.Add(element);
            }

            return builder.Build();
        }

        private PokeType GetPoke(string name)
        {
            if (Enum.TryParse(name, out PokeType result))
            {
                return result;
            }

            return PokeType.Poke;
        }
    }
}