using System;
using System.Linq;
using Hyperai.Messages;
using Hyperai.Messages.ConcreteModels;
using Hyperai.Messages.ConcreteModels.ImageSources;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ac682.Hyperai.Clients.CQHTTP.Serialization
{
    public class MessageChainParser : IMessageChainParser
    {
        public MessageChain Parse(string text)
        {
            var builder = new MessageChainBuilder();
            var array = JsonConvert.DeserializeObject<JArray>(text);
            foreach (var jToken in array)
            {
                var obj = (JObject) jToken;
                var data = obj["data"];
                MessageElement element = obj.Value<string>("type") switch
                {
                    "text" => new Plain(data!.Value<string>("text")),
                    "face" => new Face(data!.Value<int>("id")),
                    "image" => (data!.Value<string>("type") == "flash" 
                        ? new Flash(data!.Value<string>("file"), new UrlSource(new Uri(data.Value<string>("url") ?? $"http://gchat.qpic.cn/gchatpic_new/0/0-0-{data.Value<string>("file")!.Replace(".image","").ToUpper()}/0?term=0", UriKind.Absolute)))
                        : new Image(data!.Value<string>("file"), new UrlSource(new Uri(data.Value<string>("url")?? $"http://gchat.qpic.cn/gchatpic_new/0/0-0-{data.Value<string>("file")!.Replace(".image","").ToUpper()}/0?term=0", UriKind.Absolute)))
                        ),
                    "at" => data!.Value<string>("qq") == "all"
                        ? new AtAll()
                        : new At(long.Parse(data!.Value<string>("qq") ?? string.Empty)),
                    "reply" => new Quote(long.Parse(data!.Value<string>("id") ?? string.Empty)),
                    "poke" => new Poke(GetPoke(data!.Value<string>("type"))),

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