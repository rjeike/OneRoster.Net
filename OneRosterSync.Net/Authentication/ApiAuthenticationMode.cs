namespace OneRosterSync.Net.Authentication
{
	/// <summary>
	/// Determines the type of authentication to use for the Target System
	/// </summary>
	public enum ApiAuthenticationMode
	{
		/// <summary>
		/// A simple Api key based authentication mode where the api key parameter is appended to the
		/// target system api endpoint.
		/// </summary>
		ApiKey,

		/// <summary>
		/// Basic Http Authentication.
		/// See: https://en.wikipedia.org/wiki/Basic_access_authentication
		/// </summary>
		HttpBasicAuthentication,

		/// <summary>
		/// No Api Authentication.
		/// </summary>
		None
	}
}