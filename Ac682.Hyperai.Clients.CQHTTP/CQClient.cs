using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hyperai.Events;
using Hyperai.Messages;
using Hyperai.Messages.ConcreteModels;
using Hyperai.Receipts;
using Hyperai.Relations;
using Hyperai.Services;
using Microsoft.Extensions.Logging;

namespace Ac682.Hyperai.Clients.CQHTTP
{
    public sealed class CQClient : IApiClient
    {
        private readonly CQClientOptions _options;
        private readonly List<(Type, object)> handlers = new();
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        
        private WebSocketSession session;


        public CQClient(CQClientOptions options, ILoggerFactory factory)
        {
            _options = options;
            _loggerFactory = factory;
            _logger = factory.CreateLogger<CQClient>();
            session = new WebSocketSession(options.Host, options.HttpPort, options.WebSocketPort, options.AccessToken, factory.CreateLogger<WebSocketSession>());
        }

        public ApiClientConnectionState State =>
            session?.State ?? ApiClientConnectionState.Disconnected;


        public void Connect()
        {
            _logger.LogInformation("Connecting to {} on port http/{} and ws/{}.",_options.Host,_options.HttpPort,_options.WebSocketPort);
            session.Connect();
            _logger.LogInformation("Connected.");
        }

        public void Disconnect()
        {
            session.Disconnect();
        }

        public void Dispose()
        {
            session.Dispose();
        }

        public void Listen()
        {
            session.ReceiveEvents(InvokeHandler);
        }

        public void On<TEventArgs>(IEventHandler<TEventArgs> handler) where TEventArgs : GenericEventArgs
        {
            handlers.Add((typeof(TEventArgs), handler));
        }

        public async Task<T> RequestAsync<T>(T model)
        {
            if (typeof(T) == typeof(Member))
            {
                Member member = ChangeType<Member>(model);
                var group = await session.GetGroupInfoAsync(member.Group.Value.Identity);
                return ChangeType<T>(await session.GetMemberInfoAsync(group, member.Identity)) ?? model;
            }
            if (typeof(T) == typeof(Group))
            {
                Group group = ChangeType<Group>(model);
                return ChangeType<T>(await session.GetGroupInfoAsync(group.Identity)) ?? model;
            }
            if (typeof(T) == typeof(Friend))
            {
                Friend friend = ChangeType<Friend>(model);
                return ChangeType<T>(await session.GetFriendInfoAsync(friend.Identity)) ?? model;
            }
            if (typeof(T) == typeof(Self))
            {
                return ChangeType<T>(await session.GetSelfInfoAsync()) ?? model;
            }
            if (typeof(T) == typeof(MessageChain))
            {
                MessageChain messageChain = ChangeType<MessageChain>(model);
                if (messageChain.Any(x => x is Source))
                {
                    return ChangeType<T>(
                                   await session.GetMessageByIdAsync(((Source)messageChain.First(x => x is Source))
                                       .MessageId)) ?? model;
                }
            }

            return model;
        }

        public async Task<GenericReceipt> SendAsync<TArgs>(TArgs args) where TArgs : GenericEventArgs
        {
            switch (args)
            {
                case FriendMessageEventArgs fme:
                    if (fme.Message.Any(x => x is Node))
                    {
                        await session.SendFriendForwardMessageAsync(fme.User, fme.Message);
                    }
                    else
                    {
                        await session.SendFriendMessageAsync(fme.User, fme.Message);
                    }
                    break;
                case GroupMessageEventArgs gme:
                    if (gme.Message.Any(x => x is Node))
                    {
                        await session.SendGroupForwardMessageAsync(gme.Group, gme.Message);
                    }
                    else
                    {
                        await session.SendGroupMessageAsync(gme.Group, gme.Message);
                    }
                    break;
                case RecallEventArgs rea:
                    await session.RecallMessageAsync(rea.MessageId);
                    break;
                case GroupMemberCardChangedEventArgs gmcce:
                    await session.SetMemberCardAsync(gmcce.Group.Identity, gmcce.WhoseName.Identity, gmcce.Present);
                    break;
                case GroupLeftEventArgs gmle:
                    Self me = await session.GetSelfInfoAsync();
                    if (gmle.Who.Identity == me.Identity)
                    {
                        await session.LeaveGroupAsync(gmle.Group.Identity);
                    }
                    else
                    {
                        await session.KickGroupMemberAsync(gmle.Group.Identity, gmle.Who.Identity);
                    }
                    break;
                case GroupMemberMutedEventArgs gmme:
                    await session.MuteGroupMemberAsync(gmme.Group.Identity, gmme.Whom.Identity, gmme.Duration);
                    break;
                case GroupMemberUnmutedEventArgs gmue:
                    await session.UnmuteGroupMemberAsync(gmue.Group.Identity, gmue.Whom.Identity);
                    break;
                case GroupAllMutedEventArgs game:
                    await session.GroupMuteAllAsync(game.Group.Identity, !game.IsEnded);
                    break;
                case GroupNameChangedEventArgs gnce:
                    await session.SetGroupNameAsync(gnce.Group.Identity, gnce.Present);
                    break;
                case GroupResponseEventArgs gre:
                    await session.ResponseGroupRequsetAsync(gre.Flag,
                        gre.Operation == GroupResponseEventArgs.ResponseOperation.Approve, gre.Reason);
                    break;
                case FriendResponseEventArgs fre:
                    await session.ResponseFriendRequestAsync(fre.Flag,
                        fre.Operation == FriendResponseEventArgs.ResponseOperation.Approve);
                    break;
                default:
                    _logger.LogWarning($"Trying to send a unimplemented event: {args.GetType().Name}");
                    break;

            }

            return null;
        }

        private T ChangeType<T>(object obj)
        {
            return (T)Convert.ChangeType(obj, typeof(T));
        }

        [Obsolete]
        public string RequestRawAsync(string resource)
        {
            throw new NotImplementedException();
        }

        [Obsolete]
        public void SendRawAsync(string resource)
        {
            throw new NotImplementedException();
        }

        private void InvokeHandler(GenericEventArgs args)
        {
            foreach (var handler in handlers.Where(x => x.Item1.IsInstanceOfType(args)).Select(x => x.Item2))
                handler.GetType().GetMethod("Handle")?.Invoke(handler, new object[] { args });
        }
    }
}