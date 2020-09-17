using Hyperai.Messages;
using Hyperai.Messages.ConcreteModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Ac682.Hyperai.Clients.CQHTTP.Serialization
{
    public class MessageChainParser : IMessageChainParser
    {
        public MessageChain Parse(string text)
        {
            MessageChainBuilder builder = new MessageChainBuilder();
            JArray array = JsonConvert.DeserializeObject<JArray>(text);
            foreach(JObject obj in array)
            {
                JToken data = obj["data"];
                MessageComponent component = obj.Value<string>("type") switch
                {
                    "text" => new Plain(data.Value<string>("text")),
                    "face" => new Face(data.Value<int>("id")),
                    "image" => new Image(data.Value<string>("file"), new Uri(data.Value<string>("url"))),
                    "at" => data.Value<string>("at") == "all" ? (MessageComponent)new AtAll() : new At(long.Parse(data.Value<string>("at"))),
                    "reply" => new Quote(long.Parse(data.Value<string>("id"))),

                    _ => new Unknown(obj.ToString()),
                };
                builder.Add(component);
            }
            return builder.Build();
        }
    }
}
