using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IC.Net {
    public sealed class OnlineService : IDisposable {
        readonly HttpClient http = new HttpClient(new HttpClientHandler {AllowAutoRedirect=false});
        public Uri Endpoint {get;}
        public OnlineService(string endpoint) {
            Endpoint=new Uri(endpoint.Trim().TrimEnd('/')+"/");
            if(!string.IsNullOrEmpty(Endpoint.UserInfo)||!string.IsNullOrEmpty(Endpoint.Query)||!string.IsNullOrEmpty(Endpoint.Fragment)||Endpoint.AbsolutePath!="/"
                ||(Endpoint.Scheme!="https" && !(Endpoint.Scheme=="http"&&Endpoint.IsLoopback)))
                throw new ArgumentException("Use an HTTPS server address, or HTTP on localhost for testing.");
            http.Timeout=TimeSpan.FromSeconds(12);
        }
        public async Task<Dictionary<string,object?>> Request(string method,string path,Dictionary<string,object?> data=null,RankedIdentity identity=null,CancellationToken ct=default) {
            var uri=new Uri(Endpoint,path);var body=data==null?"":JsonLite.Stringify(data);
            using(var req=new HttpRequestMessage(new HttpMethod(method),uri)) {
                if(data!=null)req.Content=new StringContent(body,Encoding.UTF8,"application/json");
                if(identity!=null)foreach(var h in identity.Sign(method,uri,body))req.Headers.Add(h.Key,h.Value);
                using(var response=await http.SendAsync(req,ct).ConfigureAwait(false)) {
                    var text=await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var result=JsonLite.ParseObject(text);
                    if(!response.IsSuccessStatusCode)throw new InvalidOperationException(result.TryGetValue("error",out var e)?e?.ToString():"Server refused the request");
                    return result;
                }
            }
        }
        public Uri Socket(string room,string seat) {
            var b=new UriBuilder(Endpoint){Scheme=Endpoint.Scheme=="https"?"wss":"ws",Path="/rooms/"+Uri.EscapeDataString(room)+"/ws",Query="dataVersion=lab-2&seat="+Uri.EscapeDataString(seat)};
            return b.Uri;
        }
        public void Dispose()=>http.Dispose();
    }
}
