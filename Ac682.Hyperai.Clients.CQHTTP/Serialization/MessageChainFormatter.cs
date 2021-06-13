using System;
using System.Collections.Generic;
using System.IO;
using Hyperai.Messages;
using Hyperai.Messages.ConcreteModels;
using Hyperai.Messages.ConcreteModels.ImageSources;
using Newtonsoft.Json;

namespace Ac682.Hyperai.Clients.CQHTTP.Serialization
{
    public class MessageChainFormatter : IMessageChainFormatter
    {
        public string Format(MessageChain chain)
        {
            var cmps = new LinkedList<object>();
            foreach (var comp in chain)
                cmps.AddLast(comp switch
                {
                    Plain plain => new {type = "text", data = new {text = plain.Text}},
                    Face face => new {type = "face", data = new {id = face.FaceId}},
                    At at => new {type = "at", data = new {qq = at.TargetId.ToString()}},
                    ImageBase image when image.Source is UrlSource source => new {type = "image", data = new {file = source.Url.AbsoluteUri, type = image is Flash ? "flash": "image", cache = 0}},
                    ImageBase image when image.Source is StreamSource source => new {type = "image", data = new {file = GetBase64Url(source.Data), type= image is Flash ? "flash": "image", cache = 0}},
                    AtAll atall => new {type = "at", data = new {qq = "atall"}},
                    Quote quote => new {type = "reply", data = new {id = quote.MessageId}},
                    Poke poke => new {type = "poke", data = new { type = ((int)poke.Name).ToString(), id = "-1"}},

                    _ => new {type = "text", data = new {text = $"[{comp.TypeName}]暂不支持查看该消息，请升级 Hyperai 版本."}}
                });

            return JsonConvert.SerializeObject(cmps);
            
            
        }

        private string GetBase64Url(Stream stream)
        {
            byte[] bytes = new byte[stream.Length];
            stream.Read(bytes);
            return "base64://" + Convert.ToBase64String(bytes);
        }
    }
}