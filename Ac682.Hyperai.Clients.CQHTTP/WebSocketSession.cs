using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ac682.Hyperai.Clients.CQHTTP.DataObjects;
using Ac682.Hyperai.Clients.CQHTTP.Serialization;
using Hyperai.Events;
using Hyperai.Messages;
using Hyperai.Messages.ConcreteModels;
using Hyperai.Messages.ConcreteModels.ImageSources;
using Hyperai.Relations;
using Hyperai.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Wupoo;
using Ext = Ac682.Hyperai.Clients.CQHTTP.DataObjects.Extensions;

namespace Ac682.Hyperai.Clients.CQHTTP
{
    public sealed class WebSocketSession : IDisposable
    {
        private readonly string _accessToken;
        private readonly string _host;
        private readonly int _httpPort;
        private readonly int _websocketPort;
        private readonly IMessageChainParser parser = new MessageChainParser();
        private readonly JsonSerializerSettings serializerSettings;
        private readonly WapooOptions wapooOptions;

        private ClientWebSocket client;

        private readonly bool isDisposed = false;

        public WebSocketSession(string host, int httpPort, int websocketPort, string accessToken)
        {
            _host = host;
            _httpPort = httpPort;
            _websocketPort = websocketPort;
            _accessToken = accessToken;

            serializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore
            };
            serializerSettings.Converters.Add(new MessageChainJsonConverter());

            wapooOptions = new WapooOptions
            {
                IgnoreMediaTypeCheck = true,
                Authentication = new AuthenticationHeaderValue("Bearer", _accessToken),
                JsonSerializerOptions = serializerSettings
            };
        }

        public ApiClientConnectionState State => client == null && client!.State == WebSocketState.Open
            ? ApiClientConnectionState.Disconnected
            : ApiClientConnectionState.Connected;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Connect()
        {
            client = new ClientWebSocket();
            client.ConnectAsync(new Uri($"ws://{_host}:{_websocketPort}/event?access_token={_accessToken}"),
                CancellationToken.None).Wait();
        }

        public void ReceiveEvents(Action<GenericEventArgs> callback)
        {
            var buffer = WebSocket.CreateClientBuffer(1024, 1024);
            while (State == ApiClientConnectionState.Connected)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = client.ReceiveAsync(buffer, CancellationToken.None).GetAwaiter().GetResult();
                    ms.Write(buffer.Array, buffer.Offset, result.Count);
                } while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);

                if (result.MessageType != WebSocketMessageType.Text) continue;
                using var reader = new StreamReader(ms, Encoding.UTF8);
                var text = reader.ReadToEnd();
                var args = ParseEvent(text);
                if (args != null)
                    callback(args);
            }
        }

        private GenericEventArgs ParseEvent(string json)
        {
            var dick = JsonConvert.DeserializeObject<JObject>(json);
            var postType = dick.Value<string>("post_type");

            switch (postType)
            {
                case "meta_event":
                    return null;
                case "message":
                    switch (dick.Value<string>("message_type"))
                    {
                        case "group":
                        {
                            var message = JsonConvert.DeserializeObject<DtoGroupMessage>(json, serializerSettings);
                            var args = new GroupMessageEventArgs
                            {
                                Message = new MessageChain(message!.Message.Append(new Source(message.Message_Id))),
                                Time = DateTime.Now,
                                Group = GetGroupInfoAsync(dick.Value<long>("group_id")).GetAwaiter().GetResult()
                            };

                            args.User = message.Sender.ToMember(args.Group);
                            return args;
                        }
                        case "private":
                        {
                            var message = JsonConvert.DeserializeObject<DtoFriendMessage>(json, serializerSettings);
                            var args = new FriendMessageEventArgs
                            {
                                Message = new MessageChain(message!.Message.Append(new Source(message!.Message_Id))),
                                Time = DateTime.Now,
                                User = message!.Sender.ToFriend()
                            };
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
            await Request("send_private_msg")
                .WithJsonBody(new
                {
                    user_id = friend.Identity,
                    message = message.AsReadable()
                })
                .ForJsonResult<JObject>(obj => messageId = obj["data"].Value<long>("message_id"))
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

        public async Task RecallMessageAsync(long messageId)
        {
            await Request("delete_msg")
                .WithJsonBody(new {message_id = messageId})
                .FetchAsync();
        }

        public MessageChain PreprocessMessageChainBeforeSending(MessageChain chain)
        {
            var passes = chain.Where(x => (x is not ImageBase)||x is ImageBase image && image.Source is UrlSource);
            if(passes.Count() == chain.Count)
            {
                return chain;
            }
            else
            {
                return new MessageChain(passes);
            }
        }

        public async Task<Self> GetSelfInfoAsync()
        {
            var self = new Self();
            await Request("get_login_info")
                .ForJsonResult<JObject>(obj =>
                {
                    self.Identity = obj["data"].Value<long>("user_id");
                    self.Nickname = obj["data"].Value<string>("nickname");
                })
                .FetchAsync();
            self.Groups = new Lazy<IEnumerable<Group>>(() => GetGroupsAsync().GetAwaiter().GetResult());
            self.Friends = new Lazy<IEnumerable<Friend>>(() => GetFriendsAsync().GetAwaiter().GetResult());

            return self;
        }

        public async Task<IEnumerable<Friend>> GetFriendsAsync()
        {
            var friends = new List<Friend>();
            await Request("get_friend_list")
                .ForJsonResult<JObject>(obj =>
                {
                    foreach (var jToken in obj.Value<JArray>("data"))
                    {
                        var f = (JObject) jToken;
                        var friend = new Friend
                        {
                            Identity = f.Value<long>("user_id"),
                            Nickname = f.Value<string>("nickname"),
                            Remark = f.Value<string>("remark")
                        };
                        friends.Add(friend);
                    }
                })
                .FetchAsync();
            return friends;
        }

        public async Task<Friend> GetFriendInfoAsync(long id)
        {
            return (await GetFriendsAsync()).FirstOrDefault(x => x.Identity == id);
        }

        public async Task<IEnumerable<Group>> GetGroupsAsync()
        {
            var groups = new List<Group>();
            await Request("get_group_list")
                .ForJsonResult<JObject>(obj =>
                {
                    foreach (var jToken in obj.Value<JArray>("data"))
                    {
                        var g = (JObject) jToken;
                        var group = new Group
                        {
                            Identity = g.Value<long>("group_id"),
                            Name = g.Value<string>("group_name")
                        };
                        group.Members =
                            new Lazy<IEnumerable<Member>>(() => GetGroupMembersAsync(group).GetAwaiter().GetResult());
                        group.Owner = new Lazy<Member>(() =>
                            group.Members.Value.FirstOrDefault(x => x.Role == GroupRole.Owner));
                        groups.Add(group);
                    }
                })
                .FetchAsync();

            return groups;
        }

        public async Task<Group> GetGroupInfoAsync(long id)
        {
            var result = new Group {Identity = id};
            var task1 = Request("get_group_info")
                .WithJsonBody(new
                {
                    group_id = id
                })
                .ForJsonResult<JObject>(obj => { result.Name = obj["data"].Value<string>("group_name"); })
                .FetchAsync();
            var task2 = GetGroupMembersAsync(result);
            await task1;
            result.Members = new Lazy<IEnumerable<Member>>(await task2);
            return result;
        }

        public async Task<IEnumerable<Member>> GetGroupMembersAsync(Group group)
        {
            var members = new List<Member>();
            await Request("get_group_member_list")
                .WithJsonBody(new
                {
                    group_id = group.Identity
                })
                .ForJsonResult<JObject>(obj =>
                {
                    foreach (var jToken in obj.Value<JArray>("data"))
                    {
                        var mem = (JObject) jToken;
                        var member = new Member
                        {
                            Group = new Lazy<Group>(group),
                            Identity = mem.Value<long>("user_id"),
                            DisplayName = mem.Value<string>("card"),
                            Nickname = mem.Value<string>("nickname"),
                            Title = mem.Value<string>("title"),
                            Role = Ext.OfRole(mem.Value<string>("role"))
                        };
                        members.Add(member);
                    }
                })
                .FetchAsync();

            return members;
        }

        public async Task<Member> GetMemnerInfoAsync(Group group, long id)
        {
            var member = new Member
            {
                Identity = id,
                Group = new Lazy<Group>(group)
            };
            await Request("get_group_member_info")
                .WithJsonBody(new
                {
                    group_id = group.Identity,
                    user_id = id
                })
                .ForJsonResult<JObject>(obj =>
                {
                    member.DisplayName = obj["data"].Value<string>("card");
                    member.Nickname = obj["data"].Value<string>("nickname");
                    member.Title = obj["data"].Value<string>("title");
                    member.Role = Ext.OfRole(obj["data"].Value<string>("role"));
                })
                .FetchAsync();
            return member;
        }

        public async Task<MessageChain> GetMessageByIdAsync(long id)
        {
            MessageChain chain = null;
            await Request("get_msg")
                .WithJsonBody(new
                {
                    message_id = id
                })
                .ForJsonResult<JObject>(obj =>
                {
                    chain = new MessageChain(parser.Parse(obj["data"].Value<JArray>("message").ToString()).Append(new Source(id)));
                })
                .FetchAsync();
            return chain ?? MessageChain.Construct(new Source(id));
        }

        private Wapoo Request(string action)
        {
            return new Wapoo(wapooOptions, $"http://{_host}:{_httpPort}/{action}").ViaPost();
        }

        public void Disconnect()
        {
            client.CloseAsync(WebSocketCloseStatus.Empty, null, CancellationToken.None).Wait();
        }

        private void Dispose(bool isDisposing)
        {
            if (isDisposing && !isDisposed)
                if (State == ApiClientConnectionState.Connected)
                    Disconnect();
        }
    }
}