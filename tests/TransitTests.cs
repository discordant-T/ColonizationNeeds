using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
class TransitTests
{
    static void Check(bool ok,string message) {if(!ok)throw new Exception(message);}
    static CarrierInventory.Movement Movement(string id,string carrier,long delta,string timestamp) {return new CarrierInventory.Movement {id=id,carrier=carrier,commodity="steel",delta=delta,source="Market",timestamp=timestamp};}
    static Dictionary<string,object> Json(string value) {return (Dictionary<string,object>)new JavaScriptSerializer().DeserializeObject(value);}
    static long Total(IEnumerable<TransitLedger.Event> events) {return TransitLedger.Balances(events).Values.Sum();}
    static void Main()
    {
        var events=new List<TransitLedger.Event>();
        events.Add(TransitLedger.FromMovement(Movement("withdraw","123",-16260,"2026-10-13T01:00:00Z")));
        Check(Total(events)==16260 && ColonizationNeeds.Remaining(16260,Total(events))==0,"Withdrawal created collection shortage");
        events.Add(new TransitLedger.Event {id="partial",kind="consume",commodity="steel",amount=260,timestamp="2026-10-13T01:01:00Z"});
        Check(Total(events)==16000 && ColonizationNeeds.Remaining(16000,Total(events))==0,"Partial delivery created shortage");
        events.Add(new TransitLedger.Event {id="delivery",kind="consume",commodity="steel",amount=16000,timestamp="2026-10-13T01:02:00Z"});
        Check(Total(events)==0 && ColonizationNeeds.Remaining(0,0)==0,"Completed delivery created collection need");
        var lots=new List<TransitLedger.Event>{TransitLedger.FromMovement(Movement("a","123",-100,"2026-10-13T01:00:00Z")),TransitLedger.FromMovement(Movement("b","456",-50,"2026-10-13T01:01:00Z"))};
        lots.Add(TransitLedger.FromMovement(Movement("return","456",50,"2026-10-13T01:02:00Z")));
        Check(TransitLedger.Balances(lots)["123|steel"]==100 && !TransitLedger.Balances(lots).ContainsKey("456|steel"),"Return cleared other carrier before origin");
        lots.Add(new TransitLedger.Event {id="snapshot",kind="snapshot",cargo=new Dictionary<string,long>{{"steel",60}},timestamp="2026-10-13T01:03:00Z"});
        Check(Total(lots)==60,"Cargo snapshot did not cap lost cargo");
        lots.Add(new TransitLedger.Event {id="extra",kind="snapshot",cargo=new Dictionary<string,long>{{"steel",500}},timestamp="2026-10-13T01:04:00Z"});
        Check(Total(lots)==60,"External cargo was incorrectly credited");
        lots.Add(new TransitLedger.Event {id="loss",kind="reset",timestamp="2026-10-13T01:05:00Z"});Check(Total(lots)==0,"Ship loss retained unavailable cargo");
        var contribution=Json("{\"event\":\"ColonisationContribution\",\"timestamp\":\"2026-10-13T01:01:00Z\",\"Contributions\":[{\"Name\":\"$steel_name;\",\"Amount\":10}]}");
        Check(TransitLedger.Parse("Test","journal:1",contribution,false,"")[0].amount==10,"Delivery parsing failed");
        Check(TransitLedger.Parse("Test","blank",null,false,"").Count==0,"Blank journal row failed");
        Check(TransitLedger.Parse("Test","cargo",Json("{\"event\":\"Cargo\",\"timestamp\":\"2026-10-13T01:01:00Z\",\"Vessel\":\"SRV\",\"Count\":0}"),false,"SRV").Count==0,"SRV cleared ship transit");
        string root=Path.Combine(Path.GetTempPath(),"Transit-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string path=Path.Combine(root,"stock.json");
        try {
            using(var stock=new CarrierInventory(path,new Dictionary<string,long>())) {
                var m=Movement("persist","123",-100,"2026-10-13T01:00:00Z");stock.Apply(m);stock.Apply(m);
                var e=new TransitLedger.Event {id="deliver",kind="consume",commodity="steel",amount=40,timestamp="2026-10-13T01:01:00Z"};stock.ApplyTransit(e);stock.ApplyTransit(e);
                Check(stock.Balance("123","steel")==-100 && Total(stock.Data.transit)==60,"Replay duplicated stock/transit or delivery debited carrier");
            }
            using(var stock=new CarrierInventory(path,new Dictionary<string,long>())) Check(Total(stock.Data.transit)==60,"Restart lost transit");
        } finally {foreach(var file in Directory.GetFiles(root))File.Delete(file);Directory.Delete(root);}
        Console.WriteLine("Transit checks passed: stocked/withdrawn/partial/full delivery, return attribution, snapshots, losses, replay and restart.");
    }
}
