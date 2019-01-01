using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OneRosterSync.Net.Utils
{
    public static class JsonUtilities
    {
        public static string FormatJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            dynamic parsedJson = JsonConvert.DeserializeObject(json);
            return JsonConvert.SerializeObject(parsedJson, Formatting.Indented);
        }
    }
}
