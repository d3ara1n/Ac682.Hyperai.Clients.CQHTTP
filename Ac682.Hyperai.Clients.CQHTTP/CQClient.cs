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
        private readonly ILogger _logger;

        private bool isDisposed;
        private WebSocketSession session;


        public CQClient(CQClientOptions options, ILogger<CQClient> logger)
        {
            _options = options;
            _logger = logger;
            session = new WebSocketSession(options.Host, options.HttpPort, options.WebSocketPort, options.AccessToken);
        }

        public ApiClientConnectionState State =>
            session?.State ?? ApiClientConnectionState.Disconnected;


        public void Connect()
        {
            _logger.LogInformation($"Connecting to {_options.Host} on port http/{_options.HttpPort} and ws/{_options.WebSocketPort}.");
            session.Connect();
            _logger.LogInformation("Connected.");
        }

        public void Disconnect()
        {
            session.Disconnect();
            session.Dispose();
            session = null;
        }

        public void Dispose()
        {
            Disconnect();
            Dispose(true);
            GC.SuppressFinalize(this);
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
                return ChangeType<T>(await session.GetMemnerInfoAsync(group, member.Identity)) ?? model;
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
            if(typeof(T) == typeof(MessageChain))
            {
                MessageChain messageChain = ChangeType<MessageChain>(model);
                if(messageChain.Any( x=> x is Source))
                {
                    return ChangeType<T>(
                                   await session.GetMessageByIdAsync(((Source)messageChain.First(x => x is Source))
                                       .MessageId)) ??
                               model;
                }
            }

            return model;
        }

        public async Task<GenericReceipt> SendAsync<TArgs>(TArgs args) where TArgs : GenericEventArgs
        {
            switch (args)
            {
                case FriendMessageEventArgs fme:
                    await session.SendFriendMessageAsync(fme.User, fme.Message);
                    break;
                case GroupMessageEventArgs gme:
                    await session.SendGroupMessageAsync(gme.Group, gme.Message);
                    break;
                case GroupRecallEventArgs gre:
                    await session.RecallMessageAsync(gre.MessageId);
                    break;
                case FriendRecallEventArgs fre:
                    await session.RecallMessageAsync(fre.MessageId);
                    break;
            }

            return null;
        }

        private void Dispose(bool isDisposing)
        {
            if (isDisposed || !isDisposing) return;
            isDisposed = true;
            session.Dispose();
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