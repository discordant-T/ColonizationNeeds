using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
class CompactUITests
{
    static object Get(object obj,string key){return obj.GetType().GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);}
    static void Set(object obj,string key,object value){obj.GetType().GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);}
    static void Call(object obj,string key,params object[] args){obj.GetType().GetMethod(key,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,args);}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    [STAThread]static void Main(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string root=Path.Combine(Path.GetTempPath(),"Compact-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {using(var form=new ColonizationNeeds(true)) {
            Set(form,"settings",Path.Combine(root,"settings.json"));Set(form,"commander","Test Commander");Set(form,"selectedCargoMode","Automatic");
            var stock=new CarrierInventory(Path.Combine(root,"carrier-inventory.json"),new Dictionary<string,long>());
            stock.Data.carriers.Add(new CarrierInventory.Carrier {id="123",name="GBGB"});stock.Edit("123","steel",30,"Set total");
            stock.ApplyTransit(new TransitLedger.Event {id="inbound",kind="inbound",carrier="any",commodity="steel",amount=100,timestamp="2026-10-14T01:00:00Z"});
            Set(form,"carrierInventory",stock);Set(form,"inventoryReady",true);
            Set(form,"displayedCargo",new Dictionary<string,object>{{"steel",16260L},{"water",200L}});
            Set(form,"displayedDelivery",new Dictionary<string,object>{{"steel",16260L},{"water",200L}});
            Set(form,"updatingSelection",true);var project=(ComboBox)Get(form,"selection");project.Items.Add("All active projects (combined)");project.SelectedIndex=0;Set(form,"updatingSelection",false);
            Call(form,"RefreshCarrierChoices");Call(form,"RefreshCarrierStock");Call(form,"UpdateInboundControls");
            ((Label)Get(form,"status")).Text="4 projects · Updated 16:45";((Label)Get(form,"ravenStatus")).Text="Raven reporting: Up to date";
            form.Show();Application.DoEvents();Call(form,"SetCompactView",true);Application.DoEvents();
            Check(form.Width==510 && form.Height==440,"Compact size not applied");
            var list=(ListView)Get(form,"list");Check(list.Height>180,"Table lost space in Compact view");
            Check(list.Columns[1].Text=="Need" && list.Columns[3].Text=="In" && list.Columns[4].Text=="Out","Short headings missing");
            Check(!((Control)Get(form,"statusDetails")).Visible,"Status details did not collapse");
            Check(((Control)Get(form,"statusToggle")).Visible,"Essential status missing");
            var header=(Panel)Get(form,"viewHeader");int collapsed=header.Height;
            ((Button)Get(form,"controlsToggle")).PerformClick();Application.DoEvents();Check(header.Height>collapsed,"Controls did not expand");
            foreach(string key in new[]{"viewPlayer","viewMode","viewOptions","viewInbound"}){var control=(Control)Get(form,key);Check(control.Visible && control.Bottom<=header.ClientSize.Height,"Expanded controls clipped: "+key);}
            ((Button)Get(form,"controlsToggle")).PerformClick();
            ((Label)Get(form,"sharedStatus")).Text="Shared: 1 pending · Invalid player token";Application.DoEvents();
            Check(((Label)Get(form,"compactAlert")).Visible && ((Button)Get(form,"statusToggle")).Text.StartsWith("Attention"),"Collapsed status hid an error");
            ((Button)Get(form,"statusToggle")).PerformClick();Check(((Control)Get(form,"statusDetails")).Visible,"Detailed status could not expand");((Button)Get(form,"statusToggle")).PerformClick();
            ((Label)Get(form,"sharedStatus")).Text="Inventory: Local";
            Call(form,"BuildViewMenu");var menu=(ContextMenuStrip)Get(form,"viewMenu");
            menu.Items.OfType<ToolStripMenuItem>().Single(x=>x.Text=="Show Inbound").PerformClick();Check(list.Columns[3].Width==0,"Inbound column not hidden");
            var steel=list.Items.Cast<ListViewItem>().Single(x=>Convert.ToString(x.Tag)=="steel");Check(steel.SubItems[5].Text=="16,130","Hiding column changed accounting");
            menu.Items.OfType<ToolStripMenuItem>().Single(x=>x.Text=="Show totals").PerformClick();Check(!((Label)Get(form,"totals")).Visible,"Totals could not hide");
            form.Size=new Size(490,410);Call(form,"RememberViewSize");Call(form,"SaveViewPreferences");Call(form,"LoadViewPreferences");
            Check(form.Width==490 && !((Label)Get(form,"totals")).Visible && list.Columns[3].Width==0,"Saved compact preferences lost");
            string second=Path.Combine(root,"second");Directory.CreateDirectory(second);Set(form,"settings",Path.Combine(second,"settings.json"));Call(form,"LoadViewPreferences");
            Check(form.Width==690 && list.Columns[3].Width>0 && ((Label)Get(form,"totals")).Visible,"New player inherited Compact preferences");
            Set(form,"settings",Path.Combine(root,"settings.json"));Call(form,"LoadViewPreferences");
            Call(form,"BuildViewMenu");menu.Items.OfType<ToolStripMenuItem>().Single(x=>x.Text=="Show Inbound").PerformClick();menu.Items.OfType<ToolStripMenuItem>().Single(x=>x.Text=="Show totals").PerformClick();
            form.Size=new Size(510,440);Application.DoEvents();
            var footer=(Control)Get(form,"compactFooter");Check(footer.Bottom<=form.ClientSize.Height && footer.Height>=26 && footer.Top>=0,"Status footer outside window");
            Check(((Control)Get(form,"statusToggle")).Bottom<=footer.Height,"Status summary outside footer");
            Check(list.Bottom<=((Control)Get(form,"totals")).Top && ((Control)Get(form,"totals")).Bottom<=footer.Top,"Table, totals and status overlap");
            Check(list.Columns.Cast<ColumnHeader>().Sum(x=>x.Width)<=list.ClientSize.Width,"Normal quantities require horizontal scrolling");
            using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(args[0]);}
            // A resize may fire while native ListView rows are being replaced.
            string unchanged=new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(stock.Data);
            Set(form,"updatingCommodityRows",true);
            try {list.Items.Add(new ListViewItem("partially hosted row"));Call(form,"FitViewColumns");}
            finally {list.Items.RemoveAt(list.Items.Count-1);Set(form,"updatingCommodityRows",false);}
            int reentrantFits=0;
            ColumnWidthChangedEventHandler nested=delegate {reentrantFits++;Call(form,"FitViewColumns");};
            list.ColumnWidthChanged+=nested;
            for(int cycle=0;cycle<30;cycle++) {
                Call(form,"ClearCommodities");
                Set(form,"displayedCargo",cycle%2==0?new Dictionary<string,object>{{"steel",16260L}}:new Dictionary<string,object>{{"steel",16260L},{"water",200L}});
                Call(form,"RenderCommodities");
                form.Size=new Size(cycle%2==0?510:530,440);Application.DoEvents();
                Check(list.Columns.Count==6 && list.Columns[3].Width>0 && list.Columns[4].Width>0,"Resize lost Inbound/Outbound columns");
                var checkedSteel=list.Items.Cast<ListViewItem>().Single(x=>Convert.ToString(x.Tag)=="steel");
                Check(checkedSteel.SubItems[2].Text=="30" && checkedSteel.SubItems[3].Text=="100" && checkedSteel.SubItems[5].Text=="16,130","Project switch changed stock/inbound accounting");
            }
            list.ColumnWidthChanged-=nested;
            Check(reentrantFits>0,"Reentrant fitting scenario not exercised");
            Check(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(stock.Data)==unchanged,"Column fitting changed saved carrier or transit data");
            form.Close();
        }} finally {foreach(var sub in Directory.GetDirectories(root)){foreach(var file in Directory.GetFiles(sub))File.Delete(file);Directory.Delete(sub);}foreach(var file in Directory.GetFiles(root))File.Delete(file);Directory.Delete(root);}
        Console.WriteLine("Compact UI checks passed: narrow layout, collapsible controls/status, visible errors, hidden columns, arithmetic and profile persistence.");
    }
}
