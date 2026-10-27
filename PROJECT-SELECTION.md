# Choose active projects - version 1.29

Use **Projects...** beside the project dropdown to control which construction projects contribute to your combined requirements. The button is available in both Compact and Full view.

## Choose your current work

1. Click **Projects...**. The app reads the current commander's Raven-active projects.
2. Uncheck projects you are not currently working on. Checking or unchecking a project turns off automatic all-project selection.
3. Click **Save**. The app switches to the combined view and refreshes its totals using only the checked projects. The dropdown and status show how many projects are included.

**Select all** checks the currently listed projects as a fixed selection. **Clear all** includes none; saving this shows an empty combined view, rather than silently including everything. **Cancel** leaves the saved choices unchanged.

To keep the old behavior, enable **Use all Raven-active projects automatically (including new projects)** and Save. This is the default until you customize the selection. With a fixed selection, newly discovered projects are excluded until you check them. With automatic selection, new Raven-active projects join the rollup automatically.

## What the selection affects

Combined **To deliver** adds only the included projects' remaining commodity requirements. **To collect** uses that combined need minus the displayed Stock, Inbound and Outbound, with a minimum of zero. Carrier accounting, existing stock and cargo movements do not change when you include or exclude a project.

The individual-project dropdown still lists all Raven-active projects, including excluded ones, so you can inspect any project's quantities separately. Saving the checklist returns to the combined view. Only projects currently returned as active by Raven contribute; completing or removing one from Raven's active list removes it from the rollup on refresh.

Choices use Raven build IDs, so renaming projects or having duplicate display names does not merge their choices. The dialog shows each project's ID. A remembered choice for a temporarily absent project is retained if the same build ID reappears. A genuinely new project with a different ID needs selecting again in fixed-selection mode.

## Local preferences and other commanders

Choices are saved in **active-projects.json** in the selected commander's profile folder. They persist across restarts and are separate for each commander and PC. Shared carrier users can therefore prioritize different projects while still seeing the same shared stock. A second PC needs its own project selection.

This feature filters the local view. It does not change Raven's plan status, commander membership or project completion; it does not create or cancel in-game constructions or alter carrier orders. Delivery/depot/completion reporting remains independent and continues for eligible new events even when a project is excluded from the combined view. No backend deployment or inventory migration is needed.

If a selected project is absent, ensure it is active for the configured Raven commander and refresh. A local checkbox cannot make a planned-only or unlinked project appear in Raven's active list. If saved choices are unreadable, the app reports the error instead of silently including everything; open Projects, choose a replacement selection and Save.
