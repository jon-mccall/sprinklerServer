using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SprinklerWebServer.Model
{
    [DataContract]
    public sealed class SprinklerData
    {
        [DataMember]
        public IList<SprinklerProgram> Programs { get; set; }

        [DataMember]
        public IList<Zone> Zones { get; set; }

        public SprinklerData()
        {
            Programs = new List<SprinklerProgram>();
            Programs.Add(new SprinklerProgram() { Name = "Program1", Id = 1 });

            Zones = new List<Zone>();
            Zones.Add(new Zone() { IsEnabled = true, Name = "1" });
            Zones.Add(new Zone() { IsEnabled = true, Name = "2" });
            Zones.Add(new Zone() { IsEnabled = true, Name = "3" });
            Zones.Add(new Zone() { IsEnabled = true, Name = "4" });
            Zones.Add(new Zone() { IsEnabled = true, Name = "5" });
            Zones.Add(new Zone() { IsEnabled = true, Name = "6" });
            Zones.Add(new Zone() { IsEnabled = true, Name = "7" });
            Zones.Add(new Zone() { IsEnabled = true, Name = "8" });
            Zones.Add(new Zone() { IsEnabled = true, Name = "9" });
            Zones.Add(new Zone() { IsEnabled = true, Name = "10" });
            Zones.Add(new Zone() { IsEnabled = true, Name = "11" });
            Zones.Add(new Zone() { IsEnabled = true, Name = "12" });
            Zones.Add(new Zone() { IsEnabled = true, Name = "13" });
            Zones.Add(new Zone() { IsEnabled = true, Name = "14" });
            Zones.Add(new Zone() { IsEnabled = true, Name = "15" });
        }
    }
}
