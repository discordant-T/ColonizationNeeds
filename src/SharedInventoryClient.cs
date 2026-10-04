using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

public sealed class SharedInventoryClient : IDisposable
{
    public sealed class Change
    {
        public string requestId, commodity, operation, source;
        public long amount, expectedVersion;
    }
    public string Url { get; private set; }
    public string Token { get; private set; }
    public string Group { get; private set; }
    public string Role { get; private set; }
    public bool Ready { get; private set; }
    public bool Enabled { get; private set; }
    public bool Blocked { get; private set; }
    public string Warning { get; private set; }
    public Dictionary<string,long> Stock = new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string,long> Versions = new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
    readonly string folder, queuePath;
    readonly HttpClient http;
    readonly JavaScriptSerializer json = new JavaScriptSerializer();
    List<Change> queue = new List<Change>();
    public int Pending { get { return queue.Count; } }
    public SharedInventoryClient(string folder, string url, string token, bool enabled, HttpMessageHandler handler = null)
    {
        http=handler==null?new HttpClient():new HttpClient(handler,false); http.Timeout=TimeSpan.FromSeconds(12);
        this.folder=folder; Url=ValidateUrl(url); Token=token; Enabled=enabled; Warning="";
        if (token.Contains("\r") || token.Contains("\n")) throw new ArgumentException("Access token must be a single line.");
        using(var hash=SHA256.Create()) queuePath=Path.Combine(folder,"shared-outbox-"+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Url+"\n"+Token))).Replace("-","")+".json");
        if(File.Exists(queuePath)) queue=json.Deserialize<List<Change>>(File.ReadAllText(queuePath));
        if(queue==null) throw new IOException("Shared outbox could not be read.");
    }
    public static string ValidateUrl(string value)
    {
        Uri uri;
        if(!Uri.TryCreate(value.Trim(),UriKind.Absolute,out uri) || uri.Scheme!="https" || uri.UserInfo.Length>0 || uri.Query.Length>0 || uri.Fragment.Length>0 || uri.AbsolutePath!="/") throw new ArgumentException("Enter an HTTPS server URL without a path, query or password.");
        return uri.GetLeftPart(UriPartial.Authority)+"/";
    }
    static string Protect(string text) { return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(text),null,DataProtectionScope.CurrentUser)); }
    static string Unprotect(string text) { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(text),null,DataProtectionScope.CurrentUser)); }
    static void Write(string path,string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path+".tmp",text);
        if(File.Exists(path)) File.Replace(path+".tmp",path,null); else File.Move(path+".tmp",path);
    }
    public void SaveSettings()
    {
        if(Enabled && String.IsNullOrWhiteSpace(Token)) throw new ArgumentException("A player access token is required for shared mode.");
        Write(Path.Combine(folder,"shared-settings.json"),json.Serialize(new Dictionary<string,string>{{"url",Url},{"token",Token.Length==0?"":Protect(Token)},{"enabled",Enabled.ToString()}}));
    }
    public static SharedInventoryClient Load(string folder)
    {
        string path=Path.Combine(folder,"shared-settings.json");
        if(!File.Exists(path)) return new SharedInventoryClient(folder,"https://colonizationneeds-api.macytr.workers.dev/","",false);
        var saved=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(path));
        return new SharedInventoryClient(folder,saved["url"],saved["token"].Length==0?"":Unprotect(saved["token"]),saved["enabled"]=="True");
    }
    public void Enqueue(IEnumerable<Change> changes)
    {
        var next=new List<Change>(queue);
        foreach(var change in changes)
        {
            change.commodity=JournalCargoTracker.Canonical(change.commodity);
            if(change.amount<0 || change.amount>1000000000) throw new ArgumentException("Shared quantities must be between 0 and 1,000,000,000 tonnes.");
            change.requestId=Guid.NewGuid().ToString("N"); next.Add(change);
        }
        Write(queuePath,json.Serialize(next)); queue=next;
    }
    public bool CancelBlockedManual()
    {
        if(!Blocked || queue.Count==0 || queue[0].source!="Manual") return false;
        var next=queue.Skip(1).ToList(); Write(queuePath,json.Serialize(next)); queue=next; Blocked=false; return true;
    }
    public async Task<Dictionary<string,object>> Request(string path,object payload=null)
    {
        using(var request=new HttpRequestMessage(payload==null?HttpMethod.Get:HttpMethod.Post,Url+path))
        {
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",Token);
            if(payload!=null) request.Content=new StringContent(json.Serialize(payload),Encoding.UTF8,"application/json");
            using(var response=await http.SendAsync(request))
            {
                string text=await response.Content.ReadAsStringAsync();
                var data=json.DeserializeObject(text) as Dictionary<string,object>;
                if(!response.IsSuccessStatusCode)
                {
                    if((int)response.StatusCode==409) Blocked=true;
                    throw new IOException(data!=null && data.ContainsKey("error")?Convert.ToString(data["error"]):"Server returned HTTP "+(int)response.StatusCode);
                }
                if(data==null) throw new IOException("Unexpected shared server response.");
                return data;
            }
        }
    }
    public async Task Sync()
    {
        while(queue.Count>0 && !Blocked)
        {
            var sent=queue[0]; var response=await Request("transactions",sent);
            if(response.ContainsKey("shortfall") && Convert.ToBoolean(response["shortfall"])) Warning="Stock shortfall; reconcile manually.";
            // Remove only after acknowledgment; retries retain their stable request ID.
            var next=queue.Skip(1).ToList(); Write(queuePath,json.Serialize(next)); queue=next;
        }
        var snapshot=await Request("inventory");
        var stock=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase); var versions=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in (object[])snapshot["inventory"])
        {
            var row=(Dictionary<string,object>)item; string key=JournalCargoTracker.Canonical(Convert.ToString(row["commodity"]));
            stock[key]=Convert.ToInt64(row["quantity"]); versions[key]=Convert.ToInt64(row["version"]);
        }
        Stock=stock; Versions=versions; Group=Convert.ToString(snapshot["groupId"]); Role=Convert.ToString(snapshot["role"]); Ready=true;
    }
    public void Dispose() { http.Dispose(); }
}
