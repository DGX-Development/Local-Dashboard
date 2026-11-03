# Local Dashboard

A standalone Windows dashboard for opening configured local projects, documents and websites, with a compact Current Work checklist and deliberate sidebar editing.

This private repository contains reviewed source and original Dashboard SVG artwork. Personal navigation, checklist items, pins, preferences, generated browser snapshots, executables, installers, local review evidence and machine-specific notes are excluded. The repository is a source checkpoint; no GitHub release or installed application is provided by this setup.

## Build and configure

Requirements: Windows, PowerShell, and the Windows .NET Framework C# compiler. The optional browser fallback builder requires Node.js. No server is required.

1. Copy `catalog.example.json` to `catalog.json` in the project folder, or keep an existing personal catalog. Edit your local copy to configure destinations. The example contains only a generic website.
2. Run `& '.\Build Dashboard.ps1'` to build `LocalDashboard.exe` and its application icon. Keep the executable in this project folder with the catalog and source files.
3. Launch `LocalDashboard.exe`. This manual build does not install the app or enable Windows startup.

For the optional browser fallback, run `node build-local.cjs` after configuring the catalog, then open `index.html`. This generates the ignored `dist/catalog-data.js`; it can contain local paths and embedded images and must stay local.

## Use

- Search the configured directory and open exact folder, document or website targets.
- Use the small Edit control below the sidebar to change section labels and reorder sections or project rows. Save applies the draft; Cancel discards it. Folder names and configured destinations are preserved.
- Current Work stores checklist state locally. Checking an item moves it to Done; restoring it returns it to Current.
- Open File > Settings in the full dashboard or Settings in compact Current Work. For new profiles, close and minimize send the app to the tray. Windows startup is opt-in.

Local configuration and state live in `catalog.json` and `.local/`. Back them up separately; do not commit them. Existing machine-specific maintenance guides and review records remain local under `.dev/`.

## Verification and limits

Version 1.4.3 adds Settings to compact Current Work. Isolated window tests covered Settings in both views, close and minimize to tray, restore, start minimized, and explicit Exit. Earlier 1.4.1 review checks covered isolated insertion feedback, deferred drop commits, edge-scroll logic, mouse reordering and Cancel rollback. Continuous drag feedback, scrolling during a sustained live drag, physical keyboard acceptance, comprehensive DPI/high-contrast behavior and actual Windows sign-in remain unverified.

No universal live task monitoring is configured. Participating reports display supplied status; the checklist is selected local work. No open-source license is granted by this private source checkpoint. Preserve existing rights and attribution when reusing assets.
