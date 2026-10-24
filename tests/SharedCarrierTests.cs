using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
class SharedCarrierTests
{
    class Transport:HttpMessageHandler
    {
        public bool loseAck=true, rejectManual, modern; public int applied; public readonly HashSet<string> ids=new HashSet<string>(); public readonly List<string> endpoints=new List<string>();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel)
        {
            endpoints.Add(request.RequestUri.AbsolutePath);
            if(request.Headers.Authorization.Parameter!="test-player-token") throw new Exception("Player authentication not retained");
            if(request.Method==HttpMethod.Post)
            {
                var entry=(Dictionary<string,object>)new JavaScriptSerializer().DeserializeObject(await request.Content.ReadAsStringAsync());
                string id=Convert.ToString(entry["requestId"]);
                if(rejectManual && Convert.ToString(entry["source"])=="Manual") return new HttpResponseMessage(HttpStatusCode.Forbidden) {Content=new StringContent("{\"error\":\"Admin permission required\"}")};
                if(ids.Add(id)) applied++;
                if(loseAck) throw new TaskCanceledException("Lost acknowledgment");
                return new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent("{}")};
            }
            if(modern && request.RequestUri.AbsolutePath=="/v2/inventory") {
                string response=request.RequestUri.Query.Contains("since=test-revision")
                    ? "{\"schema\":2,\"unchanged\":true,\"revision\":\"test-revision\"}"
                    : "{\"schema\":2,\"revision\":\"test-revision\",\"groupId\":\"squad\",\"role\":\"admin\",\"registryVersion\":8,\"carriers\":[{\"id\":\"123\",\"name\":\"Updated\",\"role\":\"Squadron\",\"verified\":true}],\"inventory\":[{\"carrier_id\":\"123\",\"commodity\":\"steel\",\"quantity\":42,\"version\":9}],\"inbound\":[{\"carrier_id\":\"123\",\"commodity\":\"steel\",\"quantity\":7}],\"transit\":[{\"carrier_id\":\"123\",\"commodity\":\"steel\",\"quantity\":5}]}";
                return new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent(response)};
            }
            if(request.RequestUri.AbsolutePath=="/v2/carriers") return new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent("{\"groupId\":\"squad\",\"role\":\"admin\",\"version\":1,\"carriers\":[{\"id\":\"123\",\"name\":\"Squadron\",\"role\":\"Squadron\",\"verified\":true},{\"id\":\"456\",\"name\":\"Personal\",\"role\":\"Personal\",\"verified\":true}]}")};
            return new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent("{\"schema\":2,\"groupId\":\"squad\",\"role\":\"admin\",\"inventory\":[{\"carrier_id\":\"123\",\"commodity\":\"steel\",\"quantity\":10,\"version\":1},{\"carrier_id\":\"456\",\"commodity\":\"steel\",\"quantity\":20,\"version\":2},{\"carrier_id\":\"legacy\",\"commodity\":\"water\",\"quantity\":3,\"version\":4}]}" )};
        }
    }
    static void Check(bool test,string message) {if(!test)throw new Exception(message);}
    static void Main() {Run().GetAwaiter().GetResult();}
    static async Task Run()
    {
        string root=Path.Combine(Path.GetTempPath(),"ColonizationNeeds-shared-carrier-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            var transport=new Transport();
            using(var shared=new SharedInventoryClient(root,"https://local.test/","test-player-token",true,transport)) {
                var movement=new SharedInventoryClient.Change { requestId="event-1",carrierId="123",commodity="steel",operation="add",amount=10,source="Market",observedAt="2026-10-13T01:00:00Z" };
                shared.Enqueue(new[] {movement}); shared.Enqueue(new[] {movement}); Check(shared.Pending==1,"Journal replay duplicated outbox");
                try {await shared.Sync();} catch(TaskCanceledException){}
                Check(shared.Pending==1 && transport.applied==1,"Lost ack discarded pending event");
            }
            transport.loseAck=false;
            using(var shared=new SharedInventoryClient(root,"https://local.test/","test-player-token",true,transport)) {
                Check(shared.Pending==1,"Restart lost outbox");await shared.Sync();
                Check(transport.applied==1 && shared.Pending==0,"Retry duplicated committed event");
                transport.loseAck=true;
                var delivery=new TransitLedger.Event {id="transit-delivery",kind="consume",commodity="steel",amount=5,timestamp="2026-10-13T01:01:00Z"};
                shared.Enqueue(new[] {new SharedInventoryClient.Change {requestId=delivery.id,commodity="_transit",source="Transit",operation="transit",transit=delivery}});
                try {await shared.Sync();} catch(TaskCanceledException){}
                Check(shared.Pending==1 && transport.applied==2,"Lost transit acknowledgment was discarded");
                transport.loseAck=false;await shared.Sync();Check(shared.Pending==0 && transport.applied==2 && transport.endpoints.Contains("/v2/transit"),"Transit retry duplicated event or used stock endpoint");
                Check(shared.CarrierStock("")["steel"]==30 && shared.CarrierStock("123")["steel"]==10,"Carrier aggregation failed");
                Check(shared.CarrierVersions["456|steel"]==2 && shared.CarrierStock("legacy")["water"]==3,"Legacy stock or versions missing");
                Check(shared.RegistryReady && shared.Carriers.Count==2 && shared.Carriers[0].verified,"Shared registry was not adopted");
                shared.Enqueue(new[] {new SharedInventoryClient.Change {commodity="water",operation="add",amount=3,source="Collect"}}); await shared.Sync();
                Check(transport.endpoints.All(x=>x.StartsWith("/v2/")),"Old queue was not migrated to v2 protocol");
                transport.modern=true; transport.endpoints.Clear(); await shared.Sync();
                Check(shared.CarrierStock("123")["steel"]==42 && shared.Carriers[0].name=="Updated" && shared.CarrierRegistryVersion==8,"Combined snapshot not adopted");
                await shared.Sync();
                Check(transport.endpoints.Count==2 && transport.endpoints.All(x=>x=="/v2/inventory"),"Optimized sync still fetches registry");
                Check(shared.CarrierStock("123")["steel"]==42 && shared.InboundBalances["123|steel"]==7 && shared.TransitBalances["123|steel"]==5,"Unchanged response erased balances");
                transport.rejectManual=true;shared.Enqueue(new[] {new SharedInventoryClient.Change {carrierId="123",commodity="steel",operation="set",amount=10,source="Manual"}});
                try {await shared.Sync();} catch(IOException){}
                Check(shared.Blocked && shared.Pending==1 && shared.CancelBlockedManual() && shared.Pending==0,"Rejected manual correction could not be resolved");
            }
            Console.WriteLine("Shared carrier client checks passed: lost acknowledgment, restart, duplicate events, legacy outbox and carrier aggregation.");
        } finally { foreach(var file in Directory.GetFiles(root)) File.Delete(file);Directory.Delete(root); }
    }
}
