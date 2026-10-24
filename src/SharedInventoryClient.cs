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
        public string requestId, commodity, operation, source, carrierId, observedAt;
        public long amount, expectedVersion;
        public TransitLedger.Event transit;
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
    public Dictionary<string,long> CarrierBalances = new Dictionary<string,long>();
    public Dictionary<string,long> TransitBalances = new Dictionary<string,long>();
    public Dictionary<string,long> InboundBalances = new Dictionary<string,long>();
    public bool InboundReady { get; private set; }
    public bool TransitReady { get; private set; }
    public Dictionary<string,long> CarrierVersions = new Dictionary<string,long>();
    public List<CarrierInventory.Carrier> Carriers=new List<CarrierInventory.Carrier>();
    public long CarrierRegistryVersion { get; private set; }
    public bool RegistryReady { get; private set; }
    public Dictionary<string,long> CarrierStock(string carrier)
    {
        var result=new Dictionary<string,long>();
        foreach(var p in CarrierBalances) { var parts=p.Key.Split('|'); if(carrier.Length>0 && parts[0]!=carrier) continue; long n; result.TryGetValue(parts[1],out n); result[parts[1]]=checked(n+p.Value); }
        return result;
    }
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
            if(String.IsNullOrEmpty(change.requestId)) change.requestId=Guid.NewGuid().ToString("N");
            var existing=next.SingleOrDefault(x=>x.requestId==change.requestId);
            if(existing!=null && (existing.carrierId!=change.carrierId || existing.commodity!=change.commodity || existing.operation!=change.operation || existing.amount!=change.amount || existing.source!=change.source || existing.observedAt!=change.observedAt || json.Serialize(existing.transit)!=json.Serialize(change.transit))) throw new IOException("Journal event identity collision; review the original event.");
            if(existing==null) next.Add(change);
        }
        Write(queuePath,json.Serialize(next)); queue=next;
    }
    public static List<Change> ReplacementChanges(Dictionary<string,long> local, Dictionary<string,long> stock, Dictionary<string,long> versions)
    {
        var normalized=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
        foreach(var pair in local)
        {
            string key=JournalCargoTracker.Canonical(pair.Key);
            if(normalized.ContainsKey(key)) throw new ArgumentException("Local inventory contains duplicate aliases for "+key+". Correct it before syncing.");
            if(pair.Value<0 || pair.Value>1000000000) throw new ArgumentException("Shared quantities must be between 0 and 1,000,000,000 tonnes.");
            normalized[key]=pair.Value;
        }
        var changes=new List<Change>();
        foreach(var key in normalized.Keys.Union(stock.Keys,StringComparer.OrdinalIgnoreCase).OrderBy(x=>x))
        {
            long target,previous,version; normalized.TryGetValue(key,out target); stock.TryGetValue(key,out previous); versions.TryGetValue(key,out version);
            if(target!=previous) changes.Add(new Change { commodity=key, amount=target, operation="set", source="Manual", expectedVersion=version });
        }
        return changes;
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
                    var change=payload as Change; int code=(int)response.StatusCode;
                    if(code==409 || (change!=null && change.source=="Manual" && (code==400 || code==403))) Blocked=true;
                    throw new IOException(data!=null && data.ContainsKey("error")?Convert.ToString(data["error"]):"Server returned HTTP "+(int)response.StatusCode);
                }
                if(data==null) throw new IOException("Unexpected shared server response.");
                return data;
            }
        }
    }
    public async Task MigrateLegacy(string carrier,string commodity,long amount)
    {
        if(Pending>0 || !Ready) throw new IOException("Sync pending transactions first.");
        long version; CarrierVersions.TryGetValue(CarrierInventory.Key("legacy",commodity),out version);
        Enqueue(new[] { new Change { carrierId=carrier,commodity=commodity,amount=amount,operation="migrate",source="Manual",expectedVersion=version } });
        await Sync();
    }
    public async Task LoadCarriers()
    {
        var response=await Request("v2/carriers");
        Carriers=new JavaScriptSerializer().Deserialize<List<CarrierInventory.Carrier>>(json.Serialize(response["carriers"]));
        if(response.ContainsKey("role")) Role=Convert.ToString(response["role"]);
        if(response.ContainsKey("groupId")) Group=Convert.ToString(response["groupId"]);
        CarrierRegistryVersion=Convert.ToInt64(response["version"]); RegistryReady=true;
        if(Blocked && queue.Count>0 && (queue[0].source=="Market"||queue[0].source=="Transfer") && Carriers.Any(x=>x.id==queue[0].carrierId)) Blocked=false;
    }
    public async Task SaveCarriers(List<CarrierInventory.Carrier> carriers)
    {
        if(!RegistryReady || Role!="admin") throw new IOException("Connect as a shared group admin before changing carrier configuration.");
        if(queue.Any(x=>!String.IsNullOrEmpty(x.carrierId)&&x.carrierId!="legacy"&&!carriers.Any(c=>c.id==x.carrierId))) throw new IOException("Keep the carrier IDs referenced by this profile's pending stock movements.");
        var payload=new { requestId=Guid.NewGuid().ToString("N"),expectedVersion=CarrierRegistryVersion,carriers=carriers };
        bool blockedBefore=Blocked;
        Exception failure=null;
        try { await Request("v2/carriers",payload); }
        catch(Exception ex) { failure=ex; }
        Blocked=blockedBefore; // Configuration conflicts must not poison the inventory queue.
        await LoadCarriers();
        if(failure!=null && (Carriers.Count!=carriers.Count || carriers.Any(c=>!Carriers.Any(x=>x.id==c.id && x.name==c.name && x.role==c.role && (x.callsign??"")==(c.callsign??"") && x.verified==c.verified)))) throw new IOException("Shared carrier configuration was not confirmed: "+failure.Message,failure);
    }
    string snapshotRevision = "";
    public async Task Sync()
    {
        while(queue.Count>0 && !Blocked)
        {
            var sent=queue[0]; if(String.IsNullOrEmpty(sent.carrierId)) sent.carrierId="legacy";
            var response=await Request(sent.source=="Transit"?"v2/transit":"v2/transactions",sent);
            if(response.ContainsKey("shortfall") && Convert.ToBoolean(response["shortfall"])) Warning="Stock shortfall; reconcile manually.";
            // Remove only after acknowledgment; retries retain their stable request ID.
            var next=queue.Skip(1).ToList(); Write(queuePath,json.Serialize(next)); queue=next;
        }
        var snapshot=await Request("v2/inventory"+(snapshotRevision.Length==0?"":"?since="+Uri.EscapeDataString(snapshotRevision)));
        if(snapshot.ContainsKey("unchanged") && Convert.ToBoolean(snapshot["unchanged"]) && Ready) return;
        if(!snapshot.ContainsKey("schema") || Convert.ToInt32(snapshot["schema"])!=2) throw new IOException("Carrier backend v2 must be deployed before shared carrier tracking can be used.");
        var stock=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase); var versions=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
        var balances=new Dictionary<string,long>(); var carrierVersions=new Dictionary<string,long>();
        foreach(var item in (object[])snapshot["inventory"])
        {
            var row=(Dictionary<string,object>)item; string key=JournalCargoTracker.Canonical(Convert.ToString(row["commodity"]));
            string carrier=Convert.ToString(row["carrier_id"]); long n=Convert.ToInt64(row["quantity"]), previous;
            balances[CarrierInventory.Key(carrier,key)]=n; carrierVersions[CarrierInventory.Key(carrier,key)]=Convert.ToInt64(row["version"]);
            stock.TryGetValue(key,out previous); stock[key]=checked(previous+n); versions[key]=Convert.ToInt64(row["version"]);
        }
        var transit=new Dictionary<string,long>(); object rawTransit;
        TransitReady=snapshot.TryGetValue("transit",out rawTransit);
        if(TransitReady) foreach(var item in (object[])rawTransit) {var row=(Dictionary<string,object>)item;transit[CarrierInventory.Key(Convert.ToString(row["carrier_id"]),Convert.ToString(row["commodity"]))]=Convert.ToInt64(row["quantity"]);}
        var inbound=new Dictionary<string,long>(); object rawInbound;
        InboundReady=snapshot.TryGetValue("inbound",out rawInbound);
        if(InboundReady) foreach(var item in (object[])rawInbound) {var row=(Dictionary<string,object>)item;inbound[CarrierInventory.Key(Convert.ToString(row["carrier_id"]),Convert.ToString(row["commodity"]))]=Convert.ToInt64(row["quantity"]);}
        if(snapshot.ContainsKey("carriers")) {
            Carriers=new JavaScriptSerializer().Deserialize<List<CarrierInventory.Carrier>>(json.Serialize(snapshot["carriers"]));
            CarrierRegistryVersion=Convert.ToInt64(snapshot["registryVersion"]); RegistryReady=true;
            if(Blocked && queue.Count>0 && (queue[0].source=="Market"||queue[0].source=="Transfer") && Carriers.Any(x=>x.id==queue[0].carrierId)) Blocked=false;
        } else await LoadCarriers(); // Compatibility with the existing deployed Worker.
        InboundBalances=inbound;
        TransitBalances=transit;
        CarrierBalances=balances; CarrierVersions=carrierVersions; Stock=stock; Versions=versions; Group=Convert.ToString(snapshot["groupId"]); Role=Convert.ToString(snapshot["role"]); Ready=true;
        snapshotRevision=snapshot.ContainsKey("revision")?Convert.ToString(snapshot["revision"]):"";
    }
    public void Dispose() { http.Dispose(); }
}
