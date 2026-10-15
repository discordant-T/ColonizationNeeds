using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

// Profile folders isolate credentials and durable queues; originals are never removed during migration.
public sealed class PlayerProfiles
{
    public sealed class Profile
    {
        public string id, name;
        public override string ToString() { return name; }
    }
    public sealed class Index
    {
        public string active;
        public List<Profile> profiles = new List<Profile>();
    }
    readonly string root, indexPath;
    readonly JavaScriptSerializer json = new JavaScriptSerializer();
    Index index;
    public IEnumerable<Profile> All { get { return index.profiles.OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase).ToArray(); } }
    public Profile Active { get { return index.profiles.SingleOrDefault(x => x.id == index.active); } }
    public static string Id(string name)
    {
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(name.Trim().ToLowerInvariant()))).Replace("-", "");
    }
    public string Folder(Profile profile)
    {
        if (profile == null || profile.id != Id(profile.name)) throw new IOException("Invalid player profile identity.");
        return Path.Combine(root, "profiles", profile.id);
    }
    public PlayerProfiles(string root)
    {
        this.root = root; indexPath = Path.Combine(root, "profiles.json");
        index = File.Exists(indexPath) ? json.Deserialize<Index>(File.ReadAllText(indexPath)) : new Index();
        if (index == null || index.profiles == null || index.profiles.Any(x => x == null || String.IsNullOrWhiteSpace(x.name) || x.id != Id(x.name)) || index.profiles.Select(x => x.id).Distinct().Count() != index.profiles.Count || (index.active != null && Active == null)) throw new IOException("Player profiles could not be read. Existing account files have been retained.");
        ImportLegacy();
    }
    public void ImportLegacy()
    {
        if (index.profiles.Count > 0 || !File.Exists(Path.Combine(root, "settings.json"))) return;
        var saved = json.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(root, "settings.json")));
        string name;
        if (saved == null || !saved.TryGetValue("commander", out name) || String.IsNullOrWhiteSpace(name)) return;
        var profile = new Profile { name = name.Trim(), id = Id(name) };
        string destination = Folder(profile); Directory.CreateDirectory(destination);
        foreach (string source in Directory.GetFiles(root, "*.json"))
        {
            string file = Path.GetFileName(source);
            if (file == "profiles.json") continue;
            string target = Path.Combine(destination, file);
            if (!File.Exists(target)) File.Copy(source, target);
        }
        index.profiles.Add(profile); index.active = profile.id;
        try { Save(); } catch { index.profiles.Remove(profile); index.active = null; throw; }
    }
    public Profile Add(string name)
    {
        name = name.Trim();
        if (name.Length == 0 || name.Contains("\r") || name.Contains("\n")) throw new ArgumentException("Enter the player's exact Raven commander name on one line.");
        string id = Id(name);
        if (index.profiles.Any(x => x.id == id)) throw new ArgumentException("That player already has a profile. Select it from Player.");
        var profile = new Profile { name = name, id = id };
        string folder = Folder(profile); Directory.CreateDirectory(folder);
        if (File.Exists(Path.Combine(folder, "settings.json"))) throw new IOException("An existing profile folder needs recovery; no settings were overwritten.");
        // Credentials, mode and reporting start blank/off. Recover only this commander's legacy stock, if present.
        foreach (string source in Directory.Exists(root) ? Directory.GetFiles(root, "inventory-" + id + "*.json") : new string[0])
            File.Copy(source, Path.Combine(folder, Path.GetFileName(source)), false);
        string reports = Path.Combine(root, "raven-reports-" + id + ".json");
        if (File.Exists(reports)) File.Copy(reports, Path.Combine(folder, Path.GetFileName(reports)), false);
        File.WriteAllText(Path.Combine(folder, "settings.json"), json.Serialize(new Dictionary<string, string> { { "commander", name }, { "key", "" }, { "reportDeliveries", "false" } }));
        index.profiles.Add(profile);
        try { Save(); } catch { index.profiles.Remove(profile); throw; }
        return profile;
    }
    public void Activate(Profile profile)
    {
        if (!index.profiles.Any(x => x.id == profile.id)) throw new IOException("Unknown player profile.");
        string previous = index.active; index.active = profile.id;
        try { Save(); } catch { index.active = previous; throw; }
    }
    void Save()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(indexPath + ".tmp", json.Serialize(index));
        if (File.Exists(indexPath)) File.Replace(indexPath + ".tmp", indexPath, null); else File.Move(indexPath + ".tmp", indexPath);
    }
    public static void RunTests()
    {
        string root = Path.Combine(Path.GetTempPath(), "ColonizationNeeds-profiles-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "settings.json"), "{\"commander\":\"Alpha\",\"key\":\"encrypted-marker\",\"reportDeliveries\":\"true\"}");
            File.WriteAllText(Path.Combine(root, "shared-settings.json"), "encrypted-shared-marker");
            File.WriteAllText(Path.Combine(root, "raven-reports-test.json"), "saved-queue-marker");
            var store = new PlayerProfiles(root); var alpha = store.Active;
            if (alpha.name != "Alpha" || File.ReadAllText(Path.Combine(store.Folder(alpha), "shared-settings.json")) != "encrypted-shared-marker" || !File.Exists(Path.Combine(root, "settings.json"))) throw new Exception("Profile migration lost legacy settings/queues");
            var beta = store.Add("Beta"); string betaFolder = store.Folder(beta);
            if (File.ReadAllText(Path.Combine(betaFolder, "settings.json")).Contains("encrypted-marker") || File.Exists(Path.Combine(betaFolder, "shared-settings.json"))) throw new Exception("New player inherited another player's credentials");
            File.WriteAllText(Path.Combine(store.Folder(alpha), "stock-test.json"), "123");
            File.WriteAllText(Path.Combine(betaFolder, "stock-test.json"), "456");
            File.WriteAllText(Path.Combine(betaFolder, "raven-reports-test.json"), "beta-pending");
            store.Activate(beta); store = new PlayerProfiles(root);
            if (store.Active.name != "Beta" || store.All.Count() != 2) throw new Exception("Active player not preserved on restart");
            bool duplicate = false; try { store.Add(" alpha "); } catch (ArgumentException) { duplicate = true; }
            if (!duplicate) throw new Exception("Duplicate commander profile allowed");
            store.Activate(store.All.Single(x => x.name == "Alpha"));
            if (!File.Exists(Path.Combine(store.Folder(store.Active), "raven-reports-test.json"))) throw new Exception("Switching lost pending queue");
            if (File.ReadAllText(Path.Combine(store.Folder(store.Active), "stock-test.json")) != "123" || File.ReadAllText(Path.Combine(betaFolder, "stock-test.json")) != "456" || File.ReadAllText(Path.Combine(betaFolder, "raven-reports-test.json")) != "beta-pending") throw new Exception("Player stock or reporting queues crossed accounts");
            File.WriteAllText(Path.Combine(store.Folder(alpha), "inventory-test.requirements.json"), "{\"alpha|build-one\":{\"liquidoxygen\":1553},\"alpha|build-two\":{\"water\":999}}");
            var requirements = ColonizationNeeds.ReadOtherProfileRequirements(root, "build-one");
            if (requirements.Count != 1 || requirements["liquidoxygen"] != 1553) throw new Exception("Original totals were not matched to the exact project");
            File.WriteAllText(Path.Combine(betaFolder, "inventory-test.requirements.json"), "{\"beta|build-one\":{\"liquidoxygen\":12}}");
            if (ColonizationNeeds.ReadOtherProfileRequirements(root, "build-one").Count != 0) throw new Exception("Conflicting original totals were reused");
        }
        finally { Directory.Delete(root, true); }
    }
}
