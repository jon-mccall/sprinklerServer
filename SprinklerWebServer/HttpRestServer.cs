using SprinklerWebServer.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace SprinklerWebServer
{
    //Class to define the HTTP WebServer
    public sealed class HttpRestServer : IDisposable
    {
        private bool inited = false;
        //Create a buffer to read HTTP data
        private const uint BufferSize = 8192;
        //Port to listen on
        private int port = 8888;
        //Listener to
        private readonly StreamSocketListener listener;
        //Connection to send status information back to PoolControllerWebService
        private AppServiceConnection appServiceConnection;

        SprinklerValveController sprinklerController;
        SprinklerProgramController programController;
        //TemperatureSensors tempSensors;

        public HttpRestServer(int serverPort, AppServiceConnection connection, SprinklerValveController sprinkler, SprinklerProgramController program)//, TemperatureSensors temp)
        {
            listener = new StreamSocketListener();
            port = serverPort;
            appServiceConnection = connection;
            sprinklerController = sprinkler;
            programController = program;
            //tempSensors = temp;

            //Add event handler for HTTP connections
            listener.ConnectionReceived += (s, e) => ProcessRequestAsync(e.Socket);

            System.Diagnostics.Debug.WriteLine("Sprinkler HTTP REST server starting...");
        }

        //Call to start the listner 
        public void StartServer()
        {
#pragma warning disable CS4014
            listener.BindServiceNameAsync(port.ToString());
#pragma warning restore CS4014

        }

        public void Dispose()
        {
            listener.Dispose();
        }


        private async void ProcessRequestAsync(StreamSocket socket)
        {
            if(!inited)
            {
                // start the temp sensor here...
                // for some reason when I put it earlier init was getting called twice
                // and so it would fail the second time.  Maybe an async bug with iot?
                TemperatureSensors.InitSensors();
                inited = true;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("ProcessRequestAsync called...");
                StringBuilder request = new StringBuilder();
                //Get the incomming data
                using (IInputStream input = socket.InputStream)
                {
                    byte[] data = new byte[BufferSize];
                    IBuffer buffer = data.AsBuffer();
                    uint dataRead = BufferSize;
                    //Read all the incomming data
                    while (dataRead == BufferSize)
                    {
                        await input.ReadAsync(buffer, BufferSize, InputStreamOptions.Partial);
                        request.Append(Encoding.UTF8.GetString(data, 0, data.Length));
                        dataRead = buffer.Length;
                    }
                }

                //Got the data start processing a response
                using (IOutputStream output = socket.OutputStream)
                {
                    string requestMethod = request.ToString();
                    string[] requestParts = { "" };
                    if (requestMethod != null)
                    {
                        //Beakup the request into it parts
                        requestMethod = requestMethod.Split('\n')[0];
                        requestParts = requestMethod.Split(' ');
                    }
                    //We only respond HTTP GETS and POST methods
                    if (requestParts[0] == "GET" && requestParts.Length > 1)
                        await WriteGetResponseAsync(requestParts[1], output);
                    else if (requestParts[0] == "POST" && requestParts.Length > 1)
                        await WritePostResponseAsync(requestParts[1], output);
                    else
                        await WriteMethodNotSupportedResponseAsync(requestParts[0], output);
                }
            }
            catch (Exception) { }
        }

        //Handles all HTTP GET's
        private async Task WriteGetResponseAsync(string request, IOutputStream os)
        {
            bool urlFound = false;
            byte[] bodyArray = null;
            string responseMsg = "";

            //ZONE/[zone]/running - currently running or not?
            //ZONE/[zone]/timeleft - number of seconds left
            //program/[PROGRAM]/[zone]/runtime  -how many minutes for this program/zone
            //program/[PROGRAM]/[zone]/enabled - is this zone enabled
            //program/[PROGRAM]/starttime - what's the program start time
            //program/[PROGRAM]/progenabled - is this program enabled
            //program/[PROGRAM]/running - currently running or not?
            //query/runningZone  - currently running zone
            //query/status - json object containing datetime, temps, zone on/off list
            ///query/datetime  - current datetime on pi
            //query/temps  - string containing "inside|outside" temps


            System.Diagnostics.Debug.WriteLine("Get request is: " + request.ToUpper());
            string requestUpper = request.ToUpper();
            // check if request is for sprinkler zone info
            if (requestUpper.StartsWith("/ZONE/"))
            {
                Tuple<int, string> set = ParseZoneCommand(requestUpper);
                if (set != null && set.Item1 > -1)
                {
                    switch (set.Item2)
                    {
                        case "RUNNING": // currently running or not?
                            urlFound = true;
                            bool run = sprinklerController.QueryZoneStatus(set.Item1);
                            responseMsg = run ? "ON" : "OFF";
                            break;
                        case "TIMELEFT": // number of seconds left in current run
                            urlFound = true;
                            responseMsg = "30"; // TODO
                            break;
                    }
                }
            }
            else if (requestUpper.StartsWith("/PROGRAM/"))
            {
                Tuple<int, int, string> info = ParseProgZoneCmd(requestUpper);
                //See if the request it matches any of the valid requests urls and create the response message
                if (info != null)
                {
                    switch (info.Item3)
                    {
                        case "RUNTIME": //how many seconds is this zone set to run
                            urlFound = true;
                            responseMsg = "3600"; // TODO
                            break;
                        case "ENABLED": // is this zone or program enabled
                            urlFound = true;
                            responseMsg = "True"; // TODO
                            break;
                        case "STARTTIME": // what is the program starttime
                            urlFound = true;
                            responseMsg = DateTime.Now.ToString(); // TODO
                            break;
                        case "PROGENABLED": // is this program enabled
                            urlFound = true;
                            responseMsg = "True"; // TODO
                            break;
                        case "RUNNING": // is this program running
                            urlFound = true;
                            responseMsg = "True"; // TODO
                            break;
                    }
                }
            }
            else if (requestUpper.StartsWith("/QUERY/"))
            {
                string cmd = ParseQueryCmd(requestUpper);
                if(cmd != null)
                {
                    switch(cmd)
                    {
                        case "DATETIME":
                            urlFound = true;
                            responseMsg = DateTime.Now.ToString();
                            break;
                        case "TEMPS":
                            urlFound = true;
                            responseMsg = String.Format("{0}|{1}", TemperatureSensors.InsideTemperature, TemperatureSensors.OutsideTemperature);
                            break;
                        case "STATUS":
                            urlFound = true;
                            responseMsg = MakeStatusJson();
                            break;
                    }
                }
            }
            bodyArray = Encoding.UTF8.GetBytes(responseMsg);
            await WriteResponseAsync(request.ToUpper(), responseMsg, urlFound, bodyArray, os);
        }

        private string MakeStatusJson()
        {
            try
            {
                SprinklerStatus model = new SprinklerStatus()
                {
                    InsideTemp = TemperatureSensors.InsideTemperature,
                    OutsideTemp = TemperatureSensors.OutsideTemperature,
                    CurrentTime = DateTime.Now.ToString(),
                    ZonesOn = sprinklerController.QueryAllZonesStatusAsArray(),
                    ZoneRunSecondsLeft = programController.ZoneRunSecondsLeft,
                    IsPaused = programController.IsPaused,
                    ZonePauseSecondsLeft = programController.ZonePauseSecondsLeft,
                };

                string json = Utils.SerializeJSon(model);
                return json;
            }
            catch (Exception ex)
            {
                return MakeJsonErrorMsg("Error getting sprinkler status", ex);
                // swallow throw;
            }

        }

        private string MakeJsonErrorMsg(string msg, Exception ex)
        {
            return String.Format("{\"isError\":\"true\",\"msg\":\"{0}\", \"errorMsg\":\"{1}\",\"stackTrace\":\"{2}\"}",
                msg, ex.Message, ex.ToString());
        }

        /// <summary>
        /// Parse out the program/zone/command
        /// Note that zone might be -1 if not part of the request.
        /// 
        /// example requests 
        ///    //program/[PROGRAM]/[zone]/enabled - is this zone enabled
        ///    //program/[PROGRAM]/starttime - what's the program start time
        ///
        /// </summary>
        /// <param name="requestUpper"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        private Tuple<int, int, string> ParseProgZoneCmd(string requestUpper)
        {
            try
            {
                string[] items = requestUpper.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                int prog = int.Parse(items[1]);
                string cmd = "";
                int zone = -1;
                if(items.Length == 3)
                {
                    cmd = items[2];
                }
                else
                {
                    zone = int.Parse(items[2]);
                    cmd = items[3];
                }
                return new Tuple<int, int, string>(prog, zone, cmd);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error parsing set from " + requestUpper);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                //swallow
            }

            return null;
         }

        
        /// <summary>
        /// Parse out the program command from a message
        ///   Format is something like: PROGRAM/PAUSE/60
        /// </summary>
        /// <param name="requestUpper"></param>
        /// <returns></returns>
        private Tuple<string, string> ParseProgramCommand(string requestUpper)
        {
            try
            {
                string[] items = requestUpper.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string cmd = items[1];
                string param = items[2];
                return new Tuple<string, string>(cmd, param);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error parsing set from " + requestUpper);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                //swallow
            }

            return null;
        }


        /// <summary>
        /// Parse out the zone and command from a message
        ///   Format is something like: ZONE/[zone]/running
        /// </summary>
        /// <param name="requestUpper"></param>
        /// <returns></returns>
        private Tuple<int, string> ParseZoneCommand(string requestUpper)
        {
            try
            {
                string[] items = requestUpper.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                int zone = int.Parse(items[1]);
                string cmd = items[2];
                return new Tuple<int, string>(zone, cmd);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error parsing set from " + requestUpper);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                //swallow
            }

            return null;
        }

        private string ParseQueryCmd(string requestUpper)
        {
            try
            {
                string[] items = requestUpper.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string cmd = items[1];
                return cmd;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error parsing query command from " + requestUpper);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                //swallow
            }

            return null;
        }

        //Handles all HTTP POST's
        /// <summary>
        /// 
        /// 
        /// //ZONE/[zone]/on - currently running or not?
        /// //ZONE/[zone]/off - currently running or not?
        /// //PROGRAM/PAUSE/[minutes]  pause current running program for this number of minutes
        /// //PROGRAM/STOP
        /// //PROGRAM/RUNNEXTZONE
        /// //PROGRAM/START/[program index]  run the program at index given (zero based)
        /// </summary>
        /// <param name="request"></param>
        /// <param name="os"></param>
        /// <returns></returns>
        private async Task WritePostResponseAsync(string request, IOutputStream os)
        {
            bool urlFound = false;
            byte[] bodyArray = null;
            string responseMsg = "";

            string requestUpper = request.ToUpper();
            if(requestUpper.StartsWith("/ZONE/"))
            {
                Tuple<int, string> zc = ParseZoneCommand(requestUpper);
                int zone = zc.Item1;
                string cmd = zc.Item2;
                switch(cmd)
                {
                    case "ON":
                        {
                            urlFound = true;
                            bool stat = sprinklerController.SetZoneOn(zone);
                            responseMsg = stat ? "ON" : "OFF";
                            break;
                        }
                    case "OFF":
                        {
                            urlFound = true;
                            bool stat = sprinklerController.SetZoneOff(zone);
                            responseMsg = stat ? "OFF" : "ON";
                            break;
                        }
                }
            }
            else if(requestUpper.StartsWith("/PROGRAM/"))
            {
                Tuple<string, string> zc = ParseProgramCommand(requestUpper);
                string cmd = zc.Item1;
                switch(cmd)
                {
                    case "PAUSE":
                        urlFound = true;
                        int minutes = int.Parse(zc.Item2);
                        programController.PauseProgram(minutes);
                        responseMsg = "true";
                        break;
                    case "STOP":
                        urlFound = true;
                        programController.StopProgram();
                        responseMsg = "true";
                        break;
                    case "RUNNEXTZONE":
                        urlFound = true;
                        programController.RunNextZone();
                        responseMsg = "true";
                        break;
                    case "START":
                        urlFound = true;
                        int progIndex = int.Parse(zc.Item2);
                        programController.StartProgram(progIndex);
                        responseMsg = "true";
                        break;
                }
                

            }

            bodyArray = Encoding.UTF8.GetBytes(responseMsg);
            await WriteResponseAsync(request.ToUpper(), responseMsg, urlFound, bodyArray, os);
        }

        //Write the response for unsupported HTTP methods
        private async Task WriteMethodNotSupportedResponseAsync(string request, IOutputStream os)
        {
            bool urlFound = false;
            byte[] bodyArray = null;
            bodyArray = Encoding.UTF8.GetBytes("");
            await WriteResponseAsync(request.ToUpper(), "NOT SUPPORTED", urlFound, bodyArray, os);
        }

        //Write the response for HTTP GET's and POST's 
        private async Task WriteResponseAsync(string RequestMsg, string ResponseMsg, bool urlFound, byte[] bodyArray, IOutputStream os)
        {
            //try  //The appService will die after a day or so. Let 's try catch it seperatly so the server will still return
            //{
            //    var updateMessage = new ValueSet();
            //    updateMessage.Add("Request", RequestMsg);
            //    updateMessage.Add("Response", ResponseMsg);
            //    var responseStatus = await appServiceConnection.SendMessageAsync(updateMessage);
            //}
            //catch (Exception) { }

            try
            {
                MemoryStream bodyStream = new MemoryStream(bodyArray);
                using (Stream response = os.AsStreamForWrite())
                {
                    string header = GetHeader(urlFound, bodyStream.Length.ToString());
                    byte[] headerArray = Encoding.UTF8.GetBytes(header);
                    await response.WriteAsync(headerArray, 0, headerArray.Length);
                    if (urlFound)
                        await bodyStream.CopyToAsync(response);
                    await response.FlushAsync();
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine("Error writing response: " + ex.Message);
            }
        }

        //Creates the HTTP header text for found and not found urls
        string GetHeader(bool urlFound, string bodyStreamLength)
        {
            string header;
            if (urlFound)
            {
                header = "HTTP/1.1 200 OK\r\n" +
                           "Access-Control-Allow-Origin: *\r\n" +
                           "Content-Type: text/plain\r\n" +
                           "Content-Length: " + bodyStreamLength + "\r\n" +
                           "Connection: close\r\n\r\n";
            }
            else
            {
                header = "HTTP/1.1 404 Not Found\r\n" +
                         "Access-Control-Allow-Origin: *\r\n" +
                         "Content-Type: text/plain\r\n" +
                         "Content-Length: 0\r\n" +
                         "Connection close\r\n\r\n";
            }
            return header;
        }
    }

}
