using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class ActiveProjectUITests
{
    class Api : HttpMessageHandler
    {
        public List<string> requests = new List<string>();
        public bool extra, renamed;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            if (request.Method != HttpMethod.Get) throw new Exception("Project preference tried to write to an API");
            string path = request.RequestUri.AbsolutePath; requests.Add(path);
            string body;
            if (path.EndsWith("/active")) body = "[{\"buildId\":\"one\",\"buildName\":\"" + (renamed ? "Renamed" : "Same name") + "\"},{\"buildId\":\"two\",\"buildName\":\"Same name\"}" + (extra ? ",{\"buildId\":\"three\",\"buildName\":\"New project\"}" : "") + "]";
            else body = "{\"commodities\":{\"steel\":" + (path.EndsWith("/one") ? "200" : path.EndsWith("/two") ? "100" : "50") + "}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
    static BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(object o, string name) { return o.GetType().GetField(name, flags).GetValue(o); }
    static void Set(object o, string name, object value) { o.GetType().GetField(name, flags).SetValue(o, value); }
    static object Call(object o, string name, params object[] args) { return o.GetType().GetMethod(name, flags).Invoke(o, args); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    static void Wait(Task task)
    {
        DateTime stop = DateTime.UtcNow.AddSeconds(30);
        while (!task.IsCompleted) { Application.DoEvents(); Thread.Sleep(2);  if (DateTime.UtcNow > stop) throw new Exception("Project selection timed out"); }
        task.GetAwaiter().GetResult();
    }
    static void Refresh(ColonizationNeeds form) { Wait((Task)Call(form, "LoadProject")); }
    static void Need(ColonizationNeeds form, long amount)
    {
        var rows = (ListView)Get(form, "list");
        Check(rows.Items.Count == 1, "Expected one commodity: " + ((Label)Get(form, "status")).Text);
        Check(rows.Items[0].SubItems[1].Text == amount.ToString("N0"), "Combined need incorrect");
        Check(rows.Items[0].SubItems[2].Text == "30" && rows.Items[0].SubItems[3].Text == "100" && rows.Items[0].SubItems[4].Text == "40", "Stock/inbound/outbound changed with project scope");
        Check(rows.Items[0].SubItems[5].Text == Math.Max(0, amount - 170).ToString("N0"), "To collect did not use chosen-project need");
    }
    static IEnumerable<Control> Controls(Control root)
    { foreach (Control child in root.Controls) { yield return child; foreach (var nested in Controls(child)) yield return nested; } }
    static void Picker(ColonizationNeeds form, Action<Form> action)
    {
        Console.WriteLine("Opening project picker"); Exception failure = null;
        using (var timer = new System.Windows.Forms.Timer { Interval = 100 })
        {
            timer.Tick += delegate
            {
                var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Text == "Choose active projects");
                if (dialog == null) return;
                Console.WriteLine("Project picker found"); timer.Stop();
                try { action(dialog); Console.WriteLine("Picker action completed"); }
                catch (Exception ex) { failure = ex; dialog.Close(); }
            };
            timer.Start(); Wait((Task)Call(form, "ChooseActiveProjects")); Console.WriteLine("Picker task completed");
        }
        if (failure != null) throw failure;
    }
    static void RunChecks(ColonizationNeeds form,string root,string preview)
    {
                Set(form, "settings", Path.Combine(root, "settings.json")); Set(form, "commander", "Test Commander"); Set(form, "profileRoot", root); Set(form, "journalFolder", root);
                ((HttpClient)Get(form, "client")).Dispose(); var api = new Api(); Set(form, "client", new HttpClient(api));
                var stock = new CarrierInventory(Path.Combine(root, "carrier-inventory.json"), new Dictionary<string, long>());
                stock.Data.carriers.Add(new CarrierInventory.Carrier { id = "123", name = "Carrier" }); stock.Edit("123", "steel", 30, "Set total");
                stock.ApplyTransit(new TransitLedger.Event { id = "in", kind = "inbound", carrier = "any", commodity = "steel", amount = 100, timestamp = "2026-10-26T01:00:00Z" });
                stock.ApplyTransit(new TransitLedger.Event { id = "out", kind = "withdraw", carrier = "123", commodity = "steel", amount = 40, timestamp = "2026-10-26T01:01:00Z" });
                Set(form, "carrierInventory", stock); Set(form, "inventoryReady", true); Call(form, "RefreshCarrierChoices"); Call(form, "RefreshCarrierStock");
                string unchanged = new JavaScriptSerializer().Serialize(stock.Data);
                Refresh(form); Need(form, 300);
                var selector = (ComboBox)Get(form, "selection");
                var button = (Button)Get(form, "activeProjectsButton");
                Check(button.Visible && button.Left >= selector.Right && button.Right <= button.Parent.ClientSize.Width, "Projects control overlaps selector");
                Call(form, "SetCompactView", true); Application.DoEvents(); Check(button.Visible && button.Left >= selector.Right, "Compact view hides project selection");
                // Save a real dialog selection. Identical display names must not collide.
                Picker(form, delegate(Form dialog)
                {
                    var list = Controls(dialog).OfType<CheckedListBox>().Single();
                    Check(list.Items.Count == 2, "Picker did not show Raven projects");
                    list.SetItemChecked(1, false);
                    if(!String.IsNullOrEmpty(preview))using(var bitmap=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(preview+".projects.png");}
                    Controls(dialog).OfType<Button>().Single(x => x.Text == "Save").PerformClick();
                });
                Need(form, 200); Check(!ProjectSelectionOptions.Load(root).all && !ProjectSelectionOptions.Load(root).Includes("two"), "Selection did not persist");
                api.requests.Clear(); api.renamed = true; api.extra = true; Refresh(form); Need(form, 200);
                Check(!api.requests.Any(x => x.EndsWith("/two") || x.EndsWith("/three")), "Excluded or new unchosen project fetched into combined total");
                // Exclusion applies only to combined view, not individual inspection.
                selector.SelectedIndex = 2; Application.DoEvents(); Refresh(form); Need(form, 100);
                selector.SelectedIndex = 0; Application.DoEvents(); Refresh(form); Need(form, 200);
                string choices = File.ReadAllText(Path.Combine(root, "active-projects.json"));
                Picker(form, delegate(Form dialog) { Controls(dialog).OfType<Button>().Single(x => x.Text == "Clear all").PerformClick(); Controls(dialog).OfType<Button>().Single(x => x.Text == "Cancel").PerformClick(); });
                Check(File.ReadAllText(Path.Combine(root, "active-projects.json")) == choices, "Cancel changed saved choices"); Need(form, 200);
                Picker(form, delegate(Form dialog) { Controls(dialog).OfType<Button>().Single(x => x.Text == "Clear all").PerformClick(); Controls(dialog).OfType<Button>().Single(x => x.Text == "Save").PerformClick(); });
                Check(((ListView)Get(form, "list")).Items.Count == 0 && ((Label)Get(form, "status")).Text.Contains("No projects selected"), "Empty selection fell back to all");
                new ProjectSelectionOptions { all = false, included = new List<string> { "one" } }.Save(root);
                string second = Path.Combine(root, "second"); Directory.CreateDirectory(second); Set(form, "settings", Path.Combine(second, "settings.json")); Set(form, "commander", "Other"); Refresh(form); Need(form, 350);
                Set(form, "settings", Path.Combine(root, "settings.json")); Set(form, "commander", "Test Commander"); Refresh(form); Need(form, 200);
                new ProjectSelectionOptions().Save(root); Refresh(form); Need(form, 350);
                File.WriteAllText(Path.Combine(root, "active-projects.json"), "null"); api.requests.Clear(); Refresh(form);
                Check(!api.requests.Any(x => x.Contains("/project/")) && ((Label)Get(form, "status")).Text.Contains("unreadable"), "Corrupt choices silently included all projects");
                Check(new JavaScriptSerializer().Serialize(stock.Data) == unchanged, "Project preferences changed carrier/transit ledger");

    }
    [STAThread] static void Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        string root = Path.Combine(Path.GetTempPath(), "Active-projects-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            using (var form = new ColonizationNeeds(true))
            {
                Exception failure=null; form.ShowInTaskbar=false;
                form.Shown+=delegate {try {RunChecks(form,root,args.Length>0?args[0]:null);}catch(Exception ex){failure=ex;}finally {form.Close();}};
                Application.Run(form);
                if(failure!=null)throw failure;

            }
        }
        finally
        {
            foreach (string folder in Directory.GetDirectories(root)) { foreach (string file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder); }
            foreach (string file in Directory.GetFiles(root)) File.Delete(file); Directory.Delete(root);
        }
        Console.WriteLine("Active project UI checks passed: exact-ID subset, empty/all, new/renamed projects, cancel/restart/profile isolation, individual view and unchanged stock/transit.");
    }
}
