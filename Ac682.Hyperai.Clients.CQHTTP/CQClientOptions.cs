namespace Ac682.Hyperai.Clients.CQHTTP
{
    public class CQClientOptions
    {
        public string Host { get; init; }
        public int HttpPort { get; init; }
        public int WebSocketPort { get; init; }
        public string AccessToken { get; init; }
    }
}