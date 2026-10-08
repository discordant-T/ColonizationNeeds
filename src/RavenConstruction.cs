using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

// Explicit setup only: journal observations never create projects by themselves.
public sealed class RavenConstruction
{
    public sealed class Dock
    {
        public Dictionary<string, object> Location, Depot;
    }
    public static string Text(Dictionary<string, object> data, string key)
    { object value; return data != null && data.TryGetValue(key, out value) ? Convert.ToString(value) : ""; }
    public static Dock ReadDock(string folder, string commander)
    {
        var file = Directory.GetFiles(folder, "Journal.*.log").OrderBy(File.GetLastWriteTimeUtc).LastOrDefault();
        if (file == null) throw new IOException("No Elite Dangerous journal found.");
        var result = new Dock(); string cmdr = "";
        using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var reader = new StreamReader(stream))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                Dictionary<string, object> entry;
                try { entry = new JavaScriptSerializer().DeserializeObject(line.TrimStart('\uFEFF')) as Dictionary<string, object>; } catch (ArgumentException) { continue; } catch (InvalidOperationException) { continue; }
                string evt = Text(entry, "event");
                if (evt == "LoadGame" || evt == "Commander") { cmdr = Text(entry, evt == "LoadGame" ? "Commander" : "Name"); result.Location = result.Depot = null; }
                if (evt == "Undocked" || evt == "FSDJump" || evt == "Shutdown") result.Location = result.Depot = null;
                if (!String.Equals(cmdr, commander.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                if (evt == "Docked") { result.Location = entry; result.Depot = null; }
                if (evt == "ColonisationConstructionDepot" && result.Location != null && Text(entry, "MarketID") == Text(result.Location, "MarketID")) result.Depot = entry;
            }
        }
        if (result.Location == null || result.Depot == null || JournalCargoTracker.DepotRemaining(result.Depot).Count == 0)
            throw new InvalidOperationException("Dock at the construction site and open Construction Services first, using the configured commander.");
        if (Text(result.Depot, "ConstructionComplete").Equals("True", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("This construction is already complete.");
        return result;
    }
    readonly HttpClient client; readonly string root, key, commander;
    public RavenConstruction(HttpClient client, string root, string key, string commander)
    { this.client = client; this.root = root; this.key = key; this.commander = commander; }
    async Task<object> Request(HttpMethod method, string path, object payload, bool allowMissing)
    {
        using (var request = new HttpRequestMessage(method, root + path))
        {
            request.Headers.Add("rcc-key", key);
            request.Headers.Add("rcc-cmdr0", Convert.ToBase64String(Encoding.UTF8.GetBytes(commander)));
            if (payload != null) request.Content = new StringContent(new JavaScriptSerializer().Serialize(payload), Encoding.UTF8, "application/json");
            using (var response = await client.SendAsync(request))
            {
                if (allowMissing && response.StatusCode == HttpStatusCode.NotFound) return null;
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Raven HTTP " + (int)response.StatusCode + ". No automatic retry. Reload Raven before retrying setup.");
                string body = await response.Content.ReadAsStringAsync();
                return String.IsNullOrWhiteSpace(body) ? null : ColonizationNeeds.ReadJson(body);
            }
        }
    }
    public async Task<Dictionary<string, object>[]> Sites(Dock dock)
    {
        var raw = await Request(HttpMethod.Get, "v2/system/" + Text(dock.Location, "SystemAddress") + "/sites", null, false) as object[];
        if (raw == null) throw new InvalidOperationException("Unexpected Raven site-list format.");
        return raw.Cast<Dictionary<string, object>>().ToArray();
    }
    public static void Validate(Dictionary<string, object> site, Dock dock)
    {
        if (Text(site, "id").Length == 0 || Text(site, "buildType").Length == 0 || Text(site, "buildType").EndsWith("?"))
            throw new InvalidOperationException("Set the planned site's exact construction variant in Raven first. A type ending in ? is uncertain.");
        string body = Text(dock.Location, "BodyID");
        if (body.Length > 0 && Text(site, "bodyNum") != body) throw new InvalidOperationException("The selected plan is on a different body.");
    }
    public async Task<string> Start(Dock dock, Dictionary<string, object> chosen)
    {
        var sites = await Sites(dock);
        var site = sites.SingleOrDefault(x => Text(x, "id") == Text(chosen, "id"));
        if (site == null || Text(site, "buildType") != Text(chosen, "buildType")) throw new InvalidOperationException("The planned site changed. Reopen setup.");
        Validate(site, dock);
        string system = Text(dock.Location, "SystemAddress"), market = Text(dock.Location, "MarketID");
        var project = await Request(HttpMethod.Get, "system/" + system + "/" + market, null, true) as Dictionary<string, object>;
        if (project == null)
        {
            if (Text(site, "status") != "plan" || Text(site, "buildId").Length > 0 || Text(site, "marketId").Length > 0) throw new InvalidOperationException("The selected site is already linked or is no longer planned.");
            var remaining = JournalCargoTracker.DepotRemaining(dock.Depot);
            long max = ((object[])dock.Depot["ResourcesRequired"]).Cast<Dictionary<string, object>>().Sum(x => Convert.ToInt64(x["RequiredAmount"]));
            var payload = new Dictionary<string, object> {
                { "systemSiteId", Text(site, "id") }, { "buildName", Text(dock.Location, "StationName") },
                { "buildType", Text(site, "buildType") }, { "marketId", Convert.ToInt64(market) },
                { "systemAddress", Convert.ToInt64(system) }, { "systemName", Text(dock.Location, "StarSystem") },
                { "bodyNum", site["bodyNum"] }, { "architectName", commander },
                { "commanders", new Dictionary<string, object> { { commander, new string[0] } } },
                { "commodities", remaining }, { "maxNeed", max }, { "colonisationConstructionDepot", dock.Depot }, { "isPrimaryPort", false }
            };
            project = await Request(HttpMethod.Put, "project/", payload, false) as Dictionary<string, object>;
            // Always read by actual game identity; an HTTP success alone is insufficient.
            project = await Request(HttpMethod.Get, "system/" + system + "/" + market, null, false) as Dictionary<string, object>;
        }
        if (project == null || Text(project, "marketId") != market || Text(project, "systemAddress") != system || Text(project, "bodyNum") != Text(site, "bodyNum") || Text(project, "buildType") != Text(site, "buildType") || Text(project, "complete").Equals("True", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The existing project does not match this plan's identity and type. Inspect Raven before retrying; no duplicate project will be created.");
        string id = Text(project, "buildId"); Guid parsed;
        if (!Guid.TryParse(id, out parsed)) throw new InvalidOperationException("Raven returned no valid project ID.");
        sites = await Sites(dock); site = sites.Single(x => Text(x, "id") == Text(chosen, "id"));
        Validate(site, dock);
        if (Text(site, "bodyNum") != Text(project, "bodyNum")) throw new InvalidOperationException("The planned body changed during setup.");
        if (sites.Any(x => Text(x, "id") != Text(site, "id") && (Text(x, "buildId") == id || Text(x, "marketId") == market))) throw new InvalidOperationException("The project is linked to another planned site. Inspect Raven first.");
        if ((Text(site, "buildId").Length > 0 && Text(site, "buildId") != id) || (Text(site, "marketId").Length > 0 && Text(site, "marketId") != market) || (Text(site, "status") != "plan" && Text(site, "status") != "build") || Text(site, "buildType") != Text(chosen, "buildType")) throw new InvalidOperationException("The planned site changed during setup. Inspect Raven before retrying.");
        site["buildId"] = id; site["marketId"] = Convert.ToInt64(market); site["status"] = "build";
        site["name"] = Text(dock.Location, "StationName").Replace("Orbital Construction Site: ", "").Replace("Surface Construction Site: ", "");
        await Request(HttpMethod.Put, "v2/system/" + system + "/sites", new { update = new[] { site }, delete = new string[0] }, false);
        await Request(HttpMethod.Put, "project/" + id + "/link/" + Uri.EscapeDataString(commander), null, false);
        var verified = (await Sites(dock)).Single(x => Text(x, "id") == Text(site, "id"));
        project = await Request(HttpMethod.Get, "project/" + id, null, false) as Dictionary<string, object>;
        object members; var linked = project != null && project.TryGetValue("commanders", out members) ? members as Dictionary<string, object> : null;
        if (Text(verified, "buildId") != id || Text(verified, "marketId") != market || Text(verified, "status") != "build" || linked == null || !linked.Keys.Any(x => String.Equals(x, commander, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Setup was sent but not fully verified. Reload Raven before retrying.");
        return id;
    }
    sealed class Mock : HttpMessageHandler
    {
        public int Creates; public bool WrongBody, LoseCreateReply, SkipCommander;
        public Dictionary<string, object> Project;
        public Dictionary<string, object> Site = new Dictionary<string, object> { { "id", "plan1" }, { "name", "Xray Xray" }, { "bodyNum", 7 }, { "buildType", "asclepius" }, { "status", "plan" } };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken token)
        {
            string path = request.RequestUri.AbsolutePath; object result = null; var code = HttpStatusCode.OK;
            if (path.EndsWith("/sites"))
            {
                if (request.Method == HttpMethod.Put)
                {
                    var body = ColonizationNeeds.ReadJson(request.Content.ReadAsStringAsync().Result) as Dictionary<string, object>;
                    Site = (Dictionary<string, object>)((object[])body["update"])[0];
                }
                result = new[] { Site };
            }
            else if (path.EndsWith("/project/") && request.Method == HttpMethod.Put)
            {
                Creates++; Project = ColonizationNeeds.ReadJson(request.Content.ReadAsStringAsync().Result) as Dictionary<string, object>;
                Project["buildId"] = "3aa4585e-389d-4cff-a333-0a8f7f01b0aa";
                if (WrongBody) Project["bodyNum"] = 99;
                if (LoseCreateReply) { LoseCreateReply = false; throw new HttpRequestException("Lost create acknowledgment"); }
                result = Project;
            }
            else if (path.Contains("/link/")) { if (!SkipCommander) Project["commanders"] = new Dictionary<string, object> { { "Test", new string[0] } }; }
            else { result = Project; if (Project == null) code = HttpStatusCode.NotFound; }
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(new JavaScriptSerializer().Serialize(result)) });
        }
    }
    public static void RunTests()
    {
        string folder = Path.Combine(Path.GetTempPath(), "ColonizationNeeds-construction-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string journal = Path.Combine(folder, "Journal.test.log");
            File.WriteAllText(journal, "{\"event\":\"LoadGame\",\"Commander\":\"Test\"}\n{\"event\":\"Docked\",\"MarketID\":456}\n{\"event\":\"ColonisationConstructionDepot\",\"MarketID\":456,\"ResourcesRequired\":[{\"Name\":\"$steel_name;\",\"RequiredAmount\":20,\"ProvidedAmount\":5}]}\n");
            if (Text(ReadDock(folder, "Test").Location, "MarketID") != "456") throw new Exception("Dock reconstruction failed");
            bool rejected = false; try { ReadDock(folder, "Other"); } catch (InvalidOperationException) { rejected = true; }
            if (!rejected) throw new Exception("Construction commander isolation failed");
            File.AppendAllText(journal, "{\"event\":\"Undocked\"}\n");
            rejected = false; try { ReadDock(folder, "Test"); } catch (InvalidOperationException) { rejected = true; }
            if (!rejected) throw new Exception("Stale dock reused");
        }
        finally { Directory.Delete(folder, true); }
        var dock = new Dock {
            Location = new Dictionary<string, object> { { "SystemAddress", 123 }, { "MarketID", 456 }, { "StationName", "Orbital Construction Site: Garn Town" }, { "StarSystem", "Test System" }, { "BodyID", 7 } },
            Depot = ColonizationNeeds.ReadJson("{\"event\":\"ColonisationConstructionDepot\",\"MarketID\":456,\"ResourcesRequired\":[{\"Name\":\"$steel_name;\",\"RequiredAmount\":20,\"ProvidedAmount\":5}]}") as Dictionary<string, object>
        };
        using (var mock = new Mock()) using (var http = new HttpClient(mock))
        {
            var service = new RavenConstruction(http, "https://test.invalid/", "test-key", "Test");
            service.Start(dock, mock.Site).GetAwaiter().GetResult();
            var quantities = mock.Project["commodities"] as Dictionary<string, object>;
            if (mock.Creates != 1 || Text(mock.Site, "status") != "build" || Text(mock.Site, "name") != "Garn Town" || Convert.ToInt64(quantities["steel"]) != 15) throw new Exception("Construction creation/absolute requirements failed");
            service.Start(dock, mock.Site).GetAwaiter().GetResult();
            if (mock.Creates != 1) throw new Exception("Existing project duplicated");
        }
        foreach (string scenario in new[] { "uncertain", "wrong-body", "lost-reply", "missing-commander" })
        using (var mock = new Mock()) using (var http = new HttpClient(mock))
        {
            if (scenario == "uncertain") mock.Site["buildType"] = "asclepius?";
            mock.WrongBody = scenario == "wrong-body"; mock.LoseCreateReply = scenario == "lost-reply"; mock.SkipCommander = scenario == "missing-commander";
            if (mock.SkipCommander) { mock.Project = new Dictionary<string, object> { { "buildId", "3aa4585e-389d-4cff-a333-0a8f7f01b0aa" }, { "systemAddress", 123 }, { "marketId", 456 }, { "bodyNum", 7 }, { "buildType", "asclepius" } }; }
            var service = new RavenConstruction(http, "https://test.invalid/", "test-key", "Test"); bool failed = false;
            try { service.Start(dock, mock.Site).GetAwaiter().GetResult(); } catch { failed = true; }
            if (!failed) throw new Exception("Construction did not reject " + scenario);
            if (scenario == "uncertain" && mock.Creates != 0) throw new Exception("Uncertain type created project");
            if (scenario == "lost-reply") { service.Start(dock, mock.Site).GetAwaiter().GetResult(); if (mock.Creates != 1) throw new Exception("Lost creation response duplicated project"); }
        }
    }
}
