﻿using Newtonsoft.Json;

namespace Ac682.Hyperai.Clients.CQHTTP
{
    public class GenericRequest<TParams>
    {
        [JsonProperty("action")] public string Action { get; set; }

        [JsonProperty("params")] public TParams Params { get; set; }

        [JsonProperty("echo")] public string Echo { get; set; }
    }
}