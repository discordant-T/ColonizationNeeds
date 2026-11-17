using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// A local view preference. It never changes Raven project status or carrier balances.
public sealed class ProjectSelectionOptions
{
    public bool all = true;
    public List<string> included = new List<string>();
    public bool Includes(string id) { return all || included.Contains(id, StringComparer.OrdinalIgnoreCase); }
    public static ProjectSelectionOptions Load(string folder)
    {
        string path = Path.Combine(folder, "active-projects.json");
        if (!File.Exists(path)) return new ProjectSelectionOptions();
        var result = new JavaScriptSerializer().Deserialize<ProjectSelectionOptions>(File.ReadAllText(path));
        if (result == null || result.included == null || result.included.Any(String.IsNullOrWhiteSpace)) throw new IOException("Active project choices are unreadable. Open Projects to repair the selection.");
        return result;
    }
    public void Save(string folder)
    {
        CarrierInventory.AtomicWrite(Path.Combine(folder, "active-projects.json"), new JavaScriptSerializer().Serialize(this));
    }
}

public partial class ColonizationNeeds
{
    readonly Button activeProjectsButton = new Button { Text = "Projects...", Width = 82 };
    sealed class ActiveProjectItem
    {
        public string id, name;
        public override string ToString() { return name; }
    }
    readonly Dictionary<string, string> projectSystems = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    static string ProjectField(Dictionary<string, object> project, string field)
    {
        object value;
        return project.TryGetValue(field, out value) ? (Convert.ToString(value) ?? "").Trim() : "";
    }
    static string ProjectName(Dictionary<string, object> project)
    {
        string name = ProjectField(project, "buildName");
        return name.Length == 0 ? ProjectField(project, "buildId") : name;
    }
    string SystemName(Dictionary<string, object> project)
    {
        string name = ProjectField(project, "systemName"), known;
        if (name.Length > 0) return name;
        return projectSystems.TryGetValue(commander + "|" + ProjectField(project, "buildId"), out known) && known.Length > 0 ? known : "System unknown";
    }
    IEnumerable<IGrouping<string, Dictionary<string, object>>> GroupProjects(IEnumerable<Dictionary<string, object>> projects)
    {
        return projects.GroupBy(SystemName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key == "System unknown").ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
    }
    async Task<Dictionary<string, Dictionary<string, object>>> ResolveProjectSystems(IEnumerable<Dictionary<string, object>> projects)
    {
        var details = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in projects)
        {
            string id = ProjectField(project, "buildId"), key = commander + "|" + id;
            string name = ProjectField(project, "systemName");
            if (name.Length > 0) { projectSystems[key] = name; continue; }
            if (projectSystems.ContainsKey(key)) continue;
            try
            {
                var detail = ParseProject(await GetJson("project/" + Uri.EscapeDataString(id)));
                details[id] = detail;
                projectSystems[key] = ProjectField(detail, "systemName");
            }
            catch (HttpRequestException) { break; }
            catch (TaskCanceledException) { break; }
            catch (Exception) { /* Missing optional system metadata must not hide the project. */ break; }
        }
        return details;
    }
    int FindProjectIndex(string id)
    {
        for (int i = 1; i < selection.Items.Count; i++)
            if (String.Equals(((ProjectChoice)selection.Items[i]).Id, id, StringComparison.OrdinalIgnoreCase) && id != null) return i;
        return 0;
    }
    static string CombinedProjectName(ProjectSelectionOptions options, int selected, int available)
    {
        return options.all ? "All active projects (combined)" : "Active projects (combined: " + selected + " of " + available + ")";
    }
    async Task ChooseActiveProjects()
    {
        if (busy || editingInventory || switchingPlayer || constructionBusy || String.IsNullOrWhiteSpace(commander))
        { status.Text = "Wait for the current operation to finish and configure a commander before choosing projects."; return; }
        bool changed = false;
        busy = true; editingInventory = true;
        activeProjectsButton.Enabled = refresh.Enabled = configure.Enabled = selection.Enabled = editInventory.Enabled = false;
        try
        {
            var active = ParseActive(await GetJson("cmdr/" + Uri.EscapeDataString(commander) + "/active"));
            await ResolveProjectSystems(active);
            ProjectSelectionOptions previous;
            try { previous = ProjectSelectionOptions.Load(Path.GetDirectoryName(settings)); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message + "\nChoose and Save a replacement selection."); previous = new ProjectSelectionOptions(); }
            using (var dialog = new Form { Text = "Choose active projects", Size = new Size(570, 470), MinimumSize = new Size(460, 350), StartPosition = FormStartPosition.CenterParent, Font = Font, MinimizeBox = false })
            {
                var explanation = new Label { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8), Text = "Choose which Raven projects contribute to the combined view.\nThis preference is saved for this commander on this PC." };
                var automatic = new CheckBox { Dock = DockStyle.Top, Height = 32, Padding = new Padding(8, 0, 0, 0), Text = "Use all Raven-active projects automatically (including new projects)", Checked = previous.all };
                var checklist = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, HorizontalScrollbar = true, BorderStyle = BorderStyle.FixedSingle };
                foreach (var group in GroupProjects(active))
                {
                    checklist.Items.Add(new ActiveProjectItem { name = "— " + group.Key + " —" }, false);
                    foreach (var project in group.OrderBy(ProjectName, StringComparer.OrdinalIgnoreCase))
                    {
                        string id = ProjectField(project, "buildId");
                        checklist.Items.Add(new ActiveProjectItem { id = id, name = "    " + ProjectName(project) + "  [" + id + "]" }, previous.Includes(id));
                    }
                }
                var footer = new Panel { Dock = DockStyle.Bottom, Height = 72 };
                var count = new Label { Dock = DockStyle.Top, Height = 25, Padding = new Padding(8, 4, 0, 0) };
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.RightToLeft };
                var cancel = new Button { Text = "Cancel", Width = 80, DialogResult = DialogResult.Cancel };
                var save = new Button { Text = "Save", Width = 80 };
                var none = new Button { Text = "Clear all", Width = 80 };
                var all = new Button { Text = "Select all", Width = 85 };
                buttons.Controls.Add(cancel); buttons.Controls.Add(save); buttons.Controls.Add(none); buttons.Controls.Add(all);
                footer.Controls.Add(count); footer.Controls.Add(buttons);
                dialog.Controls.Add(checklist); dialog.Controls.Add(automatic); dialog.Controls.Add(explanation); dialog.Controls.Add(footer);
                bool updating = false;
                Action showCount = delegate { count.Text = (automatic.Checked ? active.Count : checklist.CheckedItems.Count) + " of " + active.Count + " Raven-active projects included"; };
                automatic.CheckedChanged += delegate
                {
                    if (updating) return;
                    updating = true;
                    try { if (automatic.Checked) for (int i = 0; i < checklist.Items.Count; i++) if (((ActiveProjectItem)checklist.Items[i]).id != null) checklist.SetItemChecked(i, true); }
                    finally { updating = false; }
                    showCount();
                };
                checklist.ItemCheck += delegate(object sender, ItemCheckEventArgs e)
                {
                    if (((ActiveProjectItem)checklist.Items[e.Index]).id == null) { e.NewValue = CheckState.Unchecked; return; }
                    if (updating) return;
                    automatic.Checked = false;
                    int selected = checklist.CheckedItems.Count + (e.NewValue == CheckState.Checked ? 1 : 0) - (e.CurrentValue == CheckState.Checked ? 1 : 0);
                    count.Text = selected + " of " + active.Count + " Raven-active projects included";
                };
                Action<bool> checkAll = delegate(bool value)
                {
                    automatic.Checked = false; updating = true;
                    try { for (int i = 0; i < checklist.Items.Count; i++) if (((ActiveProjectItem)checklist.Items[i]).id != null) checklist.SetItemChecked(i, value); }
                    finally { updating = false; }
                    showCount();
                };
                none.Click += delegate { checkAll(false); }; all.Click += delegate { checkAll(true); };
                save.Click += delegate
                {
                    try
                    {
                        // Keep remembered choices for temporarily absent projects, using stable build IDs.
                        var present = new HashSet<string>(active.Select(x => Convert.ToString(x["buildId"])), StringComparer.OrdinalIgnoreCase);
                        var included = previous.included.Where(x => !present.Contains(x)).Concat(checklist.CheckedItems.Cast<ActiveProjectItem>().Select(x => x.id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        new ProjectSelectionOptions { all = automatic.Checked, included = included }.Save(Path.GetDirectoryName(settings));
                        changed = true; dialog.DialogResult = DialogResult.OK;
                    }
                    catch (Exception ex) { MessageBox.Show(dialog, "Project choices could not be saved: " + ex.Message); }
                };
                dialog.AcceptButton = save; dialog.CancelButton = cancel; ApplyDialogPalette(dialog);
                checklist.BackColor = TableColor; checklist.ForeColor = TextColor; showCount(); dialog.ShowDialog(this);
            }
        }
        catch (Exception ex) { status.Text = "Project selection error: " + ex.Message; }
        finally
        {
            editingInventory = false; busy = false;
            if (!IsDisposed) { activeProjectsButton.Enabled = refresh.Enabled = configure.Enabled = selection.Enabled = true; editInventory.Enabled = carrierInventory != null; }
        }
        if (changed)
        {
            updatingSelection = true;
            try { if (selection.Items.Count > 0) selection.SelectedIndex = 0; }
            finally { updatingSelection = false; }
            await LoadProject();
        }
    }
}
