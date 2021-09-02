using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ac682.Hyperai.Clients.CQHTTP
{
    public static class Shared
    {
        public static JsonSerializerSettings SerializerSettings { get; private set; }

        static Shared()
        {
            SerializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore
            };
            SerializerSettings.Converters.Add(new MessageChainJsonConverter());
        }
    }

}
