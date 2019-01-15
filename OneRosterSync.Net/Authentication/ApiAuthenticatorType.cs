namespace OneRosterSync.Net.Authentication
{
	/// <summary>
	/// Determines the type of authentication to use for the Target System
	/// </summary>
	public enum ApiAuthenticatorType
	{
		/// <summary>
		/// No Api Authentication.
		/// </summary>
		None = 0,

		/// <summary>
		/// A simple Api key based authentication mode where the api key parameter is appended to the
		/// target system api endpoint.
		/// </summary>
		ApiKey = 1,

		/// <summary>
		/// Basic Http Authentication. (Not implemented)
		/// See: https://en.wikipedia.org/wiki/Basic_access_authentication
		/// </summary>
		HttpBasic = 2
	}
}