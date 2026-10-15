using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
class InboundTests
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static long Amount(List<TransitLedger.Event> events,string key) {long n;TransitLedger.Balances(events).TryGetValue(key,out n);return n;}
    static void Main()
    {
        var buy=(Dictionary<string,object>)new JavaScriptSerializer().DeserializeObject("{\"event\":\"MarketBuy\",\"Type\":\"steel\",\"Count\":100,\"timestamp\":\"2026-10-14T01:00:00Z\"}");
        Check(TransitLedger.Purchase("A","j:1",buy,"Coriolis",false,"123")==null,"Personal purchase counted");
        Check(TransitLedger.Purchase("A","j:1",buy,"FleetCarrier",true,"123")==null,"Carrier withdrawal also counted as Inbound");
        Check(TransitLedger.Purchase("A","j:1",buy,"",true,"123")==null,"Unknown dock context counted as station");
        var inbound=TransitLedger.Purchase("A","j:1",buy,"Coriolis",true,"123");var events=new List<TransitLedger.Event>{inbound};
        Check(Amount(events,"inbound:123|steel")==100,"Station purchase was not recorded");
        events.Add(new TransitLedger.Event {id="outbound",kind="withdraw",carrier="123",commodity="steel",amount=50,timestamp="2026-10-14T01:01:00Z"});
        events.Add(new TransitLedger.Event {id="deposit",kind="consume",carrier="123",commodity="steel",amount=40,timestamp="2026-10-14T01:02:00Z"});
        Check(Amount(events,"inbound:123|steel")==60 && Amount(events,"123|steel")==50,"Deposit did not prefer inbound destination");
        events.Add(new TransitLedger.Event {id="delivery",kind="consume",commodity="steel",amount=30,timestamp="2026-10-14T01:03:00Z"});
        Check(Amount(events,"123|steel")==20 && Amount(events,"inbound:123|steel")==60,"Delivery did not prefer Outbound");
        events.Add(new TransitLedger.Event {id="clear-inbound",kind="cap",carrier="inbound",commodity="steel",amount=0,timestamp="2026-10-14T01:04:00Z"});
        Check(Amount(events,"123|steel")==20 && Amount(events,"inbound:123|steel")==0,"Inbound correction changed Outbound");
        events.Add(new TransitLedger.Event {id="clear-outbound",kind="cap",commodity="steel",amount=0,timestamp="2026-10-14T01:05:00Z"});Check(TransitLedger.Balances(events).Count==0,"Old transit correction compatibility failed");
        string root=Path.Combine(Path.GetTempPath(),"Inbound-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {string path=Path.Combine(root,"stock.json");using(var stock=new CarrierInventory(path,new Dictionary<string,long>())) {stock.ApplyTransit(inbound);stock.ApplyTransit(inbound);Check(Amount(stock.Data.transit,"inbound:123|steel")==100,"Replay duplicated inbound");}using(var stock=new CarrierInventory(path,new Dictionary<string,long>()))Check(Amount(stock.Data.transit,"inbound:123|steel")==100,"Restart lost inbound");}
        finally{foreach(var file in Directory.GetFiles(root))File.Delete(file);Directory.Delete(root);}
        Console.WriteLine("Inbound checks passed: opt-in, carrier exclusion, mixed cargo, partial deposits/delivery, separate corrections and restart.");
    }
}
