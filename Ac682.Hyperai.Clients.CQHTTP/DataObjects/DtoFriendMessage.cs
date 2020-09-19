using Hyperai.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Ac682.Hyperai.Clients.CQHTTP.DataObjects
{
    public class DtoFriendMessage
    {
        public uint Font { get; set; }
        public long Message_Id { get; set; }
        public MessageChain Message { get; set; }
        public DtoFriendSender Sender { get; set; }
        public long User_Id { get; set; }
    }
}
