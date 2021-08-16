using Hyperai.Messages;

namespace Ac682.Hyperai.Clients.CQHTTP.ConcreteMessages
{
    public class NodeFetch : MessageElement
    {
        public NodeFetch(long id)
        {
            MessageId = id;
        }

        public long MessageId { get; set; }
        public override int GetHashCode() => MessageId.GetHashCode();
    }
}