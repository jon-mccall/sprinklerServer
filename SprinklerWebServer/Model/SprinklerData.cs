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
        private const string SprinklerFileName = "SprinklerSetup.json";

        [DataMember]
        public IList<SprinklerProgram> Programs { get; set; }

        [DataMember]
        public IList<Zone> ZoneList { get; set; }

        public SprinklerData()
        {
        }

        public static SprinklerData Load()
        {
            if (Utils.LocalFileExists(SprinklerFileName))
            {
                string json = Utils.ReadStringFromLocalFile(SprinklerFileName);
                if (json != null && json.Length > 0)
                {
                    SprinklerData data = Utils.DeserializeJsonSprinklerData(json);
                    // make sure the list of programs is editable...
                    data.Programs = new List<SprinklerProgram>(data.Programs);
                    return data;
                }
            }
            return null;
        }

        public void Save()
        {
            string json = Utils.SerializeJSonSprinklerData(this);
            Utils.SaveStringToLocalFile(SprinklerFileName, json);

        }

        public int AddProgram(string name)
        {
            int id = Programs.Max(x => x.Id) + 1;
            Programs.Add(new SprinklerProgram() { Name = name, Id = id });
            return id;
        }

        public void SetDefaults()
        {
            Programs = new List<SprinklerProgram>();
            Programs.Add(new SprinklerProgram() { Name = "Program1", Id = 1 });
            Programs.Add(new SprinklerProgram() { Name = "Program2", Id = 2 });

            // hard code zone list /names if the data file does not exist
            ZoneList = new List<Zone>();
            for (int i = 1; i < 16; i++)
            {
                ZoneList.Add(new Zone() { Id = i, IsEnabled = true, Name = String.Format("Zone {0}", i) });
            }

            //if (Utils.LocalFileExists(ZoneListFileName))
            //{
            //    string json = Utils.ReadStringFromLocalFile(ZoneListFileName);
            //    if (json != null && json.Length > 0)
            //    {
            //        ZoneList = Utils.DeserializeJsonZoneList(json);
            //    }
            //}

            //if (ZoneList == null || ZoneList.Count == 0)
            //{
            //    // hard code zone list /names if the data file does not exist
            //    ZoneList = new List<Zone>();
            //    for (int i = 1; i < 16; i++)
            //    {
            //        ZoneList.Add(new Zone() { Id = i, IsEnabled = true, Name = String.Format("Zone {0}", i) });
            //    }
            //}

        }
    }
}
