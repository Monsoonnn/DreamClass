using System.Collections.Generic;

namespace DreamClass.Network {
    public class ApiRequest {
        public string Endpoint;
        public string Method;
        public string Body;
        public Dictionary<string, string> Headers;

        public ApiRequest( string endpoint, string method, string body = "", Dictionary<string, string> headers = null ) {
            Endpoint = endpoint;
            Method = method;
            Body = body;
            Headers = headers;
        }
    }
}
