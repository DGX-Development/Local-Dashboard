# Local Dashboard user guide

Version 3.0.3.

## Getting started

Local Dashboard keeps your folders, documents, websites and task lists together. A new profile starts empty.

### Add your links
1. Select a Dashboard category.
2. Choose Edit below the sidebar.
3. Add your links and choose Save.

### Set up tasks
1. Open Settings > Tasks.
2. Choose or create a TXT or Markdown source for each list you want to use.
3. Choose Save, then open Tasks and select your list.
4. Choose Edit to add or change items.

Checklists is ready to use without a text-file source. Checkmarks save automatically. Current and Done show unfinished and completed items.

### Find your controls
- Settings is in the top menu beside View and Help.
- Compact view hides the top menu. Expand returns to the full window.
- Refresh rereads the current task file.
- Help > Run setup again opens the optional walkthrough.

Hide from Tasks hides a list without deleting its source. Show in Tasks brings it back. Closing an edited Dashboard asks whether to save, discard or keep editing.

## Projects, folders and websites

A fresh profile starts with empty Projects, Documents, Websites and All links categories, ready for your backup or your own links. No personal folders or task lists are included.

The supplied Examples folder contains a generic project folder, document and task file that you can choose when learning the app.

Use Edit below the sidebar to add your own folder, document or website. Folder opens a folder in Explorer.

Open opens a document in its Windows app. Website uses Firefox if installed, otherwise your default browser.

Changing a destination does not move or copy your files.

Existing profiles retain their saved catalog, categories and labels. An existing category may have its own Open folder action.

New profiles do not assume a workspace location. Unavailable destinations are disabled until their saved location is corrected.

## Reorder categories and links

Open Edit below the sidebar. Drag a category by its handle on the left, or drag a link in the selected category on the right.

You can also focus a handle and press Ctrl+Up or Ctrl+Down. Save applies the new order.

Cancel discards the draft.

Use Edit to select a dashboard entry and change its display name, destination, or icon. Save applies the changes together.

Cancel leaves the catalog as it was. Display names are independent of folder names.

If the saved catalog changes while you edit, cancel and reopen Edit before saving again.

## Managing your dashboard

Use Edit below the sidebar to open Edit Dashboard. Add or rename a category, choose an icon, or edit its links in the same window.

Remove a category with links by choosing another category for them. All links is the combined view.

Show or hide it in Settings > Appearance. Save applies the draft together and Cancel discards it.

Choose a category, then Add link. Enter its name, choose Folder, Document or Website under Opens, and enter an address or browse to the location.

For websites, you can enter a domain such as example.com; Dashboard adds https://. An explicit http:// or https:// address is preserved.

When you save a website link without a custom icon, Dashboard tries the site and may send its domain to a public icon index if needed. Choose Change link icon to set your own.

For a link with several destinations, select a website destination before choosing Use website icon. Save keeps your choice.

Dashboard looks for missing automatic website icons when you open a category or refresh the directory. The picker has Common and All icons.

Search covers the full collection. Choose Browse local image to use your own image, or Use default icon to reset.

Save applies all changes; Cancel discards the draft. Remove link only removes the dashboard entry, never its linked file or folder.

A display name does not rename a file or folder.

Links are saved in catalog.json beside the app. The editor manages entries without manual JSON editing.

All links is a browsing view. Open Edit and select a category before editing its links.

For a link with several destinations, select the one you want to edit or remove. To remove its last destination, remove the link.

Back up configuration before major changes.

The starter catalog uses @app/ for example destinations relative to the application folder. The editor shows their actual locations.

Keep the supplied Examples folder with a portable copy. Your own destinations use their full paths.

Task documents and Checklists have separate locations in Settings > Tasks.

## Search and pinned links

Use the Search box to find a saved project, document or website across categories. Search still covers all links when the All links sidebar button is hidden.

Clear the search to show every entry or select a category to browse it.

Select the star to add a link to your favorites. Favorites appear first in sections that use the default order.

A custom order takes precedence. Click the star again to remove a favorite.

Use View > Refresh directory to reload saved changes to your dashboard links.

## Tasks and done items

Each task list displays its own selected text document and lets you edit that file. Each item has one title and a brief description.

Details keeps its supplied Status, Next and longer notes collapsed initially. Repeated document headings are kept in Source and full document rather than stacked above the tasks.

Deferred work is separate under Deferred work · Do not start. Viewing a task does not authorize or start it.

Use Edit below the lists to add, change or remove checklist items. Use the joined Current and Done filter below list navigation to switch between unfinished and completed checklist items.

Check an item to move it to Done, or uncheck it in Done to restore it. Checkoffs save automatically in the selected checklist JSON.

Existing entries and custom labels are preserved.

## Editing task lists

Choose Edit below the lists in the Tasks sidebar, or beside the tabs in Compact view, to organize task lists and Checklists in one window. Drag a list by its handle to change the sidebar order.

Select a list to rename it, choose a source, or hide it from the Tasks sidebar. Hide from Tasks keeps the source file.

Choose Show in Tasks to bring the tab back. Add, edit or remove items in the right pane.

Done stages the item, Save applies the pending changes, and Cancel discards changes you have not saved. If a source file changes while you edit, Dashboard shows the conflict.

Select a task in the editor and use Edit item to change its title, brief description and longer notes. Supplied Status/Next fields and other tasks are kept.

An unusual task opens as complete task text so unfamiliar information remains editable.

Add item puts a new numbered task after the last task in the file, keeping existing numbers. Use Edit document to put it under a different heading.

Edit document opens the full selected file, including plain text with no task fields. It can also be used to rearrange text.

Drag an item by its handle to reorder it. Use Edit document to move a task across headings.

Select an item and confirm Remove item to stage its removal.

Save writes to the same file shown in Source and full document. Cancel leaves it unchanged.

A single Updated line before the first task is refreshed when content changes. Existing encoding and line endings are kept by task editing.

Whole-document editing saves the text you enter.

If the file or its selected location changes while you edit, Save refuses to overwrite it and keeps your draft open. Copy anything you need, cancel, Refresh and reopen the latest document.

A locked or read-only file reports an error without replacing it.

Checkoffs in the main Checklists view save immediately.

## Get help from an agent

In setup, choose Set up with an AI assistant, then Copy instructions for AI chat. Open your AI chat, paste the instructions and send them.

The assistant will ask what you want and show you a plan. With desktop access, it can apply the plan after you approve it.

Otherwise, it can give you the exact steps and task-file text. Open Help > Run setup again to get the instructions later.

Start Here.txt beside LocalDashboard.exe contains the same instructions and can be attached to an AI chat before the app opens.

To get help editing a selected task file, turn on Show agent edit prompts in Settings > Tasks. In a task list or Checklists, choose Agent prompt, review it, and copy it into your AI chat.

A task-list prompt includes the selected file path. Share it only if you want the agent to see that path.

After the file is edited, choose Refresh to read the latest copy.

## Appearance and taskbar color

Open Settings > Appearance to choose System, Light or Dark and an accent color. Changes preview immediately.

Save keeps them; Cancel restores your saved appearance.

Windows can occasionally leave the taskbar icon on the previous color, even when the app and tray icon have changed. Rapid color changes make this more likely.

Pause briefly after a selection. If the taskbar icon does not change to your selected color, click Reset in Settings > Appearance, wait a second, then select your color again.

This should refresh the icon. Choose Save to keep the color.

Windows taskbar color updates can still occasionally lag. You do not need to reset your profile or restore a backup.

## Window and startup

Close to the system tray keeps Dashboard running when you click X. With that option off, X quits.

Minimize to the system tray hides Dashboard near the Windows clock. With that option off, Minimize keeps it on the taskbar.

Double-click the tray icon to return to your current view. Right-click it and choose Open Dashboard, Open Tasks, or Exit.

You can also quit through File > Exit.

Start minimized applies when you open Dashboard and when Windows starts it. Dashboard and Compact view remember separate sizes and positions.

Lock window sizes prevents resizing and maximizing while keeping moving and minimizing available. Settings stays resizable so you can turn the lock off.

Compact and full views retain their separate sizes. Resizable working dialogs also remember their dimensions.

To unlock windows and restore defaults, choose Reset window sizes and then Save. Cancel leaves the lock and sizes unchanged.

## Formatting task documents

Recommended format, not a requirement

1. Short task title
   A brief description in ordinary language.
   Status: Optional supplied status.
   Next: Optional supplied next step.

   Notes:
   Longer explanations, references and background go here.

The task title and brief description stay visible. Dashboard displays consecutive numbers in the task view and keeps the original identifier in Details and Source and full document.

Details contains longer notes and supplied Status/Next fields and starts collapsed. A repeated single-project heading can supply the displayed title; Details retains the original task title.

Several jobs keep distinct titles and project context. Do not add Status or Next merely to satisfy the app.

Task: and Description: are also recognized when supplied.

Group projects with #### Project name. Put deferred work under ### Later / on hold: do not start now.

A project may have several jobs numbered 1.1, 1.2 and so on, each with its own supplied status and next step. Preserve useful source numbers and references.

Plain text with no recognized task headings is shown as written. Mixed documents preserve unrecognized paragraphs and labels too.

Multiple Status/Next fields in one older item stay together in Details rather than assigning the last status to the whole project. Source and full document keeps every original heading, instruction and paragraph available.

You can give this format guide to anyone who edits the same file outside Dashboard. Keep task identifiers and any useful references when editing the source.

Use Updated at the top of the document if you want to record when the list last changed.

Reading and Refresh do not rewrite formatting. An explicit Save in the task editor changes the selected file.

HTML rendering and automatic task execution are not supported.

## Task files and refresh

In Settings > Tasks, choose Choose source… to create a task file or select an existing TXT or Markdown file. You can also open Tasks settings from Set up this list, including in Compact view.

Save connects the file and adds its tab if it is Task list 2 or Task list 3. Rename changes the tab label.

Disconnect and Save clear the selection and hide an extra tab. Dashboard keeps the file.

Checklists autosaves to its default file. To use an existing Dashboard checklist JSON, choose Choose checklist file and Save.

Settings shows the active path. Use default checklist and Save switches back to the default file and preserves both checklists.

Opening a task tab rereads its selected file. Refresh beside Search rereads the current tab.

A failed read retains the last good snapshot and reports that it may be out of date.

Local TXT and Markdown files can be up to 1 MiB. Use readable UTF-8, or a text file with a supported encoding marker.

A file in a sync folder must be available locally; its sync application handles sign-in and synchronization. Network shares use existing Windows permissions.

## Always on top and the system tray

Choose the pin at the top of Dashboard or View > Always on top to keep the window above other windows. A filled pin shows when the option is on.

Choose it again to turn it off.

When Dashboard is in the system tray near the Windows clock, double-click its icon or right-click it and choose Open Dashboard or Open Tasks. Opening the same launcher again also brings its window back.

In Settings > Window, Close to the system tray and Minimize to the system tray are both on by default. Turning off Close to the system tray makes X quit.

Turning off Minimize to the system tray keeps Dashboard on the taskbar when minimized. Start when I sign in to Windows and Start minimized are both off on a fresh profile.

Start minimized applies to manual launches and Windows sign-in. File > Exit and the tray menu Exit quit Dashboard.

## Accent color

Settings opens on Appearance. It offers System, Light and Dark with immediate preview.

Named swatches are coordinated with the chosen theme. Custom color offers Hue, Saturation and Brightness sliders, and the hex field accepts an exact six-digit color.

Reset restores the default for the displayed theme. Each theme remembers its selected accent when saved.

The sample card, main window, command icons, window icon, system tray and taskbar icon preview the selected color while Settings is open. Icon color uses your exact selection; text and focus use a readable shade.

Save keeps your changes. Cancel or closing Settings restores the last saved appearance.

Windows may leave the taskbar icon one color behind if you change colors rapidly. Pause between selections to let the taskbar refresh.

## Saved data and update conflicts

Each installed or portable copy has a data folder beside its executable. It holds catalog.json and the .local settings folder.

Upgrades preserve your catalog, settings and checklist. During interactive uninstall, choose Keep settings to reuse your setup later or Remove settings to clear settings, links and favorites. Silent uninstall keeps settings.

Task files, checklist data and custom icons stay in place. This installation's Windows startup entry is removed whether you keep or remove settings. Each copy uses its own data folder.

Task lists read the files you selected. Cached snapshots are separate from their originals.

A task list saves only when you explicitly choose Save. Dashboard saves Checklists automatically.

Current and Done are views of the same checklist. Use Edit, select an item, choose Remove item and then Save to remove it from the checklist.

If another update saved first, an outdated edit is rejected. Refresh and review the latest version before trying again.

A damaged or unsupported checklist is not replaced with an empty one. Use Back up configuration before moving or manually editing data.

## Synced task files

To use a file already synced to this PC, choose Choose source, then Choose existing file for its local TXT or Markdown copy in Settings > Tasks and make it available offline. Your sync application handles sign-in and synchronization.

Refresh reads the local copy. Saving a task list edits that copy, and your sync application handles uploading it.

Dashboard cannot confirm that it matches the online version. Checklists uses Dashboard's checklist format.

HTML documents are not supported as task-list sources.

## Configuration backups

File > Back up configuration saves dashboard links, settings, favorites, layout and custom icons, plus available copies of your task lists and Checklists. The result reports unavailable copies.

Linked files and folders need their own backups.

Choose Restore from a backup on the first setup screen, or File > Restore configuration from the Dashboard. Before restoring, Dashboard saves a backup of your current configuration.

It reconnects included custom icons automatically. It saves restored task lists as new files.

Review the choice for each list. Dashboard defaults to keeping a working list or using the restored copy if no current file is set up or Dashboard can't open it.

A missing backup copy leaves the current selection intact. Windows sign-in startup keeps its setting on this PC.

Selecting a standalone catalog.json in Restore configuration offers a separate Import links confirmation. It shows the link count and replaces only dashboard links, preserving settings and task-list selections.

A copy of the previous catalog is retained in the data folder. Relative example paths keep their source location; linked files are not copied.

All links shows every imported entry.

Dashboard reads older backups. When included, the first two text-list copies restore to Task list 1 and Task list 2.

A backup without a third list keeps the current Task list 3 selection. If a backup contains only the path to an icon, Dashboard uses the original image when it is available.

You can choose a replacement in Edit Dashboard if the file is missing.

## Optional setup

On the welcome screen, choose Restore from a backup if you have a saved configuration. A successful restore finishes setup.

Choose Next for Appearance and window behavior in step two, then add a first link and choose task files in the following optional steps. The final page summarizes completion.

Finish saves your appearance, window behavior, link and task-file choices; Skip setup leaves those draft choices unchanged. Use Edit below the sidebar to add more links.

Help > Run setup again reopens the walkthrough.

In Settings > Tasks, use Rename beside a task section to change its button label. Dashboard saves your checklist automatically.

## Sample content

Samples are optional. New profiles start empty.

### Load examples
1. Open Help > Run setup again, or use the welcome page on first launch.
2. Choose Load sample content. This applies immediately.
3. Explore the entries labeled Sample, then edit them or add your own.

Loading again does not duplicate existing sample IDs. Loading does not replace your content or change reminder, monitoring or startup settings. Restoring a backup does not automatically load samples.

### Remove examples
Choose Remove samples on the same page. It removes only unchanged sample entries. Edited or completed examples are kept; you can remove those individually using the editor. Linked files are not deleted.

## Check for updates

Choose Help > Check for updates to compare your version with the latest stable GitHub release. If a newer release is available, choose View release to open its page in your browser.

If your installed version is newer than the latest stable release, the result shows both versions and says no update is needed. If the check fails, try again when GitHub is reachable.

Exit Dashboard before running a new installer or replacing your portable executable.

## Report an issue

Help > Report an issue explains that you will continue on GitHub. Choose Open GitHub to open the public Local Dashboard new-issue page in your browser, or Cancel to stay here.

A GitHub account is required to submit an issue. Local Dashboard stays open.

No report or personal data is sent automatically.

## One running Dashboard

Local Dashboard 2.2.0 and newer share one running instance per Windows user, across builds and profiles. Opening it again brings the existing window forward.

Exit the running version before opening a different build. This version also refuses to start while it detects an older Dashboard in your Windows session.

Older builds cannot honor the new guard if launched afterward; use the current shortcut and retire obsolete launchers. Your lists and profile are never switched or forcibly closed.

## Troubleshooting

If a folder or document does not open, check that it still exists. Correct a moved destination in Edit Dashboard, then choose Save.

If Checklist unavailable appears, checkoffs are disabled until the saved file can be read safely. Existing data is left unchanged.

Ask for help repairing the file rather than deleting or overwriting it.

If Dashboard opens behind another window, choose its taskbar button. If it is hidden in the tray, double-click its notification-area icon or reopen its launcher.

F1 opens this guide. Ctrl+R refreshes the directory, Ctrl+Shift+C opens Tasks, and Ctrl+Q exits.

