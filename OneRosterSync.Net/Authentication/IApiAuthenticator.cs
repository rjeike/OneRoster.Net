using System.Net.Http;

namespace OneRosterSync.Net.Authentication
{
    public interface IApiAuthenticator
    {
		/// <summary>
		/// Modify the client so that default authorization header or api query key parameters are
		/// added to the outgoing request.
		/// </summary>
		/// <param name="client"></param>
	    void AddAuthentication(HttpClient client);
    }
}
