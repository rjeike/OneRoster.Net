using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace OneRosterSync.Net.Processing
{
    public class ApiManager
    {
        HttpClient client;

        public ApiManager()
        {
            // "https://localhost:44312/api/MockApi/update";

            client = new HttpClient();
            client.BaseAddress = new Uri("https://localhost:44312/api/mockapi/");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
        }

        public async Task<string> Update(object o)
        {
            string jsonX = Newtonsoft.Json.JsonConvert.SerializeObject(o);
            string json = $"\"{jsonX}\"";
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync("update", content);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            //return response.RequestMessage + "\n" + response.StatusCode;
            return responseBody;
        }
    }
}
