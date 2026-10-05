using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

public class ColonizationNeeds : Form
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern int GetScrollPos(IntPtr handle, int bar);
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
    static void ThemeTitle(Form form)
    {
        int dark = 1, background = ColorTranslator.ToWin32(WindowColor), text = ColorTranslator.ToWin32(TextColor), border = ColorTranslator.ToWin32(AccentColor);
        DwmSetWindowAttribute(form.Handle, 20, ref dark, 4);
        DwmSetWindowAttribute(form.Handle, 35, ref background, 4);
        DwmSetWindowAttribute(form.Handle, 36, ref text, 4);
        DwmSetWindowAttribute(form.Handle, 34, ref border, 4);
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ThemeTitle(this); }
    public sealed class AmberSlider : Control
    {
        int value = 100;
        public int Minimum { get; set; }
        public int Maximum { get; set; }
        public int TickFrequency { get; set; }
        public event EventHandler ValueChanged;
        public int Value { get { return value; } set { int next = Math.Max(Minimum, Math.Min(Maximum, value)); if (this.value == next) return; this.value = next; Invalidate(); if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); } }
        public AmberSlider() { Minimum = 30; Maximum = 100; Height = 45; TabStop = true; AccessibleRole = AccessibleRole.Slider; AccessibleName = "Window opacity"; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true); }
        int ThumbX { get { return 10 + (int)((Width - 20) * (Value - Minimum) / (double)Math.Max(1, Maximum - Minimum)); } }
        void SetFromMouse(int x) { Value = Minimum + (int)Math.Round((Maximum - Minimum) * Math.Max(0, Math.Min(Width - 20, x - 10)) / (double)Math.Max(1, Width - 20)); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { Focus(); Capture = true; SetFromMouse(e.X); } }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (Capture && e.Button == MouseButtons.Left) SetFromMouse(e.X); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Capture = false; }
        protected override bool IsInputKey(Keys key) { return key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || base.IsInputKey(key); }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) Value--; else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) Value++; else if (e.KeyCode == Keys.Home) Value = Minimum; else if (e.KeyCode == Keys.End) Value = Maximum; }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); int y = 14;
            using (var track = new SolidBrush(AccentColor)) e.Graphics.FillRectangle(track, 10, y - 2, Math.Max(1, Width - 20), 4);
            using (var amber = new SolidBrush(TextColor)) { e.Graphics.FillRectangle(amber, 10, y - 2, Math.Max(1, ThumbX - 10), 4); e.Graphics.FillRectangle(amber, ThumbX - 5, y - 9, 10, 18); }
            using (var ticks = new Pen(AccentColor)) for (int n = Minimum; n <= Maximum; n += Math.Max(1, TickFrequency)) { int x = 10 + (Width - 20) * (n - Minimum) / Math.Max(1, Maximum - Minimum); e.Graphics.DrawLine(ticks, x, 28, x, 32); }
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(1, 1, Width - 2, Height - 2), TextColor, BackColor);
        }
    }
    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr handle, string appName, string idList);
    static readonly Color WindowColor = Color.FromArgb(18, 17, 15);
    static readonly Color TableColor = Color.FromArgb(8, 8, 8);
    static readonly Color TextColor = Color.FromArgb(240, 151, 35);
    static readonly Color AccentColor = Color.FromArgb(65, 39, 12);
    static readonly Color NeededColor = Color.FromArgb(255, 112, 112);
    static readonly Color DeliveryNeededColor = Color.FromArgb(102, 224, 255);
    static readonly Color CoveredColor = Color.FromArgb(99, 191, 105);

    static void StyleCombo(ComboBox combo)
    {
        combo.FlatStyle = FlatStyle.Flat;
        combo.DrawMode = DrawMode.OwnerDrawFixed;
        combo.DrawItem += delegate(object sender, DrawItemEventArgs e)
        {
            using (var brush = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? AccentColor : TableColor)) e.Graphics.FillRectangle(brush, e.Bounds);
            string text = e.Index >= 0 ? combo.GetItemText(combo.Items[e.Index]) : combo.Text;
            TextRenderer.DrawText(e.Graphics, text, combo.Font, e.Bounds, TextColor, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        };
    }
    static void ApplyDialogPalette(Control control)
    {
        var form = control as Form; if (form != null) { form.HandleCreated += delegate { ThemeTitle(form); }; if (form.IsHandleCreated) ThemeTitle(form); }
        control.ForeColor = TextColor;
        control.BackColor = WindowColor;
        var button = control as Button;
        if (button != null)
        {
            button.FlatStyle = FlatStyle.Flat; button.BackColor = AccentColor;
            button.ForeColor = Color.FromArgb(255, 176, 59); button.FlatAppearance.BorderSize = 0;
        }
        if (control is ComboBox) StyleCombo((ComboBox)control);
        if (control is TextBox || control is NumericUpDown || control is ComboBox) control.BackColor = TableColor;
        foreach (Control child in control.Controls) ApplyDialogPalette(child);
    }
    // Category and display-name facts from EDCD/FDevIDs commodity.csv.
    static readonly Dictionary<string, string[]> CommodityCatalog = CreateCommodityCatalog();
    static Dictionary<string, string[]> CreateCommodityCatalog()
    {
        var catalog = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        const string data = @"advancedcatalysers|Technology|Advanced Catalysers
agriculturalmedicines|Medicines|Agri-Medicines
aluminium|Metals|Aluminium
animalmeat|Foods|Animal Meat
autofabricators|Technology|Auto-Fabricators
basicmedicines|Medicines|Basic Medicines
battleweapons|Weapons|Battle Weapons
beer|Legal Drugs|Beer
bioreducinglichen|Technology|Bioreducing Lichen
biowaste|Waste|Biowaste
buildingfabricators|Machinery|Building Fabricators
ceramiccomposites|Industrial Materials|Ceramic Composites
cmmcomposite|Industrial Materials|CMM Composite
coffee|Foods|Coffee
combatstabilisers|Medicines|Combat Stabilisers
computercomponents|Technology|Computer Components
copper|Metals|Copper
cropharvesters|Machinery|Crop Harvesters
emergencypowercells|Machinery|Emergency Power Cells
evacuationshelter|Consumer Items|Evacuation Shelter
fish|Foods|Fish
foodcartridges|Foods|Food Cartridges
fruitandvegetables|Foods|Fruit and Vegetables
geologicalequipment|Machinery|Geological Equipment
grain|Foods|Grain
hazardousenvironmentsuits|Technology|H.E. Suits
heliostaticfurnaces|Machinery|Microbial Furnaces
insulatingmembrane|Industrial Materials|Insulating Membrane
liquidoxygen|Chemicals|Liquid Oxygen
liquor|Legal Drugs|Liquor
medicaldiagnosticequipment|Technology|Medical Diagnostic Equipment
microcontrollers|Technology|Micro Controllers
militarygradefabrics|Textiles|Military Grade Fabrics
mineralextractors|Machinery|Mineral Extractors
mutomimager|Technology|Muon Imager
nonlethalweapons|Weapons|Non-Lethal Weapons
pesticides|Chemicals|Pesticides
polymers|Industrial Materials|Polymers
powergenerators|Machinery|Power Generators
reactivearmour|Weapons|Reactive Armour
resonatingseparators|Technology|Resonating Separators
robotics|Technology|Robotics
semiconductors|Industrial Materials|Semiconductors
steel|Metals|Steel
structuralregulators|Technology|Structural Regulators
superconductors|Industrial Materials|Superconductors
surfacestabilisers|Chemicals|Surface Stabilisers
survivalequipment|Consumer Items|Survival Equipment
tea|Foods|Tea
terrainenrichmentsystems|Technology|Land Enrichment Systems
thermalcoolingunits|Machinery|Thermal Cooling Units
titanium|Metals|Titanium
tritium|Chemicals|Tritium
water|Chemicals|Water
waterpurifiers|Machinery|Water Purifiers
wine|Legal Drugs|Wine";
        foreach (var line in data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('|'); catalog[parts[0]] = new[] { parts[1], parts[2] };
            catalog[CommodityKey(parts[2])] = catalog[parts[0]];
        }
        catalog["combatstabilizers"] = catalog["combatstabilisers"];
        catalog["muonimager"] = catalog["mutomimager"];
        catalog["landenrichmentsystems"] = catalog["terrainenrichmentsystems"];
        catalog["microbialfurnaces"] = catalog["heliostaticfurnaces"];
        return catalog;
    }
    static string CommodityKey(string key)
    {
        return Regex.Replace(key.Replace("$", "").Replace("_name;", "").Replace("_name", "").ToLowerInvariant(), "[^a-z0-9]", "");
    }
    public static string CommodityCategory(string key)
    {
        string[] info; return CommodityCatalog.TryGetValue(CommodityKey(key), out info) ? info[0] : "Other";
    }
    public static string CommodityName(string key)
    {
        string[] info; return CommodityCatalog.TryGetValue(CommodityKey(key), out info) ? info[1] : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(key.Replace("_", " "));
    }
    const string ApiBase = "https://ravencolonial100-awcbdvabgze4c5cq.canadacentral-01.azurewebsites.net/api/";
    readonly TextBox project = new TextBox { Dock = DockStyle.Fill };
    readonly Button refresh = new Button { Text = "Refresh", Dock = DockStyle.Right, Width = 75 };
    readonly Button configure = new Button { Text = "Settings", Dock = DockStyle.Right, Width = 75 };
    readonly Button editInventory = new Button { Text = "Edit inventory", Dock = DockStyle.Left, Width = 110, Enabled = false };
    readonly Label totals = new Label { Dock = DockStyle.Bottom, Height = 55, Padding = new Padding(8), Text = "Totals (t): —" };
    Dictionary<string, long> inventory = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    Dictionary<string, object> displayedCargo;
    HashSet<string> displayedUnknown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    bool editingInventory;
    readonly ComboBox selection = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    string commander = "", apiKey = "";
    bool updatingSelection;
    class ProjectChoice
    {
        public string Id, Name;
        public override string ToString() { return Name; }
    }
    readonly CheckBox pin = new CheckBox { Text = "Always on top", Checked = true, AutoSize = true };
    readonly CheckBox auto = new CheckBox { Text = "Refresh every minute", Checked = true, AutoSize = true };
    readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(8), Text = "Paste a Raven Colonial project link or build ID." };
    readonly ListView list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
    readonly HttpClient client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
    readonly Timer timer = new Timer { Interval = 60000 };
    readonly Timer cargoTimer = new Timer { Interval = 2000 };
    readonly ComboBox cargoMode = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly Label cargoStatus = new Label { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(8), Text = "Cargo tracking: Manual" };
    int windowOpacity = 100;
    SharedInventoryClient shared;
    bool sharedBusy, sharedConfigFailed;
    readonly Timer sharedTimer = new Timer { Interval = 5000 };
    readonly Label sharedStatus = new Label { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(8), Text = "Inventory: Local" };
    string selectedCargoMode = "Manual";
    string journalFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games", "Frontier Developments", "Elite Dangerous");
    JournalCargoTracker cargoTracker;
    readonly Dictionary<string, Dictionary<string, long>> depotBalances = new Dictionary<string, Dictionary<string, long>>();
    Dictionary<string, object> displayedDelivery;
    readonly Dictionary<string, Dictionary<string, object>> deliveryProjects = new Dictionary<string, Dictionary<string, object>>();
    bool changingMode, inventoryReady = true;
    readonly string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColonizationNeeds", "settings.json");
    bool busy;
    string shownId;

    void MigrateLegacySettings()
    {
        string target = Path.GetDirectoryName(settings);
        string legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RavenNeeds");
        if (!Directory.Exists(legacy)) return;
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(legacy))
        {
            string name = Path.GetFileName(file);
            if (name != "settings.json" && !(name.StartsWith("inventory-", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))) continue;
            string destination = Path.Combine(target, name);
            if (!File.Exists(destination)) File.Copy(file, destination);
        }
    }

    public ColonizationNeeds() : this(false) { }
    public ColonizationNeeds(bool preview)
    {
        Text = "ColonizationNeeds — Elite Dangerous";
        Size = new Size(480, 565); MinimumSize = new Size(460, 395);
        Font = new Font("Segoe UI", 9); TopMost = true;
        BackColor = WindowColor; ForeColor = TextColor;
        refresh.BackColor = configure.BackColor = AccentColor;
        refresh.ForeColor = configure.ForeColor = Color.FromArgb(255, 176, 59);
        refresh.FlatStyle = configure.FlatStyle = FlatStyle.Flat;
        editInventory.BackColor = refresh.BackColor; editInventory.ForeColor = refresh.ForeColor; editInventory.FlatStyle = FlatStyle.Flat;
        foreach (var button in new[] { refresh, configure, editInventory }) { button.FlatAppearance.BorderSize = 0; button.FlatAppearance.MouseOverBackColor = Color.FromArgb(95, 57, 14); }
        StyleCombo(selection); StyleCombo(cargoMode);
        selection.ForeColor = TextColor; selection.BackColor = TableColor;
        status.ForeColor = Color.FromArgb(203, 133, 48);
        totals.BackColor = Color.FromArgb(30, 24, 17);
        status.Text = "Open Settings to configure your Raven Colonial account.";
        list.BackColor = TableColor; list.ForeColor = TextColor;
        list.BorderStyle = BorderStyle.None;
        list.HandleCreated += delegate { SetWindowTheme(list.Handle, "DarkMode_Explorer", null); SetWindowTheme(SendMessage(list.Handle, 0x101F, IntPtr.Zero, IntPtr.Zero), "DarkMode_ItemsView", null); };
        list.OwnerDraw = true;
        list.DrawColumnHeader += delegate(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var brush = new SolidBrush(Color.FromArgb(42, 30, 16))) e.Graphics.FillRectangle(brush, e.Bounds);
            var bounds = e.Bounds; bounds.Inflate(-4, 0);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            flags |= e.Header.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left;
            TextRenderer.DrawText(e.Graphics, e.Header.Text, Font, bounds, TextColor, flags);
        };
        list.DrawItem += delegate(object sender, DrawListViewItemEventArgs e) { e.DrawDefault = false; };
        list.DrawSubItem += delegate(object sender, DrawListViewSubItemEventArgs e)
        {
            Color background = e.Item.Selected ? Color.FromArgb(48, 34, 17) : TableColor;
            using (var brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
            var bounds = e.Bounds; bounds.Inflate(-4, 0);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            flags |= e.Header.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left;
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, list.Font, bounds, e.SubItem.ForeColor, flags);
        };
        list.ItemSelectionChanged += delegate { list.Invalidate(); };
        list.GotFocus += delegate { list.Invalidate(); };
        list.LostFocus += delegate { list.Invalidate(); };
        list.Columns.Add("Commodity", 190);
        list.Columns.Add("Required", 76, HorizontalAlignment.Right);
        list.Columns.Add("Carrier stock", 88, HorizontalAlignment.Right);
        list.Columns.Add("Still needed", 88, HorizontalAlignment.Right);
        list.ShowItemToolTips = true;
        list.SizeChanged += delegate { list.Columns[0].Width = Math.Max(130, list.ClientSize.Width - 252 - SystemInformation.VerticalScrollBarWidth - 4); };
        list.MultiSelect = false; list.HideSelection = false;
        var header = new Panel { Dock = DockStyle.Top, Height = 135, Padding = new Padding(8) };
        var entry = new Panel { Dock = DockStyle.Top, Height = 25 };
        entry.Controls.Add(project); entry.Controls.Add(refresh); entry.Controls.Add(configure); entry.Controls.Add(editInventory);
        var options = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 25 };
        options.Controls.Add(pin); options.Controls.Add(auto);
        var modePanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 32 };
        modePanel.Controls.Add(new Label { Text = "Cargo mode", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        cargoMode.Items.AddRange(new object[] { "Manual", "Collect", "Colonize" }); cargoMode.SelectedIndex = 0;
        cargoMode.BackColor = TableColor; cargoMode.ForeColor = TextColor;
        modePanel.Controls.Add(cargoMode);
        modePanel.Controls.Add(new Label { Text = "Collect + / Colonize − (on loading)", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        header.Controls.Add(modePanel);
        header.Controls.Add(selection); header.Controls.Add(entry); header.Controls.Add(options);
        selection.BringToFront();
        project.Visible = false;
        Controls.Add(list); Controls.Add(totals); Controls.Add(status); Controls.Add(cargoStatus); Controls.Add(sharedStatus); Controls.Add(header);
        pin.CheckedChanged += delegate { TopMost = pin.Checked; };
        refresh.Click += async delegate { await LoadProject(); };
        configure.Click += async delegate { if (Configure()) { shownId = null; ClearCommodities(); LoadInventory(); ResetCargoTracker(); cargoTimer.Start(); updatingSelection = true; selection.Items.Clear(); updatingSelection = false; await LoadProject(); } };
        cargoMode.SelectedIndexChanged += delegate
        {
            if (changingMode) return;
            string next = Convert.ToString(cargoMode.SelectedItem);
            try
            {
                if (cargoTracker != null) { try { PollCargo(); } catch { if (next != "Manual") throw; } }
                SaveTrackingSettings(next, journalFolder);
                selectedCargoMode = next;
                ResetCargoTracker();
                RenderCommodities();
            }
            catch (Exception ex)
            {
                changingMode = true; cargoMode.SelectedItem = selectedCargoMode; changingMode = false;
                cargoStatus.Text = "Mode unchanged: " + ex.Message;
            }
        };
        cargoTimer.Tick += delegate { if (!editingInventory) { try { PollCargo(); } catch (Exception ex) { cargoStatus.Text = "Cargo tracking paused: " + ex.Message; } } };
        sharedTimer.Tick += async delegate { if (!editingInventory) await SyncShared(); };
        editInventory.Click += delegate { EditInventory(); };
        list.DoubleClick += delegate { EditInventory(); };
        list.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; EditInventory(); } };
        selection.SelectedIndexChanged += async delegate { if (!updatingSelection) await LoadProject(); };
        project.KeyDown += async delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await LoadProject(); } };
        timer.Tick += async delegate { if (!editingInventory && auto.Checked && !String.IsNullOrWhiteSpace(commander)) await LoadProject(); };
        try
        {
            if (!preview) MigrateLegacySettings();
            if (File.Exists(settings))
            {
                var saved = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(File.ReadAllText(settings));
                commander = saved["commander"];
                if (!String.IsNullOrEmpty(saved["key"])) apiKey = Unprotect(saved["key"]);
            }
        }
        catch { status.Text = "Saved settings could not be read. Open Settings to re-enter your account."; }
        try
        {
            string trackingPath = Path.Combine(Path.GetDirectoryName(settings), "tracking.json");
            if (File.Exists(trackingPath))
            {
                var saved = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(File.ReadAllText(trackingPath));
                int percent; if (saved.ContainsKey("opacity") && Int32.TryParse(saved["opacity"], out percent)) windowOpacity = Math.Max(30, Math.Min(100, percent));
                if (saved.ContainsKey("journalFolder")) journalFolder = saved["journalFolder"];
                if (saved.ContainsKey("mode") && cargoMode.Items.Contains(saved["mode"])) selectedCargoMode = saved["mode"];
            }
        }
        catch { cargoStatus.Text = "Tracking settings unreadable; using Manual mode."; }
        changingMode = true; cargoMode.SelectedItem = selectedCargoMode; changingMode = false;
        if (!preview)
        {
            try { shared = SharedInventoryClient.Load(Path.GetDirectoryName(settings)); }
            catch { sharedConfigFailed=true; sharedStatus.Text = "Shared settings unreadable. Open Settings → Shared inventory."; }
        }
        if (!preview) Shown += async delegate { timer.Start(); sharedTimer.Start(); if (commander.Length > 0 || Configure()) { LoadInventory(); ResetCargoTracker(); cargoTimer.Start(); await SyncShared(); await LoadProject(); } };
        if (!preview) { RestoreWindowLocation(); FormClosing += delegate { SaveWindowLocation(); }; }
        FormClosed += delegate { timer.Stop(); timer.Dispose(); cargoTimer.Stop(); cargoTimer.Dispose(); sharedTimer.Stop(); sharedTimer.Dispose(); if(shared != null) shared.Dispose(); client.Dispose(); };
        Opacity = windowOpacity / 100.0;
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ColonizationNeeds/1.19");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
    }

    void SaveTrackingSettings(string mode, string folder)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settings));
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(settings), "tracking.json"), new JavaScriptSerializer().Serialize(new Dictionary<string, string> { { "mode", mode }, { "journalFolder", folder }, { "opacity", windowOpacity.ToString(CultureInfo.InvariantCulture) } }));
    }
    public static Point VisibleWindowLocation(Point location, Size size, Rectangle workingArea)
    {
        return new Point(Math.Max(workingArea.Left, Math.Min(location.X, workingArea.Right - size.Width)), Math.Max(workingArea.Top, Math.Min(location.Y, workingArea.Bottom - size.Height)));
    }
    void RestoreWindowLocation()
    {
        try
        {
            string path = Path.Combine(Path.GetDirectoryName(settings), "window.json");
            if (!File.Exists(path)) return;
            var saved = new JavaScriptSerializer().Deserialize<Dictionary<string, int>>(File.ReadAllText(path));
            var location = new Point(saved["x"], saved["y"]);
            StartPosition = FormStartPosition.Manual;
            Location = VisibleWindowLocation(location, Size, Screen.FromPoint(location).WorkingArea);
        }
        catch { /* An unreadable position must not prevent the application from opening. */ }
    }
    void SaveWindowLocation()
    {
        try
        {
            Point location = WindowState == FormWindowState.Normal ? Location : RestoreBounds.Location;
            string path = Path.Combine(Path.GetDirectoryName(settings), "window.json"), temp = path + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(temp, new JavaScriptSerializer().Serialize(new Dictionary<string, int> { { "x", location.X }, { "y", location.Y } }));
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
        catch { /* Position saving is best effort; closing must remain possible. */ }
    }
    void ResetCargoTracker()
    {
        try { cargoTracker = new JournalCargoTracker(journalFolder); cargoStatus.Text = "Cargo tracking: " + selectedCargoMode + " · Watching new ship loads"; }
        catch (Exception ex) { cargoTracker = null; cargoStatus.Text = "Cargo tracking paused: " + ex.Message; }
    }
    bool SharedEnabled { get { return shared != null && shared.Enabled; } }
    async Task SyncShared()
    {
        if (!SharedEnabled || sharedBusy) return;
        var current = shared; sharedBusy = true;
        try
        {
            await current.Sync();
            if (IsDisposed || current != shared) return;
            inventory = new Dictionary<string,long>(current.Stock, StringComparer.OrdinalIgnoreCase);
            if(displayedCargo != null) foreach(var key in displayedCargo.Keys)
            {
                long value; if(current.Stock.TryGetValue(JournalCargoTracker.Canonical(key), out value)) inventory[key] = value;
            }
            inventoryReady = true; RenderCommodities();
            sharedStatus.Text = "Shared: " + current.Group + " · " + current.Pending + " pending" + (current.Blocked ? " · Edit conflict; open Shared inventory" : " · " + DateTime.Now.ToString("HH:mm:ss")) + " " + current.Warning;
        }
        catch(Exception ex) { if(!IsDisposed) sharedStatus.Text = "Shared: " + current.Pending + " pending · " + ex.Message; }
        finally { sharedBusy = false; }
    }
    void SaveInventory(Dictionary<string, long> updated)
    {
        string path = InventoryPath(), temp = path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(temp, new JavaScriptSerializer().Serialize(updated));
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
        inventory = updated; inventoryReady = true;
    }
    public static long ApplyCargoMode(long held, long acquired, string mode)
    {
        if (held < 0 || acquired < 0) throw new ArgumentOutOfRangeException();
        if (mode == "Collect") return checked(held + acquired);
        if (mode == "Colonize") return Remaining(held, acquired);
        return held;
    }
    void PollCargo()
    {
        if(sharedConfigFailed) throw new IOException("Resolve unreadable Shared inventory settings before automatic tracking.");
        if (cargoTracker == null) ResetCargoTracker();
        if (cargoTracker == null || String.IsNullOrWhiteSpace(commander)) return;
        if (!inventoryReady && selectedCargoMode != "Manual") throw new IOException("Saved inventory could not be loaded. Correct it manually first.");
        bool changed = false;
        cargoTracker.Poll(commander, delegate(Dictionary<string, long> gained)
        {
            if (selectedCargoMode == "Manual") return;
            if (SharedEnabled)
            {
                shared.Enqueue(gained.Select(x => new SharedInventoryClient.Change { commodity=x.Key, amount=x.Value, operation=selectedCargoMode=="Collect"?"add":"colonize", source=selectedCargoMode }));
                sharedStatus.Text = "Shared: " + shared.Pending + " pending changes";
                return;
            }
            var updated = new Dictionary<string, long>(inventory, StringComparer.OrdinalIgnoreCase);
            long quantity = 0; bool shortage = false;
            foreach (var pair in gained)
            {
                string key = updated.Keys.FirstOrDefault(x => JournalCargoTracker.Canonical(x) == pair.Key) ?? pair.Key;
                long held; updated.TryGetValue(key, out held);
                if (selectedCargoMode == "Colonize" && pair.Value > held) shortage = true;
                updated[key] = ApplyCargoMode(held, pair.Value, selectedCargoMode); quantity = checked(quantity + pair.Value);
            }
            SaveInventory(updated);
            changed = true;
            cargoStatus.Text = selectedCargoMode + ": " + (selectedCargoMode == "Collect" ? "+" : "−") + quantity.ToString("N0") + " t · " + DateTime.Now.ToString("HH:mm:ss") + (shortage ? " · Stock shortfall; correct manually" : "");
        }, delegate(string market, string timestamp, Dictionary<string, long> remaining)
        {
            if (selectedCargoMode != "Colonize") return;
            DateTimeOffset observedAt;
            remaining["__observedUtcTicks"] = DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out observedAt) ? observedAt.UtcTicks : DateTime.UtcNow.Ticks;
            depotBalances[commander.Trim().ToLowerInvariant() + "|" + market] = remaining;
            Directory.CreateDirectory(Path.GetDirectoryName(settings));
            string path = InventoryPath() + ".deliveries.json";
            File.WriteAllText(path + ".tmp", new JavaScriptSerializer().Serialize(depotBalances));
            if (File.Exists(path)) File.Replace(path + ".tmp", path, null); else File.Move(path + ".tmp", path);
            RecalculateDelivery(); changed = true;
            cargoStatus.Text = "Colonize: depot requirements updated · " + DateTime.Now.ToString("HH:mm:ss");
        });
        if (changed) RenderCommodities();
        if (selectedCargoMode != "Manual" && !String.Equals(cargoTracker.CurrentCommander, commander, StringComparison.OrdinalIgnoreCase)) cargoStatus.Text = "Cargo tracking: waiting for commander " + commander + ".";
    }

    string InventoryPath()
    {
        using (var hash = SHA256.Create())
            return Path.Combine(Path.GetDirectoryName(settings), "inventory-" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(commander.Trim().ToLowerInvariant()))).Replace("-", "") + ".json");
    }

    public static Dictionary<string, long> ParseInventory(string json)
    {
        var saved = new JavaScriptSerializer().Deserialize<Dictionary<string, long>>(json);
        if (saved == null || saved.Any(x => String.IsNullOrWhiteSpace(x.Key) || x.Value < 0)) throw new Exception("Invalid saved inventory.");
        return new Dictionary<string, long>(saved, StringComparer.OrdinalIgnoreCase);
    }

    void LoadInventory()
    {
        if(SharedEnabled) { inventory = new Dictionary<string,long>(shared.Stock, StringComparer.OrdinalIgnoreCase); inventoryReady=true; return; }
        inventoryReady = true;
        inventory = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        try { if (File.Exists(InventoryPath())) inventory = ParseInventory(File.ReadAllText(InventoryPath())); }
        catch { inventoryReady = false; MessageBox.Show(this, "Saved inventory could not be read. Automatic cargo changes are paused. Your previous file is kept as a backup when you next save.", "Inventory"); }
    }

    void ClearCommodities()
    {
        displayedCargo = null; list.Items.Clear(); list.Groups.Clear(); totals.Text = "Totals (t): —"; editInventory.Enabled = false;
    }

    public static long Remaining(long required, long held)
    {
        if (required < 0 || held < 0) throw new ArgumentOutOfRangeException();
        return held >= required ? 0 : required - held;
    }

    public static long AdjustInventory(long held, long amount, string operation)
    {
        if (held < 0 || amount < 0) throw new ArgumentOutOfRangeException();
        if (operation == "Set total") return amount;
        if (operation == "Remove")
        {
            if (amount > held) throw new ArgumentException("You cannot remove more than the current inventory.");
            return held - amount;
        }
        if (operation == "Add") return checked(held + amount);
        throw new ArgumentException("Choose Add, Remove, or Set total.");
    }

    void RenderCommodities()
    {
        if (displayedCargo == null) return;
        long totalRequired = 0, totalHeld = 0, totalRemaining = 0;
        int unknown = 0;
        var rows = new List<ListViewItem>();
        var groups = new Dictionary<string, ListViewGroup>();
        foreach (var item in displayedCargo.OrderBy(x => CommodityCategory(x.Key) == "Other" ? "~" : CommodityCategory(x.Key), StringComparer.OrdinalIgnoreCase).ThenBy(x => CommodityName(x.Key), StringComparer.OrdinalIgnoreCase))
        {
            long required = Convert.ToInt64(item.Value);
            long held; inventory.TryGetValue(item.Key, out held);
            bool uncertain = displayedUnknown.Contains(item.Key);
            bool requiredUncertain = uncertain;
            if (required == 0 && held == 0 && !uncertain && selectedCargoMode != "Colonize") continue;
            long remaining = selectedCargoMode == "Colonize" ? (displayedDelivery != null && displayedDelivery.ContainsKey(item.Key) ? Convert.ToInt64(displayedDelivery[item.Key]) : required) : Remaining(required, held);
            if (selectedCargoMode == "Colonize" && displayedDelivery != null && displayedDelivery.ContainsKey(item.Key) && Convert.ToInt64(displayedDelivery[item.Key]) >= 0) uncertain = false;
            if (remaining < 0) { remaining = 0; uncertain = true; }
            var row = new ListViewItem(CommodityName(item.Key)); row.ToolTipText = row.Text;
            string category = CommodityCategory(item.Key);
            if (!groups.ContainsKey(category)) groups[category] = new ListViewGroup(category, HorizontalAlignment.Left);
            row.Group = groups[category];
            row.Tag = item.Key;
            row.SubItems.Add(requiredUncertain ? (required > 0 ? required.ToString("N0") + " + ?" : "Unknown") : required.ToString("N0"));
            row.SubItems.Add(held.ToString("N0"));
            row.SubItems.Add(uncertain ? (remaining > 0 ? remaining.ToString("N0") + " + ?" : "Unknown") : remaining.ToString("N0"));
            row.ForeColor = !uncertain && remaining == 0 ? CoveredColor : TextColor;
            row.UseItemStyleForSubItems = false;
            foreach (ListViewItem.ListViewSubItem cell in row.SubItems) cell.ForeColor = row.ForeColor;
            row.SubItems[3].ForeColor = !uncertain && remaining == 0 ? CoveredColor : selectedCargoMode == "Colonize" ? DeliveryNeededColor : NeededColor;
            rows.Add(row);
            totalRequired = checked(totalRequired + required); totalHeld = checked(totalHeld + held); totalRemaining = checked(totalRemaining + remaining);
            if (uncertain) unknown++;
        }
        string selected = list.SelectedItems.Count > 0 ? Convert.ToString(list.SelectedItems[0].Tag) : null;
        // Keep the same commodity at the same vertical offset, even if rows above it change.
        ListViewItem anchor = list.Items.Cast<ListViewItem>().FirstOrDefault(x => x.Bounds.Bottom > Font.Height + 10 && x.Bounds.Top < list.ClientSize.Height);
        string anchorKey = anchor == null ? null : Convert.ToString(anchor.Tag);
        int anchorY = anchor == null ? 0 : anchor.Bounds.Top;
        int anchorIndex = anchor == null ? 0 : anchor.Index;
        int scrollPosition = list.IsHandleCreated ? GetScrollPos(list.Handle, 1) : 0;
        bool sameRows = list.Items.Count == rows.Count && list.Items.Cast<ListViewItem>().Select(x => Convert.ToString(x.Tag)).SequenceEqual(rows.Select(x => Convert.ToString(x.Tag)));
        list.BeginUpdate();
        try
        {
            if (sameRows)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    for (int column = 1; column < 4; column++) list.Items[i].SubItems[column].Text = rows[i].SubItems[column].Text;
                    for (int column = 0; column < 4; column++) list.Items[i].SubItems[column].ForeColor = rows[i].SubItems[column].ForeColor;
                    list.Items[i].ForeColor = rows[i].ForeColor;
                }
            }
            else
            {
            list.Items.Clear(); list.Groups.Clear();
            foreach (var group in groups.OrderBy(x => x.Key == "Other" ? "~" : x.Key, StringComparer.OrdinalIgnoreCase)) list.Groups.Add(group.Value);
            list.ShowGroups = true; list.Items.AddRange(rows.ToArray());
            foreach (ListViewItem row in list.Items) if (Convert.ToString(row.Tag) == selected) row.Selected = true;
            }
        }
        finally { list.EndUpdate(); }
        if (!sameRows && anchorKey != null && list.Items.Count > 0)
        {
            var restored = list.Items.Cast<ListViewItem>().FirstOrDefault(x => Convert.ToString(x.Tag) == anchorKey) ?? list.Items[Math.Min(anchorIndex, list.Items.Count - 1)];
            restored.EnsureVisible();
            SendMessage(list.Handle, 0x1014, IntPtr.Zero, new IntPtr(restored.Bounds.Top - anchorY)); // LVM_SCROLL, pixel offset.
        }
        string suffix = unknown > 0 ? " + unknown" : "";
        totals.Text = "Totals (t): Required " + totalRequired.ToString("N0") + suffix + " | Stock " + totalHeld.ToString("N0") + " | Needed " + totalRemaining.ToString("N0") + suffix + "\nDouble-click to Add, Remove, or Set total.";
        editInventory.Enabled = !busy;
    }

    void EditInventory()
    {
        if(SharedEnabled && !shared.Ready) { MessageBox.Show(this,"Connect to shared inventory before editing it."); return; }
        if (busy || displayedCargo == null) return;
        if (list.SelectedItems.Count == 0) { MessageBox.Show(this, "Select a commodity first, then click Edit inventory.", "Inventory"); return; }
        var row = list.SelectedItems[0]; string commodity = Convert.ToString(row.Tag);
        long held; inventory.TryGetValue(commodity, out held);
        long sharedVersion=0; if(SharedEnabled) shared.Versions.TryGetValue(JournalCargoTracker.Canonical(commodity),out sharedVersion);
        editingInventory = true;
        try
        {
            using (var dialog = new Form { Text = "Inventory — " + row.Text, Size = new Size(420, 315), Font = Font, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false })
            {
                dialog.Controls.Add(new Label { Left = 15, Top = 18, Width = 370, Text = "Current inventory: " + held.ToString("N0") + " t" });
                var operation = new ComboBox { Left = 15, Top = 47, Width = 370, DropDownStyle = ComboBoxStyle.DropDownList };
                operation.Items.AddRange(SharedEnabled && (!shared.Ready || shared.Role != "admin" || shared.Pending > 0) ? new object[] { "Add", "Remove" } : new object[] { "Add", "Remove", "Set total" });
                dialog.Controls.Add(operation);
                var amountLabel = new Label { Left = 15, Top = 82, Width = 370, Text = "Quantity to add (tonnes)" };
                dialog.Controls.Add(amountLabel);
                var quantity = new NumericUpDown { Left = 15, Top = 106, Width = 370, Minimum = 0, Maximum = Int64.MaxValue, Value = 0, ThousandsSeparator = true, DecimalPlaces = 0 };
                dialog.Controls.Add(quantity);
                var result = new Label { Left = 15, Top = 140, Width = 370, Height = 24 };
                dialog.Controls.Add(result);
                dialog.Controls.Add(new Label { Left = 15, Top = 172, Width = 370, Height = 42, Text = "Manual corrections update carrier stock.\nAutomatic modes act when cargo is loaded onto your ship." });
                var save = new Button { Left = 215, Top = 230, Width = 80, Text = "Apply" };
                var cancel = new Button { Left = 305, Top = 230, Width = 80, Text = "Cancel", DialogResult = DialogResult.Cancel };
                dialog.Controls.Add(save); dialog.Controls.Add(cancel); dialog.AcceptButton = save; dialog.CancelButton = cancel;
                Action preview = delegate
                {
                    try
                    {
                        long next = AdjustInventory(held, Decimal.ToInt64(quantity.Value), Convert.ToString(operation.SelectedItem));
                        result.Text = "New inventory: " + next.ToString("N0") + " t"; result.ForeColor = TextColor; save.Enabled = true;
                    }
                    catch (ArgumentException ex) { result.Text = ex.Message; result.ForeColor = Color.Firebrick; save.Enabled = false; }
                    catch (OverflowException) { result.Text = "The resulting quantity is too large."; result.ForeColor = Color.Firebrick; save.Enabled = false; }
                };
                operation.SelectedIndexChanged += delegate
                {
                    string mode = Convert.ToString(operation.SelectedItem);
                    amountLabel.Text = mode == "Set total" ? "New total in inventory (tonnes)" : "Quantity to " + mode.ToLowerInvariant() + " (tonnes)";
                    quantity.Value = mode == "Set total" ? held : 0;
                    preview();
                };
                quantity.ValueChanged += delegate { preview(); };
                operation.SelectedIndex = 0;
                save.Click += delegate
                {
                    try
                    {
                        if(SharedEnabled)
                        {
                            string key=JournalCargoTracker.Canonical(commodity); long version=sharedVersion;
                            string op=Convert.ToString(operation.SelectedItem);
                            shared.Enqueue(new[] { new SharedInventoryClient.Change { commodity=key, amount=Decimal.ToInt64(quantity.Value), operation=op=="Set total"?"set":op.ToLowerInvariant(), expectedVersion=version, source="Manual" } });
                            sharedStatus.Text="Shared: "+shared.Pending+" pending changes"; dialog.DialogResult=DialogResult.OK; return;
                        }
                        var updated = new Dictionary<string, long>(inventory, StringComparer.OrdinalIgnoreCase);
                        updated[commodity] = AdjustInventory(held, Decimal.ToInt64(quantity.Value), Convert.ToString(operation.SelectedItem));
                        // Check footer arithmetic before committing the edit.
                        long sum = 0; foreach (var p in displayedCargo) { long n; if (updated.TryGetValue(p.Key, out n)) sum = checked(sum + n); }
                        SaveInventory(updated); RenderCommodities(); dialog.DialogResult = DialogResult.OK;
                    }
                    catch { MessageBox.Show(dialog, "Inventory could not be saved. Check the quantity and access to your Local AppData folder."); }
                };
                ApplyDialogPalette(dialog);
                preview();
                dialog.ShowDialog(this);
            }
        }
        finally { editingInventory = false; }
    }

    static string Protect(string key) { return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser)); }
    static string Unprotect(string key) { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(key), null, DataProtectionScope.CurrentUser)); }
    void ConfigureShared()
    {
        if(sharedBusy) { MessageBox.Show(this,"Synchronization is in progress. Try again in a moment."); return; }
        bool wasEditing=editingInventory; editingInventory=true;
        try
        {
            using(var dialog=new Form { Text="Shared carrier inventory", Size=new Size(440,420), Font=Font, StartPosition=FormStartPosition.CenterParent, FormBorderStyle=FormBorderStyle.FixedDialog, MaximizeBox=false, MinimizeBox=false })
            {
                var enabled=new CheckBox { Text="Use shared inventory", Checked=SharedEnabled, Left=15, Top=15, Width=380 };
                var url=new TextBox { Text=shared==null?"https://colonizationneeds-api.macytr.workers.dev/":shared.Url, Left=15, Top=70, Width=390 };
                var token=new TextBox { Text=shared==null?"":shared.Token, UseSystemPasswordChar=true, Left=15, Top=128, Width=390 };
                dialog.Controls.Add(enabled); dialog.Controls.Add(new Label { Text="Server URL", Left=15, Top=46, Width=390 }); dialog.Controls.Add(url);
                dialog.Controls.Add(new Label { Text="Player access token (not the owner secret)", Left=15, Top=104, Width=390 }); dialog.Controls.Add(token);
                dialog.Controls.Add(new Label { Text="Your token identifies your player and carrier group.\nLocal stock is kept separately; it is not uploaded automatically.\nChanges appear after server confirmation, normally within 5 seconds.", Left=15, Top=162, Width=390, Height=64 });
                var test=new Button { Text="Test connection", Left=15, Top=235, Width=125 };
                var ledger=new Button { Text="View ledger", Left=145, Top=235, Width=110 };
                var import=new Button { Text="Sync local to shared", Left=260, Top=235, Width=145 };
                var cancelEdit=new Button { Text="Cancel conflicted edit", Left=15, Top=277, Width=180, Enabled=shared!=null && shared.Blocked };
                var message=new Label { Left=15, Top=315, Width=390, Height=25 };
                var save=new Button { Text="Save", Left=230, Top=345, Width=80 };
                var cancel=new Button { Text="Cancel", Left=325, Top=345, Width=80, DialogResult=DialogResult.Cancel };
                dialog.Controls.Add(test); dialog.Controls.Add(ledger); dialog.Controls.Add(cancelEdit); dialog.Controls.Add(message); dialog.Controls.Add(save); dialog.Controls.Add(cancel);
                dialog.Controls.Add(import);
                bool importing=false;
                dialog.FormClosing += delegate(object sender,FormClosingEventArgs e) { if(importing) e.Cancel=true; };
                import.Click += async delegate
                {
                    SharedInventoryClient connection=null; bool adopted=false;
                    importing=true; import.Enabled=test.Enabled=ledger.Enabled=save.Enabled=cancel.Enabled=cancelEdit.Enabled=url.Enabled=token.Enabled=enabled.Enabled=false;
                    try
                    {
                        if(shared!=null && shared.Pending>0) throw new IOException("Resolve pending shared changes before replacing totals.");
                        if(String.IsNullOrWhiteSpace(commander) || !File.Exists(InventoryPath())) throw new IOException("No saved local inventory exists for your configured commander.");
                        var local=ParseInventory(File.ReadAllText(InventoryPath()));
                        connection=new SharedInventoryClient(Path.GetDirectoryName(settings),url.Text,token.Text,true);
                        if(connection.Pending>0) throw new IOException("This connection has pending changes. Resolve them before syncing.");
                        await connection.Sync();
                        if(connection.Role!="admin") throw new IOException("Only a shared group admin can replace the totals.");
                        var changes=SharedInventoryClient.ReplacementChanges(local,connection.Stock,connection.Versions);
                        if(changes.Count==0) { message.Text="Shared totals already match local inventory."; return; }
                        using(var preview=new Form { Text="Replace shared inventory — "+connection.Group, Size=new Size(540,440), Font=Font, StartPosition=FormStartPosition.CenterParent })
                        {
                            var explanation=new Label { Dock=DockStyle.Top, Height=65, Padding=new Padding(8), Text="Replace shared totals with saved local counts for "+commander+".\nShared commodities absent locally become zero. Your local copy is kept.\nEach correction is recorded in the shared ledger." };
                            var lines=changes.Select(x=>CommodityName(x.commodity)+": "+(connection.Stock.ContainsKey(x.commodity)?connection.Stock[x.commodity]:0).ToString("N0")+" → "+x.amount.ToString("N0")+" t").ToArray();
                            var contents=new TextBox { Multiline=true, ReadOnly=true, Dock=DockStyle.Fill, ScrollBars=ScrollBars.Vertical, Lines=lines };
                            var buttons=new FlowLayoutPanel { Dock=DockStyle.Bottom, Height=38, FlowDirection=FlowDirection.RightToLeft };
                            var confirm=new Button { Text="Replace totals", Width=125, DialogResult=DialogResult.OK };
                            var abort=new Button { Text="Cancel", Width=90, DialogResult=DialogResult.Cancel };
                            buttons.Controls.Add(confirm); buttons.Controls.Add(abort); preview.Controls.Add(contents); preview.Controls.Add(explanation); preview.Controls.Add(buttons); preview.CancelButton=abort; ApplyDialogPalette(preview);
                            if(preview.ShowDialog(dialog)!=DialogResult.OK) return;
                        }
                        connection.SaveSettings(); connection.Enqueue(changes);
                        if(shared!=null) shared.Dispose(); shared=connection; adopted=true; sharedConfigFailed=false;
                        enabled.Checked=true; LoadInventory(); ResetCargoTracker();
                        await SyncShared();
                        message.Text=shared.Pending==0?"Local counts synced. Shared mode is now enabled.":"Sync queued: "+shared.Pending+" remaining. Check shared status.";
                    }
                    catch(Exception ex) { message.Text=ex.Message; }
                    finally
                    {
                        if(connection!=null && !adopted) connection.Dispose();
                        importing=false; if(!dialog.IsDisposed) { import.Enabled=test.Enabled=ledger.Enabled=save.Enabled=cancel.Enabled=url.Enabled=token.Enabled=enabled.Enabled=true; cancelEdit.Enabled=shared!=null && shared.Blocked; }
                    }
                };
                test.Click += async delegate
                {
                    test.Enabled=false; save.Enabled=false;
                    try { using(var connection=new SharedInventoryClient(Path.GetDirectoryName(settings),url.Text,token.Text,true)) { var data=await connection.Request("inventory"); message.Text="Connected: "+data["groupId"]+" · "+data["role"]; } }
                    catch(Exception ex) { message.Text=ex.Message; }
                    finally { if(!dialog.IsDisposed) { test.Enabled=true; save.Enabled=true; } }
                };
                ledger.Click += async delegate
                {
                    ledger.Enabled=false;
                    try
                    {
                        using(var connection=new SharedInventoryClient(Path.GetDirectoryName(settings),url.Text,token.Text,true))
                        {
                            var data=await connection.Request("ledger?after=0");
                            using(var view=new Form { Text="Shared ledger — first 200 entries", Size=new Size(740,440), StartPosition=FormStartPosition.CenterParent, Font=Font })
                            {
                                var text=new TextBox { Multiline=true, ReadOnly=true, Dock=DockStyle.Fill, ScrollBars=ScrollBars.Both, WordWrap=false };
                                var lines=new List<string>();
                                foreach(var item in (object[])data["transactions"]) { var row=(Dictionary<string,object>)item; lines.Add(row["created_at"]+" | "+row["player"]+" | "+row["commodity"]+" | "+row["operation"]+" "+row["amount"]+" | "+row["before_balance"]+" → "+row["after_balance"]+" | "+row["source"]); }
                                text.Lines=lines.ToArray(); view.Controls.Add(text); ApplyDialogPalette(view); view.ShowDialog(dialog);
                            }
                        }
                    }
                    catch(Exception ex) { message.Text=ex.Message; }
                    finally { if(!dialog.IsDisposed) ledger.Enabled=true; }
                };
                cancelEdit.Click += delegate { if(shared!=null && shared.CancelBlockedManual()) { cancelEdit.Enabled=false; message.Text="Conflicted edit cancelled. Refresh before correcting stock."; } };
                save.Click += delegate
                {
                    try
                    {
                        var next=new SharedInventoryClient(Path.GetDirectoryName(settings),url.Text,token.Text,enabled.Checked);
                        try
                        {
                            if(shared!=null && shared.Pending>0 && (next.Url!=shared.Url || next.Token!=shared.Token || next.Enabled!=shared.Enabled)) throw new IOException("Resolve pending changes before switching inventory or credentials.");
                            next.SaveSettings();
                        }
                        catch { next.Dispose(); throw; }
                        if(shared!=null) shared.Dispose(); shared=next; sharedConfigFailed=false; LoadInventory(); RenderCommodities(); ResetCargoTracker();
                        sharedStatus.Text=SharedEnabled?"Shared: connecting…":"Inventory: Local"; dialog.DialogResult=DialogResult.OK;
                    }
                    catch(Exception ex) { message.Text=ex.Message; }
                };
                ApplyDialogPalette(dialog); dialog.CancelButton=cancel; dialog.ShowDialog(this);
            }
        }
        finally { editingInventory=wasEditing; }
    }
    bool Configure()
    {
        using (var dialog = new Form { Text = "ColonizationNeeds settings", Size = new Size(410, 455), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, Font = Font })
        {
            var name = new TextBox { Text = commander, Left = 15, Top = 42, Width = 360 };
            var key = new TextBox { Text = apiKey, Left = 15, Top = 100, Width = 360, UseSystemPasswordChar = true };
            dialog.Controls.Add(new Label { Text = "Raven Colonial commander name (account identifier)", Left = 15, Top = 18, Width = 370 });
            dialog.Controls.Add(name);
            dialog.Controls.Add(new Label { Text = "API key (optional for public read access)", Left = 15, Top = 76, Width = 370 });
            dialog.Controls.Add(key);
            dialog.Controls.Add(new Label { Text = "Saved key is encrypted for your Windows account.", Left = 15, Top = 130, Width = 370 });
            dialog.Controls.Add(new Label { Text = "Elite Dangerous journal folder", Left = 15, Top = 165, Width = 370 });
            var folder = new TextBox { Text = journalFolder, Left = 15, Top = 188, Width = 275 };
            var browse = new Button { Text = "Browse", Left = 295, Top = 186, Width = 80 };
            dialog.Controls.Add(folder); dialog.Controls.Add(browse);
            browse.Click += delegate { using (var picker = new FolderBrowserDialog { SelectedPath = folder.Text, Description = "Select the folder containing Journal.*.log and Cargo.json" }) { if (picker.ShowDialog(dialog) == DialogResult.OK) folder.Text = picker.SelectedPath; } };
            var opacityLabel = new Label { Text = "Window opacity: " + windowOpacity + "%", Left = 15, Top = 225, Width = 360 };
            var opacitySlider = new AmberSlider { Minimum = 30, Maximum = 100, Value = windowOpacity, TickFrequency = 10, Left = 15, Top = 250, Width = 360 };
            int originalOpacity = windowOpacity;
            dialog.Controls.Add(opacityLabel); dialog.Controls.Add(opacitySlider);
            opacitySlider.ValueChanged += delegate { opacityLabel.Text = "Window opacity: " + opacitySlider.Value + "%"; Opacity = opacitySlider.Value / 100.0; };
            dialog.FormClosed += delegate { if (dialog.DialogResult != DialogResult.OK) { windowOpacity = originalOpacity; Opacity = windowOpacity / 100.0; } };
            var sharedButton = new Button { Text = "Shared inventory…", Left = 15, Top = 310, Width = 180 };
            sharedButton.Click += delegate { ConfigureShared(); }; dialog.Controls.Add(sharedButton);
            var save = new Button { Text = "Save", Left = 210, Top = 370, Width = 80 };
            var cancel = new Button { Text = "Cancel", Left = 295, Top = 370, Width = 80, DialogResult = DialogResult.Cancel };
            dialog.Controls.Add(save); dialog.Controls.Add(cancel); dialog.AcceptButton = save; dialog.CancelButton = cancel;
            save.Click += delegate
            {
                if (String.IsNullOrWhiteSpace(name.Text)) { MessageBox.Show(dialog, "Enter your commander name as shown on Raven Colonial."); return; }
                if (key.Text.Contains("\r") || key.Text.Contains("\n")) { MessageBox.Show(dialog, "The API key must be a single line."); return; }
                try
                {
                    var saved = new Dictionary<string, string> { { "commander", name.Text.Trim() }, { "key", key.Text.Length == 0 ? "" : Protect(key.Text.Trim()) } };
                    Directory.CreateDirectory(Path.GetDirectoryName(settings));
                    File.WriteAllText(settings, new JavaScriptSerializer().Serialize(saved));
                    int previousOpacity = windowOpacity;
                    windowOpacity = opacitySlider.Value;
                    try { SaveTrackingSettings(selectedCargoMode, folder.Text.Trim()); } catch { windowOpacity = previousOpacity; throw; }
                    journalFolder = folder.Text.Trim();
                    commander = name.Text.Trim(); apiKey = key.Text.Trim(); dialog.DialogResult = DialogResult.OK;
                }
                catch { MessageBox.Show(dialog, "Settings could not be saved. Check access to your Local AppData folder."); }
            };
            ApplyDialogPalette(dialog);
            return dialog.ShowDialog(this) == DialogResult.OK;
        }
    }

    async Task<string> GetJson(string path)
    {
        using (var request = new HttpRequestMessage(HttpMethod.Get, ApiBase + path))
        {
            request.Headers.Add("rcc-cmdr0", Convert.ToBase64String(Encoding.UTF8.GetBytes(commander)));
            if (apiKey.Length > 0) request.Headers.Add("rcc-key", apiKey);
            using (var response = await client.SendAsync(request))
            {
                if (!response.IsSuccessStatusCode) throw new Exception("API returned HTTP " + (int)response.StatusCode + ". Check your account settings.");
                var body = await response.Content.ReadAsStringAsync();
                // Validate before the caller interprets the API-specific schema.
                ReadJson(body);
                return body;
            }
        }
    }

    public static object ReadJson(string json)
    {
        if (String.IsNullOrWhiteSpace(json)) throw new Exception("Raven Colonial returned an empty response. Try Refresh again.");
        var text = json.Trim().TrimStart('\uFEFF');
        if (text.StartsWith("<")) throw new Exception("Raven Colonial returned a web page instead of API data. Try Refresh again.");
        try { return new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.DeserializeObject(text); }
        catch (ArgumentException) { throw new Exception("Raven Colonial returned invalid JSON. Try Refresh again."); }
        catch (InvalidOperationException) { throw new Exception("Raven Colonial returned invalid JSON. Try Refresh again."); }
    }

    public static List<Dictionary<string, object>> ParseActive(string json)
    {
        var raw = ReadJson(json);
        var wrapper = raw as Dictionary<string, object>;
        if (wrapper != null)
        {
            foreach (var key in new[] { "projects", "active", "data", "result" }) if (wrapper.ContainsKey(key)) { raw = wrapper[key]; break; }
        }
        var array = raw as object[];
        if (array == null) throw new Exception("Unexpected active-project response from Raven Colonial.");
        var result = new List<Dictionary<string, object>>();
        foreach (var item in array)
        {
            var p = item as Dictionary<string, object>;
            if (p == null && item is string) p = new Dictionary<string, object> { { "buildId", item } };
            if (p == null || !p.ContainsKey("buildId")) throw new Exception("An active project has no build ID.");
            BuildId(Convert.ToString(p["buildId"])); result.Add(p);
        }
        return result;
    }

    public static string BuildId(string input)
    {
        input = input.Trim();
        Uri uri;
        if (Uri.TryCreate(input, UriKind.Absolute, out uri))
        {
            if (uri.Scheme != "https" || (uri.Host != "ravencolonial.com" && uri.Host != "www.ravencolonial.com")) throw new Exception("Use a Raven Colonial HTTPS project link.");
            var match = Regex.Match(uri.Fragment + "&" + uri.Query.TrimStart('?'), @"(?:[#?&]|^)build=([^&]+)", RegexOptions.IgnoreCase);
            if (!match.Success) throw new Exception("The link must contain #build= followed by the build ID.");
            input = Uri.UnescapeDataString(match.Groups[1].Value);
        }
        if (!Regex.IsMatch(input, @"^[A-Za-z0-9_-]{1,128}$")) throw new Exception("Enter a project build ID or a link containing #build=ID.");
        return input;
    }

    public static Dictionary<string, object> ParseProject(string json)
    {
        var obj = ReadJson(json) as Dictionary<string, object>;
        if (obj == null) throw new Exception("The API did not return a project object.");
        foreach (var key in new[] { "data", "project", "result", "value" })
            if (!obj.ContainsKey("commodities") && obj.ContainsKey(key) && obj[key] is Dictionary<string, object>) obj = (Dictionary<string, object>)obj[key];
        if (!obj.ContainsKey("commodities") || !(obj["commodities"] is Dictionary<string, object>)) throw new Exception("No commodity requirements returned for this project.");
        return obj;
    }

    public static long DeliveryRemaining(long api, long observed, long apiTicks, long observedTicks)
    {
        if (apiTicks > 0 && apiTicks >= observedTicks) return api;
        return observed;
    }

    void RecalculateDelivery()
    {
        displayedDelivery = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var data in deliveryProjects.Values)
        {
            string market = data.ContainsKey("marketId") ? Convert.ToString(data["marketId"]) : "";
            object nested;
            if (market.Length == 0 && data.TryGetValue("colonisationConstructionDepot", out nested) && nested is Dictionary<string, object>)
            {
                var depot = (Dictionary<string, object>)nested;
                if (depot.ContainsKey("MarketID")) market = Convert.ToString(depot["MarketID"]);
            }
            Dictionary<string, long> snapshot;
            depotBalances.TryGetValue(commander.Trim().ToLowerInvariant() + "|" + market, out snapshot);
            DateTimeOffset apiTime; long apiTicks = data.ContainsKey("timestamp") && DateTimeOffset.TryParse(Convert.ToString(data["timestamp"]), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out apiTime) ? apiTime.UtcTicks : 0;
            long observedTicks = 0; if (snapshot != null) snapshot.TryGetValue("__observedUtcTicks", out observedTicks);
            foreach (var pair in (Dictionary<string, object>)data["commodities"])
            {
                long api = Convert.ToInt64(pair.Value), observed;
                long amount = snapshot != null && snapshot.TryGetValue(JournalCargoTracker.Canonical(pair.Key), out observed) ? DeliveryRemaining(api, observed, apiTicks, observedTicks) : api;
                long previous = displayedDelivery.ContainsKey(pair.Key) ? Convert.ToInt64(displayedDelivery[pair.Key]) : 0;
                displayedDelivery[pair.Key] = previous < 0 || amount < 0 ? -1L : checked(previous + amount);
            }
        }
    }

    async Task LoadProject()
    {
        if (busy) return;
        if (String.IsNullOrWhiteSpace(commander)) { status.Text = "Open Settings to enter your commander name."; return; }
        var choice = selection.SelectedItem as ProjectChoice;
        string id = choice == null ? "" : choice.Id;
        busy = true; refresh.Enabled = false; configure.Enabled = false; selection.Enabled = false; editInventory.Enabled = false;
        if (shownId != id) { ClearCommodities(); shownId = null; }
        status.Text = "Fetching project…";
        try
        {
            var active = ParseActive(await GetJson("cmdr/" + Uri.EscapeDataString(commander) + "/active"));
            updatingSelection = true;
            try
            {
                selection.Items.Clear(); selection.Items.Add(new ProjectChoice { Id = "", Name = "All active projects (combined)" });
                foreach (var p in active)
                    selection.Items.Add(new ProjectChoice { Id = Convert.ToString(p["buildId"]), Name = p.ContainsKey("buildName") ? Convert.ToString(p["buildName"]) : Convert.ToString(p["buildId"]) });
                selection.SelectedIndex = 0;
                for (int i = 1; i < selection.Items.Count; i++) if (((ProjectChoice)selection.Items[i]).Id == id) selection.SelectedIndex = i;
                string selectedId = ((ProjectChoice)selection.SelectedItem).Id;
                if (selectedId != id) { id = selectedId; shownId = null; ClearCommodities(); }
            }
            finally { updatingSelection = false; }
            if (active.Count == 0) { ClearCommodities(); shownId = null; status.Text = "No active projects for " + commander + ". Updated " + DateTime.Now.ToString("HH:mm:ss"); return; }
            var cargo = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var unknownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int projectCount = 0;
            var fetchedProjects = new Dictionary<string, Dictionary<string, object>>();
            foreach (var p in active)
            {
                var build = Convert.ToString(p["buildId"]);
                if (id.Length > 0 && build != id) continue;
                var data = ParseProject(await GetJson("project/" + Uri.EscapeDataString(build)));
                if (!data.ContainsKey("marketId") && p.ContainsKey("marketId")) data["marketId"] = p["marketId"];
                fetchedProjects[build] = data;
                projectCount++;
                foreach (var pair in (Dictionary<string, object>)data["commodities"])
                {
                    long value;
                    if (!Int64.TryParse(Convert.ToString(pair.Value, CultureInfo.InvariantCulture), out value)) throw new Exception("Unexpected commodity quantity from the API.");
                    if (value < 0) { unknownKeys.Add(pair.Key); if (!cargo.ContainsKey(pair.Key)) cargo[pair.Key] = 0L; }
                    else cargo[pair.Key] = checked((cargo.ContainsKey(pair.Key) ? Convert.ToInt64(cargo[pair.Key]) : 0L) + value);
                }
            }
                deliveryProjects.Clear(); foreach (var pair in fetchedProjects) deliveryProjects[pair.Key] = pair.Value;
                string deliveryPath = InventoryPath() + ".deliveries.json";
                if (File.Exists(deliveryPath))
                {
                    var saved = new JavaScriptSerializer().Deserialize<Dictionary<string, Dictionary<string, long>>>(File.ReadAllText(deliveryPath));
                    foreach (var pair in saved) depotBalances[pair.Key] = pair.Value;
                }
                RecalculateDelivery();
                displayedCargo = cargo; displayedUnknown = unknownKeys; RenderCommodities(); shownId = id;
                Text = "ColonizationNeeds — " + commander;
                status.Text = projectCount + " project(s) · Updated " + DateTime.Now.ToString("HH:mm:ss");
        }
        catch (TaskCanceledException) { status.Text = "Request timed out. " + (shownId == id ? "Displayed data is stale." : "Try Refresh again."); }
        catch (Exception ex) { status.Text = ex.Message + (shownId == id ? " Displayed data is stale." : ""); }
        finally { busy = false; if (!IsDisposed) { refresh.Enabled = true; configure.Enabled = true; selection.Enabled = true; editInventory.Enabled = displayedCargo != null; } }
    }

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--self-test")
        {
            JournalCargoTracker.RunTests();
            if (DeliveryRemaining(100, 60, 10, 20) != 60 || DeliveryRemaining(60, 60, 30, 20) != 60 || DeliveryRemaining(80, 60, 30, 20) != 80 || DeliveryRemaining(100, 0, 10, 20) != 0) throw new Exception("Delivery refresh reconciliation failed");
            if (ApplyCargoMode(40, 15, "Collect") != 55 || ApplyCargoMode(40, 15, "Colonize") != 25 || ApplyCargoMode(10, 15, "Colonize") != 0 || ApplyCargoMode(40, 15, "Manual") != 40) throw new Exception("Cargo mode arithmetic failed");
            if (BuildId("https://ravencolonial.com/#build=abc-123") != "abc-123") throw new Exception("Link parsing failed");
            var parsed = ParseProject("{\"project\":{\"commodities\":{\"steel\":123,\"water\":0,\"copper\":-1}}}");
            if (((Dictionary<string, object>)parsed["commodities"]).Count != 3) throw new Exception("Schema parsing failed");
            bool rejected = false; try { BuildId("https://example.com/#build=x"); } catch { rejected = true; }
            if (!rejected) throw new Exception("Host validation failed");
            if (ParseActive("[{\"buildId\":\"a\",\"buildName\":\"Test\"}]").Count != 1 || ParseActive("[]").Count != 0) throw new Exception("Active project parsing failed");
            foreach (var bad in new[] { "<!DOCTYPE html><html></html>", "Invalid account", "", "{broken" })
            {
                bool handled = false;
                try { ParseActive(bad); } catch (Exception ex) { handled = !ex.Message.Contains("Invalid JSON primitive"); }
                if (!handled) throw new Exception("Non-JSON response handling failed");
            }
            if (ParseActive("\uFEFF[]").Count != 0) throw new Exception("BOM handling failed");
            if (Remaining(100, 25) != 75 || Remaining(100, 100) != 0 || Remaining(100, 150) != 0 || Remaining(0, 10) != 0) throw new Exception("Inventory subtraction failed");
            var stock = ParseInventory("{\"steel\":25,\"water\":0}");
            if (stock["STEEL"] != 25 || ParseInventory(new JavaScriptSerializer().Serialize(stock))["steel"] != 25) throw new Exception("Inventory persistence failed");
            bool negativeRejected = false; try { ParseInventory("{\"steel\":-1}"); } catch { negativeRejected = true; }
            if (!negativeRejected) throw new Exception("Negative inventory accepted");
            if (AdjustInventory(40, 15, "Add") != 55 || AdjustInventory(40, 15, "Remove") != 25 || AdjustInventory(40, 12, "Set total") != 12 || AdjustInventory(40, 40, "Remove") != 0) throw new Exception("Inventory adjustments failed");
            bool overRemoval = false; try { AdjustInventory(40, 41, "Remove"); } catch (ArgumentException) { overRemoval = true; }
            if (!overRemoval) throw new Exception("Over-removal accepted");
            bool overflowRejected = false; try { AdjustInventory(Int64.MaxValue, 1, "Add"); } catch (OverflowException) { overflowRejected = true; }
            if (!overflowRejected) throw new Exception("Overflow accepted");
            if (CommodityCategory("water") != "Chemicals" || CommodityCategory("foodcartridges") != "Foods" || CommodityCategory("ceramiccomposites") != "Industrial Materials" || CommodityCategory("steel") != "Metals" || CommodityCategory("$MilitaryGradeFabrics_name;") != "Textiles" || CommodityCategory("futurecommodity") != "Other") throw new Exception("Commodity categories failed");
            if (CommodityName("foodcartridges") != "Food Cartridges" || CommodityCategory("microbialfurnaces") != "Machinery") throw new Exception("Commodity labels/aliases failed");
            return;
        }
        if (args.Length > 0 && args[0] == "--test-key-protection")
        {
            if (Unprotect(Protect("test-secret")) != "test-secret") throw new Exception("Credential encryption failed");
            return;
        }
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new ColonizationNeeds());
    }
}



















