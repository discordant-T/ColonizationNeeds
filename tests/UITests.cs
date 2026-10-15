using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
class UITests
{
    static void Set(object obj,string key,object value) {obj.GetType().GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);}
    static object Get(object obj,string key) {return obj.GetType().GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);}
    static void Call(object obj,string key) {obj.GetType().GetMethod(key,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,null);}
    [STAThread] static void Main(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string root=Path.Combine(Path.GetTempPath(),"ColonizationNeeds-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            using(var form=new ColonizationNeeds(true)) {
                Set(form,"settings",Path.Combine(root,"settings.json"));Set(form,"commander","Test Commander");
                var ledger=new CarrierInventory(Path.Combine(root,"carrier-inventory.json"),new Dictionary<string,long>{{"steel",1000},{"water",200}});
                ledger.Data.carriers.Add(new CarrierInventory.Carrier{id="123",name="Squadron carrier"});ledger.Data.carriers.Add(new CarrierInventory.Carrier{id="456",name="Personal carrier"});
                ledger.MoveLegacy("123","steel",600);ledger.MoveLegacy("456","steel",400);
                Set(form,"carrierInventory",ledger);Set(form,"inventoryReady",true);Set(form,"displayedCargo",new Dictionary<string,object>{{"steel",2000L},{"water",500L},{"titanium",0L}});
                Set(form,"displayedRequired",new Dictionary<string,long>{{"steel",2500},{"water",700},{"titanium",100}});
                Set(form,"displayedDelivery",new Dictionary<string,object>{{"steel",2000L},{"water",500L},{"titanium",0L}});
                Set(form,"updatingSelection",true);var projects=(ComboBox)Get(form,"selection");projects.Items.Add("All active projects (combined)");projects.SelectedIndex=0;Set(form,"updatingSelection",false);
                Call(form,"RefreshCarrierChoices");Call(form,"RefreshCarrierStock");
                form.Show();Application.DoEvents();
                var button=(Button)Get(form,"carrierSettings");if(button.Bottom>button.Parent.ClientSize.Height)throw new Exception("Carrier settings button clipped");
                var list=(ListView)Get(form,"list");if(list.Items.Count!=3)throw new Exception("Required or completed rows missing");
                if(list.Columns[1].Text!="To deliver" || list.Columns[3].Text!="Inbound" || list.Columns[4].Text!="Outbound")throw new Exception("Duplicate/confusing columns retained");
                var steel=list.Items.Cast<ListViewItem>().Single(x=>Convert.ToString(x.Tag)=="steel");if(steel.SubItems[1].Text!="2,000" || steel.SubItems[2].Text!="1,000" || steel.SubItems[3].Text!="0" || steel.SubItems[5].Text!="1,000")throw new Exception("Stocked display arithmetic failed");
                var view=(ComboBox)Get(form,"carrierView");view.SelectedIndex=1;Application.DoEvents();
                if(list.Items[0].SubItems.Count!=6)throw new Exception("Collection shortage column missing");
                view.SelectedIndex=0;Application.DoEvents();
                ledger.Apply(new CarrierInventory.Movement {id="ui-withdraw",carrier="123",commodity="steel",delta=-600,source="Market",timestamp="2026-10-13T01:00:00Z"});Call(form,"RefreshCarrierStock");
                steel=list.Items.Cast<ListViewItem>().Single(x=>Convert.ToString(x.Tag)=="steel");if(steel.SubItems[2].Text!="400" || steel.SubItems[4].Text!="600" || steel.SubItems[5].Text!="1,000")throw new Exception("Withdrawal caused false collection shortage in UI");
                ledger.ApplyTransit(new TransitLedger.Event {id="ui-deliver",kind="consume",commodity="steel",amount=600,timestamp="2026-10-13T01:01:00Z"});
                Set(form,"displayedDelivery",new Dictionary<string,object>{{"steel",1400L},{"water",500L},{"titanium",0L}});Call(form,"RefreshCarrierStock");
                steel=list.Items.Cast<ListViewItem>().Single(x=>Convert.ToString(x.Tag)=="steel");if(steel.SubItems[1].Text!="1,400" || steel.SubItems[3].Text!="0" || steel.SubItems[5].Text!="1,000")throw new Exception("Delivery caused false collection shortage in UI");
                ledger.ApplyTransit(new TransitLedger.Event {id="ui-inbound",kind="inbound",carrier="any",commodity="steel",amount=100,timestamp="2026-10-13T02:00:00Z"});Call(form,"RefreshCarrierStock");
                steel=list.Items.Cast<ListViewItem>().Single(x=>Convert.ToString(x.Tag)=="steel");if(steel.SubItems[3].Text!="100" || steel.SubItems[5].Text!="900")throw new Exception("Inbound did not reduce combined collection need");
                view.SelectedIndex=1;Application.DoEvents();steel=list.Items.Cast<ListViewItem>().Single(x=>Convert.ToString(x.Tag)=="steel");if(steel.SubItems[3].Text!="0")throw new Exception("Either-carrier Inbound was allocated twice");view.SelectedIndex=0;
                var collecting=(CheckBox)Get(form,"collecting");Set(form,"selectedCargoMode","Automatic");Call(form,"UpdateInboundControls");collecting.Checked=true;Call(form,"LoadInboundIntent");if(!collecting.Checked)throw new Exception("Inbound intent did not persist");
                using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(args[0]);}
                form.Close();
            }
            Console.WriteLine("UI smoke checks passed: carrier selector, project selector, stock rendering and layout.");
        } finally {foreach(var file in Directory.GetFiles(root))File.Delete(file);Directory.Delete(root);}
    }
}
