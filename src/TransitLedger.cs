using System;
using System.Collections.Generic;
using System.Linq;

// Carrier withdrawals become Outbound; opted-in station purchases become Inbound.
public static class TransitLedger
{
    public sealed class Event
    {
        public string id,kind,carrier,commodity,timestamp;
        public long amount;
        public Dictionary<string,long> cargo;
    }
    sealed class Lot { public string carrier,commodity; public long amount; }
    public static Event FromMovement(CarrierInventory.Movement m)
    { return new Event { id=m.id,kind=m.delta<0?"withdraw":"consume",carrier=m.carrier,commodity=m.commodity,amount=Math.Abs(m.delta),timestamp=m.timestamp }; }
    static int Priority(Lot lot,string preferred)
    {
        bool inbound=lot.carrier.StartsWith("inbound:");
        if(String.IsNullOrEmpty(preferred))return inbound?1:0;
        if(inbound && (lot.carrier=="inbound:"+preferred || lot.carrier=="inbound:any"))return 0;
        if(lot.carrier==preferred)return 1;
        return inbound?2:3;
    }
    static void Consume(List<Lot> lots,string commodity,long amount,string preferred)
    {
        foreach(var lot in lots.Where(x=>x.commodity==commodity).OrderBy(x=>Priority(x,preferred))) {
            long take=Math.Min(lot.amount,amount); lot.amount-=take; amount-=take; if(amount==0)break;
        }
    }
    public static Dictionary<string,long> Balances(IEnumerable<Event> events)
    {
        var lots=new List<Lot>();
        // LINQ ordering is stable for events sharing a journal timestamp.
        foreach(var e in events.OrderBy(x=>DateTimeOffset.Parse(x.timestamp).UtcTicks)) {
            if(e.kind=="withdraw" || e.kind=="inbound") lots.Add(new Lot {carrier=e.kind=="inbound"?"inbound:"+e.carrier:e.carrier,commodity=e.commodity,amount=e.amount});
            else if(e.kind=="consume") Consume(lots,e.commodity,e.amount,e.carrier);
            else if(e.kind=="reset") lots.Clear();
            else if(e.kind=="cap") {var scoped=lots.Where(x=>x.carrier.StartsWith("inbound:")==(e.carrier=="inbound")).ToList();long total=scoped.Where(x=>x.commodity==e.commodity).Sum(x=>x.amount);Consume(scoped,e.commodity,Math.Max(0,total-e.amount),null);}
            else if(e.kind=="snapshot") foreach(var key in lots.Select(x=>x.commodity).Distinct().ToArray()) {
                long held=0;e.cargo.TryGetValue(key,out held);long total=lots.Where(x=>x.commodity==key).Sum(x=>x.amount);Consume(lots,key,Math.Max(0,total-held),null);
            }
            lots.RemoveAll(x=>x.amount==0);
        }
        var result=new Dictionary<string,long>();
        foreach(var lot in lots.Where(x=>x.amount>0)) {string key=CarrierInventory.Key(lot.carrier,lot.commodity);long n;result.TryGetValue(key,out n);result[key]=checked(n+lot.amount);}
        return result;
    }
    public static Event Purchase(string commander,string identity,Dictionary<string,object> entry,string stationType,bool collecting,string destination)
    {
        if(!collecting || entry==null || Text(entry,"event")!="MarketBuy" || stationType.Length==0 || stationType=="SRV" || stationType.IndexOf("Carrier",StringComparison.OrdinalIgnoreCase)>=0)return null;
        DateTimeOffset at;if(!DateTimeOffset.TryParse(Text(entry,"timestamp"),out at))return null;
        long n=Convert.ToInt64(entry["Count"]);if(n<=0 || n>1000000000)throw new ArgumentException("Invalid purchase quantity");
        return new Event {id=CarrierInventory.EventId(commander,identity,"inbound"),kind="inbound",carrier=String.IsNullOrEmpty(destination)?"any":destination,commodity=JournalCargoTracker.Canonical(Text(entry,"Type")),amount=n,timestamp=at.ToUniversalTime().ToString("o")};
    }
    static string Text(Dictionary<string,object> row,string key) {object v;return row.TryGetValue(key,out v)?Convert.ToString(v):"";}
    public static List<Event> Parse(string commander,string identity,Dictionary<string,object> entry,bool registeredCarrier,string stationType)
    {
        var result=new List<Event>();if(entry==null)return result;
        string kind=Text(entry,"event"),timestamp=Text(entry,"timestamp");
        DateTimeOffset at;if(!DateTimeOffset.TryParse(timestamp,out at))return result;
        Action<string,long> consume=delegate(string commodity,long amount) {if(amount<0)throw new ArgumentException("Negative cargo amount");if(amount>0)result.Add(new Event {kind="consume",commodity=JournalCargoTracker.Canonical(commodity),amount=amount});};
        if(kind=="ColonisationContribution") foreach(var row in RavenDeliveryReporter.Contributions(entry))consume(row.Key,row.Value);
        else if(kind=="MarketSell" && !registeredCarrier && stationType.IndexOf("Carrier",StringComparison.OrdinalIgnoreCase)<0)consume(Text(entry,"Type"),Convert.ToInt64(entry["Count"]));
        else if(kind=="EjectCargo" && stationType!="SRV")consume(Text(entry,"Type"),Convert.ToInt64(entry["Count"]));
        else if(kind=="Died")result.Add(new Event {kind="reset"});
        else if(kind=="Cargo" && Text(entry,"Vessel")=="Ship") {
            object rows; if(entry.TryGetValue("Inventory",out rows) && rows is object[]) {
                var cargo=new Dictionary<string,long>();foreach(var raw in (object[])rows) {var row=(Dictionary<string,object>)raw;string key=JournalCargoTracker.Canonical(Text(row,"Name"));long n=Convert.ToInt64(row["Count"]);if(n<0)throw new ArgumentException("Negative cargo snapshot");long old;cargo.TryGetValue(key,out old);cargo[key]=checked(old+n);}
                result.Add(new Event {kind="snapshot",cargo=cargo});
            } else if(Text(entry,"Count")=="0") result.Add(new Event {kind="reset"});
        }
        // Unconfigured/ambiguous carrier movements stay in the existing review queue.
        // Applying their stock movement also applies transit once, at the original timestamp.
        for(int i=0;i<result.Count;i++) {result[i].id=CarrierInventory.EventId(commander,identity,"transit-"+i);result[i].timestamp=at.ToUniversalTime().ToString("o");}
        return result;
    }
}
