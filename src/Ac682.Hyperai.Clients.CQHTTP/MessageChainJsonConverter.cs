using System;
using System.Diagnostics.CodeAnalysis;
using Ac682.Hyperai.Clients.CQHTTP.Serialization;
using Hyperai.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ac682.Hyperai.Clients.CQHTTP
{
    public class MessageChainJsonConverter : JsonConverter<MessageChain>
    {
        private static readonly IMessageChainFormatter formatter = new MessageChainFormatter();
        private static readonly IMessageChainParser parser = new MessageChainParser();

        public override MessageChain ReadJson(JsonReader reader, Type objectType,
            [AllowNull] MessageChain existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var text = JArray.Load(reader).ToString();
            return parser.Parse(text);
        }

        public override void WriteJson(JsonWriter writer, [AllowNull] MessageChain value, JsonSerializer serializer)
        {
            var text = formatter.Format(value);
            writer.WriteRawValue(text);
        }
    }
}