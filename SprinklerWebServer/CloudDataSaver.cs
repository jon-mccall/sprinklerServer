using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace SprinklerWebServer
{

    //{
    //"title": "JnJTemps",
    //"outputUrl": "http://data.sparkfun.com/output/yA6WZAzxg5T9vmWwnJAW",
    //"inputUrl": "http://data.sparkfun.com/input/yA6WZAzxg5T9vmWwnJAW",
    //"manageUrl": "http://data.sparkfun.com/streams/yA6WZAzxg5T9vmWwnJAW",
    //"publicKey": "yA6WZAzxg5T9vmWwnJAW",
    //"privateKey": "4W5loWwXYDFoEz4evG64",
    //"deleteKey": "av41zvlOyAFnK9Jvr4dJ"
    //}

    /// <summary>
    /// 
    /// http://data.sparkfun.com/input/yA6WZAzxg5T9vmWwnJAW?private_key=4W5loWwXYDFoEz4evG64&inside=7.89&outside=20.06
    /// </summary>
    public sealed class CloudDataSaver
    {
       public CloudDataSaver()
        {
            // start controller timer
            IAsyncAction asyncAction2 = Windows.System.Threading.ThreadPool.RunAsync(
            (workItem) =>
            {
                ControllerThread();
            });
        }

        /// <summary>
        /// main sprinkler controller thread, starts programs and advances sets
        /// </summary>
        private async void ControllerThread()
        {
            // wait for a bit on startup before running out thread
            System.Diagnostics.Debug.WriteLine("SprinklerProgramController started");
            while(true)
            {
                Task.Delay(60000).Wait();
                SaveTemps(TemperatureSensors.InsideTemperature, TemperatureSensors.OutsideTemperature);
            }
        }

        public async void SaveTemps(string inside, string outside)
        {
            try
            {
                string inputUrl = "http://data.sparkfun.com/input/yA6WZAzxg5T9vmWwnJAW";
                string privateKey = "4W5loWwXYDFoEz4evG64";
                string url = String.Format("{0}?private_key={1}&inside={2}&outside={3}", inputUrl, privateKey, inside, outside);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                WebResponse response = await request.GetResponseAsync();
                //ThingsSpeakReq = (HttpWebRequest)WebRequest.Create(strUpdateURI);

                //ThingsSpeakResp = (HttpWebResponse)ThingsSpeakReq.GetResponse();

                //if (!(string.Equals(ThingsSpeakResp.StatusDescription, "OK")))
                //{
                //    Exception exData = new Exception(ThingsSpeakResp.StatusDescription);
                //    throw exData;
                //}
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error saving temps: " + ex.ToString());
                // swallow throw;
            }
        }
    }
}
