using Ac682.Hyperai.Clients.CQHTTP.DataObjects;
using Hyperai.Events;
using Hyperai.Messages;
using Hyperai.Relations;
using Hyperai.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ac682.Hyperai.Clients.CQHTTP
{
    public class WebSocketSession : IDisposable
    {
        public ApiClientConnectionState State => client == null && client.State == WebSocketState.Open ? ApiClientConnectionState.Disconnected : ApiClientConnectionState.Connected;
        private readonly string _host;
        private readonly int _port;
        private readonly string _accessToken;

        private ClientWebSocket client;
        private JsonSerializerSettings serializerSettings;

        public WebSocketSession(string host, int port, string accessToken)
        {
            _host = host;
            _port = port;
            _accessToken = accessToken;

            serializerSettings = new JsonSerializerSettings()
            {
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore
            };
            serializerSettings.Converters.Add(new MessageChainJsonConverter());
        }

        public void Connect()
        {
            client = new ClientWebSocket();
            client.ConnectAsync(new Uri($"ws://{_host}:{_port}/?access_token={_accessToken}"), CancellationToken.None).Wait();
        }

        public void ReceiveEvents(Action<GenericEventArgs> callback)
        {
            ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(1024, 1024);
            while (State == ApiClientConnectionState.Connected)
            {
                WebSocketReceiveResult result;
                using (var ms = new MemoryStream())
                {
                    do
                    {
                        result = client.ReceiveAsync(buffer, CancellationToken.None).GetAwaiter().GetResult();
                        ms.Write(buffer.Array, buffer.Offset, result.Count);
                    }
                    while (!result.EndOfMessage);

                    ms.Seek(0, SeekOrigin.Begin);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        using (var reader = new StreamReader(ms, Encoding.UTF8))
                        {
                            string text = reader.ReadToEnd();
                            GenericEventArgs args = ParseEvent(text);
                            if (args != null)
                                callback(args);
                        }
                    }
                }
            }
        }

        private GenericEventArgs ParseEvent(string json)
        {
            JObject dick = JsonConvert.DeserializeObject<JObject>(json);
            string postType = dick.Value<string>("post_type");

            switch (postType)
            {
                case "meta_event":
                    return null;
                case "message":
                    switch (dick.Value<string>("message_type"))
                    {
                        case "group":
                            {
                                DtoGroupMessage message = JsonConvert.DeserializeObject<DtoGroupMessage>(json, serializerSettings);
                                GroupMessageEventArgs args = new GroupMessageEventArgs()
                                {
                                    Message = message.Message,
                                    Time = DateTime.Now,
                                };
                                args.Group = new Group()
                                {
                                    Identity = message.GroupId,
                                };
                                args.User = message.Sender.ToMember(args.Group);
                                return args;
                            }
                        case "private":
                            {
                                DtoFriendMessage message = JsonConvert.DeserializeObject<DtoFriendMessage>(json, serializerSettings);
                                FriendMessageEventArgs args = new FriendMessageEventArgs()
                                {
                                    Message = message.Message,
                                    Time = DateTime.Now
                                };
                                args.User = message.Sender.ToFriend();
                                return args;
                            }
                        default:
                            return null;
                    }
                default:
                    // discard
                    return null;
            }
        }

        public async Task SendFriendMessageAsync(Friend friend, MessageChain message)
        {
            await SendRawAsync("send_private_msg", new { user_id = friend.Identity, message = message });
        }

        public async Task SendGroupMessageAsync(Group group, MessageChain message)
        {
            await SendRawAsync("send_group_msg", new { group_id = group.Identity, message = message });
        }

        public async Task SendRawAsync(string action, object body)
        {
            GenericRequest<object> req = new GenericRequest<object>()
            {
                Action = action,
                Echo = Guid.NewGuid().ToString(),
                Params = body
            };

            ArraySegment<byte> buffer = WebSocket.CreateClientBuffer(1024, 1024);
            string text = JsonConvert.SerializeObject(req, serializerSettings);
            await client.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public void Disconnect()
        {
            client.CloseAsync(WebSocketCloseStatus.Empty, "dead.", CancellationToken.None).Wait();
        }

        bool isDisposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing && !isDisposed)
            {
                if (State == ApiClientConnectionState.Connected)
                {
                    Disconnect();
                }
            }
        }
    }
}
