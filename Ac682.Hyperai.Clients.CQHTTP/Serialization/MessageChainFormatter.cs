using Hyperai.Messages;
using Hyperai.Messages.ConcreteModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Ac682.Hyperai.Clients.CQHTTP.Serialization
{
    public class MessageChainFormatter : IMessageChainFormatter
    {
        public string Format(MessageChain chain)
        {
            LinkedList<object> cmps = new LinkedList<object>();
            foreach(MessageComponent comp in chain)
            {
                cmps.AddLast(comp switch
                {
                    Plain plain => new { type = "text", data = new { text = plain.Text } },
                    Face face => new { type = "face", data = new { id = face.FaceId} },
                    Image image => new { type = "image", data = new { file = image.ImageId ?? image.Url.AbsoluteUri, url = image.Url} },
                    At at => new { type = "at", data = new { qq = at.TargetId.ToString()} },
                    AtAll atall => new {type = "at", data = new { qq = "atall"} },
                    Quote quote => new { type = "reply", data = new { id = quote.MessageId} },

                    _ => new { type = "text", data = new { text = $"[{comp.TypeName}]暂不支持查看该消息，请升级 Hyperai 版本."} }

                });
            }

            return JsonConvert.SerializeObject(cmps);
        }
    }
}
