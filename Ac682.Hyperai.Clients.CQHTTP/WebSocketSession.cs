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
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Ext = Ac682.Hyperai.Clients.CQHTTP.DataObjects.Extensions;
using System.Threading.Tasks;
using Wupoo;
using Hyperai.Messages.ConcreteModels;

namespace Ac682.Hyperai.Clients.CQHTTP
{
    public class WebSocketSession : IDisposable
    {
        public ApiClientConnectionState State => client == null && client.State == WebSocketState.Open ? ApiClientConnectionState.Disconnected : ApiClientConnectionState.Connected;
        private readonly string _host;
        private readonly int _httpPort;
        private readonly int _websocketPort;
        private readonly string _accessToken;

        private ClientWebSocket client;
        private JsonSerializerSettings serializerSettings;
        private WapooOptions wapooOptions;

        public WebSocketSession(string host, int httpPort, int websocketPort, string accessToken)
        {
            _host = host;
            _httpPort = httpPort;
            _websocketPort = websocketPort;
            _accessToken = accessToken;

            serializerSettings = new JsonSerializerSettings()
            {
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore
            };
            serializerSettings.Converters.Add(new MessageChainJsonConverter());

            wapooOptions = new WapooOptions()
            {
                IgnoreMediaTypeCheck = true,
                Authentication = new AuthenticationHeaderValue("Bearer", _accessToken),
                JsonSerializerOptions = serializerSettings,
            };
        }

        public void Connect()
        {
            client = new ClientWebSocket();
            client.ConnectAsync(new Uri($"ws://{_host}:{_websocketPort}/event?access_token={_accessToken}"), CancellationToken.None).Wait();
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
                                    Message = new MessageChain(message.Message.Append(new Source(message.Message_Id))),
                                    Time = DateTime.Now,
                                };

                                args.Group = GetGroupInfoAsync(dick.Value<long>("group_id")).GetAwaiter().GetResult();
                                args.User = message.Sender.ToMember(args.Group);
                                return args;
                            }
                        case "private":
                            {
                                DtoFriendMessage message = JsonConvert.DeserializeObject<DtoFriendMessage>(json, serializerSettings);
                                FriendMessageEventArgs args = new FriendMessageEventArgs()
                                {
                                    Message = new MessageChain(message.Message.Append(new Source(message.Message_Id))),
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

        public async Task<long> SendFriendMessageAsync(Friend friend, MessageChain message)
        {
            long messageId = -1;
            await Request($"send_private_msg")
                .WithJsonBody(new
                {
                    user_id = friend.Identity,
                    message = message.AsReadable()
                })
                .ForJsonResult<JObject>((obj) => messageId = obj["data"].Value<long>("message_id"))
                .FetchAsync();
            return messageId;
        }

        public async Task<long> SendGroupMessageAsync(Group group, MessageChain message)
        {
            long messageId = -1;
            await Request("send_group_msg")
                .WithJsonBody(new
                {
                    group_id = group.Identity,
                    message = message.AsReadable()
                })
                .ForJsonResult<JObject>(obj => messageId = obj["data"].Value<long>("message_id"))
                .FetchAsync();
            return messageId;
        }

        private async Task<Group> GetGroupInfoAsync(long id)
        {
            Group result = new Group() { Identity = id };
            Task task1 = Request("get_group_info")
                .WithJsonBody(new
                {
                    group_id = id,
                })
                .ForJsonResult<JObject>(obj =>
                {
                    result.Name = obj["data"].Value<string>("group_name");
                })
                .FetchAsync();
            Task<IEnumerable<Member>> task2 = GetGroupMembersAsync(result);
            await task1;
            result.Members = new Lazy<IEnumerable<Member>>(await task2);
            return result;
        }

        private async Task<IEnumerable<Member>> GetGroupMembersAsync(Group group)
        {
            List<Member> members = new List<Member>();
            await Request("get_group_member_list")
                .WithJsonBody(new
                {
                    group_id = group.Identity
                })
                .ForJsonResult<JArray>(arr =>
                {
                    foreach (JObject obj in arr)
                    {
                        Member member = new Member()
                        {
                            Group = new Lazy<Group>(group),
                            Identity = obj.Value<long>("user_id"),
                            DisplayName = obj.Value<string>("card"),
                            Nickname = obj.Value<string>("nickname"),
                            Title = obj.Value<string>("title"),
                            Role = Ext.OfRole(obj.Value<string>("role")),
                        };
                        members.Add(member);
                    }
                })
                .FetchAsync();

            return members;
        }



        private Wapoo Request(string action)
        {
            return new Wapoo(wapooOptions, $"http://{_host}:{_httpPort}/{action}").ViaPost();
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
