using System;
using System.Net.Http;

namespace OneRosterSync.Net.Authentication
{
    public class HttpBasicAuthenticator : IApiAuthenticator
    {
	    public void AddAuthentication(HttpClient client)
	    {
		    throw new NotImplementedException();
	    }
    }
}
