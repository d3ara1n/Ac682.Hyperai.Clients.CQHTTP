using Hyperai.Events;
using Hyperai.Receipts;
using Hyperai.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ac682.Hyperai.Clients.CQHTTP
{
    public class CQClient : IApiClient
    {
        public ApiClientConnectionState State => session == null ? ApiClientConnectionState.Disconnected : session.State;
        private WebSocketSession session;
        private readonly List<(Type, object)> handlers = new List<(Type, object)>();
        private readonly CQClientOptions _options;


        public CQClient(CQClientOptions options)
        {
            _options = options;
            session = new WebSocketSession(options.Host, options.HttpPort, options.WebSocketPort, options.AccessToken);
        }


        public void Connect()
        {
            session.Connect();
        }

        public void Disconnect()
        {
            session.Disconnect();
            session.Dispose();
            session = null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool isDisposed = false;

        protected virtual void Dispose(bool isDisposing)
        {
            if (!isDisposed && isDisposing)
            {
                isDisposed = true;
                session.Dispose();
            }
        }

        public void Listen()
        {
            session.ReceiveEvents((args) => InvokeHandler(args));
        }

        public void On<TEventArgs>(IEventHandler<TEventArgs> handler) where TEventArgs : GenericEventArgs
        {
            handlers.Add((typeof(TEventArgs), handler));
        }

        public Task<T> RequestAsync<T>(T model)
        {
            throw new NotImplementedException();
        }

        [Obsolete]
        public string RequestRawAsync(string resource)
        {
            throw new NotImplementedException();
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
            }
            return null;
        }

        [Obsolete]
        public void SendRawAsync(string resource)
        {
            throw new NotImplementedException();
        }

        private void InvokeHandler(GenericEventArgs args)
        {
            foreach (object handler in handlers.Where(x => x.Item1.IsAssignableFrom(args.GetType())).Select(x => x.Item2))
            {
                handler.GetType().GetMethod("Handle").Invoke(handler, new object[] { args });
            }
        }
    }
}
