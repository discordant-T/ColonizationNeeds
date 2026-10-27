using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

public partial class ColonizationNeeds
{
    public sealed class ViewPreferences
    {
        public bool compact, controlsExpanded, detailsExpanded;
        public bool inbound=true, outbound=true, totals=true;
        public int compactWidth=510,compactHeight=440,fullWidth=690,fullHeight=630;
    }
    ViewPreferences viewPreferences=new ViewPreferences();
    Panel viewHeader,viewEntry,viewPlayer,viewOptions,viewMode,viewInbound;
    readonly FlowLayoutPanel quickRow=new FlowLayoutPanel {WrapContents=false};
    readonly Label destinationCaption=new Label {Text="Inbound destination",AutoSize=true,Padding=new Padding(0,5,0,0)};
    readonly Button viewButton=new Button {Text="View",Width=55};
    readonly Button controlsToggle=new Button {Text="Controls ▸",Width=85};
    readonly Button statusToggle=new Button {Dock=DockStyle.Top,Height=26,TextAlign=ContentAlignment.MiddleLeft};
    readonly Label compactAlert=new Label {Dock=DockStyle.Top,Height=26,AutoEllipsis=true,Padding=new Padding(6,4,6,0)};
    readonly Panel statusDetails=new Panel {Dock=DockStyle.Bottom,Height=104};
    readonly Panel compactFooter=new Panel {Dock=DockStyle.Bottom,Height=26};
    readonly ContextMenuStrip viewMenu=new ContextMenuStrip();
    readonly ToolTip compactHelp=new ToolTip {AutoPopDelay=15000};
    bool viewReady,applyingView,updatingCommodityRows,fittingViewColumns;

    void InitializeCompactUI(Panel header,Panel entry,Panel playerRow,FlowLayoutPanel options,FlowLayoutPanel mode,FlowLayoutPanel inbound)
    {
        header.Controls.Add(activeProjectsButton); activeProjectsButton.BackColor=AccentColor; activeProjectsButton.ForeColor=TextColor; activeProjectsButton.FlatStyle=FlatStyle.Flat; activeProjectsButton.FlatAppearance.BorderSize=0; activeProjectsButton.Click+=async delegate {await ChooseActiveProjects();};
        viewHeader=header;viewEntry=entry;viewPlayer=playerRow;viewOptions=options;viewMode=mode;viewInbound=inbound;
        foreach(Control row in new Control[]{entry,playerRow,options,mode,inbound,selection})row.Dock=DockStyle.None;
        foreach(var button in new[]{viewButton,controlsToggle,statusToggle}) {
            button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=0;button.BackColor=AccentColor;button.ForeColor=TextColor;
        }
        entry.Controls.Add(viewButton);entry.Controls.Add(controlsToggle);
        header.Controls.Add(quickRow);inbound.Controls.Add(destinationCaption);destinationCaption.SendToBack();
        compactFooter.Controls.Add(statusDetails);compactFooter.Controls.Add(compactAlert);compactFooter.Controls.Add(statusToggle);
        Controls.Add(compactFooter);
        Controls.SetChildIndex(list,0);Controls.SetChildIndex(totals,1);Controls.SetChildIndex(compactFooter,2);
        foreach(var label in new[]{status,cargoStatus,sharedStatus,ravenStatus}) {
            Controls.Remove(label);label.Dock=DockStyle.Top;label.Height=26;label.Padding=new Padding(6,4,6,0);label.AutoEllipsis=true;
            statusDetails.Controls.Add(label);label.BringToFront();label.TextChanged+=delegate {UpdateCompactStatus();};
        }
        foreach(var label in new[]{status,cargoStatus,sharedStatus,ravenStatus})compactHelp.SetToolTip(label,label.Text);
        viewButton.Click+=delegate {BuildViewMenu();viewMenu.Show(viewButton,new Point(0,viewButton.Height));};
        controlsToggle.Click+=delegate {viewPreferences.controlsExpanded=!viewPreferences.controlsExpanded;ApplyViewLayout();SaveViewPreferences();};
        statusToggle.Click+=delegate {viewPreferences.detailsExpanded=!viewPreferences.detailsExpanded;UpdateCompactStatus();SaveViewPreferences();};
        compactAlert.Click+=delegate {viewPreferences.detailsExpanded=true;UpdateCompactStatus();SaveViewPreferences();};
        header.SizeChanged+=delegate {if(viewReady&&!applyingView)LayoutViewHeader();};
        list.SizeChanged+=delegate {if(viewReady)FitViewColumns();};
        list.ColumnWidthChanging+=delegate(object sender,ColumnWidthChangingEventArgs e) {
            if(viewReady && ((e.ColumnIndex==3&&!viewPreferences.inbound)||(e.ColumnIndex==4&&!viewPreferences.outbound))){e.Cancel=true;e.NewWidth=0;}
        };
        FormClosing+=delegate {if(viewReady){RememberViewSize();SaveViewPreferences();}};
        FormClosed+=delegate {compactHelp.Dispose();viewMenu.Dispose();};
        viewReady=true;ApplyViewLayout();
    }
    void RememberViewSize()
    {
        Size size=WindowState==FormWindowState.Normal?Size:RestoreBounds.Size;
        if(viewPreferences.compact){viewPreferences.compactWidth=size.Width;viewPreferences.compactHeight=size.Height;}
        else {viewPreferences.fullWidth=size.Width;viewPreferences.fullHeight=size.Height;}
    }
    void SaveViewPreferences()
    {
        if(!viewReady)return;
        try {CarrierInventory.AtomicWrite(Path.Combine(Path.GetDirectoryName(settings),"view.json"),new JavaScriptSerializer().Serialize(viewPreferences));}
        catch(Exception ex){status.Text="View preference save failed: "+ex.Message;}
    }
    void LoadViewPreferences()
    {
        viewPreferences=new ViewPreferences();
        try {
            string path=Path.Combine(Path.GetDirectoryName(settings),"view.json");
            if(File.Exists(path))viewPreferences=new JavaScriptSerializer().Deserialize<ViewPreferences>(File.ReadAllText(path))??new ViewPreferences();
        }catch {status.Text="View preferences unreadable; using Full view.";}
        ApplyViewLayout();
        Size=new Size(Math.Max(MinimumSize.Width,Math.Min(1600,viewPreferences.compact?viewPreferences.compactWidth:viewPreferences.fullWidth)),Math.Max(MinimumSize.Height,Math.Min(1200,viewPreferences.compact?viewPreferences.compactHeight:viewPreferences.fullHeight)));
    }
    void SetCompactView(bool compact)
    {
        RememberViewSize();viewPreferences.compact=compact;ApplyViewLayout();
        Size=new Size(compact?viewPreferences.compactWidth:viewPreferences.fullWidth,compact?viewPreferences.compactHeight:viewPreferences.fullHeight);
        Location=VisibleWindowLocation(Location,Size,Screen.FromControl(this).WorkingArea);SaveViewPreferences();
    }
    void BuildViewMenu()
    {
        viewMenu.Items.Clear();
        Action<string,bool,Action> add=delegate(string text,bool value,Action action) {var item=new ToolStripMenuItem(text){Checked=value};item.Click+=delegate {action();};viewMenu.Items.Add(item);};
        add("Compact view",viewPreferences.compact,delegate {SetCompactView(true);});
        add("Full view",!viewPreferences.compact,delegate {SetCompactView(false);});
        viewMenu.Items.Add(new ToolStripSeparator());
        add("Show Inbound",viewPreferences.inbound,delegate {viewPreferences.inbound=!viewPreferences.inbound;FitViewColumns();SaveViewPreferences();});
        add("Show Outbound",viewPreferences.outbound,delegate {viewPreferences.outbound=!viewPreferences.outbound;FitViewColumns();SaveViewPreferences();});
        add("Show totals",viewPreferences.totals,delegate {viewPreferences.totals=!viewPreferences.totals;ApplyViewLayout();SaveViewPreferences();});
        add("Status details",viewPreferences.detailsExpanded,delegate {viewPreferences.detailsExpanded=!viewPreferences.detailsExpanded;UpdateCompactStatus();SaveViewPreferences();});
        viewMenu.Items.Add(new ToolStripSeparator());
        var logItem=new ToolStripMenuItem("Tracking log…");logItem.Click+=delegate {ShowTrackingLog();};viewMenu.Items.Add(logItem);
    }
    void ShowTrackingLog()
    {
        try {
            string path=Path.Combine(Path.GetDirectoryName(settings),"tracking-status.log");
            using(var dialog=new Form {Text="Tracking log — "+commander,Size=new Size(650,400),StartPosition=FormStartPosition.CenterParent,Font=Font}) {
                var text=new TextBox {Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,WordWrap=false,ScrollBars=ScrollBars.Both,Text=File.Exists(path)?File.ReadAllText(path):"No tracking state changes have been logged for this player yet."};
                dialog.Controls.Add(text);ApplyDialogPalette(dialog);dialog.ShowDialog(this);
            }
        }catch(Exception ex){MessageBox.Show(this,"Tracking log could not be opened: "+ex.Message);}
    }
    void ApplyViewLayout()
    {
        if(!viewReady)return;applyingView=true;SuspendLayout();
        try {
            bool compact=viewPreferences.compact;
            MinimumSize=compact?new Size(480,330):new Size(660,430);
            collecting.Parent=compact?quickRow:viewInbound;carrierView.Parent=compact?quickRow:viewMode;
            if(!compact){viewMode.Controls.SetChildIndex(carrierView,2);viewInbound.Controls.SetChildIndex(collecting,0);}
            if(compact)quickRow.Controls.SetChildIndex(carrierView,0);
            collecting.Text=compact?"Collect to carrier":"Collecting for carriers";
            carrierView.Width=compact?155:135;
            destinationCaption.Visible=compact;quickRow.Visible=compact;
            controlsToggle.Visible=compact;controlsToggle.Text="Controls "+(viewPreferences.controlsExpanded?"▾":"▸");
            foreach(var row in new[]{viewPlayer,viewMode,viewInbound,viewOptions})row.Visible=!compact||viewPreferences.controlsExpanded;
            totals.Visible=viewPreferences.totals;totals.Height=compact?38:55;totals.Padding=new Padding(6,3,6,0);
            LayoutViewHeader();FitViewColumns();UpdateCompactStatus();
        }finally {ResumeLayout(true);applyingView=false;}
    }
    void LayoutViewHeader()
    {
        bool compact=viewPreferences.compact;int width=Math.Max(100,viewHeader.ClientSize.Width-16),top=8;
        viewEntry.SetBounds(8,top,width,25);top+=27;
        foreach(var button in new[]{editInventory,refresh,configure,viewButton,controlsToggle})button.Dock=DockStyle.None;
        editInventory.Text=compact?"Stock…":"Edit inventory";editInventory.SetBounds(0,0,compact?70:110,25);
        controlsToggle.SetBounds(74,0,85,25);viewButton.SetBounds(width-55,0,55,25);
        configure.SetBounds(width-115,0,60,25);refresh.SetBounds(width-175,0,60,25);
        selection.SetBounds(8,top,width-88,25);activeProjectsButton.SetBounds(8+width-82,top,82,25);top+=27;
        if(compact){quickRow.SetBounds(8,top,width,30);top+=30;}
        if(!compact||viewPreferences.controlsExpanded)foreach(var row in new[]{viewPlayer,viewMode,viewInbound,viewOptions}){int height=row==viewPlayer?30:row==viewOptions?25:32;row.SetBounds(8,top,width,height);top+=height;}
        viewHeader.Height=top+3;
    }
    void FitViewColumns()
    {
        // Native SizeChanged can arrive synchronously inside Clear/AddRange/EndUpdate.
        // Do not enumerate ListView's transient row map, or recurse while changing widths.
        if(updatingCommodityRows || fittingViewColumns || IsDisposed || list.IsDisposed || viewPreferences==null || list.Columns.Count<6)return;
        fittingViewColumns=true;
        try {
            bool compact=viewPreferences.compact;
            string[] names=compact?new[]{"Commodity","Need","Stock","In","Out","Collect"}:new[]{"Commodity","To deliver","Stock","Inbound","Outbound","To collect"};
            int[] widths=compact?new[]{0,66,66,60,60,66}:new[]{0,88,76,76,76,88};
            // Measure everything before applying native column changes, which can resize the view.
            foreach(ListViewItem row in list.Items) {
                if(row==null)continue;
                for(int i=1;i<Math.Min(6,row.SubItems.Count);i++)
                    if((i!=3||viewPreferences.inbound)&&(i!=4||viewPreferences.outbound))
                        widths[i]=Math.Max(widths[i],TextRenderer.MeasureText(row.SubItems[i].Text,list.Font).Width+12);
            }
            int numericWidth=0;
            for(int i=1;i<6;i++) {
                bool visible=(i!=3||viewPreferences.inbound)&&(i!=4||viewPreferences.outbound);
                int width=visible?widths[i]:0;
                if(list.Columns[i].Text!=names[i])list.Columns[i].Text=names[i];
                if(list.Columns[i].Width!=width)list.Columns[i].Width=width;
                numericWidth+=width;
            }
            int commodityWidth=Math.Max(compact?90:130,list.ClientSize.Width-numericWidth-SystemInformation.VerticalScrollBarWidth-4);
            if(list.Columns[0].Width!=commodityWidth)list.Columns[0].Width=commodityWidth;
        }finally {fittingViewColumns=false;}
    }
    void UpdateCompactStatus()
    {
        if(!viewReady)return;
        int pending=SharedEnabled?shared.Pending:0;
        int review=carrierInventory==null?0:carrierInventory.Data.review.Count(x=>!x.resolved);
        if(ravenReporter!=null)review+=ravenReporter.Review.Count();
        string[] warningWords={"paused","failed","error","unreadable","conflict","timed out","unavailable","not found","not configured","invalid","denied","required","could not","configure","discrepanc","need review","shortfall","reconcil"};
        string alert=new[]{cargoStatus.Text,sharedStatus.Text,ravenStatus.Text,status.Text}.FirstOrDefault(x=>warningWords.Any(word=>(x??"").IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0))??"";
        if(SharedEnabled && shared.Blocked && alert.Length==0)alert="Shared edit conflict; open Settings → Shared inventory.";
        bool idle=selectedCargoMode!="Manual" && cargoTracker!=null && String.IsNullOrEmpty(cargoTracker.CurrentCommander);
        string state=alert.Length>0?"Attention":idle?"Idle":SharedEnabled?(shared.Ready&&pending==0?"Synced":"Syncing"):"Local";
        statusToggle.Text=state+" · "+selectedCargoMode+" · "+pending+" pending · "+review+" review"+(ravenReporter!=null&&ravenReporter.Pending>0?" · Raven pending":"")+" "+(viewPreferences.detailsExpanded?"▾":"▸");
        compactAlert.Text=alert;compactAlert.ForeColor=NeededColor;compactAlert.Visible=alert.Length>0;
        statusDetails.Visible=viewPreferences.detailsExpanded;
        compactFooter.Height=26+(alert.Length>0?26:0)+(viewPreferences.detailsExpanded?104:0);
        int minimumHeight=Math.Max(viewPreferences.compact?330:430,viewHeader.Height+compactFooter.Height+(viewPreferences.totals?totals.Height:0)+100+Math.Max(30,Height-ClientSize.Height));
        MinimumSize=new Size(viewPreferences.compact?480:660,minimumHeight);
        foreach(var label in new[]{status,cargoStatus,sharedStatus,ravenStatus})compactHelp.SetToolTip(label,label.Text);
        compactHelp.SetToolTip(compactAlert,alert);compactHelp.SetToolTip(statusToggle,String.Join("\n",new[]{status.Text,cargoStatus.Text,sharedStatus.Text,ravenStatus.Text}));
    }
}
