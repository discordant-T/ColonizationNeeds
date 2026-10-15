using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

public static class CarrierTests
{
    static void Check(bool ok,string message) { if(!ok) throw new Exception("Carrier test: "+message); }
    static Dictionary<string,object> Parse(string text) { return new JavaScriptSerializer().DeserializeObject(text) as Dictionary<string,object>; }
    public static void RunTests()
    {
        string root=Path.Combine(Path.GetTempPath(),"ColonizationNeeds-carrier-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        CarrierInventory ledger=null;
        try
        {
            ledger=new CarrierInventory(Path.Combine(root,"stock.json"),new Dictionary<string,long>{{"steel",100}});
            bool locked=false; try { using(var duplicate=new CarrierInventory(Path.Combine(root,"stock.json"),new Dictionary<string,long>())) {} } catch(IOException) { locked=true; } Check(locked,"Duplicate local application instance allowed");
            ledger.Data.carriers.Add(new CarrierInventory.Carrier{id="123",name="Squadron"}); ledger.Data.carriers.Add(new CarrierInventory.Carrier{id="456",name="Personal"}); ledger.Save();
            ledger.MoveLegacy("123","steel",60); ledger.MoveLegacy("456","steel",40);
            Check(ledger.Stock("")["steel"]==100 && ledger.Balance("legacy","steel")==0,"Migration inflated combined stock");
            var sold=CarrierInventory.Parse("A","file:1","123","FleetCarrier",Parse("{\"event\":\"MarketSell\",\"timestamp\":\"2026-10-13T01:00:00Z\",\"Type\":\"steel\",\"Count\":20}"));
            ledger.Apply(sold.Single()); ledger.Apply(sold.Single()); Check(ledger.Balance("123","steel")==80,"Duplicate sale");
            var bought=CarrierInventory.Parse("A","file:2","123","FleetCarrier",Parse("{\"event\":\"MarketBuy\",\"timestamp\":\"2026-10-13T01:00:01Z\",\"Type\":\"steel\",\"Count\":90}"));
            ledger.Apply(bought.Single()); Check(ledger.Balance("123","steel")==-10,"Withdrawal was clamped or rejected");
            var transfer=CarrierInventory.Parse("A","file:3","123","FleetCarrier",Parse("{\"event\":\"CargoTransfer\",\"timestamp\":\"2026-10-13T01:00:02Z\",\"Transfers\":[{\"Type\":\"steel\",\"Count\":30,\"Direction\":\"tocarrier\"},{\"Type\":\"water\",\"Count\":5,\"Direction\":\"toship\"},{\"Type\":\"water\",\"Count\":8,\"Direction\":\"tosrv\"}]}"));
            foreach(var m in transfer) ledger.Apply(m); Check(transfer.Count==2 && ledger.Balance("123","steel")==20 && ledger.Balance("123","water")==-5,"Direct transfer directions");
            Check(CarrierInventory.Parse("A","file:4","","",Parse("{\"event\":\"CargoTransfer\",\"Transfers\":[{\"Type\":\"steel\",\"Count\":1,\"Direction\":\"toship\"}]}")).Single().reason.Length>0,"Ambiguous transfer was trusted");
            Check(CarrierInventory.Parse("A","file:4","888","Coriolis",Parse("{\"event\":\"CargoTransfer\",\"Transfers\":[{\"Type\":\"steel\",\"Count\":1,\"Direction\":\"toship\"}]}")).Count==0,"SRV transfer counted");
            Check(CarrierInventory.Parse("A","file:5","123","FleetCarrier",Parse("{\"event\":\"ColonisationContribution\",\"Contributions\":[{\"Name\":\"steel\",\"Amount\":20}]}")).Count==0,"Delivery deducted carrier stock twice");
            ledger.Edit("123","steel",12,"Set total"); ledger.Apply(new CarrierInventory.Movement{id="late",carrier="123",commodity="steel",delta=100,timestamp="2000-01-01T00:00:00Z"}); Check(ledger.Balance("123","steel")==12,"Old event undid manual reconciliation");
            ledger.Dispose(); ledger=new CarrierInventory(Path.Combine(root,"stock.json"),new Dictionary<string,long>{{"steel",999}}); Check(ledger.Balance("123","steel")==12,"Restart replayed legacy migration");
            // First launch establishes a baseline. Subsequent launches recover appended events and separate players.
            string journal=Path.Combine(root,"Journal.01.log"), cursor=Path.Combine(root,"cursor.json");
            File.WriteAllText(journal,"{\"event\":\"LoadGame\",\"Commander\":\"A\"}\n{\"event\":\"Docked\",\"MarketID\":123,\"StationType\":\"FleetCarrier\"}\n");
            var tracker=new JournalCargoTracker(root,cursor); int calls=0;
            tracker.Movement=delegate(string cmdr,string id,string market,string type,Dictionary<string,object> entry) { if(Convert.ToString(entry["event"])=="CargoTransfer") {Check(market=="123"&&type=="FleetCarrier","Carrier context not recovered"); calls++;} };
            File.AppendAllText(journal,"{\"event\":\"CargoTransfer\",\"Transfers\":[{\"Type\":\"steel\",\"Count\":1,\"Direction\":\"tocarrier\"}]}\n");
            tracker.Poll("A",delegate(Dictionary<string,long> gained){}); Check(calls==1,"New transfer missing");
            File.AppendAllText(journal,"{\"event\":\"CargoTransfer\",\"Transfers\":[{\"Type\":\"steel\",\"Count\":1,\"Direction\":\"tocarrier\"}]}\n");
            tracker=new JournalCargoTracker(root,cursor); tracker.Movement=delegate(string cmdr,string id,string market,string type,Dictionary<string,object> entry){if(Convert.ToString(entry["event"])=="CargoTransfer")calls++;};
            tracker.Poll("A",delegate(Dictionary<string,long> gained){}); tracker.Poll("A",delegate(Dictionary<string,long> gained){}); Check(calls==2,"Offline recovery or cursor deduplication failed");
            File.AppendAllText(journal,"{\"event\":\"LoadGame\",\"Commander\":\"B\"}\n{\"event\":\"CargoTransfer\",\"Transfers\":[{\"Type\":\"steel\",\"Count\":1,\"Direction\":\"tocarrier\"}]}\n"); tracker.Poll("A",delegate(Dictionary<string,long> gained){}); Check(calls==2,"Wrong player processed");
            Check(CarrierInventory.EventId("A","file:1","0")!=CarrierInventory.EventId("B","file:1","0"),"Player event identities collide");
            string discovery=Path.Combine(root,"discovery");Directory.CreateDirectory(discovery);
            File.WriteAllText(Path.Combine(discovery,"Journal.01.log"),"{\"event\":\"LoadGame\",\"Commander\":\"A\"}\n{\"event\":\"Docked\",\"StationType\":\"FleetCarrier\",\"StationName\":\"ABC-123\",\"MarketID\":123,\"timestamp\":\"2026-10-13T01:00:00Z\"}\n{\"event\":\"CarrierStats\",\"CarrierID\":123,\"Name\":\"My Carrier\",\"Callsign\":\"ABC-123\",\"timestamp\":\"2026-10-13T01:01:00Z\"}\n{\"event\":\"Docked\",\"StationType\":\"Coriolis\",\"StationName\":\"Station\",\"MarketID\":999,\"timestamp\":\"2026-10-13T01:02:00Z\"}\n{\"event\":\"LoadGame\",\"Commander\":\"B\"}\n{\"event\":\"Docked\",\"StationType\":\"FleetCarrier\",\"StationName\":\"Other\",\"MarketID\":456,\"timestamp\":\"2026-10-13T01:03:00Z\"}\n{broken");
            var discovered=CarrierDiscovery.Scan(discovery,"A");Check(discovered.Count==1 && discovered[0].id=="123" && discovered[0].name=="My Carrier" && discovered[0].callsign=="ABC-123" && discovered[0].verified,"Discovery identity, name, type or player isolation failed");
            Check(CarrierDiscovery.Scan(discovery,"B").Single().id=="456","Other player's discovery failed");
            File.Delete(Path.Combine(discovery,"Journal.01.log"));Directory.Delete(discovery);
        }
        finally { if(ledger!=null) ledger.Dispose(); foreach(var f in Directory.GetFiles(root)) File.Delete(f); Directory.Delete(root); }
    }
}
