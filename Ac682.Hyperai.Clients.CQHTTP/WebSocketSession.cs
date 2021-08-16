using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ac682.Hyperai.Clients.CQHTTP.ConcreteMessages;
using Ac682.Hyperai.Clients.CQHTTP.DataObjects;
using Ac682.Hyperai.Clients.CQHTTP.Serialization;
using Hyperai.Events;
using Hyperai.Messages;
using Hyperai.Messages.ConcreteModels;
using Hyperai.Messages.ConcreteModels.FileSources;
using Hyperai.Relations;
using Hyperai.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Wupoo;
using Ext = Ac682.Hyperai.Clients.CQHTTP.DataObjects.Extensions;

namespace Ac682.Hyperai.Clients.CQHTTP
{
    public sealed class WebSocketSession : IDisposable
    {
        public ApiClientConnectionState State => client != null && client!.State == WebSocketState.Open
            ? ApiClientConnectionState.Connected
            : ApiClientConnectionState.Disconnected;

        private readonly string _accessToken;
        private readonly string _host;
        private readonly int _httpPort;
        private readonly int _websocketPort;
        private readonly ILogger _logger;
        private readonly IMessageChainParser parser = new MessageChainParser();
        private readonly WapooOptions wapooOptions;

        private ClientWebSocket client;


        private readonly bool isDisposed = false;

        public WebSocketSession(string host, int httpPort, int websocketPort, string accessToken, ILogger logger)
        {
            _host = host;
            _httpPort = httpPort;
            _websocketPort = websocketPort;
            _accessToken = accessToken;
            _logger = logger;

            wapooOptions = new WapooOptions
            {
                IgnoreMediaTypeCheck = true,
                Authentication = new AuthenticationHeaderValue("Bearer", _accessToken),
                JsonSerializerOptions = Shared.SerializerSettings
            };
        }

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
                ms.Close();
                GenericEventArgs args = null;
                try
                {
                    args = ParseEvent(text);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error occurred while parsing events.");
                }

                if (args != null)
                {
                    callback(args);
                }
                else
                {
                    _logger.LogWarning("Un resolved event received: \n" + text);
                }
            }
        }

        private GenericEventArgs ParseEvent(string json)
        {
            var dick = JsonConvert.DeserializeObject<JObject>(json);
            var postType = dick.Value<string>("post_type");

            switch (postType)
            {
                case "message":
                    switch (dick.Value<string>("message_type"))
                    {
                        case "group":
                        {
                            var message = JsonConvert.DeserializeObject<DtoGroupMessage>(json, Shared.SerializerSettings);
                            var args = new GroupMessageEventArgs
                            {
                                Message = PostProcessMessageAsync(
                                        new MessageChain(message!.Message.Prepend(new Source(message.Message_Id))))
                                    .GetAwaiter().GetResult(),
                                Time = DateTime.Now,
                                Group = GetGroupInfoAsync(dick.Value<long>("group_id")).GetAwaiter().GetResult()
                            };

                            args.User = message.Sender.ToMember(args.Group);
                            return args;
                        }
                        case "private":
                        {
                            var message = JsonConvert.DeserializeObject<DtoFriendMessage>(json, Shared.SerializerSettings);
                            var args = new FriendMessageEventArgs
                            {
                                Message = PostProcessMessageAsync(
                                        new MessageChain(message!.Message.Prepend(new Source(message!.Message_Id))))
                                    .GetAwaiter().GetResult(),
                                Time = DateTime.Now,
                                User = message!.Sender.ToFriend()
                            };
                            return args;
                        }
                        default:
                            return null;
                    }
                case "notice":
                {
                    switch (dick.Value<string>("notice_type"))
                    {
                        case "friend_add":
                        {
                            var args = new FriendResponseEventArgs()
                            {
                                Operation = FriendResponseEventArgs.ResponseOperation.Approve,
                                Who = dick.Value<long>("user_id")
                            };
                            return args;
                        }
                        case "group_admin":
                        {
                            var args = new GroupPermissionChangedEventArgs
                            {
                                Group = GetGroupInfoAsync(dick.Value<long>("group_id")).GetAwaiter().GetResult()
                            };
                            args.Whom = GetMemberInfoAsync(args.Group, dick.Value<long>("user_info")).GetAwaiter()
                                .GetResult();
                            switch (dick.Value<string>("sub_type"))
                            {
                                case "set":
                                {
                                    args.Original = GroupRole.Member;
                                    args.Present = GroupRole.Administrator;
                                }
                                    break;
                                case "unset":
                                {
                                    args.Original = GroupRole.Administrator;
                                    args.Present = GroupRole.Member;
                                }
                                    break;
                                default:
                                    return null;
                            }

                            return args;
                        }
                        case "group_ban":
                        {
                            Self me = GetSelfInfoAsync().GetAwaiter().GetResult();
                            switch (dick.Value<string>("sub_type"))
                            {
                                case "ban":
                                {
                                    var args = new GroupMemberMutedEventArgs()
                                    {
                                        Duration = TimeSpan.FromSeconds(dick.Value<long>("duration"))
                                    };
                                    args.Group = GetGroupInfoAsync(dick.Value<long>("group_id")).GetAwaiter()
                                        .GetResult();
                                    args.Operator = GetMemberInfoAsync(args.Group, dick.Value<long>("operator_id"))
                                        .GetAwaiter().GetResult();
                                    args.Whom = GetMemberInfoAsync(args.Group, dick.Value<long>("user_id")).GetAwaiter()
                                        .GetResult();
                                    return args;
                                }
                                case "lift_ban":
                                {
                                    var args = new GroupMemberUnmutedEventArgs();
                                    args.Group = GetGroupInfoAsync(dick.Value<long>("group_id")).GetAwaiter()
                                        .GetResult();
                                    args.Operator = GetMemberInfoAsync(args.Group, dick.Value<long>("operator_id"))
                                        .GetAwaiter().GetResult();
                                    args.Whom = GetMemberInfoAsync(args.Group, dick.Value<long>("user_id")).GetAwaiter()
                                        .GetResult();
                                    return args;
                                }
                                default:
                                    return null;
                            }
                        }
                        case "group_decrease":
                        {
                            Self me = GetSelfInfoAsync().GetAwaiter().GetResult();
                            switch (dick.Value<string>("sub_type"))
                            {
                                case "kick_me":
                                {
                                    var args = new GroupLeftEventArgs();
                                    args.Group = GetGroupInfoAsync(dick.Value<long>("group_id")).GetAwaiter()
                                        .GetResult();
                                    args.IsKicked = true;
                                    args.Who = GetMemberInfoAsync(args.Group, me.Identity).GetAwaiter().GetResult();
                                    args.Operator = GetMemberInfoAsync(args.Group, dick.Value<long>("operator_id"))
                                        .GetAwaiter().GetResult();
                                    return args;
                                }
                                case "kick":
                                {
                                    var args = new GroupLeftEventArgs();
                                    args.Group = GetGroupInfoAsync(dick.Value<long>("group_id")).GetAwaiter()
                                        .GetResult();
                                    args.IsKicked = true;
                                    args.Operator = GetMemberInfoAsync(args.Group, dick.Value<long>("operator_id"))
                                        .GetAwaiter().GetResult();
                                    args.Who = GetMemberInfoAsync(args.Group, dick.Value<long>("user_id")).GetAwaiter()
                                        .GetResult();
                                    return args;
                                }
                                case "leave":
                                {
                                    var args = new GroupLeftEventArgs();
                                    args.Group = GetGroupInfoAsync(dick.Value<long>("group_id")).GetAwaiter()
                                        .GetResult();
                                    args.IsKicked = false;
                                    args.Operator = GetMemberInfoAsync(args.Group, dick.Value<long>("operator_id"))
                                        .GetAwaiter().GetResult();
                                    args.Who = GetMemberInfoAsync(args.Group, dick.Value<long>("user_id")).GetAwaiter()
                                        .GetResult();
                                    return args;
                                }
                                default:
                                    return null;
                            }
                        }
                        case "group_increase":
                        {
                            switch (dick.Value<string>("sub_type"))
                            {
                                case "invite":
                                case "approve":
                                {
                                    var args = new GroupJoinedEventArgs();
                                    args.Group = GetGroupInfoAsync(dick.Value<long>("group_id")).GetAwaiter()
                                        .GetResult();
                                    args.Who = GetMemberInfoAsync(args.Group, dick.Value<long>("user_id")).GetAwaiter()
                                        .GetResult();
                                    args.Operator = GetMemberInfoAsync(args.Group, dick.Value<long>("operator_id"))
                                        .GetAwaiter().GetResult();
                                    return args;
                                }
                                default:
                                    return null;
                            }
                        }
                        case "group_recall":
                        {
                            var args = new GroupRecallEventArgs();
                            args.Group = GetGroupInfoAsync(dick.Value<long>("group_id")).GetAwaiter().GetResult();
                            args.Operator = GetMemberInfoAsync(args.Group, dick.Value<long>("operator_id")).GetAwaiter()
                                .GetResult();
                            args.WhoseMessage = GetMemberInfoAsync(args.Group, dick.Value<long>("user_id")).GetAwaiter()
                                .GetResult();
                            args.MessageId = dick.Value<long>("message_id");
                            return args;
                        }
                        case "friend_recall":
                        {
                            var args = new FriendRecallEventArgs();
                            args.MessageId = dick.Value<long>("message_id");
                            args.WhoseMessage = GetFriendInfoAsync(dick.Value<long>("user_id")).GetAwaiter()
                                .GetResult();
                            return args;
                        }
                        case "notify":
                        {
                            switch (dick.Value<string>("sub_type"))
                            {
                                case "poke":
                                {
                                    // TODO: 支持 Poke
                                    return null;
                                }
                                case "honor":
                                {
                                    // 不需要支持 群荣誉变更
                                    return null;
                                }
                                default:
                                    return null;
                            }
                        }
                        default:
                            return null;
                    }
                }
                case "request":
                {
                    switch (dick.Value<string>("request_type"))
                    {
                        case "friend":
                        {
                            var args = new FriendRequestEventArgs()
                            {
                                Who = dick.Value<long>("user_id"),
                                Comment = dick.Value<string>("comment"),
                                Flag = dick.Value<string>("flag")
                            };
                            return args;
                        }
                        case "group" when dick.Value<string>("sub_type") == "add":
                        {
                            var args = new GroupRequestEventArgs()
                            {
                                Who = dick.Value<long>("user_id"),
                                Comment = dick.Value<string>("comment"),
                                GroupId = dick.Value<long>("group_id"),
                                Flag = dick.Value<string>("flag")
                            };
                            return args;
                        }
                        default:
                            return null;
                    }
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
        
        public async Task<long> SendFriendForwardMessageAsync(Friend friend, MessageChain forward)
        {
            long messageId = -1;
            await Request("send_friend_forward_msg")
                .WithJsonBody(new
                {
                    user_id = friend.Identity,
                    messages = forward.AsReadable()
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

        public async Task<long> SendGroupForwardMessageAsync(Group group, MessageChain forward)
        {
            long messageId = -1;
            await Request("send_group_forward_msg")
                .WithJsonBody(new
                {
                    group_id = group.Identity,
                    messages = forward.AsReadable()
                })
                .ForJsonResult<JObject>(obj => messageId = obj["data"].Value<long>("message_id"))
                .FetchAsync();
            return messageId;
        }

        public async Task RecallMessageAsync(long messageId)
        {
            await Request("delete_msg")
                .WithJsonBody(new { message_id = messageId })
                .FetchAsync();
        }

        public async Task SetMemberCardAsync(long groupId, long memberId, string name)
        {
            await Request("set_group_card")
                .WithJsonBody(new { group_id = groupId, user_id = memberId, card = name })
                .FetchAsync();
        }

        public async Task SetGroupNameAsync(long groupId, string name)
        {
            await Request("set_group_name ")
                .WithJsonBody(new { group_id = groupId, group_name = name })
                .FetchAsync();
        }

        public async Task ResponseGroupRequsetAsync(string flag, bool approve, string reason = "")
        {
            await Request("set_group_add_request")
                .WithJsonBody(new { flag, approve, reason })
                .FetchAsync();
        }

        public async Task ResponseFriendRequestAsync(string flag, bool approve)
        {
            await Request("set_friend_add_request")
                .WithJsonBody(new { flag, approve })
                .FetchAsync();
        }

        public async Task LeaveGroupAsync(long groupId)
        {
            await Request("set_group_leave")
                .WithJsonBody(new { group_id = groupId })
                .FetchAsync();
        }

        public async Task KickGroupMemberAsync(long groupId, long memberId)
        {
            await Request("set_group_kick")
                .WithJsonBody(new { group_id = groupId, user_id = memberId })
                .FetchAsync();
        }

        public async Task MuteGroupMemberAsync(long groupId, long memberId, TimeSpan duration)
        {
            await Request("set_group_ban")
                .WithJsonBody(new { group_id = groupId, user_id = memberId, duration = duration.TotalSeconds })
                .FetchAsync();
        }

        public async Task UnmuteGroupMemberAsync(long groupId, long memberId)
        {
            await Request("set_group_ban")
                .WithJsonBody(new { group_id = groupId, user_id = memberId, duration = TimeSpan.FromSeconds(0) })
                .FetchAsync();
        }

        public async Task GroupMuteAllAsync(long groupId, bool mute = true)
        {
            await Request("set_group_whole_ban ")
                .WithJsonBody(new { group_id = groupId, enable = mute })
                .FetchAsync();
        }

        public MessageChain PreprocessMessageChainBeforeSending(MessageChain chain)
        {
            var passes = chain.Where(x => (x is not ImageBase) || x is ImageBase image && image.Source is UrlSource);
            if (passes.Count() == chain.Count)
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
                        var f = (JObject)jToken;
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
                        var g = (JObject)jToken;
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
            // TODO: 自己被踢就获取不到 GROUP，此时返回一个 new Group(id)
            var result = new Group { Identity = id };
            var task1 = Request("get_group_info")
                .WithJsonBody(new
                {
                    group_id = id
                })
                .ForJsonResult<JObject>(obj => { result.Name = obj["data"].Value<string>("group_name"); })
                .WhenException((Exception e) => {})
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
                        var mem = (JObject)jToken;
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
                .WhenException((Exception e) => { })
                .FetchAsync();

            return members;
        }

        public async Task<Member> GetMemberInfoAsync(Group group, long id)
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
                .WhenException((Exception e) => {})
                .FetchAsync();
            return member;
        }

        public async Task<MessageChain> GetMessageByIdAsync(long id)
        {
            return (await GetMessageInfoByIdAsync(id)).Item3;
        }

        public async Task<(long, string, MessageChain)> GetMessageInfoByIdAsync(long id)
        {
            MessageChain chain = null;
            long senderId = 0;
            string senderName = string.Empty;
            await Request("get_msg")
                .WithJsonBody(new
                {
                    message_id = id
                })
                .ForJsonResult<JObject>(async obj =>
                {
                    senderId = obj["data"]["sender"].Value<long>("user_id");
                    senderName = obj["data"]["sender"].Value<string>("nickname");
                    chain = new MessageChain(
                        (await PostProcessMessageAsync(parser.Parse(obj["data"].Value<JArray>("message")!.ToString())))
                        .Prepend(new Source(id)));
                })
                .FetchAsync();
            return (senderId, senderName, chain ?? MessageChain.Construct(new Source(id)));
        }

        private async Task<MessageChain> PostProcessMessageAsync(MessageChain chain)
        {
            return chain.Any(x => x is ForwardFetch)
                ? await GetForwardAsync(((ForwardFetch)chain.First(x => x is ForwardFetch)).ForwardId)
                : new MessageChain(chain.Select(async x => x switch
                {
                    NodeFetch node => await GetNodeAsync(node.MessageId),
                    _ => x
                }).Select(x => x.Result));
        }

        private async Task<MessageChain> GetForwardAsync(string forwardId)
        {
            MessageChain chain = null;
            await Request("get_forward_msg")
                .WithJsonBody(new { id = forwardId })
                .ForJsonResult<JObject>(async obj =>
                {
                    chain = await PostProcessMessageAsync(
                        parser.Parse(JArray.FromObject(obj["data"].Value<JArray>("messages").Select(x => JObject.FromObject(new
                        {
                            type = "node",
                            data = new
                            {
                                uin = x["sender"].Value<long>("user_id"),
                                name = x["sender"].Value<string>("nickname"),
                                content = x["content"]
                            }
                        }))).ToString()));
                })
                .FetchAsync();

            return chain ?? MessageChain.Construct();
        }

        private async Task<Node> GetNodeAsync(long messageId)
        {
            var (senderId, senderName, chain) = await GetMessageInfoByIdAsync(messageId);
            return new Node(senderId, senderName, chain);
        }

        private Wapoo Request(string action)
        {
            return new Wapoo(wapooOptions, $"http://{_host}:{_httpPort}/{action}").ViaPost();
        }

        public void Disconnect()
        {
            client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).Wait();
        }

        private void Dispose(bool isDisposing)
        {
            if (isDisposing && !isDisposed)
                if (State == ApiClientConnectionState.Connected)
                    Disconnect();
        }
    }
}