using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace SprinklerWebServer
{
    //Class to define the HTTP WebServer
    public sealed class WebServer : IDisposable
    {
        String sMyWebServerRoot = "C:\\MyWebServerRoot\\";
        //Create a buffer to read HTTP data
        private const uint BufferSize = 8192;
        //Port to listen on
        private int port = 8889;
        //Listener to
        private readonly StreamSocketListener listener;
        Dictionary<string, string> mimeTypeMap = new Dictionary<string, string>();

        public WebServer(int serverPort)
        {
            listener = new StreamSocketListener();
            port = serverPort;

            mimeTypeMap.Add(".htm",  "text/html" );
            mimeTypeMap.Add(".html", "text/html" );
            mimeTypeMap.Add(".asp",  "text/html" );
            mimeTypeMap.Add(".css", "text/css");
            mimeTypeMap.Add(".js", "text/javascript");
            mimeTypeMap.Add(".bmp", "image/bmp");
            mimeTypeMap.Add(".jpg", "image/jpeg");
            mimeTypeMap.Add(".png", "image/png");

            //Add event handler for HTTP connections
            listener.ConnectionReceived += (s, e) => ProcessRequestAsync(e.Socket);

            System.Diagnostics.Debug.WriteLine("Sprinkler Web server starting...");
            System.Diagnostics.Debug.WriteLine("Current dir: "+ Directory.GetCurrentDirectory());

            sMyWebServerRoot = String.Format(@"{0}\web", Directory.GetCurrentDirectory());
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
            string name = "";
            try
            {
                //System.Diagnostics.Debug.WriteLine("ProcessRequestAsync called...");
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
                        //System.Diagnostics.Debug.WriteLine(requestMethod);
                        //Beakup the request into it parts
                        requestMethod = requestMethod.Split('\n')[0];
                        requestParts = requestMethod.Split(' ');
                    }
                    name = requestMethod;
                    //We only respond HTTP GETS and POST methods
                    if (requestParts[0] == "GET")
                    {
                        await HandleGetRequest(requestMethod, requestParts, output);
                    }
                    //if (requestParts[0] == "GET")
                    //    await WriteGetResponseAsync(requestParts[1], output);
                    //else if (requestParts[0] == "POST")
                    //    await WritePostResponseAsync(requestParts[1], output);
                    else
                    {
                        string msg = "<H2>Error!! Method " + requestParts[0] + "not supported.</H2><Br>";
                        //sErrorMessage = sErrorMessage + "Please check data\\Vdirs.Dat";

                        //Format The Message
                        byte[] header = MakeReponseHeader("HTTP/1.1", "", msg.Length, "");

                        //Send to the browser
                        await SendToBrowser(msg, header, output);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error ("+ name + "): " + ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);

            }
            finally
            {
                //System.Diagnostics.Debug.WriteLine("ProcessRequestAsync Finished! " + name);

            }
        }




        private async Task HandleGetRequest(string requestMethod, string[] requestParts, IOutputStream output)
        {
            // Look for HTTP request
            int iStartPos = requestMethod.IndexOf("HTTP", 1);


            // Get the HTTP text and version e.g. it will return "HTTP/1.1"
            string sHttpVersion = requestMethod.Substring(iStartPos, 8);

            string sRequestedFile = requestParts[1];

            /////////////////////////////////////////////////////////////////////
            // Identify the Physical Directory
            /////////////////////////////////////////////////////////////////////
            if (sRequestedFile == "/")
            {
                sRequestedFile += "index.html";
            }

            string sfile = sRequestedFile.Replace('/', '\\');
            //why path.combine not working????
            //string sPhysicalFilePath = Path.Combine(sMyWebServerRoot, sfile);
            string sPhysicalFilePath = String.Format("{0}{1}", sMyWebServerRoot, sfile);
            // some requests have ?_12341234 attached, remove that
            if(sPhysicalFilePath.Contains("?"))
            {
                sPhysicalFilePath = sPhysicalFilePath.Substring(0, sPhysicalFilePath.IndexOf('?'));
            }

            //System.Diagnostics.Debug.WriteLine("File Requested : " + sRequestedFile);

            //If the physical directory does not exists then
            // dispaly the error message
            string sErrorMessage = null;
            if (!File.Exists(sPhysicalFilePath))
            {
                sErrorMessage = "<H2>Error!! Requested Resource does not exists</H2><Br>";
                //sErrorMessage = sErrorMessage + "Please check data\\Vdirs.Dat";

                //Format The Message
                byte[] header = MakeReponseHeader(sHttpVersion, "", sErrorMessage.Length, " 404 Not Found");

                //Send to the browser
                await SendToBrowser(sErrorMessage, header, output);

            }
            else
            {
                // Get TheMime Type
                String sMimeType = GetMimeType(sRequestedFile);

                int iTotBytes = 0;

                string sResponse = "";

                FileStream fs = new FileStream(sPhysicalFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                // Create a reader that can read bytes from the FileStream.


                BinaryReader reader = new BinaryReader(fs);
                byte[] bytes = new byte[fs.Length];
                int read;
                while ((read = reader.Read(bytes, 0, bytes.Length)) != 0)
                {
                    // Read from the file and write the data to the network
                    sResponse = sResponse + Encoding.ASCII.GetString(bytes, 0, read);

                    iTotBytes = iTotBytes + read;

                }
                reader.Dispose();
                fs.Dispose();

                //System.Diagnostics.Debug.WriteLine("Read {0} bytes from {1}", iTotBytes, sRequestedFile);

                byte[] header = MakeReponseHeader(sHttpVersion, sMimeType, iTotBytes, " 200 OK");
                await SendToBrowser(bytes, header, output);
            }
        }

        /// <summary>
        /// This function takes FileName as Input and returns the mime type..
        /// </summary>
        /// <param name="sRequestedFile">To indentify the Mime Type</param>
        /// <returns>Mime Type</returns>
        public string GetMimeType(string sRequestedFile)
        {
            string ext = Path.GetExtension(sRequestedFile).ToLower();
            if (mimeTypeMap.ContainsKey(ext))
            {
                return mimeTypeMap[ext];
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("UNKNOWN MIME TYPE: " + ext);
                return mimeTypeMap[".html"];
            }
        }



        /// <summary>
        /// This function send the Header Information to the client (Browser)
        /// </summary>
        /// <param name="sHttpVersion">HTTP Version</param>
        /// <param name="sMIMEHeader">Mime Type</param>
        /// <param name="iTotBytes">Total Bytes to be sent in the body</param>
        /// <param name="mySocket">Socket reference</param>
        /// <returns></returns>
        public byte[] MakeReponseHeader(string sHttpVersion, string sMIMEHeader, int iTotBytes, string sStatusCode)
        {

            StringBuilder sBuffer = new StringBuilder();

            // if Mime type is not provided set default to text/html
            if (sMIMEHeader.Length == 0)
            {
                sMIMEHeader = "text/html";  // Default Mime Type is text/html
            }

            sBuffer.Append(sHttpVersion);
            sBuffer.Append(sStatusCode);
            sBuffer.Append("\r\nServer: cx1193719-b\r\nContent-Type: ");
            sBuffer.Append(sMIMEHeader);
            sBuffer.Append("\r\nAccept-Ranges: bytes\r\nContent-Length: ");
            sBuffer.Append(iTotBytes);
            sBuffer.Append("\r\nConnection: close\r\n\r\n");


            //sBuffer = sBuffer + sHttpVersion + sStatusCode + "\r\n";
            //sBuffer = sBuffer + "Server: cx1193719-b\r\n";
            //sBuffer = sBuffer + "Content-Type: " + sMIMEHeader + "\r\n";
            //sBuffer = sBuffer + "Accept-Ranges: bytes\r\n";
            //sBuffer = sBuffer + "Content-Length: " + iTotBytes + "\r\n";
            //sBuffer = sBuffer + "Connection: close\r\n\r\n";

            //System.Diagnostics.Debug.WriteLine("Reponse header: " + sBuffer);

            Byte[] bSendData = Encoding.ASCII.GetBytes(sBuffer.ToString());
            return bSendData;

        }

        /// <summary>
        /// Overloaded Function, takes string, convert to bytes and calls 
        /// overloaded sendToBrowserFunction.
        /// </summary>
        /// <param name="sData">The data to be sent to the browser(client)</param>
        /// <param name="mySocket">Socket reference</param>
        private async Task SendToBrowser(String sData, [ReadOnlyArray()] byte[] header, IOutputStream os)
        {
            await SendToBrowser(Encoding.ASCII.GetBytes(sData), header, os);
        }

        /// <summary>
        /// Sends data to the browser (client)
        /// </summary>
        /// <param name="bSendData">Byte Array</param>
        /// <param name="mySocket">Socket reference</param>
        private async Task SendToBrowser([ReadOnlyArray()] byte[] bSendData, [ReadOnlyArray()]byte[] header, IOutputStream os)
        {
            try
            {
                MemoryStream bodyStream = new MemoryStream(bSendData);
                using (Stream response = os.AsStreamForWrite())
                {

                    await response.WriteAsync(header, 0, header.Length);
                    await bodyStream.CopyToAsync(response);
                    await response.FlushAsync();
                    //System.Diagnostics.Debug.WriteLine("No. of bytes send {0}", bSendData.Length);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error writing response: " + ex.Message);
            }

        }


    }
}
