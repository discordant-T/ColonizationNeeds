using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

// Local ledger is committed before the journal cursor. Legacy files are never overwritten.
public sealed class CarrierInventory : IDisposable
{
    public sealed class Carrier
    {
        public string id, name, callsign, lastSeen, role;
        public bool verified;
        public override string ToString() { return name + (String.IsNullOrEmpty(callsign)||name==callsign?"":" · "+callsign) + " (" + id + ")"; }
    }
    public sealed class Movement { public string id, carrier, commodity, source, timestamp, reason; public long delta; public bool resolved; }
    public sealed class State
    {
        public List<Carrier> carriers = new List<Carrier>();
        public Dictionary<string,long> balances = new Dictionary<string,long>();
        public Dictionary<string,string> cutoffs = new Dictionary<string,string>();
        public List<string> applied = new List<string>();
        public List<Movement> review = new List<Movement>();
        public List<TransitLedger.Event> transit = new List<TransitLedger.Event>();
    }
    readonly string path;
    readonly FileStream profileLock;
    public State Data;
    public CarrierInventory(string path, Dictionary<string,long> legacy)
    {
        this.path=path;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        try { profileLock=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); }
        catch(IOException) { throw new IOException("Another application instance is using this player's carrier ledger. Close it before opening this profile."); }
        try {
        if(File.Exists(path)) Data=new JavaScriptSerializer { MaxJsonLength=32*1024*1024 }.Deserialize<State>(File.ReadAllText(path));
        else { Data=new State(); foreach(var p in legacy) { string key=Key("legacy",p.Key); if(Data.balances.ContainsKey(key)) throw new IOException("Legacy stock has duplicate commodity aliases. Correct the original inventory before migrating."); Data.balances[key]=p.Value; } Save(); }
        if(Data!=null && Data.transit==null) Data.transit=new List<TransitLedger.Event>();
        if(Data==null || Data.balances==null || Data.applied==null || Data.review==null || Data.carriers==null) throw new IOException("Carrier inventory is unreadable; original stock has been retained.");
        } catch { profileLock.Dispose(); throw; }
    }
    public static string Key(string carrier,string commodity) { return carrier+"|"+JournalCargoTracker.Canonical(commodity); }
    public static string EventId(string commander,string identity,string suffix)
    { using(var hash=SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(commander.Trim().ToLowerInvariant()+"\n"+identity+"\n"+suffix))).Replace("-","").ToLowerInvariant(); }
    public static bool ValidCarrier(string id) { return Regex.IsMatch(id??"",@"^[1-9][0-9]{0,19}$"); }
    public void Save() { AtomicWrite(path,new JavaScriptSerializer { MaxJsonLength=32*1024*1024 }.Serialize(Data)); }
    public void Dispose() { profileLock.Dispose(); }
    public static void AtomicWrite(string path,string text)
    { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path+".tmp",text); if(File.Exists(path)) File.Replace(path+".tmp",path,path+".bak"); else File.Move(path+".tmp",path); }
    public long Balance(string carrier,string commodity) { long n; Data.balances.TryGetValue(Key(carrier,commodity),out n); return n; }
    public Dictionary<string,long> Stock(string carrier)
    {
        var stock=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
        foreach(var p in Data.balances) { var parts=p.Key.Split('|'); if(carrier.Length>0 && parts[0]!=carrier) continue; long n; stock.TryGetValue(parts[1],out n); stock[parts[1]]=checked(n+p.Value); }
        return stock;
    }
    public void Apply(Movement m)
    {
        if(Data.applied.Contains(m.id)) return;
        string key=Key(m.carrier,m.commodity), cutoff;
        DateTimeOffset observed, reconciled;
        if(Data.cutoffs.TryGetValue(key,out cutoff) && DateTimeOffset.TryParse(m.timestamp,out observed) && DateTimeOffset.TryParse(cutoff,out reconciled) && observed<=reconciled) { Data.applied.Add(m.id); try { Save(); } catch { Data.applied.Remove(m.id); throw; } return; }
        long previous=Balance(m.carrier,m.commodity), next=checked(previous+m.delta);
        if(next < -1000000000 || next>1000000000) throw new ArgumentException("Carrier stock exceeds the supported range; review the event.");
        var snapshot=new JavaScriptSerializer().Serialize(Data);
        Data.balances[key]=next; Data.applied.Add(m.id);
        Data.transit.Add(TransitLedger.FromMovement(m));
        try { Save(); } catch { Data=new JavaScriptSerializer().Deserialize<State>(snapshot); throw; }
    }
    public void ApplyTransit(TransitLedger.Event e)
    {
        if(Data.transit.Any(x=>x.id==e.id)) return;
        Data.transit.Add(e); try { Save(); } catch { Data.transit.Remove(e); throw; }
    }
    public void Hold(Movement m)
    { if(Data.applied.Contains(m.id) || Data.review.Any(x=>x.id==m.id)) return; Data.review.Add(m); try { Save(); } catch { Data.review.Remove(m); throw; } }
    public void Edit(string carrier,string commodity,long amount,string operation)
    {
        string key=Key(carrier,commodity); long previous=Balance(carrier,commodity);
        long next=operation=="Set total"?amount:operation=="Add"?checked(previous+amount):checked(previous-amount);
        if(amount<0 || next<0 || next>1000000000) throw new ArgumentException("Use Set total to reconcile a discrepancy; stock must be between 0 and 1,000,000,000 tonnes.");
        var snapshot=new JavaScriptSerializer().Serialize(Data);
        Data.balances[key]=next;
        if(operation=="Set total") Data.cutoffs[key]=DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
        try { Save(); } catch { Data=new JavaScriptSerializer().Deserialize<State>(snapshot); throw; }
    }
    public void MoveLegacy(string carrier,string commodity,long amount)
    {
        if(!Data.carriers.Any(x=>x.id==carrier)) throw new ArgumentException("Select a registered carrier.");
        long old=Balance("legacy",commodity); if(amount<0 || amount>old) throw new ArgumentException("Insufficient legacy stock.");
        string snapshot=new JavaScriptSerializer().Serialize(Data);
        Data.balances[Key("legacy",commodity)]=old-amount; Data.balances[Key(carrier,commodity)]=checked(Balance(carrier,commodity)+amount);
        try { Save(); } catch { Data=new JavaScriptSerializer().Deserialize<State>(snapshot); throw; }
    }
    static string Text(Dictionary<string,object> d,string key) { object v; return d!=null && d.TryGetValue(key,out v)?Convert.ToString(v):""; }
    public static List<Movement> Parse(string commander,string identity,string market,string stationType,Dictionary<string,object> entry)
    {
        var result=new List<Movement>(); string evt=Text(entry,"event"), timestamp=Text(entry,"timestamp");
        Action<string,long,string,string> add=delegate(string commodity,long delta,string source,string reason) {
            commodity=JournalCargoTracker.Canonical(commodity); if(commodity.Length==0 || delta==0) return;
            result.Add(new Movement { id=EventId(commander,identity,result.Count.ToString()),carrier=market,commodity=commodity,delta=delta,source=source,timestamp=timestamp,reason=reason });
        };
        if(evt=="MarketBuy" || evt=="MarketSell")
        { long count=Convert.ToInt64(entry["Count"]); if(count<0) throw new ArgumentException("Negative trade quantity."); add(Text(entry,"Type"),evt=="MarketSell"?count:-count,"Market",""); }
        if(evt=="CargoTransfer")
        {
            object rows; if(entry.TryGetValue("Transfers",out rows)) foreach(var item in (object[])rows)
            {
                var row=item as Dictionary<string,object>; string direction=Text(row,"Direction").ToLowerInvariant();
                if(direction!="tocarrier" && direction!="toship") continue;
                // toship can also mean SRV -> ship. Require confirmed carrier docking context.
                if(direction=="toship" && stationType.Length>0 && stationType.IndexOf("Carrier",StringComparison.OrdinalIgnoreCase)<0) continue;
                long count=Convert.ToInt64(row["Count"]); if(count<0) throw new ArgumentException("Negative transfer quantity.");
                string reason=stationType.IndexOf("Carrier",StringComparison.OrdinalIgnoreCase)>=0 && ValidCarrier(market)?"":"Carrier docking context unavailable; verify this was not an SRV transfer.";
                add(Text(row,"Type"),direction=="tocarrier"?count:-count,"Transfer",reason);
            }
        }
        return result;
    }
}
