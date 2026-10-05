using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

// Contributions are non-idempotent. Persist InFlight before POST; never blindly retry an uncertain result.
public sealed class RavenDeliveryReporter : IDisposable
{
    public sealed class Report
    {
        public string id, system, market, build, kind, state, error, timestamp;
        public Dictionary<string, object> payload;
    }
    public sealed class Saved
    {
        public List<Report> reports = new List<Report>();
        public Dictionary<string, string> depotSignatures = new Dictionary<string, string>();
    }
    readonly string path, commander;
    readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
    readonly FileStream lease;
    Saved saved;
    bool syncing;
    DateTime retryAfter;
    public string Status { get; private set; }
    public IEnumerable<Report> Review { get { return saved.reports.Where(x => x.state == "Uncertain").ToArray(); } }
    public int Pending { get { return saved.reports.Count(x => x.state == "Pending" || x.state == "InFlight"); } }
    public RavenDeliveryReporter(string directory, string commander)
    {
        this.commander = commander;
        Directory.CreateDirectory(directory);
        path = Path.Combine(directory, "raven-reports-" + Hash(commander.Trim().ToLowerInvariant()) + ".json");
        lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            saved = File.Exists(path) ? json.Deserialize<Saved>(File.ReadAllText(path)) : new Saved();
            if (saved == null || saved.reports == null || saved.depotSignatures == null) throw new IOException("Invalid delivery reporting state.");
            foreach (var report in saved.reports.Where(x => x.state == "InFlight")) { report.state = report.kind == "Contribution" ? "Uncertain" : "Pending"; report.error = "Interrupted request; verify contribution on Raven before retrying."; }
            Save(); UpdateStatus();
        }
        catch { lease.Dispose(); throw; }
    }
    static string Hash(string text)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
    }
    void Save()
    {
        var durable = new Saved { depotSignatures = saved.depotSignatures, reports = saved.reports.Select(x => x.state == "Sent" ? new Report { id = x.id, system = x.system, market = x.market, build = x.build, kind = x.kind, state = x.state, timestamp = x.timestamp } : x).ToList() };
        File.WriteAllText(path + ".tmp", json.Serialize(durable));
        if (File.Exists(path)) File.Replace(path + ".tmp", path, null); else File.Move(path + ".tmp", path);
        foreach (var report in saved.reports.Where(x => x.state == "Sent")) report.payload = null;
    }
    void UpdateStatus()
    {
        int review = Review.Count();
        Status = review > 0 ? "Raven: " + review + " delivery report(s) need review in Settings" : Pending > 0 ? "Raven reporting: " + Pending + " pending" : "Raven reporting: Up to date";
    }
    public static Dictionary<string, long> Contributions(Dictionary<string, object> entry)
    {
        var result = new Dictionary<string, long>(); object rows;
        if (!entry.TryGetValue("Contributions", out rows) || !(rows is object[])) throw new IOException("Delivery event has no contribution list.");
        foreach (var value in (object[])rows)
        {
            var row = value as Dictionary<string, object>;
            if (row == null || !row.ContainsKey("Name") || !row.ContainsKey("Amount")) throw new IOException("Incomplete delivery contribution.");
            string key = JournalCargoTracker.Canonical(Convert.ToString(row["Name"]));
            long amount = Convert.ToInt64(row["Amount"]);
            if (amount < 0 || key.Length == 0) throw new IOException("Invalid delivery contribution.");
            if (amount > 0) result[key] = checked((result.ContainsKey(key) ? result[key] : 0L) + amount);
        }
        return result;
    }
    public void Capture(string identity, string system, string market, Dictionary<string, object> entry)
    {
        string id = Hash(commander.Trim().ToLowerInvariant() + "\n" + identity);
        if (saved.reports.Any(x => x.id == id)) return;
        string evt = Convert.ToString(entry["event"]), signature = null;
        Dictionary<string, object> payload;
        if (evt == "ColonisationContribution")
        {
            var cargo = Contributions(entry); if (cargo.Count == 0) return;
            payload = cargo.ToDictionary(x => x.Key, x => (object)x.Value);
        }
        else if (evt == "ColonisationConstructionDepot")
        {
            var remaining = JournalCargoTracker.DepotRemaining(entry); if (remaining.Count == 0) return;
            long maximum = 0;
            foreach (var row in (object[])entry["ResourcesRequired"]) maximum = checked(maximum + Convert.ToInt64(((Dictionary<string, object>)row)["RequiredAmount"]));
            string complete = entry.ContainsKey("ConstructionComplete") ? Convert.ToString(entry["ConstructionComplete"]) : "";
            string failed = entry.ContainsKey("ConstructionFailed") ? Convert.ToString(entry["ConstructionFailed"]) : "";
            signature = Hash(String.Join(";", remaining.OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value)) + ";" + maximum + ";" + complete + ";" + failed);
            string old;
            if (market.Length > 0 && saved.depotSignatures.TryGetValue(market, out old) && old == signature) return;
            payload = new Dictionary<string, object> { { "commodities", remaining }, { "maxNeed", maximum }, { "colonisationConstructionDepot", entry } };
        }
        else return;
        var report = new Report { id = id, system = system, market = market, kind = evt == "ColonisationContribution" ? "Contribution" : "Depot", state = "Pending", payload = payload, timestamp = entry.ContainsKey("timestamp") ? Convert.ToString(entry["timestamp"]) : "" };
        saved.reports.Add(report);
        string previous = null; bool hadPrevious = market.Length > 0 && saved.depotSignatures.TryGetValue(market, out previous);
        if (signature != null && market.Length > 0) saved.depotSignatures[market] = signature;
        try { Save(); }
        catch { saved.reports.Remove(report); if (signature != null && market.Length > 0) { if (hadPrevious) saved.depotSignatures[market] = previous; else saved.depotSignatures.Remove(market); } throw; }
        UpdateStatus();
    }
    public void Resolve(string id, bool retry)
    {
        var report = saved.reports.Single(x => x.id == id && x.state == "Uncertain");
        report.state = retry ? "Pending" : "Sent";
        try { Save(); } catch { report.state = "Uncertain"; throw; }
        retryAfter = DateTime.MinValue; UpdateStatus();
    }
    public async Task Sync(HttpClient client, string baseUrl, string apiKey, Func<string, string, Task<string>> resolveBuild)
    {
        if (syncing || DateTime.UtcNow < retryAfter) return;
        if (String.IsNullOrWhiteSpace(apiKey)) { Status = "Raven reporting: API key required"; return; }
        syncing = true;
        try
        {
            foreach (var report in saved.reports.Where(x => x.state == "Pending").ToArray())
            {
                if (report.kind == "Contribution" && Review.Any()) continue;
                try
                {
                    if (String.IsNullOrEmpty(report.build))
                    {
                        report.build = await resolveBuild(report.system, report.market);
                        if (String.IsNullOrEmpty(report.build)) { Status = "Raven: project lookup pending for market " + report.market; retryAfter = DateTime.UtcNow.AddSeconds(30); continue; }
                        Save();
                    }
                    // A newer pending snapshot supersedes an older unsent depot observation.
                    if (report.kind == "Depot" && saved.reports.Any(x => x.kind == "Depot" && x.market == report.market && x.state == "Pending" && String.CompareOrdinal(x.timestamp, report.timestamp) > 0)) { report.state = "Sent"; Save(); continue; }
                    string endpoint = "project/" + Uri.EscapeDataString(report.build);
                    if (report.kind == "Contribution") endpoint += "/contribute/" + Uri.EscapeDataString(commander);
                    else
                    {
                        // Do not overwrite a more recent project observation with an offline snapshot.
                        using (var read = Request(HttpMethod.Get, baseUrl + endpoint, apiKey, null))
                        using (var response = await client.SendAsync(read))
                        {
                            if (!response.IsSuccessStatusCode) throw new IOException("Project check HTTP " + (int)response.StatusCode);
                            var project = ColonizationNeeds.ParseProject(await response.Content.ReadAsStringAsync());
                            DateTimeOffset serverTime, journalTime;
                            if (project.ContainsKey("timestamp") && DateTimeOffset.TryParse(Convert.ToString(project["timestamp"]), out serverTime) && DateTimeOffset.TryParse(report.timestamp, out journalTime) && serverTime > journalTime) { report.state = "Sent"; Save(); continue; }
                        }
                        report.payload["buildId"] = report.build;
                    }
                    report.state = "InFlight"; Save();
                    using (var request = Request(report.kind == "Contribution" ? HttpMethod.Post : new HttpMethod("PATCH"), baseUrl + endpoint, apiKey, json.Serialize(report.payload)))
                    using (var response = await client.SendAsync(request))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            int code = (int)response.StatusCode;
                            report.state = report.kind == "Contribution" && (code >= 500 || code == 408) ? "Uncertain" : "Pending";
                            report.error = "Raven HTTP " + code;
                            Save(); Status = "Raven report: " + report.error + (report.state == "Uncertain" ? " · review in Settings" : " · will retry"); retryAfter = DateTime.UtcNow.AddMinutes(1); return;
                        }
                    }
                    report.state = "Sent"; report.error = null;
                    try { Save(); } catch { report.state = report.kind == "Contribution" ? "Uncertain" : "Pending"; throw; }
                }
                catch (Exception ex)
                {
                    if (report.state == "InFlight") report.state = report.kind == "Contribution" ? "Uncertain" : "Pending";
                    report.error = ex.Message;
                    try { Save(); } catch { }
                    Status = "Raven report: " + ex.Message + (report.state == "Uncertain" ? " · review in Settings" : " · pending"); retryAfter = DateTime.UtcNow.AddMinutes(1); return;
                }
            }
            UpdateStatus();
        }
        finally { syncing = false; }
    }
    HttpRequestMessage Request(HttpMethod method, string url, string key, string body)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("rcc-key", key);
        request.Headers.Add("rcc-cmdr0", Convert.ToBase64String(Encoding.UTF8.GetBytes(commander)));
        if (body != null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return request;
    }
    public void Dispose() { lease.Dispose(); }
}
