using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
class RavenReporterTest {
 class Transport:HttpMessageHandler {
  public int posts, patches, gets; public bool timeout, rejected, newer; public string postBody, patchBody;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation) {
   if(!request.Headers.Contains("rcc-key") || request.Headers.GetValues("rcc-key").Single()!="test-key") throw new Exception("Auth missing");
   if(request.Method==HttpMethod.Get) { gets++; return new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent("{\"buildId\":\"A\",\"commodities\":{\"steel\":100},\"timestamp\":\""+(newer?"2026-10-05T00:00:00Z":"2026-10-01T00:00:00Z")+"\",\"colonisationConstructionDepot\":{\"timestamp\":\""+(newer?"2026-10-05T00:00:00Z":"2026-10-01T00:00:00Z")+"\"}}")}; }
   if(request.Method==HttpMethod.Post) {
    posts++; postBody=await request.Content.ReadAsStringAsync();
    if(!request.RequestUri.AbsolutePath.EndsWith("/contribute/Test")) throw new Exception("Incorrect contribution route");
    if(timeout) throw new TaskCanceledException("Simulated lost acknowledgment");
    if(rejected) return new HttpResponseMessage(HttpStatusCode.Unauthorized);
   } else { patches++; patchBody=await request.Content.ReadAsStringAsync(); }
   return new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent("{}")};
  }
 }
 static Dictionary<string,object> Parse(string text) {return (Dictionary<string,object>)new JavaScriptSerializer().DeserializeObject(text);}
 static void Assert(bool test,string error) {if(!test)throw new Exception(error);}
 static readonly string Contribution="{\"event\":\"ColonisationContribution\",\"timestamp\":\"2026-10-04T00:00:00Z\",\"Contributions\":[{\"Name\":\"$steel_name;\",\"Amount\":20},{\"Name\":\"steel\",\"Amount\":5}]}";
 static readonly string Depot="{\"event\":\"ColonisationConstructionDepot\",\"timestamp\":\"2026-10-04T00:00:01Z\",\"MarketID\":123,\"ResourcesRequired\":[{\"Name\":\"$steel_name;\",\"RequiredAmount\":100,\"ProvidedAmount\":25}]}";
 static Task<string> Resolve(string system,string market){Assert(system=="456"&&market=="123","Wrong project context");return Task.FromResult("A");}
 static void Main(){Run().GetAwaiter().GetResult();}
 static async Task Run(){
  string root=Path.Combine(Path.GetTempPath(),"ColonizationNeeds-report-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try {
   var metadataOnly=Parse("{\"timestamp\":\"2026-10-06T00:00:00Z\"}");
   Assert(ColonizationNeeds.DepotObservationTicks(metadataOnly)==0,"Generic project update mistaken for depot observation");
   Assert(ColonizationNeeds.DeliveryRemaining(1553,402,ColonizationNeeds.DepotObservationTicks(metadataOnly),1)==402,"Confirmed 1151-tonne delivery lost on refresh");
   Assert(ColonizationNeeds.DeliveryRemaining(300,402,0,1)==300,"Later deliveries from other players hidden");
   var transport=new Transport();using(var http=new HttpClient(transport)){
    using(var reporter=new RavenDeliveryReporter(root,"Test")){
     reporter.Capture("file:100","456","123",Parse(Contribution));reporter.Capture("file:100","456","123",Parse(Contribution));
     reporter.Capture("file:200","456","123",Parse(Depot));reporter.Capture("file:300","456","123",Parse(Depot));
     Assert(reporter.Pending==2,"Duplicate queue entries");
     await reporter.Sync(http,"https://example.test/api/","test-key",Resolve);
     Assert(transport.posts==1&&transport.patches==1&&reporter.Pending==0,"Send failed");
     Assert(reporter.History.Single(x=>x.kind=="Contribution").cargo["steel"]==25&&reporter.History.Single(x=>x.kind=="Contribution").outcome=="Submitted","Delivery history missing quantities/status");
     Assert(transport.postBody.Contains("25")&&transport.patchBody.Contains("75")&&transport.patchBody.Contains("colonisationConstructionDepot"),"Incorrect payload arithmetic");
     Assert(!File.ReadAllText(Directory.GetFiles(root,"*.json").Single()).Contains("test-key"),"Credential persisted");
    }
    using(var reporter=new RavenDeliveryReporter(root,"Test")){
     reporter.Capture("file:100","456","123",Parse(Contribution));await reporter.Sync(http,"https://example.test/api/","test-key",Resolve);
     Assert(transport.posts==1,"Restart replayed delivery");
     Assert(reporter.History.Single(x=>x.kind=="Contribution").cargo["steel"]==25,"Restart lost delivery history");
     transport.timeout=true;reporter.Capture("file:400","456","123",Parse(Contribution));await reporter.Sync(http,"https://example.test/api/","test-key",Resolve);
     Assert(reporter.Review.Count()==1&&transport.posts==2,"Uncertain POST not held");
    }
    using(var reporter=new RavenDeliveryReporter(root,"Test")){
     transport.timeout=false; await reporter.Sync(http,"https://example.test/api/","test-key",Resolve);
     Assert(transport.posts==2,"Uncertain delivery blindly retried");
     reporter.Resolve(reporter.Review.Single().id,false);Assert(!reporter.Review.Any(),"Recorded resolution failed");
     transport.rejected=true;reporter.Capture("file:500","456","123",Parse(Contribution));await reporter.Sync(http,"https://example.test/api/","test-key",Resolve);
     Assert(reporter.Pending==1&&!reporter.Review.Any(),"Explicit rejection should remain retryable");
    }
    using(var reporter=new RavenDeliveryReporter(root,"Test")){
     transport.rejected=false;await reporter.Sync(http,"https://example.test/api/","test-key",Resolve);Assert(reporter.Pending==0,"Rejected request did not resume after restart");
     transport.timeout=true;reporter.Capture("file:600","456","123",Parse(Contribution));await reporter.Sync(http,"https://example.test/api/","test-key",Resolve);
     reporter.Resolve(reporter.Review.Single().id,true);transport.timeout=false;await reporter.Sync(http,"https://example.test/api/","test-key",Resolve);Assert(!reporter.Review.Any()&&reporter.Pending==0,"Explicit retry failed");
     transport.newer=true;int patches=transport.patches;reporter.Capture("file:700","456","123",Parse(Depot.Replace("25}","50}")));await reporter.Sync(http,"https://example.test/api/","test-key",Resolve);Assert(transport.patches==patches,"Old depot overwrote newer server snapshot");
    }
   }
   string journals=Path.Combine(root,"journals");Directory.CreateDirectory(journals);
   string file=Path.Combine(journals,"Journal.01.log");File.WriteAllText(file,"{\"event\":\"LoadGame\",\"Commander\":\"Test\"}\n{\"event\":\"Location\",\"SystemAddress\":456,\"MarketID\":123}\n");
   var tracker=new JournalCargoTracker(journals);int reports=0;
   File.AppendAllText(file,Contribution+"\n{\"event\":\"CargoDepot\",\"SubType\":\"Deliver\",\"Type\":\"steel\",\"Count\":25}\n");
   Action<string,string,string,Dictionary<string,object>> capture=delegate(string id,string system,string market,Dictionary<string,object> entry){Assert(system=="456"&&market=="123","Journal location missing");reports++;};
   tracker.Poll("Test",delegate(Dictionary<string,long> cargo){throw new Exception("Delivery counted as acquisition");},null,capture);tracker.Poll("Test",delegate(Dictionary<string,long> cargo){},null,capture);
   Assert(reports==1,"Journal replayed or CargoDepot duplicated delivery");
   Console.WriteLine("Raven reporter checks passed.");
  }finally{Directory.Delete(root,true);}
 }
}
