using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
class QuietStatusUITests
{
    static object Get(object obj,string name){return obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);}
    static void Set(object obj,string name,object value){obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);}
    static void Call(object obj,string name,params object[] args){obj.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,args);}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    [STAThread]static void Main()
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string root=Path.Combine(Path.GetTempPath(),"Quiet-status-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        string journals=Path.Combine(root,"journals");Directory.CreateDirectory(journals);
        string journal=Path.Combine(journals,"Journal.test.log"),cursor=Path.Combine(root,"cursor.json");
        File.WriteAllText(journal,"{\"event\":\"LoadGame\",\"Commander\":\"Test\"}\n");
        var first=new JournalCargoTracker(journals,cursor);
        var restarted=new JournalCargoTracker(journals,cursor);
        Check(restarted.CurrentCommander=="Test","Durable cursor restart erased observed commander without new journal rows");
        try {using(var form=new ColonizationNeeds(true)) {
            Set(form,"settings",Path.Combine(root,"settings.json"));Set(form,"commander","Test");Set(form,"journalFolder",journals);Set(form,"selectedCargoMode","Automatic");Set(form,"cargoTracker",restarted);
            Set(form,"carrierInventory",new CarrierInventory(Path.Combine(root,"stock.json"),new Dictionary<string,long>()));Set(form,"inventoryReady",true);
            ((Label)Get(form,"status")).Text="1 project · Updated";form.Show();Call(form,"SetCompactView",true);
            var cargo=(Label)Get(form,"cargoStatus");var alert=(Label)Get(form,"compactAlert");var summary=(Button)Get(form,"statusToggle");
            Call(form,"PollCargo");Check(!alert.Visible && !cargo.Text.Contains("paused"),"Matching idle journal reported waiting/mismatch");
            File.AppendAllText(journal,"{\"event\":\"Shutdown\"}\n");Call(form,"PollCargo");
            Check(!alert.Visible && cargo.Text.Contains("idle") && summary.Text.StartsWith("Idle"),"Routine idle created a visible warning");
            int changes=0;cargo.TextChanged+=delegate {changes++;};
            int lines=File.ReadAllLines(Path.Combine(root,"tracking-status.log")).Length;
            for(int i=0;i<5;i++){Call(form,"RefreshCarrierStock");Call(form,"PollCargo");}
            Check(changes==0 && File.ReadAllLines(Path.Combine(root,"tracking-status.log")).Length==lines,"Poll/refresh alternation flashed or flooded the log");
            File.AppendAllText(journal,"{\"event\":\"LoadGame\",\"Commander\":\"Other\"}\n");Call(form,"PollCargo");
            Check(alert.Visible && cargo.Text.Contains("does not match"),"Real commander mismatch was hidden");
            changes=0;for(int i=0;i<5;i++){Call(form,"RefreshCarrierStock");Call(form,"PollCargo");}Check(changes==0,"Real mismatch still flashed");
            Call(form,"SetTrackingProblem","Journal read failed");Call(form,"RefreshCarrierStock");Check(alert.Visible && cargo.Text.Contains("Journal read failed"),"Stock refresh cleared a tracking failure");
            File.AppendAllText(journal,"{\"event\":\"LoadGame\",\"Commander\":\"Test\"}\n");Call(form,"PollCargo");Check(!alert.Visible && !cargo.Text.Contains("paused"),"Successful matching poll did not clear failure");
            form.Close();
        }}finally {foreach(var file in Directory.GetFiles(journals))File.Delete(file);Directory.Delete(journals);foreach(var file in Directory.GetFiles(root))File.Delete(file);Directory.Delete(root);}
        Console.WriteLine("Quiet status checks passed: restart identity, idle silence, stable poll/refresh, transition logging and visible genuine failures.");
    }
}
