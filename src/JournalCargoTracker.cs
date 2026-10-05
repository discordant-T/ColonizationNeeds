using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

// Reads only complete, newly appended journal lines. Cargo snapshots are not acquisitions.
public sealed class JournalCargoTracker
{
    sealed class Cursor { public long Offset; public bool SkipPartial; public string Commander = "", Vessel = "Ship"; }
    readonly Dictionary<string, Cursor> cursors = new Dictionary<string, Cursor>(StringComparer.OrdinalIgnoreCase);
    readonly string folder;
    string currentCommander = "", currentVessel = "Ship";
    public string CurrentCommander { get { return currentCommander; } }
    public JournalCargoTracker(string folder)
    {
        this.folder = folder;
        var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "Journal.*.log").OrderBy(File.GetLastWriteTimeUtc).ToArray() : new string[0];
        foreach (var file in files)
        {
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                long length = stream.Length;
                if (length > 0) stream.Position = length - 1;
                cursors[file] = new Cursor { Offset = length, SkipPartial = length > 0 && stream.ReadByte() != '\n' };
            }
        }
        if (files.Length > 0)
        {
            var cursor = cursors[files[files.Length - 1]];
            using (var stream = new FileStream(files[files.Length - 1], FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    try { UpdateContext(cursor, Parse(line)); } catch (ArgumentException) { }
                }
            }
            currentCommander = cursor.Commander; currentVessel = cursor.Vessel;
        }
    }
    static Dictionary<string, object> Parse(string line) { return new JavaScriptSerializer().DeserializeObject(line.TrimStart('\uFEFF')) as Dictionary<string, object>; }
    static string Text(Dictionary<string, object> data, string key) { object value; return data != null && data.TryGetValue(key, out value) ? Convert.ToString(value) : ""; }
    static long Count(Dictionary<string, object> data, string key, long fallback)
    {
        object value; if (!data.TryGetValue(key, out value)) return fallback;
        long n = Convert.ToInt64(value); if (n < 0) throw new ArgumentException("Negative cargo quantity in journal."); return n;
    }
    static void UpdateContext(Cursor cursor, Dictionary<string, object> data)
    {
        string evt = Text(data, "event");
        if (evt == "LoadGame") { cursor.Commander = Text(data, "Commander"); cursor.Vessel = "Ship"; }
        if (evt == "Commander") cursor.Commander = Text(data, "Name");
        if (evt == "Cargo" && Text(data, "Vessel").Length > 0) cursor.Vessel = Text(data, "Vessel");
        if (evt == "LaunchSRV") cursor.Vessel = "SRV";
        if (evt == "DockSRV" || evt == "ShipyardSwap" || evt == "ShipyardNew") cursor.Vessel = "Ship";
        if (evt == "Shutdown") cursor.Commander = "";
    }
    public static Dictionary<string, long> Acquisitions(Dictionary<string, object> data, string vessel)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (data == null) return result;
        Action<string, long> add = delegate(string key, long count)
        {
            key = Canonical(key);
            if (key.Length > 0 && count > 0) result[key] = checked((result.ContainsKey(key) ? result[key] : 0) + count);
        };
        string evt = Text(data, "event");
        if (evt == "CargoTransfer")
        {
            object value;
            if (data.TryGetValue("Transfers", out value)) foreach (var row in (object[])value)
            {
                var transfer = row as Dictionary<string, object>;
                if (String.Equals(Text(transfer, "Direction"), "toship", StringComparison.OrdinalIgnoreCase)) add(Text(transfer, "Type"), Count(transfer, "Count", 0));
            }
            return result;
        }
        if (!String.Equals(vessel, "Ship", StringComparison.OrdinalIgnoreCase)) return result;
        if (evt == "MarketBuy" || evt == "BuyDrones" || evt == "PowerplayCollect") add(Text(data, "Type"), Count(data, "Count", 0));
        if (evt == "CollectCargo" || evt == "MiningRefined") add(Text(data, "Type"), Count(data, "Count", 1));
        if (evt == "CargoDepot" && Text(data, "UpdateType") == "Collect") add(Text(data, "CargoType"), Count(data, "Count", 0));
        if (evt == "MissionCompleted")
        {
            object value;
            if (data.TryGetValue("CommodityReward", out value)) foreach (var row in (object[])value)
            {
                var reward = row as Dictionary<string, object>; add(Text(reward, "Name"), Count(reward, "Count", 1));
            }
        }
        return result;
    }
    public static void RunTests()
    {
        string root = Path.Combine(Path.GetTempPath(), "ColonizationNeeds-cargo-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string first = Path.Combine(root, "Journal.01.log"), second = Path.Combine(root, "Journal.02.log");
        try
        {
            File.WriteAllText(first, "{\"event\":\"LoadGame\",\"Commander\":\"Test\"}\n{\"event\":\"MarketBuy\",\"Type\":\"steel\",\"Count\":99}\n");
            var tracker = new JournalCargoTracker(root);
            long total = 0;
            Action<Dictionary<string, long>> add = delegate(Dictionary<string, long> gained) { total += gained.Values.Sum(); };
            tracker.Poll("Test", add);
            if (total != 0) throw new Exception("Historical cargo replayed");
            File.AppendAllText(first, "{\"event\":\"MarketBuy\",\"Type\":\"steel\",\"Count\":5}\n{\"event\":\"Cargo\",\"Vessel\":\"Ship\",\"Inventory\":[{\"Name\":\"steel\",\"Count\":104}]}\n");
            tracker.Poll("Test", add); tracker.Poll("Test", add);
            if (total != 5) throw new Exception("Cargo snapshot or duplicate counted");
            File.AppendAllText(first, "{\"event\":\"MiningRefined\",\"Type\":\"steel\"");
            tracker.Poll("Test", add);
            if (total != 5) throw new Exception("Incomplete line processed");
            File.AppendAllText(first, "}\n{\"event\":\"CargoTransfer\",\"Transfers\":[{\"Type\":\"steel\",\"Count\":3,\"Direction\":\"toship\"},{\"Type\":\"steel\",\"Count\":8,\"Direction\":\"fromship\"}]}\n{\"event\":\"LaunchSRV\"}\n{\"event\":\"CollectCargo\",\"Type\":\"steel\"}\n{\"event\":\"DockSRV\"}\n{\"event\":\"CollectCargo\",\"Type\":\"steel\"}\n");
            tracker.Poll("Test", add);
            if (total != 10) throw new Exception("Transfer/SRV handling failed");
            File.AppendAllText(first, "{\"event\":\"MarketBuy\",\"Type\":\"steel\",\"Count\":7}\n");
            bool failed = false;
            try { tracker.Poll("Test", delegate(Dictionary<string, long> gained) { throw new IOException("Simulated failed save"); }); } catch (IOException) { failed = true; }
            tracker.Poll("Test", add);
            if (!failed || total != 17) throw new Exception("Failed save retry failed");
            File.WriteAllText(second, "{\"event\":\"LoadGame\",\"Commander\":\"Other\"}\n{\"event\":\"MarketBuy\",\"Type\":\"steel\",\"Count\":50}\n{\"event\":\"LoadGame\",\"Commander\":\"Test\"}\n{\"event\":\"MarketBuy\",\"Type\":\"steel\",\"Count\":2}\n");
            tracker.Poll("Test", add);
            if (total != 19) throw new Exception("Rollover/commander isolation failed");
            File.AppendAllText(second, "{\"event\":\"MarketBuy\",\"Type\":\"steel\",\"Count\":100");
            tracker = new JournalCargoTracker(root);
            File.AppendAllText(second, "}\n{\"event\":\"MarketBuy\",\"Type\":\"steel\",\"Count\":4}\n");
            tracker.Poll("Test", add); tracker.Poll("Test", add);
            if (total != 23) throw new Exception("Restart or initial partial cargo replayed");
            long depotCalls = 0;
            Action<string, string, Dictionary<string, long>> receive = delegate(string market, string timestamp, Dictionary<string, long> remaining)
            {
                if (market != "123" || remaining["steel"] != 60 || remaining["water"] != 0) throw new Exception("Depot balance parsing failed");
                depotCalls++;
            };
            File.AppendAllText(second, "{\"event\":\"ColonisationConstructionDepot\",\"MarketID\":123,\"ResourcesRequired\":[{\"Name\":\"$steel_name;\",\"RequiredAmount\":100,\"ProvidedAmount\":40},{\"Name\":\"water\",\"RequiredAmount\":10,\"ProvidedAmount\":15}]}\n");
            tracker.Poll("Test", add, receive); tracker.Poll("Test", add, receive);
            if (depotCalls != 1 || total != 23) throw new Exception("Depot snapshot replayed or affected stock");
            File.AppendAllText(second, "{\"event\":\"LoadGame\",\"Commander\":\"Other\"}\n{\"event\":\"ColonisationConstructionDepot\",\"MarketID\":123,\"ResourcesRequired\":[{\"Name\":\"steel\",\"RequiredAmount\":100,\"ProvidedAmount\":100}]}\n");
            tracker.Poll("Test", add, receive);
            if (depotCalls != 1) throw new Exception("Other commander depot processed");
        }
        finally
        {
            if (File.Exists(first)) File.Delete(first);
            if (File.Exists(second)) File.Delete(second);
            Directory.Delete(root);
        }
    }
    public static string Canonical(string key)
    {
        string value = key.Trim().ToLowerInvariant().Replace("$", "").Replace("_name;", "").Replace("_name", "");
        if (value == "combatstabilizers") return "combatstabilisers";
        if (value == "muonimager") return "mutomimager";
        if (value == "landenrichmentsystems") return "terrainenrichmentsystems";
        if (value == "microbialfurnaces") return "heliostaticfurnaces";
        return value;
    }
    public void Poll(string expectedCommander, Action<Dictionary<string, long>> apply)
    {
        Poll(expectedCommander, apply, null);
    }
    public static Dictionary<string, long> DepotRemaining(Dictionary<string, object> data)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        object resources;
        if (Text(data, "event") != "ColonisationConstructionDepot" || !data.TryGetValue("ResourcesRequired", out resources)) return result;
        foreach (var resource in (object[])resources)
        {
            var row = resource as Dictionary<string, object>;
            string key = Canonical(Text(row, "Name"));
            if (key.Length == 0 || row == null) continue;
            if (!row.ContainsKey("RequiredAmount") || !row.ContainsKey("ProvidedAmount")) throw new ArgumentException("Incomplete construction-depot quantities.");
            long required = Count(row, "RequiredAmount", 0), provided = Count(row, "ProvidedAmount", 0);
            result[key] = Math.Max(0, required - provided);
        }
        return result;
    }
    public void Poll(string expectedCommander, Action<Dictionary<string, long>> apply, Action<string, string, Dictionary<string, long>> depot)
    {
        if (!Directory.Exists(folder)) throw new IOException("Journal folder not found. Choose it in Settings.");
        foreach (var file in Directory.GetFiles(folder, "Journal.*.log").OrderBy(File.GetLastWriteTimeUtc))
        {
            Cursor cursor;
            if (!cursors.TryGetValue(file, out cursor))
            {
                cursor = new Cursor { Commander = currentCommander, Vessel = currentVessel }; cursors[file] = cursor;
            }
            if (new FileInfo(file).Length == cursor.Offset) continue;
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (stream.Length < cursor.Offset) { cursor.Offset = stream.Length; continue; }
                stream.Position = cursor.Offset;
                using (var line = new MemoryStream())
                {
                    int next;
                    while ((next = stream.ReadByte()) >= 0)
                    {
                        if (next != '\n') { line.WriteByte((byte)next); continue; }
                        if (cursor.SkipPartial) { cursor.SkipPartial = false; cursor.Offset = stream.Position; line.SetLength(0); continue; }
                        string text = Encoding.UTF8.GetString(line.ToArray()).Trim(); line.SetLength(0);
                        var data = text.Length > 0 ? Parse(text) : null;
                        UpdateContext(cursor, data);
                        currentCommander = cursor.Commander; currentVessel = cursor.Vessel;
                        if (String.Equals(cursor.Commander, expectedCommander.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            if (depot != null && Text(data, "event") == "ColonisationConstructionDepot")
                            {
                                string market = Text(data, "MarketID");
                                var remaining = DepotRemaining(data);
                                if (market.Length > 0 && remaining.Count > 0) depot(market, Text(data, "timestamp"), remaining);
                            }
                            var gained = Acquisitions(data, cursor.Vessel);
                            if (gained.Count > 0) apply(gained); // Commit inventory before acknowledging this line.
                        }
                        cursor.Offset = stream.Position;
                    }
                }
            }
        }
    }
}
