using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SprinklerWebServer.Model
{
    [DataContract]
    public sealed class SprinklerStatus
    {
        [DataMember]
        public string InsideTemp { get; set; }
        [DataMember]
        public string OutsideTemp { get; set; }
        [DataMember]
        public IList<bool> ZonesOn { get; set; }
        [DataMember]
        public string CurrentTime { get; set; }
        [DataMember]
        public int ZoneRunSecondsLeft { get; set; }
        [DataMember]
        public bool IsPaused { get; set; }
        [DataMember]
        public int ZonePauseSecondsLeft { get; set; }
    }
}
