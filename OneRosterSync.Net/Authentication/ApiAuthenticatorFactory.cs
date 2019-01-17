using System;

namespace OneRosterSync.Net.Authentication
{
	public class ApiAuthenticatorFactory
	{
		public static IApiAuthenticator GetApiAuthenticator(ApiAuthenticatorType apiAuthenticatorType, string jsonAuthData)
		{
			switch (apiAuthenticatorType)
			{
				case ApiAuthenticatorType.None:
					return null;
				case ApiAuthenticatorType.ApiKey:
					var authDataObj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(jsonAuthData);
					return new ApiKeyAuthenticator(authDataObj.apiKey.ToString(), authDataObj.apiKeyParameterName.ToString());
				case ApiAuthenticatorType.HttpBasic:
					return new HttpBasicAuthenticator();
				default:
					throw new ArgumentOutOfRangeException(nameof(apiAuthenticatorType), apiAuthenticatorType, null);
			}
		}
	}
}