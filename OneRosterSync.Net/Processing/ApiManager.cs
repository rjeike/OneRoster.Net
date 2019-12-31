using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OneRosterSync.Net.Extensions;
using OneRosterSync.Net.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Serialization;
using OneRosterSync.Net.Authentication;

namespace OneRosterSync.Net.Processing
{
    public class ApiManager : IDisposable
    {

		public IApiAuthenticator ApiAuthenticator { get; set; }

        private readonly ILogger Logger = ApplicationLogging.Factory.CreateLogger<ApiManager>();

        private readonly HttpClient client;


        public ApiManager(string endpoint, IApiAuthenticator apiAuthenticator = null)
        {
	        ApiAuthenticator = apiAuthenticator;

            client = new HttpClient
            {
                BaseAddress = new Uri(endpoint)
            };
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
        }

        public void Dispose()
        {
            client.Dispose();
        }

        public async Task<ApiResponse> Post(string entityEndpoint, object data) 
        {
            try
            {
				// create post data
	            var serializerSettings = new JsonSerializerSettings
	            {
		            ContractResolver = new CamelCasePropertyNamesContractResolver()
	            };

	            var json = JsonConvert.SerializeObject(data, serializerSettings);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

				// Putting json content to formdata since this is how Moodle accepts data. Later we'll make this configurable
	            IList<KeyValuePair<string, string>> nameValueCollection = new List<KeyValuePair<string, string>> {
		            new KeyValuePair<string, string>("payload", json)
	            };

				// add authentication
				ApiAuthenticator?.AddAuthentication(client);

	            // submit request
                //Logger.Here().LogInformation($"Posting\n {json}");

				// PostAsync ignores any query string present in the baseAddress hence we need to add it back.
				// Not sure if there is more elegant way to do this. I might later switch to some other http library.

	            var response = await client.PostAsync(BuildEndpoint(client.BaseAddress, entityEndpoint), 
		            new FormUrlEncodedContent(nameValueCollection));

                // retrieve response
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();
                //Logger.Here().LogInformation($"Response: {response.StatusCode}\n {responseBody}");

				Logger.Here().LogInformation($"SEND >> {json} {System.Environment.NewLine}RECEIVED << {responseBody}{System.Environment.NewLine}");

                // parse response
                ApiResponse result = JsonConvert.DeserializeObject<ApiResponse>(responseBody);
                if (!result.Success && string.IsNullOrEmpty(result.ErrorMessage))
                {
                    result.ErrorMessage = responseBody;
                }
                return result;
            }
            catch (Exception ex)
            {
                string message = $"Error communicating with LMS\n {ex.Message}";
                Logger.Here().LogInformation(message);
                return new ApiResponse { Success = false, ErrorMessage = message };
            }
        }

	    private static string BuildEndpoint(Uri baseUrl, string entityEndpoint)
	    {
			// TODO: Replace this junk by https://flurl.io/

			var uriBuilder = new UriBuilder(baseUrl);

			// If the entity end point is just a query string
		    if (entityEndpoint.Trim().StartsWith('?'))
		    {
			    var query = HttpUtility.ParseQueryString(uriBuilder.Query);
				foreach (var kvp in entityEndpoint.Replace("?", "").Split("&"))
				{
					var splits = kvp.Split("=");

					if (splits.Length >= 1)
						query.Add(splits[0], splits[1]);
				}

			    uriBuilder.Query = query.ToString();
		    }
		    else
		    {
			    uriBuilder.Path += entityEndpoint;
		    }

		    return uriBuilder.ToString();

	    }
    }
}