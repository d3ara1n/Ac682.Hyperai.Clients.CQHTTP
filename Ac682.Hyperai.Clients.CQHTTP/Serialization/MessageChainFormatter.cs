using System;
using System.Collections.Generic;
using System.IO;
using Hyperai.Messages;
using Hyperai.Messages.ConcreteModels;
using Hyperai.Messages.ConcreteModels.FileSources;
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
                    Plain plain => new { type = "text", data = new { text = plain.Text } },
                    Face face => new { type = "face", data = new { id = face.FaceId } },
                    At at => new { type = "at", data = new { qq = at.TargetId.ToString() } },
                    ImageBase { Source: UrlSource source } image => new { type = "image", data = new { file = source.Url.AbsoluteUri, type = image is Flash ? "flash" : "image", cache = 0 } },
                    ImageBase { Source: StreamSource source } image => new { type = "image", data = new { file = GetBase64Url(source.Data), type = image is Flash ? "flash" : "image", cache = 0 } },
                    AtAll atall => new { type = "at", data = new { qq = "atall" } },
                    Quote quote => new { type = "reply", data = new { id = quote.MessageId } },
                    Poke poke => new { type = "poke", data = new { type = ((int)poke.Name).ToString(), id = "-1" } },
                    XmlContent xml => new { type = "xml", data = new { data = xml.Content } },
                    JsonContent json => new { type = "json", data = new { data = json.Content } },

                    Music music => new
                    {
                        type = "music",
                        data = new
                        {
                            type = music.Type switch
                            {
                                Music.MusicSource.QqMusic => "qq",
                                Music.MusicSource.Music163 => "163",
                                Music.MusicSource.XiaMi => "xm"
                            },
                            id = music.MusicId
                        }
                    },
                    Video { Source: UrlSource source } video => new { type = "video", data = new { file = source.Url.AbsoluteUri } },
                    Video { Source: StreamSource source } video => new { type = "video", data = new { file = GetBase64Url(source.Data) } },
                    Voice { Source: UrlSource source } video => new { type = "voice", data = new { file = source.Url.AbsoluteUri } },
                    Voice { Source: StreamSource source } video => new { type = "voice", data = new { file = GetBase64Url(source.Data) }},

                    Node node => new {type = "node", data = new { uin = node.UserId.ToString(), name = node.UserDisplayName, content = node.Message }},

                    _ => new { type = "text", data = new { text = $"[{comp.TypeName}]暂不支持查看该消息，请升级 Hyperai 版本." } }
                });

            return JsonConvert.SerializeObject(cmps, Shared.SerializerSettings);


        }

        private string GetBase64Url(Stream stream)
        {
            byte[] bytes = new byte[stream.Length];
            stream.Read(bytes);
            stream.Position = 0;
            return "base64://" + Convert.ToBase64String(bytes);
        }
    }
}