# Changelog

All notable changes to this project are documented here, based on commit history.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

Contributors:
- **John Hickey** (peterparker57) — original author
- **Mark Sarson** (msarson) — fork contributor
- **Oleg Fomin** — PR #3 contributor

---

## 2026-02-28

### Changed
- Replaced hardcoded `C:\Clarion12\bin` path in `.csproj` with a `Directory.Build.props`
  `$(ClarionBin)` variable. Developers no longer need to edit the project file — override
  via a gitignored `Directory.Build.props.user` file or the `CLARION_BIN` environment
  variable. Default remains `C:\Clarion12\bin`.

---

## 2026-01-28

### Fixed
- WebView2 initialization error when first adding the addin — removed
  `GetAvailableBrowserVersionString()` pre-check that was incorrectly showing
  "WebView2 not installed" on first add. Initialization now deferred to
  `HandleCreated` event via `BeginInvoke` for correct timing.

---

## 2026-01-27

### Fixed
- Scroll sync broken by `aboutModal` null reference error — `addEventListener`
  was running before the DOM element existed, halting all subsequent JavaScript
  including scroll sync. Added null checks and switched to `onscroll` property
  with `setTimeout` delay for WebView2 compatibility.
- Tools menu path corrected from `/Workspace/Tools` to
  `/SharpDevelop/Workbench/MainMenu/Tools` so items appear in the main menu.

### Changed
- Menu items moved to View/Tools menu; pad title renamed to "Markdown Editor (Pad)".
- About dialog updated to credit Oleg Fomin; fixed name spelling (Dinko Bakun).

### Docs
- README updated to document window mode, project structure, and new author credits.

---

## 2026-01-26 — PR #3 (Oleg Fomin)

### Added
- **Document (window) view mode** — new menu option "Markdown Editor (Window)" opens
  the editor as a document tab in the main IDE area (via `AbstractViewContent`),
  alongside the existing dockable pad mode.
  - New files: `MarkdownEditorViewContent.cs`, `ShowMarkdownEditorWindowCommand.cs`

---

## 2026-01-24 (post-PR #1)

### Added
- **About dialog** — credits John Hickey, Mark Sarson, Oleg Fomin, Dinko Bakun;
  includes link to clarionlive.com.
- **WebView2 menu close fix** — added `WebView2MenuCloseFilter` (`IMessageFilter`)
  to intercept mouse clicks at Windows message level so dropdowns close correctly
  when clicking into WebView2 panes.

---

## 2026-01-24 — PR #1 (Mark Sarson, merged)

### Added
- **Mermaid diagram support** with WebView2 virtual host mapping
  (`app.local` → Resources folder), enabling CDN-loaded Mermaid.js.
  Supports flowcharts, sequence diagrams, ERDs, Gantt charts, state diagrams.
- **WebView2 Runtime detection** on startup — friendly dialog with one-click
  download link if runtime is missing; graceful fallback page in the pane.
- **Print styles** — hides editor/toolbar when printing; only the preview prints.
- **Disabled WebView2 context menus** for a cleaner UI.

### Fixed
- **Critical deadlock** in "Insert to IDE" — `task.Wait()` on the UI thread was
  deadlocking with WebView2's async `ExecuteScriptAsync`. Fixed by making
  `InsertToIdeEditor()` fully async with `await`.
- **WebView2Loader.dll not deployed** — added MSBuild target to auto-copy native
  DLL from NuGet packages folder to output directory.
- **Nested code blocks** — markdown parser now matches opening fence length (3+
  backticks) and only closes on a matching fence, enabling docs about code blocks.

### Docs
- Updated README with WebView2 requirements, build/deploy instructions, and
  Clarion syntax highlighting example.

---

## 2026-01-24 (initial development — Mark Sarson)

### Added
- **WebView2 migration** — replaced IE-based `WebBrowser` control with modern
  Chromium-based WebView2; integrated `highlight.js` (11.9.0) with a custom
  Clarion language definition for offline syntax highlighting of 190+ languages.
- **Dark mode** toggle button in preview header.
- **Scroll synchronisation** between editor and preview with toggle checkbox.
- Horizontal scrolling support for long lines in the editor.
- Auto-hide format toolbar in fullscreen preview mode.
- Updated to .NET Framework 4.8 / C# 7.3; updated HintPath for Clarion 11.1.

---

## 2026-01-23 — Initial commit (John Hickey)

### Added
- Dockable pad integrating into the Clarion IDE workspace.
- Split-pane interface with live markdown preview.
- Expand/collapse preview mode.
- Formatting toolbar (Bold, Italic, Code, Headers, Lists, Tables, etc.).
- File operations: New, Open, Save, Save As.
- IDE integration: Insert to active editor.
- Keyboard shortcuts: Ctrl+Alt+M, Ctrl+B, Ctrl+I.
- Settings persistence.
- Built targeting .NET Framework 4.0 for Clarion 11/12 compatibility.

