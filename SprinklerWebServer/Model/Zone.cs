using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SprinklerWebServer.Model
{
    [DataContract]
    public sealed class Zone
    {
        [DataMember]
        public bool IsEnabled { get; set; }
        [DataMember]
        public int Id { get; set; }
        [DataMember]
        public string Name { get; set; }
    }
}
