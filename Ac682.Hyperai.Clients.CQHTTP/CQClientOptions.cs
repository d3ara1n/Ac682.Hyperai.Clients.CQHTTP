using System;
using System.Collections.Generic;
using System.Text;

namespace Ac682.Hyperai.Clients.CQHTTP
{
    public class CQClientOptions
    {
        public string Host { get; set; }
        public int HttpPort { get; set; }
        public int WebSocketPort { get; set; }
        public string AccessToken { get; set; }
    }
}
