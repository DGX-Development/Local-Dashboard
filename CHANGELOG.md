# Changelog

## 3.0.0 - 2026-11-13

- Migrates the existing WPF application to self-contained .NET 10 on Windows x64.
- Preserves existing profiles and older configuration backups; new backups embed readable custom icons and restore their references.
- Improves website address normalization and icon selection, shared dropdowns, scrolling, custom color sliders and compact task navigation spacing.
- Updates portable taskbar and tray accent handling. Rapid selections may leave the taskbar icon one color behind while Windows refreshes it.
- Opens the public GitHub issue page after a brief confirmation and distinguishes a local version newer than the latest stable release.
- Refreshes setup, Help, installation and support documentation. Fresh release packages start empty for setup or backup restoration.

## 2.2.7 - 2026-11-13

- Refines the notification-area menu and taskbar icon appearance.


## 2.2.6 - 2026-11-12

- Setup follows the Windows light or dark theme and displays the Local Dashboard icon in the wizard header.
- Uninstall offers Keep settings or Remove settings, with Keep settings as the default.
- Check for updates from Help to find the latest stable release on GitHub.
- About uses a shorter layout, Appearance has a compact theme selector, and tray behavior is configured in Settings.

## 2.2.5 - 2026-11-12

- Manage up to three editable task lists and Checklists in a shared Tasks workspace. Search within the selected list and use the sidebar Edit action to manage lists and items.
- Use the guided setup to restore a backup or choose task sources. Optional copyable agent prompts are available for setup and task editing.
- Apply consistent dropdown, checkbox, confirmation and editor styles. Custom color scrolls its controls into view, and search hints clear when the field receives focus.
- Use Dashboard and Tasks from the notification-area menu. The app retains separate window and compact-view preferences.

## 2.2.4 - 2026-11-10

- Configuration backups now include readable custom icon images picked outside the Dashboard folder. Restore reconnects included images in a fresh profile.
- Use website icon follows the selected destination on links with multiple destinations, including in custom categories. Dashboard saves the icon selection and refreshes it when the website changes.
- Dashboard looks for missing automatic website icons when you open a category or reload the catalog.

## 2.2.3 - 2026-11-10

- Use consistent wording for restored copies. See which task files Dashboard will use before confirming.
- Include Dashboard-owned custom icons and available task lists in configuration backups. Restore can reconnect separate copies while preserving originals.
- Choose a checklist file in Settings > Tasks. Task views reload on entry and still offer manual Refresh. Personal tasks renumber after deletion.
- Give the Edit Dashboard category and link lists more room. Use a website favicon automatically or switch back to it from a custom icon.
- Direct cloud-provider sign-in is deferred.

## 2.2.2 - 2026-11-10

- Manage categories, links, icons and ordering in Edit Dashboard. Drag whole rows or use the keyboard. Save applies the draft and Cancel discards it.
- Choose built-in icons from a searchable picker or browse for a local image. Reassign links when removing a category.
- Open Tasks from the sidebar. Load newly chosen task files on Save and refresh them beside the task tabs.
- Expand the setup walkthrough, sample content and Help, including an optional AI setup guide.
- Keep separate full and compact window placements, with a reset in Settings. Use consistent selection markers and outline or filled pins.
- Improve configuration backup and restore, including a catalog-only import with a recovery copy.
- Improve Windows taskbar accent-icon refresh and shorten temporary task-cache paths.
- Direct sign-in for Google Drive, Dropbox and OneDrive remains planned.


## 2.2.1 - 2026-11-09

- Replaced personal-style sidebar defaults with Projects, Documents, Websites and All links.
- Included a generic example folder, document and website; retained saved catalogs and custom category labels.
- Removed automatic Desktop/Current Work.txt selection. New Agent tasks and Personal tasks selections start empty.
- Updated all built-in Help topics and installation instructions for the actual first-run behavior.

## 2.2.0 - 2026-11-09

- Added a per-user Windows installer, portable package and release checksums.
- Added the WPF interface with consistent 36-DIP buttons, native 24-DIP Fluent icons, selected-navigation markers and subtle menu shadows. Main-sidebar Move/Edit controls retain their smaller size.
- Standardized the Tasks area as Agent tasks, Personal tasks and Checklists, with fixed section navigation and readable task details.
- Added Personal task editing, safe handling of externally changed documents and a lossless full-text fallback.
- Added appearance previews, theme-aware publisher artwork, task locations, setup guidance and configuration backups with task-list restore choices.
- Fixed Edit dashboard links opening without a valid selection and corrected Compact view and setup icons.
- Added a shared per-user instance guard, clean release title/About presentation and structured issue-report fields.
- Kept direct provider sign-in marked Coming soon. Existing local files in synchronized folders remain usable.

## Earlier source checkpoint

The repository previously held the 1.4.3 Windows Forms source checkpoint. Intermediate local review builds were not GitHub releases.
