using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
class CarrierSetupUITests
{
    static void Set(object obj,string key,object value) {obj.GetType().GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);}
    [STAThread] static void Main(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string root=Path.Combine(Path.GetTempPath(),"ColonizationNeeds-setup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            string folder=Path.Combine(root,"journals");Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,"Journal.01.log"),"{\"event\":\"LoadGame\",\"Commander\":\"Sample\"}\n{\"event\":\"Docked\",\"StationType\":\"FleetCarrier\",\"StationName\":\"Squadron example\",\"MarketID\":111,\"timestamp\":\"2026-10-13T01:00:00Z\"}\n{\"event\":\"Docked\",\"StationType\":\"FleetCarrier\",\"StationName\":\"Personal example\",\"MarketID\":222,\"timestamp\":\"2026-10-13T01:01:00Z\"}\n");
            using(var form=new ColonizationNeeds(true)) {
                Set(form,"settings",Path.Combine(root,"settings.json"));Set(form,"commander","Sample");Set(form,"journalFolder",folder);
                var ledger=new CarrierInventory(Path.Combine(root,"carrier-inventory.json"),new Dictionary<string,long>{{"steel",50}});Set(form,"carrierInventory",ledger);
                form.Show();Exception failure=null;int ticks=0;
                using(var timer=new Timer {Interval=100}) {
                    timer.Tick+=delegate {
                        var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="Discover and configure carriers");if(dialog==null)return;
                        var combos=dialog.Controls.OfType<ComboBox>().ToList();var discovered=combos.First();ticks++;
                        if(discovered.Items.Count!=2) {if(ticks>100){failure=new Exception("Carrier discovery did not complete");dialog.Close();timer.Stop();}return;}
                        try {
                            var save=dialog.Controls.OfType<Button>().Single(x=>x.Text=="Save carriers");if(!save.Enabled)return;
                            discovered.SelectedItem=discovered.Items.Cast<CarrierInventory.Carrier>().Single(x=>x.id=="111");dialog.Controls.OfType<Button>().Single(x=>x.Text=="Use as Squadron").PerformClick();
                            discovered.SelectedItem=discovered.Items.Cast<CarrierInventory.Carrier>().Single(x=>x.id=="222");dialog.Controls.OfType<Button>().Single(x=>x.Text=="Use as Personal").PerformClick();
                            save.PerformClick();
                            if(ledger.Data.carriers.Count!=2||ledger.Data.carriers.Any(x=>!x.verified)||ledger.Stock("")["steel"]!=50)throw new Exception("Discovery setup altered stock or saved incorrect selections");
                            if(ledger.Data.carriers.Single(x=>x.role=="Squadron").id!="111")throw new Exception("Squadron assignment wrong");
                            using(var bitmap=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(args[0]);}
                        } catch(Exception ex){failure=ex;}
                        timer.Stop();dialog.Close();
                    };
                    timer.Start();var task=(Task)typeof(ColonizationNeeds).GetMethod("ConfigureCarriers",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);task.GetAwaiter().GetResult();
                }
                form.Close();if(failure!=null)throw failure;
            }
            File.Delete(Path.Combine(folder,"Journal.01.log"));Directory.Delete(folder);
            Console.WriteLine("Carrier setup UI checks passed: observed-carrier selection, role assignment, save and unchanged inventory.");
        } finally {foreach(var file in Directory.GetFiles(root))File.Delete(file);Directory.Delete(root);}
    }
}
