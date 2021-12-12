#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace OSFMServerBanlogBot
{
    public static class JsonManager
    {
        public static void SerializeJson(string path, object thingToSerialize)
        {
            try
            {
                // create the directory provided in path if it doesn't exist
                string fullDirectoryName = new FileInfo(path).Directory.FullName;
                if (!Directory.Exists(fullDirectoryName))
                    Directory.CreateDirectory(fullDirectoryName);

                // then write the result of JsonConvert.SerializeObject to the path
                string json = JsonConvert.SerializeObject(thingToSerialize, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                ExceptionLogger.LogException(ex);
            }
        }

        public static object? DeserializeJson<T>(string path)
        {
            if (!File.Exists(path))
                // just return null and pray i handle it later
                return null;

            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
        }
    }
}
