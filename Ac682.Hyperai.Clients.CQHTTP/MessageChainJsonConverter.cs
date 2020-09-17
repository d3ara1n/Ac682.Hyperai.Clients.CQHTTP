using Ac682.Hyperai.Clients.CQHTTP.Serialization;
using Hyperai.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Ac682.Hyperai.Clients.CQHTTP
{
    public class MessageChainJsonConverter : JsonConverter<MessageChain>
    {
        private readonly static IMessageChainFormatter formatter = new MessageChainFormatter();
        private readonly static IMessageChainParser parser = new MessageChainParser();
        public override MessageChain ReadJson(JsonReader reader, Type objectType, [AllowNull] MessageChain existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string text = JArray.Load(reader).ToString();
            return parser.Parse(text);
        }

        public override void WriteJson(JsonWriter writer, [AllowNull] MessageChain value, JsonSerializer serializer)
        {
            string text = formatter.Format(value);
            writer.WriteRaw(text);
        }
    }
}
