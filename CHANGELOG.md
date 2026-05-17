# Changelog

All notable changes to this project are documented here, based on commit history.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

Contributors:
- **John Hickey** (peterparker57) — original author
- **Mark Sarson** (msarson) — fork contributor
- **Oleg Fomin** — PR #3 contributor

--- 

## v1.1.2 — 2026-05-17 — Strip trailing `#` on ATX headings

### Fixed
- **Trailing `#` characters on ATX headings were not removed in the preview**
  ([#3](https://github.com/msarson/ClarionMarkdownEditor/issues/3), reported by
  Pierre-Jean Quinto). The inline markdown parser stripped the leading `#`s
  but left the optional closing `#` sequence in the rendered text, so
  `# Introduction #` rendered as `Introduction #` instead of `Introduction`.
  Per [CommonMark §4.2](https://spec.commonmark.org/0.31.2/#atx-headings), the
  closing `#` sequence — when preceded by whitespace — is decorative and must
  be stripped. Added a second `.replace(/\s+#+\s*$/, '')` to the heading text
  extraction in `markdown-editor.js`.

---

## v1.1.1 — 2026-05-15 — Fix format toolbar visible after Start Page → URL tab

### Fixed
- **Format toolbar stayed visible when opening a URL into a read-only tab from the
  Start Page** ([#5](https://github.com/msarson/ClarionMarkdownEditor/issues/5)).
  `hideStartPage` set the toolbar's `display` as an inline style, which overrode
  the `.format-toolbar.hidden` class added by the v1.1.0 read-only visibility
  logic. Made `applyFormatToolbarVisibility` the single source of truth — it now
  also considers `isStartPageActive` and clears the inline style before toggling
  the class, so the class-based rule actually wins. Most visible when clicking
  **View README** in Addin Finder while the editor's Start Page was up.

---

## v1.1.0 — 2026-05-15 — Open Markdown from URL

### Added
- **Open Markdown from URL** ([#4](https://github.com/msarson/ClarionMarkdownEditor/issues/4)).
  Load a Markdown document directly from a URL via **Tools → Open Markdown from URL...**
  or the new **🌐 Open URL...** button on the Start Page. Supported URL forms:
  - Raw URLs (`raw.githubusercontent.com/...`)
  - GitHub blob URLs (`github.com/owner/repo/blob/branch/...`) — rewritten to raw
  - Bare GitHub repo URLs (`github.com/owner/repo`) — probe `README.md` on `main`, fall back to `master`
- **Read-only tab mode** for URL-loaded documents — locked textarea, 🔒 badge, italic tab
  title, format toolbar hidden while the tab is active. Opens in expanded (rendered-only)
  view by default since URL tabs are for reading. **Ctrl+S** routes through Save As to
  let the user save a local editable copy — the tab then drops the lock and the
  expanded mode automatically.
- **Per-tab expanded/split preference** — Expand/Split is now remembered per tab,
  so switching between a URL tab (expanded) and a file tab (split) no longer leaks
  state across tabs.
- **Relative URL resolution** — `![](images/foo.png)` and similar relative references
  inside a fetched README resolve against the source URL. Relative `.md` link clicks
  open in a new in-editor tab; absolute `http(s)` links open in the system default
  browser via `Process.Start` (so the WebView2 never navigates away from the editor).
- **Recent URLs list** on the Start Page, persisted in `SettingsService` alongside
  recent files (capped at 15 entries).
- **Public API for cross-addin invocation**: `MarkdownEditorApi.OpenUrl(string url)`.
  Other Clarion IDE addins can call this via reflection without taking a hard
  reference on `ClarionMarkdownEditor.dll` — useful for things like an "View README"
  action in addin-finder that delegates to the editor when present.
- **Disk cache** for fetched URLs under `%APPDATA%\ClarionMarkdownEditor\cache\` —
  conditional GETs (ETag / If-Modified-Since) so reopens hit 304, and a stale copy
  is served when the network is unavailable (with a status bar hint).
- **Friendly fetch errors** — distinct messages for 404 / 401-403 / 5xx / timeout /
  generic transport failure, each with the originally requested URL beneath.

### Changed
- `System.Net.Http` added to project references (not in the SDK default for the
  WindowsDesktop SDK).
- `addTab` / `updateTabInfo` JS signatures gained `isReadOnly` and `baseUrl`
  parameters. Defaults preserve old-style calls.

---

## v1.0.2 — 2026-05-14 — Save corrupts content containing HTML or backslashes

### Fixed
- **Saving silently corrupted any file containing `<`, backslashes, tabs, or non-ASCII characters**
  ([#1](https://github.com/msarson/ClarionMarkdownEditor/issues/1)). `WebView2.ExecuteScriptAsync`
  returns the script's result as a JSON-encoded string, in which Chromium escapes `<`
  to its Unicode escape sequence (HTML-safe escaping), single backslashes to `\\`,
  and any non-ASCII character to its `\uXXXX` form. Both save paths
  (`GetEditorContentAsync` and `GetTabContentFromJs`) hand-rolled a decoder that only
  handled `\n`, `\r`, and `\"` — every other escape passed through as the literal
  characters of the escape and was written to disk. So embedded HTML like `<style>`
  became a six-character Unicode escape followed by `style>`, single backslashes
  in paths became doubled, and any literal backslash-`n` inside content was converted
  to a real newline. Replaced both decoders with a proper JSON string decoder
  (`DecodeJsonString`) that handles the full JSON escape set including `\uXXXX`.
- **Repaired README content** previously damaged by this bug — restored single
  backslashes in Windows paths, removed Unicode-escape leftovers from XML examples,
  and rejoined a Windows path that had been split across two lines.

---

## 2026-02-28 — Start Page & Cross-Instance Fixes

### Fixed
- **Duplicate tab when clicking recent file from Start Page**: Clicking a file from the
  Start Page recent files list was opening a second tab instead of switching to the
  already-open tab. Root cause: `postMessage` serialises JS objects to JSON, escaping
  backslashes (`C:\path` → `C:\\path`). `ExtractNestedJsonValue` was returning the raw
  escaped string, so the path never matched the single-backslash path stored in
  `_openTabs`. Added `UnescapeJsonString` helper used by both `ExtractJsonValue` and
  `ExtractNestedJsonValue`.
- **Cross-instance duplicate dialog not shown from Start Page**: The same JSON unescape
  bug caused the `_allInstances` guard in `OpenFile` to also miss — the escaped path
  never matched `HasFileOpen`, so the "file already open in another instance" dialog was
  silently skipped for all Start Page recent file clicks (both pad and document modes).
  Fixed by the same `UnescapeJsonString` change.
- **View → Markdown Editor Window creates duplicate window**: Choosing the menu item when
  a Markdown Editor document tab was already open created an additional empty instance.
  Now checks `ViewContentCollection` first and calls `SelectWindow()` on the existing
  instance if found.

### Added
- **ODS logging via `OutputDebugString`**: A `Log()` helper using P/Invoke
  `OutputDebugString` and a `debugLog` JS→C# message type allow real-time tracing in
  DebugView++ (filter `[MarkdownEditor]`), active in both Debug and Release builds.

---

## 2026-02-28 — Single Editor Instance & Dark Mode Sync

### Added
- **Single shared editor instance**: Opening multiple `.md` files from the IDE now
  routes them all into one Markdown Editor instance as internal tabs, rather than
  spawning a separate WebView2 process per file. `MarkdownDisplayBinding` finds the
  existing `MarkdownEditorViewContent` in the workbench and calls `Load()` on it;
  `OpenFile` handles deduplication (switches to already-open tab) and recent file history.

### Fixed
- **View menu dark mode checkmark out of sync**: Toggling dark mode from the Start Page
  button updated the JS UI but never notified C#, so `_isDarkMode` stayed stale and the
  **View > Dark Mode** tick remained wrong. `toggleDarkMode()` now posts a
  `darkModeChanged` message; C# handler updates `_isDarkMode` and persists it to settings.

---

## 2026-02-28 — Editor Polish & Bug Fixes

### Added
- **Undo support for toolbar actions**: Bold, italic, code, link, table and all other
  toolbar/keyboard formatting operations now integrate with the browser's native undo
  stack (Ctrl+Z). Previously, programmatic `editor.value` writes bypassed undo history;
  now uses `document.execCommand('insertText')` throughout.
- **Ctrl+S save**: Ctrl+S now saves the current document directly from the editor.
  Uses fully async save path to prevent UI deadlock.

### Fixed
- **Dirty indicator not clearing on undo**: The `*` tab dirty indicator now correctly
  clears when the user undoes all changes back to the last-saved content. Each tab
  tracks a `cleanContent` baseline (set on load/save) and compares on each change.
- **Dirty state lock on close**: Previously `contentChanged` was posted to C# on every
  keystroke (even undos back to clean), causing C# to always mark `IsDirty = true` and
  block IDE close with a spurious "unsaved changes" prompt. Now uses `tabDirtyChanged`
  message with actual dirty state, only posted when the state changes.
- **Ctrl+S deadlock**: `SaveMarkdownFile` used `.GetAwaiter().GetResult()` on the UI
  thread to retrieve editor content, deadlocking because `ExecuteScriptAsync` needs the
  UI thread to dispatch its completion. Fixed by making `SaveMarkdownFile` `async void`
  and using `await GetEditorContentAsync()`.
- **IDE tab title shows filename**: The Clarion IDE tab hosting the editor now always
  shows "Markdown Editor" instead of the opened filename. The filename is tracked
  internally but no longer used as the tab title.

---

## 2026-02-28 — Code Refactoring (JS/CSS Extraction)

### Changed
- Extracted all JavaScript (~1094 lines) from `markdown-editor.html` into a separate
  `Resources/markdown-editor.js` file, loaded via `<script src>`.
- Extracted all CSS (~576 lines) from `markdown-editor.html` into a separate
  `Resources/markdown-editor.css` file, loaded via `<link rel="stylesheet">`.
- `markdown-editor.html` reduced from ~1800 lines to 133 lines (structure only).
- Both new files added as `<Content>` items in `.csproj` with `PreserveNewest`, so they
  are copied to the output folder alongside the HTML and served via the WebView2
  virtual host (`https://app.local/`).

---



### Added
- **File type handler**: Opening a `.md` or `.markdown` file in the Clarion IDE now
  automatically opens it in the Markdown Editor as a document tab, instead of the
  default text editor. Implemented via `IDisplayBinding` / `DisplayBindingDoozer`.
- **Dark Mode in View menu**: Dark mode can now be toggled from **View > Dark Mode**
  at any time, without needing to navigate back to the Start Page. The preference is
  persisted across sessions via `SettingsService`.

### Fixed
- Addin assembly load error caused by a double-path in the `.addin` `Import` element
  (`MarkdownEditor\MarkdownEditor\...`). Assembly path is now simply `ClarionMarkdownEditor.dll`
  (relative to the `.addin` file location).
- Display binding was not triggered on file open because the built-in `Text` binding
  (which matches all files) was registered first. Fixed with `insertbefore="Text"`.
- File not opened automatically when using the display binding — WebView2 initializes
  asynchronously, so `LoadFile()` now stores a pending path that is opened once
  `NavigationCompleted` fires, instead of showing the Start Page.
- **View > Start Page** checkmark not updating — `ShowStartPage()` now sets
  `_activeTabId = "startPage"` so the menu reflects the correct active item.

---

## 2026-02-28

### Build System
- Converted `.csproj` from old-style MSBuild format to SDK-style
  (`Microsoft.NET.Sdk.WindowsDesktop`). `dotnet build` now works out of the box —
  NuGet packages (including WebView2) are restored automatically, no `nuget.exe`
  or separate restore step required.
- Removed `packages.config` (superseded by `PackageReference`).

### Changed
- Replaced hardcoded `C:\Clarion12\bin` path with a `Directory.Build.props`
  `$(ClarionBin)` variable. Override via a gitignored `Directory.Build.props.user`
  file or the `CLARION_BIN` environment variable. Default remains `C:\Clarion12\bin`.

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

