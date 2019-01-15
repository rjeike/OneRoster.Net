using System;
using System.Net.Http;
using System.Web;

namespace OneRosterSync.Net.Authentication
{
	public class ApiKeyAuthenticator: IApiAuthenticator
	{
		private readonly string apiKey;
		private readonly string apiKeyParameterName;

		public ApiKeyAuthenticator(string apiKey, string apiKeyParameterName = "apiKey")
		{
			this.apiKey = apiKey;
			this.apiKeyParameterName = apiKeyParameterName;
		}

		public void AddAuthentication(HttpClient client)
		{
			var uriBuilder = new UriBuilder(client.BaseAddress);
			var query = HttpUtility.ParseQueryString(client.BaseAddress.Query);
			query.Add(apiKeyParameterName, apiKey);

			uriBuilder.Query = query.ToString();
			client.BaseAddress = uriBuilder.Uri;
		}
	}
}