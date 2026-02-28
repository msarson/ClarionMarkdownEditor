# ClarionMarkdownEditor

A modern Markdown file viewer and editor addin for the Clarion IDE. Features a split-pane interface with live preview, syntax highlighting, dark mode, scroll synchronization, and seamless IDE integration.

![Clarion IDE Addin](https://img.shields.io/badge/Clarion-IDE%20Addin-blue)
![.NET Framework 4.8](https://img.shields.io/badge/.NET%20Framework-4.8-purple)
![WebView2](https://img.shields.io/badge/WebView2-Chromium-green)
![License](https://img.shields.io/badge/License-MIT-green)
 
## Features

- **Split-Pane Editor**: Side-by-side markdown source and live HTML preview
- **Live Preview**: Real-time rendering as you type
- **Mermaid Diagrams**: Create flowcharts, sequence diagrams, ERDs, Gantt charts, and more
  - Just use ` ```mermaid ` code blocks
  - See `MermaidExample.md` for examples
  - Supports all Mermaid diagram types (flowchart, sequence, ER, Gantt, state, class, etc.)
- **Syntax Highlighting**: Full code syntax highlighting powered by Highlight.js
  - Custom Clarion language definition included
  - Supports 190+ languages (JavaScript, Python, C#, SQL, etc.)
  - Atom One Dark theme (works in both light and dark modes)
  
  To use syntax highlighting, type three backticks followed by the language name:
  
  ```clarion
  MyProc PROCEDURE
  CODE
    MESSAGE('Hello from Clarion!')
    RETURN
  ```
- **Dark Mode**: Toggle between light and dark themes via **View > Dark Mode** or the 🌓 button on the Start Page — preference is remembered across sessions
- **Scroll Synchronization**: Bidirectional scroll sync between editor and preview (toggleable)
- **Horizontal Scrolling**: Long lines scroll instead of wrapping
- **Expand/Collapse Preview**: Toggle between split view and full-width preview mode
- **Formatting Toolbar**: Quick buttons for common markdown syntax:
  - Bold, Italic, Inline Code, Code Blocks
  - Headers (H1, H2, H3)
  - Bullet Lists, Numbered Lists
  - Blockquotes, Horizontal Rules
  - Links, Images, Tables
- **File Operations**: New, Open, Save, Save As
- **About Dialog**: View credits and project information (File > About)
- **IDE Integration**: Insert markdown content directly into the active Clarion editor
- **Keyboard Shortcuts**:
  - `Ctrl+Alt+M` - Open Markdown Editor pad
  - `Ctrl+S` - Save current document
  - `Ctrl+B` - Bold
  - `Ctrl+I` - Italic
  - All formatting shortcuts support **undo** (Ctrl+Z)
- **Dockable Pad**: Can be docked anywhere in the Clarion IDE workspace
- **File Type Handler**: Opening a `.md` or `.markdown` file in the IDE automatically opens it in the Markdown Editor
- **Remembers Settings**: Last opened folder and dark mode preference saved between sessions
- **Dirty Indicator**: The editor tab shows `*` when a document has unsaved changes — clears automatically when changes are undone back to the last-saved state

## Requirements

### Runtime (End Users)
- Clarion 11.1 or Clarion 12
- .NET Framework 4.8 or higher
- **Microsoft Edge WebView2 Runtime** (usually pre-installed on Windows 10/11)
  - Download: https://developer.microsoft.com/microsoft-edge/webview2/

### Development (Building from Source)
- **[.NET SDK](https://dotnet.microsoft.com/download)** (any modern version — used to run `dotnet build`)
- Clarion IDE installed (for reference DLLs)

> **Note:** Visual Studio is _not_ required. The .NET SDK is a free, lightweight
> command-line toolchain. Download the latest version from
> https://dotnet.microsoft.com/download and run the installer — `dotnet build`
> will then be available in any terminal.

### Recommended IDEs

You can open ClarionMarkdownEditor.slnx in any of the following:

- **[Visual Studio Code](https://code.visualstudio.com/)** (free, lightweight)
  Recommended extensions:
  - [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) — IntelliSense, build, debug
  - [C#](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp) — language support (installed with C# Dev Kit)
  - [NuGet Gallery](https://marketplace.visualstudio.com/items?itemName=patcx.vscode-nuget-gallery) — browse and manage NuGet packages
  - [XML](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-xml) — syntax support for .addin and .props files

- **[Visual Studio Community](https://visualstudio.microsoft.com/vs/community/)** (free for open source)
  Open the .slnx file directly — all packages restore automatically on first build.

- **[JetBrains Rider](https://www.jetbrains.com/rider/)** (commercial, free for open source)
  Full .NET IDE with excellent SDK-style project support.

## Installation

### From Release

1. Download the latest release
2. Copy all files to:
   ```
   {CLARION_PATH}\accessory\addins\MarkdownEditor\
   ```
   Required files:
   - `ClarionMarkdownEditor.dll`
   - `ClarionMarkdownEditor.addin`
   - `Microsoft.Web.WebView2.Core.dll`
   - `Microsoft.Web.WebView2.WinForms.dll`
   - `WebView2Loader.dll`
   - `Resources\markdown-editor.html`
   - `Resources\markdown-editor.css`
   - `Resources\markdown-editor.js`
   - `Resources\highlight.min.js`
   - `Resources\atom-one-dark.min.css`
3. Ensure WebView2 Runtime is installed
4. Restart Clarion IDE

### Building from Source

1. **Clone this repository**
   ```bash
   git clone https://github.com/msarson/ClarionMarkdownEditor.git
   cd ClarionMarkdownEditor
   ```

2. **Configure your Clarion path**

   The project uses `Directory.Build.props` to locate your Clarion installation.
   The default path is `C:\Clarion12\bin`.

   **If your Clarion is installed elsewhere**, create a file called
   `ClarionMarkdownEditor\Directory.Build.props.user` (gitignored) with:
   ```xml
   <Project>
     <PropertyGroup>
       <ClarionBin>C:\Clarion\Clarion11.1\bin</ClarionBin>
     </PropertyGroup>
   </Project>
   ```

   Alternatively, set the `CLARION_BIN` environment variable before building:
   ```powershell
   $env:CLARION_BIN = "C:\Clarion\Clarion11.1\bin"
   ```

3. **Build** — NuGet packages (including WebView2) are restored automatically:
   ```bash
   dotnet build ClarionMarkdownEditor\ClarionMarkdownEditor.csproj -c Release
   ```

4. **Deploy to Clarion**

   Copy from `ClarionMarkdownEditor\bin\Release\net48\` to `{CLARION_PATH}\accessory\addins\MarkdownEditor\`:
   - `ClarionMarkdownEditor.dll`
   - `ClarionMarkdownEditor.addin`
   - `Microsoft.Web.WebView2.Core.dll`
   - `Microsoft.Web.WebView2.WinForms.dll`
   - `WebView2Loader.dll`
   - `Resources\markdown-editor.html`
   - `Resources\markdown-editor.css`
   - `Resources\markdown-editor.js`
   - `Resources\highlight.min.js`
   - `Resources\atom-one-dark.min.css`

5. **Restart Clarion IDE**

## Screenshots
```
┌─────────────────────────────────────────────────────────────┐
│ New  Open  Save  Save As │ Insert to IDE │ filename.md     │
├─────────────────────────────────────────────────────────────┤
│ B │ I │ \u003C/> │ {} │ Link │ Img │ H1 │ H2 │ H3 │ List │ ...  │
├────────────────────────────┬────────────────────────────────┤
│ MARKDOWN                   │ PREVIEW                [Expand]│
├────────────────────────────┼────────────────────────────────┤
│ # My Document              │ My Document                    │
│                            │ ───────────                    │
│ This is **bold** text.     │ This is bold text.             │
│                            │                                │
│ - Item 1                   │ • Item 1                       │
│ - Item 2                   │ • Item 2                       │
└────────────────────────────┴────────────────────────────────┘
```

### Expanded Preview Mode
```
┌─────────────────────────────────────────────────────────────┐
│ PREVIEW                                              [Split]│
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  My Document                                                │
│  ───────────────────────────────────────────                │
│                                                             │
│  This is bold text.                                         │
│                                                             │
│  • Item 1                                                   │
│  • Item 2                                                   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## Usage

### Opening the Editor

The Markdown Editor can be opened in two different modes:

#### Dockable Pad Mode (Default)
- **Keyboard**: Press `Ctrl+Alt+M`
- **Menu**: Go to `Tools → Markdown Editor (Pad)`
- Opens as a dockable tool window that can be positioned on any side of the IDE

#### Document Window Mode
- **Menu**: Go to `Tools → Markdown Editor (Window)`
- Opens in the main document area alongside your source code files
- Useful when you want to work on markdown files like regular documents

### Editing Markdown

1. Click **Open** to load an existing `.md` file, or start typing in a new document
2. Use the formatting toolbar for quick markdown syntax insertion
3. Preview updates in real-time as you type

### Preview Modes

- **Split View**: Editor and preview side-by-side (default)
- **Expanded Preview**: Click **Expand** button to hide the editor and view preview full-width
- **Return to Split**: Click **Split** button to restore side-by-side view

### Insert to IDE

Click **Insert to IDE** to insert the current markdown content at the cursor position in the active Clarion source editor.

## Supported Markdown Syntax

| Element | Syntax |
|---------|--------|
| Heading 1 | `# Heading` |
| Heading 2 | `## Heading` |
| Heading 3 | `### Heading` |
| Bold | `**bold**` or `__bold__` |
| Italic | `*italic*` or `_italic_` |
| Inline Code | `` `code` `` |
| Code Block | ` ```language ... ``` ` |
| Link | `[text](url)` |
| Image | `![alt](url)` |
| Unordered List | `- item` or `* item` |
| Ordered List | `1. item` |
| Blockquote | `> quote` |
| Horizontal Rule | `---` or `***` |
| Table | `| Col1 | Col2 |` |

## Project Structure

```
MarkDownAddin/
├── ClarionMarkdownEditor.sln
├── addin-config.json
├── README.md
└── ClarionMarkdownEditor/
    ├── ClarionMarkdownEditor.csproj
    ├── ClarionMarkdownEditor.addin      # SharpDevelop addin manifest
    ├── Directory.Build.props            # Clarion path config (ClarionBin variable)
    ├── Properties/
    │   └── AssemblyInfo.cs
    ├── MarkdownEditorPad.cs             # Dockable pad container
    ├── MarkdownEditorViewContent.cs     # Document window container
    ├── MarkdownEditorControl.cs         # Main control with WebBrowser
    ├── MarkdownEditorControl.Designer.cs
    ├── ShowMarkdownEditorCommand.cs     # Tools menu command (Pad mode)
    ├── ShowMarkdownEditorWindowCommand.cs # Tools menu command (Window mode)
    ├── Services/
    │   ├── EditorService.cs             # IDE editor interaction
    │   ├── SettingsService.cs           # User settings persistence
    │   └── ScriptBridge.cs              # JS-to-C# communication
    └── Resources/
        ├── markdown-editor.html         # HTML structure (UI skeleton)
        ├── markdown-editor.css          # All editor styles (light/dark/layout)
        ├── markdown-editor.js           # All editor behaviour (tabs, preview, dirty tracking)
        ├── highlight.min.js             # Highlight.js syntax highlighting library
        └── atom-one-dark.min.css        # Highlight.js theme
```

## Technical Details

### Architecture

- **UI Layer**: HTML/CSS/JavaScript in WebView2 (Chromium-based)
- **Modern Browser Engine**: WebView2 provides full modern web standards support
- **Native Toolbar**: WinForms ToolStrip for file operations
- **Markdown Parser**: Custom lightweight parser implemented in JavaScript
- **Syntax Highlighting**: Highlight.js 11.9.0 with custom Clarion language definition
- **IDE Integration**: Uses reflection for compatibility across Clarion IDE versions

### Why WebView2?

Migrated from old IE-based WebBrowser to WebView2 (Chromium) to enable:
- Modern JavaScript support (ES6+)
- Proper execution of minified libraries
- Full CSS3 support including flexbox and grid
- Better performance and security
- Syntax highlighting with Highlight.js

### Syntax Highlighting Implementation

- **Library**: Highlight.js 11.9.0
- **Theme**: Atom One Dark
- **Injection**: C#-based file injection (no CDN dependencies, works offline)
- **Custom Language**: Full Clarion language definition from [discourse-highlightjs-clarion](https://github.com/msarson/discourse-highlightjs-clarion)
- **Files**: `highlight.min.js` (121KB) and `atom-one-dark.min.css` (856 bytes)

### Settings Storage

User settings are stored in:
```
%APPDATA%\\ClarionMarkdownEditor\\settings.txt
```

## Development Notes

### Key Commits

1. **Initial commit**: Basic markdown editor with WebBrowser control
2. **Dark mode & UI enhancements**: Added dark mode, scroll sync, horizontal scrolling
3. **WebView2 migration**: Replaced IE WebBrowser with Chromium WebView2
4. **Syntax highlighting**: Integrated Highlight.js with custom Clarion support

### Known Limitations

- **WebView2 Runtime Required**: Users must have WebView2 Runtime installed (typically pre-installed on Windows 10/11)
- **32-bit Only**: Built for x86 to match Clarion IDE architecture
- **Print Feature**: Print styles included but pagination needs work

### Future Enhancements

- Multi-page print support
- Export to PDF
- Markdown templates
- Spell checking
- Find/Replace in editor

## Contributing

Contributions are welcome! Please feel free to submit issues or pull requests.

## License

MIT License - see LICENSE file for details.

## Authors

- **John Hickey** - Original author
- **Mark Sarson** - WebView2 enhancements, Mermaid support
- **Oleg Fomin** - Document window mode feature
- **Claude Code** - AI pair programming assistant

From an idea by **Dinko Bakun**

## Acknowledgments

- Built for the Clarion IDE (SharpDevelop-based)
- Inspired by popular markdown editors like Typora and Mark Text
- Custom Clarion syntax highlighting from [discourse-highlightjs-clarion](https://github.com/msarson/discourse-highlightjs-clarion)
- [ClarionLive](https://www.clarionlive.com) - Clarion developer community

