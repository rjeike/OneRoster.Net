using System.Net.Http;

namespace OneRosterSync.Net.Authentication
{
    public interface IApiAuthenticator
    {
	    void AddAuthentication(HttpClient client);
    }
}
