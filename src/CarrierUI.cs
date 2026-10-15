using System;

using System.Collections.Generic;

using System.Drawing;

using System.IO;

using System.Linq;

using System.Windows.Forms;

public partial class ColonizationNeeds

{

    CarrierInventory carrierInventory;

    readonly ComboBox carrierView = new ComboBox { Width=135, DropDownStyle=ComboBoxStyle.DropDownList };

    readonly Button carrierSettings = new Button { Text="Carriers / review", Width=115 };

    bool refreshingCarriers;
    bool collectingInbound, changingInbound;
    string inboundDestination="any";
    readonly CheckBox collecting = new CheckBox {Text="Collecting for carriers",AutoSize=true};
    readonly ComboBox inboundTarget = new ComboBox {Width=210,DropDownStyle=ComboBoxStyle.DropDownList};
    void UpdateInboundControls()
    {
        changingInbound=true;
        try {
            collecting.Checked=collectingInbound; collecting.Enabled=selectedCargoMode=="Automatic";
            inboundTarget.Items.Clear();inboundTarget.Items.Add(new CarrierInventory.Carrier {id="any",name="Either carrier"});
            if(carrierInventory!=null)foreach(var c in ActiveCarriers)inboundTarget.Items.Add(c);
            inboundTarget.SelectedIndex=0;
            foreach(var c in inboundTarget.Items.Cast<CarrierInventory.Carrier>())if(c.id==inboundDestination)inboundTarget.SelectedItem=c;
            inboundTarget.Enabled=collectingInbound && selectedCargoMode=="Automatic";
        } finally {changingInbound=false;}
    }
    void ChangeInboundIntent()
    {
        if(changingInbound)return;
        bool next=collecting.Checked;var target=inboundTarget.SelectedItem as CarrierInventory.Carrier;
        string destination=target==null?"any":target.id;
        try {
            if(cargoTracker!=null)PollCargo(); // Drain with the previous intent before changing it.
            var saved=new Dictionary<string,string> {{"enabled",next?"true":"false"},{"destination",destination}};
            CarrierInventory.AtomicWrite(Path.Combine(Path.GetDirectoryName(settings),"inbound-settings.json"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(saved));
            collectingInbound=next;inboundDestination=destination;
        } catch(Exception ex) {MessageBox.Show(this,"Collection intent unchanged: "+ex.Message);}
        UpdateInboundControls();
    }
    void LoadInboundIntent()
    {
        collectingInbound=false;inboundDestination="any";
        string path=Path.Combine(Path.GetDirectoryName(settings),"inbound-settings.json");
        if(File.Exists(path)) {
            var saved=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(path));
            collectingInbound=saved.ContainsKey("enabled") && saved["enabled"]=="true";
            if(saved.ContainsKey("destination") && (saved["destination"]=="any" || CarrierInventory.ValidCarrier(saved["destination"])))inboundDestination=saved["destination"];
        }
        UpdateInboundControls();
    }

    List<CarrierInventory.Carrier> ActiveCarriers { get { return SharedEnabled ? (shared.RegistryReady?shared.Carriers:new List<CarrierInventory.Carrier>()) : carrierInventory.Data.carriers; } }

    string SelectedCarrier { get { var c=carrierView.SelectedItem as CarrierInventory.Carrier; return c==null?"":c.id; } }

    void EnsureCarrierInventory()

    {

        if(carrierInventory!=null) return;

        var legacy=new Dictionary<string,long>(); if(File.Exists(InventoryPath())) legacy=ParseInventory(File.ReadAllText(InventoryPath()));

        carrierInventory=new CarrierInventory(Path.Combine(Path.GetDirectoryName(settings),"carrier-inventory.json"),legacy);

        RefreshCarrierChoices();

    }

    void RefreshCarrierChoices()

    {

        string selected=SelectedCarrier; refreshingCarriers=true;

        try

        {

            carrierView.Items.Clear(); carrierView.Items.Add(new CarrierInventory.Carrier { id="", name="All carriers" });

            if(carrierInventory!=null) foreach(var c in ActiveCarriers) carrierView.Items.Add(c);

            carrierView.Items.Add(new CarrierInventory.Carrier { id="legacy", name="Legacy stock" });

            carrierView.SelectedIndex=0;

            foreach(var c in carrierView.Items.Cast<CarrierInventory.Carrier>()) if(c.id==selected) carrierView.SelectedItem=c;

        }

        finally { refreshingCarriers=false; }
        UpdateInboundControls();

    }

    void RefreshCarrierStock()

    {

        EnsureCarrierInventory();

        var raw=SharedEnabled?shared.CarrierStock(SelectedCarrier):carrierInventory.Stock(SelectedCarrier);

        inventory=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);

        foreach(var p in raw) inventory[p.Key]=Math.Max(0,p.Value);

        if(displayedCargo!=null) foreach(var key in displayedCargo.Keys.ToArray()) { long n; if(inventory.TryGetValue(JournalCargoTracker.Canonical(key),out n)) inventory[key]=n; }

        UpdateCargoStatus();

        RenderCommodities();

    }

    string trackingProblem="";
    string loggedTrackingState="";
    void UpdateCargoStatus()
    {
        int review=carrierInventory==null?0:carrierInventory.Data.review.Count(x=>!x.resolved);
        int debts=SharedEnabled?shared.CarrierBalances.Count(x=>x.Value<0):carrierInventory==null?0:carrierInventory.Data.balances.Count(x=>x.Value<0);
        string state="Tracking: "+selectedCargoMode;
        if(trackingProblem.Length>0)state="Cargo tracking paused: "+trackingProblem;
        else if(selectedCargoMode!="Manual" && cargoTracker!=null && !String.IsNullOrWhiteSpace(commander)) {
            if(String.IsNullOrEmpty(cargoTracker.CurrentCommander))state+=" · idle (no active commander journal)";
            else if(!String.Equals(cargoTracker.CurrentCommander,commander,StringComparison.OrdinalIgnoreCase))
                state="Cargo tracking paused: journal commander "+cargoTracker.CurrentCommander+" does not match selected player "+commander+".";
        }
        string text=state+" · "+review+" to review"+(debts>0?" · "+debts+" stock discrepancies; Carriers / review":"");
        if(cargoStatus.Text!=text)cargoStatus.Text=text;
        if(loggedTrackingState==state)return;
        // Per-profile, bounded, transition-only diagnostic log. Never send credentials or journals.
        try {
            string path=Path.Combine(Path.GetDirectoryName(settings),"tracking-status.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if(File.Exists(path) && new FileInfo(path).Length>128*1024)File.WriteAllText(path,"");
            File.AppendAllText(path,DateTime.UtcNow.ToString("o")+" "+state.Replace("\r"," ").Replace("\n"," ")+Environment.NewLine);
            loggedTrackingState=state;
        }catch { /* Diagnostics must never prevent inventory/cursor commits. */ }
    }
    void SetTrackingProblem(string message)
    {
        trackingProblem=message;UpdateCargoStatus();
    }

    void TrackMovement(string cmdr,string identity,string market,string stationType,Dictionary<string,object> data)

    {

        EnsureCarrierInventory();

        var purchase=TransitLedger.Purchase(cmdr,identity,data,stationType,collectingInbound && selectedCargoMode=="Automatic" && (inboundDestination=="any" || ActiveCarriers.Any(c=>c.id==inboundDestination)),inboundDestination);
        if(purchase!=null)SubmitTransit(purchase);
        foreach(var e in TransitLedger.Parse(cmdr,identity,data,ActiveCarriers.Any(x=>x.id==market),stationType))
            SubmitTransit(e);
        foreach(var m in CarrierInventory.Parse(cmdr,identity,market,stationType,data))

        {

            if(carrierInventory.Data.review.Any(x=>x.id==m.id && x.resolved)) continue;

            // Ordinary station trades are irrelevant. Ambiguous direct transfers must remain reviewable.

            bool known=ActiveCarriers.Any(x=>x.id==m.carrier);

            if(!known && m.source=="Market" && stationType.IndexOf("Carrier",StringComparison.OrdinalIgnoreCase)<0) continue;

            if(!known && m.reason.Length==0) m.reason="Carrier is not registered. Assign the correct carrier or dismiss.";

            if(selectedCargoMode=="Manual") m.reason="Captured while automatic tracking was paused. Apply or dismiss explicitly.";

            if(m.reason.Length>0) { carrierInventory.Hold(m); continue; }

            SubmitMovement(m);

        }

    }

    void SubmitMovement(CarrierInventory.Movement m)

    {

        if(SharedEnabled)

            shared.Enqueue(new[] { new SharedInventoryClient.Change { requestId=m.id, carrierId=m.carrier, commodity=m.commodity, amount=Math.Abs(m.delta), operation=m.delta>0?"add":"remove", source=m.source, observedAt=m.timestamp } });

        else carrierInventory.Apply(m);

    }

    void SubmitTransit(TransitLedger.Event e)
    {
        if(SharedEnabled) shared.Enqueue(new[] { new SharedInventoryClient.Change { requestId=e.id,commodity="_transit",operation="transit",source="Transit",transit=e } });
        else carrierInventory.ApplyTransit(e);
    }
    long TransitStock(string commodity,bool inbound=false)
    {
        var balances=SharedEnabled?(inbound?shared.InboundBalances:shared.TransitBalances):TransitLedger.Balances(carrierInventory.Data.transit);
        long total=0; string canonical=JournalCargoTracker.Canonical(commodity);
        foreach(var row in balances) { var parts=row.Key.Split('|'); bool isInbound=parts[0].StartsWith("inbound:");string carrier=isInbound?parts[0].Substring(8):parts[0];if((SharedEnabled || isInbound==inbound) && parts[1]==canonical && (SelectedCarrier.Length==0 || carrier==SelectedCarrier)) total=checked(total+row.Value); }
        return total;
    }
    async System.Threading.Tasks.Task ConfigureCarriers()

    {

        if(sharedBusy || editingInventory) return;

        try { EnsureCarrierInventory(); } catch(Exception ex) { MessageBox.Show(this,ex.Message); return; }

        bool previous=editingInventory; editingInventory=true;

        try

        {

            if(SharedEnabled) { try { await shared.LoadCarriers(); } catch(Exception ex) { MessageBox.Show(this,"Shared carrier configuration unavailable: "+ex.Message); return; } }

            using(var dialog=new Form { Text="Discover and configure carriers", Size=new Size(760,745), StartPosition=FormStartPosition.CenterParent, Font=Font })

            {

                bool operationBusy=false;

                dialog.FormClosing+=delegate(object sender,FormClosingEventArgs e) {if(operationBusy)e.Cancel=true;};

                dialog.Controls.Add(new Label { Left=15,Top=15,Width=715,Height=45,Text="Select carriers observed in this commander's journals. Discovery never imports stock or deliveries.\nIf a carrier is missing, dock there, then click Rescan. Shared members use the admin's selections." });

                var discovered=new ComboBox {Left=15,Top=65,Width=570,DropDownStyle=ComboBoxStyle.DropDownList};

                var rescan=new Button {Left=600,Top=65,Width=125,Text="Rescan journals"};

                var evidence=new Label {Left=15,Top=105,Width=715,Height=25};

                dialog.Controls.Add(discovered);dialog.Controls.Add(rescan);dialog.Controls.Add(evidence);

                var configured=new Dictionary<string,CarrierInventory.Carrier>();

                var setupSource=ActiveCarriers;
                if(SharedEnabled && setupSource.Count==0 && shared.Role=="admin") setupSource=carrierInventory.Data.carriers;
                int oldIndex=0;foreach(var old in setupSource) {string role=String.IsNullOrEmpty(old.role)?(oldIndex++==0?"Squadron":"Personal"):old.role;configured[role]=new CarrierInventory.Carrier {id=old.id,name=old.name,callsign=old.callsign,lastSeen=old.lastSeen,verified=old.verified,role=role};}

                var slotLabels=new Dictionary<string,Label>();

                foreach(var role in new[]{"Squadron","Personal"}) {

                    var label=new Label {Left=15,Top=175+(role=="Personal"?35:0),Width=710,Height=30};slotLabels[role]=label;dialog.Controls.Add(label);

                }

                Action updateSlots=delegate {foreach(var role in slotLabels.Keys) {CarrierInventory.Carrier c;slotLabels[role].Text=role+": "+(configured.TryGetValue(role,out c)?c.ToString()+(c.verified?" · observed":" · manual ID"):"Not configured");}};updateSlots();

                bool canConfigure=!SharedEnabled||shared.Role=="admin";

                var squad=new Button {Left=15,Top=135,Width=190,Text="Use as Squadron",Enabled=canConfigure};

                var personal=new Button {Left=220,Top=135,Width=190,Text="Use as Personal",Enabled=canConfigure};

                var manual=new Button {Left=15,Top=250,Width=170,Text="Enter ID manually",Enabled=canConfigure};

                var save=new Button {Left=535,Top=250,Width=190,Text=SharedEnabled?"Save shared carriers":"Save carriers",Enabled=canConfigure};

                var setupStatus=new Label {Left=15,Top=290,Width=710,Height=45,Text=canConfigure?"Choose each carrier, then save. Existing inventory is retained.":"Shared group carriers are configured by an admin. Discovery is available for verification."};

                dialog.Controls.AddRange(new Control[]{squad,personal,manual,save,setupStatus});

                Action<string> select=delegate(string role) {

                    var c=discovered.SelectedItem as CarrierInventory.Carrier;if(c==null)return;

                    configured[role]=new CarrierInventory.Carrier {id=c.id,name=c.name,callsign=c.callsign,lastSeen=c.lastSeen,verified=true,role=role};updateSlots();

                };squad.Click+=delegate {select("Squadron");};personal.Click+=delegate {select("Personal");};

                discovered.SelectedIndexChanged+=delegate {var c=discovered.SelectedItem as CarrierInventory.Carrier;evidence.Text=c==null?"":"Last observed (UTC): "+c.lastSeen;};

                Func<System.Threading.Tasks.Task> scan=async delegate {

                    operationBusy=true;rescan.Enabled=false;save.Enabled=false;

                    try {

                        var observations=await System.Threading.Tasks.Task.Run(()=>CarrierDiscovery.Scan(journalFolder,commander));

                        discovered.Items.Clear();foreach(var c in observations)discovered.Items.Add(c);
                        foreach(var role in configured.Keys.ToArray()) {
                            var current=configured[role];var observation=observations.FirstOrDefault(x=>x.id==current.id);
                            if(observation!=null) configured[role]=new CarrierInventory.Carrier {id=current.id,name=current.name,callsign=observation.callsign,lastSeen=observation.lastSeen,verified=true,role=role};
                        }updateSlots();

                        if(discovered.Items.Count>0)discovered.SelectedIndex=0;

                        else evidence.Text="No carriers observed for this player. Dock at a carrier and rescan.";

                    } catch(Exception ex) {evidence.Text=ex.Message;}

                    finally {operationBusy=false;rescan.Enabled=true;save.Enabled=canConfigure;}

                };rescan.Click+=async delegate {await scan();};dialog.Shown+=async delegate {await scan();};

                manual.Click+=delegate {

                    using(var entry=new Form {Text="Manual carrier ID",Size=new Size(440,300),Font=Font,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}) {

                        var role=new ComboBox {Left=15,Top=15,Width=390,DropDownStyle=ComboBoxStyle.DropDownList};role.Items.AddRange(new object[]{"Squadron","Personal"});role.SelectedIndex=0;

                        var name=new TextBox {Left=15,Top=75,Width=390};var id=new TextBox {Left=15,Top=135,Width=390};

                        entry.Controls.AddRange(new Control[]{role,new Label{Left=15,Top=50,Width=390,Text="Carrier name"},name,new Label{Left=15,Top=110,Width=390,Text="Numeric journal MarketID"},id});

                        var accept=new Button {Left=225,Top=200,Width=80,Text="Use ID"};entry.Controls.Add(accept);entry.Controls.Add(new Button{Left=320,Top=200,Width=85,Text="Cancel",DialogResult=DialogResult.Cancel});

                        accept.Click+=delegate {

                            string market=id.Text.Trim();if(!CarrierInventory.ValidCarrier(market)||String.IsNullOrWhiteSpace(name.Text)){MessageBox.Show(entry,"Enter a name and numeric MarketID.");return;}

                            var observed=discovered.Items.Cast<CarrierInventory.Carrier>().FirstOrDefault(x=>x.id==market);

                            if(observed==null && MessageBox.Show(entry,"This ID was not observed in this player's journals. Confirm it was copied from the intended carrier's Docked entry.","Verify manual ID",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;

                            string key=Convert.ToString(role.SelectedItem);configured[key]=new CarrierInventory.Carrier {id=market,name=name.Text.Trim(),role=key,verified=observed!=null,callsign=observed==null?"":observed.callsign,lastSeen=observed==null?"":observed.lastSeen};entry.DialogResult=DialogResult.OK;updateSlots();

                        };ApplyDialogPalette(entry);entry.ShowDialog(dialog);

                    }

                };

                save.Click+=async delegate {

                    if(operationBusy)return;operationBusy=true;save.Enabled=false;rescan.Enabled=false;squad.Enabled=personal.Enabled=manual.Enabled=false;

                    try {

                        var next=new[]{"Squadron","Personal"}.Where(configured.ContainsKey).Select(x=>configured[x]).ToList();

                        if(next.Select(x=>x.id).Distinct().Count()!=next.Count)throw new ArgumentException("Select two different carriers, or configure just one.");

                        if(SharedEnabled) {await shared.SaveCarriers(next);}

                        else {

                            if(carrierInventory.Data.carriers.Any(c=>!next.Any(n=>n.id==c.id)&&(carrierInventory.Data.balances.Any(p=>p.Key.StartsWith(c.id+"|")&&p.Value!=0)||carrierInventory.Data.review.Any(m=>!m.resolved&&m.carrier==c.id))))throw new ArgumentException("Keep carrier IDs that have stock or pending review events.");

                            var old=carrierInventory.Data.carriers;carrierInventory.Data.carriers=next;try{carrierInventory.Save();}catch{carrierInventory.Data.carriers=old;throw;}

                        }

                        RefreshCarrierChoices();setupStatus.Text=SharedEnabled?"Shared carriers saved. Other players receive the same selections when they sync.":"Carriers saved.";

                    } catch(Exception ex){setupStatus.Text=ex.Message;}

                    finally {operationBusy=false;save.Enabled=canConfigure;rescan.Enabled=true;squad.Enabled=personal.Enabled=manual.Enabled=canConfigure;}

                };

                var review=new ListBox { Left=15,Top=340,Width=710,Height=180 }; dialog.Controls.Add(review);

                var pending=new List<CarrierInventory.Movement>();

                Action populate=delegate {

                    review.Items.Clear(); pending=carrierInventory.Data.review.Where(x=>!x.resolved).ToList();

                    foreach(var m in pending) review.Items.Add(m.timestamp+" · "+m.commodity+" "+m.delta+" · "+m.reason);

                }; populate();

                var target=new ComboBox { Left=15,Top=535,Width=300,DropDownStyle=ComboBoxStyle.DropDownList }; dialog.Controls.Add(target);

                Action fill=delegate { target.Items.Clear(); foreach(var c in ActiveCarriers) target.Items.Add(c); if(target.Items.Count>0) target.SelectedIndex=0; }; fill();

                setupStatus.TextChanged+=delegate { fill(); };

                var apply=new Button { Left=330,Top=535,Width=180,Text="Apply to carrier" }; var dismiss=new Button { Left=525,Top=535,Width=200,Text="Dismiss event" }; dialog.Controls.Add(apply); dialog.Controls.Add(dismiss);

                Action<bool> resolve=delegate(bool accept) {
                    if(operationBusy)return;

                    if(review.SelectedIndex<0) return; var m=pending[review.SelectedIndex];

                    try {

                        if(accept) { var c=target.SelectedItem as CarrierInventory.Carrier; if(c==null) throw new ArgumentException("Select the actual carrier."); m.carrier=c.id; SubmitMovement(m); }

                        m.resolved=true; try { carrierInventory.Save(); } catch { m.resolved=false; throw; } populate();

                    } catch(Exception ex) { MessageBox.Show(dialog,ex.Message); }

                }; apply.Click+=delegate { resolve(true); }; dismiss.Click+=delegate { resolve(false); };

                var discrepancies=new Label { Left=15,Top=580,Width=710,Height=65 };

                var balances=SharedEnabled?shared.CarrierBalances:carrierInventory.Data.balances;

                discrepancies.Text=String.Join("\n",balances.Where(x=>x.Value<0).Select(x=>x.Key+": "+x.Value+" t — missing intake or starting stock")); dialog.Controls.Add(discrepancies);

                dialog.Controls.Add(new Button { Left=600,Top=660,Width=125,Text="Close",DialogResult=DialogResult.OK }); ApplyDialogPalette(dialog); dialog.ShowDialog(this);

            }

        }

        finally { editingInventory=previous; RefreshCarrierStock(); }

    }

    void EditCarrierInventory()

    {

        try { EnsureCarrierInventory(); } catch(Exception ex) { MessageBox.Show(this,ex.Message); return; }

        if(busy || sharedBusy) return;

        if(SharedEnabled && (!shared.Ready || shared.Pending>0)) { MessageBox.Show(this,shared.Blocked?"A pending correction conflicts with server stock. Open Settings → Shared inventory → Cancel conflicted edit, then refresh.":"Shared changes sync automatically. Wait for 0 pending, then retry. Check the shared status for connection errors."); return; }

        string commodity=list.SelectedItems.Count>0?Convert.ToString(list.SelectedItems[0].Tag):"";

        bool previous=editingInventory; editingInventory=true;

        try

        {

            using(var dialog=new Form { Text="Edit carrier stock",Size=new Size(450,365),StartPosition=FormStartPosition.CenterParent,Font=Font,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false })

            {

                var carrier=new ComboBox { Left=15,Top=15,Width=400,DropDownStyle=ComboBoxStyle.DropDownList };

                foreach(var c in ActiveCarriers) carrier.Items.Add(c); carrier.Items.Add(new CarrierInventory.Carrier { id="legacy",name="Legacy stock" });

                carrier.SelectedIndex=0; foreach(var c in carrier.Items.Cast<CarrierInventory.Carrier>()) if(c.id==SelectedCarrier) carrier.SelectedItem=c;

                var symbol=new ComboBox { Left=15,Top=55,Width=400 }; foreach(var key in CommodityCatalog.Keys.Select(JournalCargoTracker.Canonical).Distinct().OrderBy(x=>x)) symbol.Items.Add(key); symbol.Text=JournalCargoTracker.Canonical(commodity);

                var op=new ComboBox { Left=15,Top=95,Width=400,DropDownStyle=ComboBoxStyle.DropDownList }; op.Items.AddRange(SharedEnabled && shared.Role!="admin" ? new object[] { "Add","Remove","Clear my Outbound","Clear my Inbound" } : new object[] { "Add","Remove","Set total","Move from legacy","Clear my Outbound","Clear my Inbound" }); op.SelectedIndex=0;

                var amount=new NumericUpDown { Left=15,Top=135,Width=400,Maximum=1000000000,ThousandsSeparator=true };

                var info=new Label { Left=15,Top=180,Width=400,Height=70,Text="Choose a carrier and commodity. Set total reconciles the current stock and excludes older delayed events. Move from legacy preserves combined stock." };

                var apply=new Button { Left=225,Top=270,Width=90,Text="Apply" }; var close=new Button { Left=325,Top=270,Width=90,Text="Cancel",DialogResult=DialogResult.Cancel };

                dialog.Controls.AddRange(new Control[] { carrier,symbol,op,amount,info,apply,close });

                apply.Click+=async delegate {

                    try {

                        string id=((CarrierInventory.Carrier)carrier.SelectedItem).id, key=JournalCargoTracker.Canonical(symbol.Text), operation=Convert.ToString(op.SelectedItem); long n=Decimal.ToInt64(amount.Value);

                        if(!System.Text.RegularExpressions.Regex.IsMatch(key,@"^[a-z0-9_]{1,80}$")) throw new ArgumentException("Choose a valid commodity symbol.");

                        if(operation=="Move from legacy" && id=="legacy") throw new ArgumentException("Select a destination carrier.");

                        if(operation=="Clear my Outbound" || operation=="Clear my Inbound") {
                            if(MessageBox.Show(dialog,"Clear your "+(operation=="Clear my Inbound"?"Inbound":"Outbound")+" cargo for this commodity across all carriers? Stock is unchanged. Use this only when the cargo is no longer available.","Correct transit",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
                            SubmitTransit(new TransitLedger.Event { id=Guid.NewGuid().ToString("N"),kind="cap",carrier=operation=="Clear my Inbound"?"inbound":"outbound",commodity=key,amount=0,timestamp=DateTime.UtcNow.ToString("o") });
                            if(SharedEnabled)await shared.Sync(); RefreshCarrierStock(); dialog.DialogResult=DialogResult.OK; return;
                        }
                        if(operation=="Set total" && MessageBox.Show(dialog,"Confirm this is the carrier's actual stock now. Older delayed movements for this commodity will be excluded.","Reconcile stock",MessageBoxButtons.OKCancel)!=DialogResult.OK) return;

                        if(SharedEnabled) {

                            long version; shared.CarrierVersions.TryGetValue(CarrierInventory.Key(id,key),out version);

                            if(operation=="Move from legacy") { await shared.MigrateLegacy(id,key,n); }

                            else shared.Enqueue(new[] { new SharedInventoryClient.Change { carrierId=id,commodity=key,amount=n,operation=operation=="Set total"?"set":operation.ToLowerInvariant(),source="Manual",expectedVersion=version } });

                        } else if(operation=="Move from legacy") carrierInventory.MoveLegacy(id,key,n); else carrierInventory.Edit(id,key,n,operation);

                        dialog.DialogResult=DialogResult.OK;

                    } catch(Exception ex) { MessageBox.Show(dialog,ex.Message); }

                };

                ApplyDialogPalette(dialog); dialog.ShowDialog(this);

            }

        }

        finally { editingInventory=previous; RefreshCarrierStock(); }

    }

}

