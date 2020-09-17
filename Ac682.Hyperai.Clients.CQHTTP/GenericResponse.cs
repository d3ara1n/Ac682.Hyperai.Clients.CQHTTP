using System;
using System.Collections.Generic;
using System.Text;

namespace Ac682.Hyperai.Clients.CQHTTP
{
    public class GenericResponse<TData>
    {
        public string Status { get; set; }
        public string Echo { get; set; }
        public uint Code { get; set; }
        public TData Data { get; set; }
    }
}
