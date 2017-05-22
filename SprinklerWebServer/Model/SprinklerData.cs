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
                    // TODO - remove test code....
                    if(data.Programs[0].ZoneTimes[0] == 2)
                    {
                        // its a default setup, change it!
                        if(data.Programs.Count > 1)
                        {
                            data.Programs.RemoveAt(1);
                        }
                        data.Programs[0].StartHour = 6;
                        data.Programs[0].StartMinute = 50;
                        data.Programs[0].ZoneTimes[0] = 45;
                        data.Programs[0].ZoneTimes[1] = 45;
                        data.Programs[0].ZoneTimes[2] = 45;
                        data.Programs[0].ZoneTimes[3] = 45;
                        data.Programs[0].ZoneTimes[4] = 45;
                        data.Programs[0].ZoneTimes[5] = 5;
                        data.Programs[0].ZoneTimes[6] = 45;
                        data.Programs[0].ZoneTimes[7] = 45;
                        data.Programs[0].ZoneTimes[8] = 45;
                        data.Programs[0].ZoneTimes[9] = 45;
                        data.Programs[0].ZoneTimes[10] = 45;
                        data.Programs[0].ZoneTimes[11] = 45;
                        data.Programs[0].ZoneTimes[12] = 45;
                        data.Programs[0].ZoneTimes[13] = 0;
                        //data.Programs[0].ZoneTimes[14] = 0;
                        //data.Programs[0].ZoneTimes[15] = 0;

                    }
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
