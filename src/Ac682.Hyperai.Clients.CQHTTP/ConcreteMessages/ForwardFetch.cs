using Hyperai.Messages;

namespace Ac682.Hyperai.Clients.CQHTTP.ConcreteMessages
{
    public class ForwardFetch : MessageElement
    {
        public ForwardFetch(string id)
        {
            ForwardId = id;
        }
        public string ForwardId {get;set;}
        public override int GetHashCode() => ForwardId.GetHashCode();
    }
}