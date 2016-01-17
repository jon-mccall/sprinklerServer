using SprinklerWebServer.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace SprinklerWebServer
{
    public static class Utils
    {
        //public static string SerializeJSon<T>(T t)
        //{
        //    try
        //    {
        //        MemoryStream stream = new MemoryStream();
        //        DataContractJsonSerializer ds = new DataContractJsonSerializer(typeof(T));
        //        DataContractJsonSerializerSettings s = new DataContractJsonSerializerSettings();
        //        ds.WriteObject(stream, t);
        //        string jsonString = Encoding.UTF8.GetString(stream.ToArray());
        //        //stream.Close();
        //        return jsonString;
        //    }
        //    catch (Exception ex)
        //    {
        //        return ex.ToString();
        //        //throw;
        //    }
        //}

        
        public static string SerializeJSonSprinkProg(SprinklerProgram t)
        {
            try
            {
                MemoryStream stream = new MemoryStream();
                DataContractJsonSerializer ds = new DataContractJsonSerializer(typeof(SprinklerProgram));
                DataContractJsonSerializerSettings s = new DataContractJsonSerializerSettings();
                ds.WriteObject(stream, t);
                string jsonString = Encoding.UTF8.GetString(stream.ToArray());
                //stream.Close();
                return jsonString;
            }
            catch (Exception ex)
            {
                return ex.ToString();
                //throw;
            }
        }

        public static string SerializeJSon(SprinklerStatus t)
        {
            try
            {
                MemoryStream stream = new MemoryStream();
                DataContractJsonSerializer ds = new DataContractJsonSerializer(typeof(SprinklerStatus));
                DataContractJsonSerializerSettings s = new DataContractJsonSerializerSettings();
                ds.WriteObject(stream, t);
                string jsonString = Encoding.UTF8.GetString(stream.ToArray());
                //stream.Close();
                return jsonString;
            }
            catch (Exception ex)
            {
                return ex.ToString();
                //throw;
            }
        }

        internal static string SerializeJSonZoneList(List<Zone> zones)
        {
            try
            {
                MemoryStream stream = new MemoryStream();
                DataContractJsonSerializer ds = new DataContractJsonSerializer(typeof(List<Zone>));
                DataContractJsonSerializerSettings s = new DataContractJsonSerializerSettings();
                ds.WriteObject(stream, zones);
                string jsonString = Encoding.UTF8.GetString(stream.ToArray());
                //stream.Close();
                return jsonString;
            }
            catch (Exception ex)
            {
                return ex.ToString();
                //throw;
            }
        }

        internal static List<Zone> DeserializeJsonZoneList(string json)
        {
            try
            {

                byte[] arrayOfMyString = Encoding.UTF8.GetBytes(json);
                MemoryStream stream = new MemoryStream(arrayOfMyString);
                DataContractJsonSerializer ds = new DataContractJsonSerializer(typeof(List<Zone>));
                List<Zone> list = (List < Zone > )ds.ReadObject(stream);

                return list;
            }
            catch (Exception ex)
            {
                throw;
            }
        }


        //public static void SaveStringToLocalFile(string filename, string content)
        //{
        //    // saves the string 'content' to a file 'filename' in the app's local storage folder
        //    byte[] fileBytes = System.Text.Encoding.UTF8.GetBytes(content.ToCharArray());

        //    // create a file with the given filename in the local folder; replace any existing file with the same name
        //    StorageFile file = Windows.Storage.ApplicationData.Current.LocalFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting).GetResults();

        //    // write the char array created from the content string into the file
        //    using (var stream = file.OpenStreamForWriteAsync();
        //    {
        //        stream.Write(fileBytes, 0, fileBytes.Length);
        //    }
        //}

        //public static async Task<string> ReadStringFromLocalFile(string filename)
        //{
        //    // reads the contents of file 'filename' in the app's local storage folder and returns it as a string

        //    // access the local folder
        //    StorageFolder local = Windows.Storage.ApplicationData.Current.LocalFolder;
        //    // open the file 'filename' for reading
        //    Stream stream = await local.OpenStreamForReadAsync(filename);
        //    string text;

        //    // copy the file contents into the string 'text'
        //    using (StreamReader reader = new StreamReader(stream))
        //    {
        //        text = reader.ReadToEnd();
        //    }

        //    return text;
        //}
    }
}
