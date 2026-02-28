using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClarionMarkdownEditor.Services;

namespace ClarionMarkdownEditor
{
    /// <summary>
    /// Represents a single file tab in the tabbed markdown editor.
    /// </summary>
    public class FileTab
    {
        public string Id { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public bool IsDirty { get; set; }
    }

    /// <summary>
    /// Represents recent file information for the Start Page.
    /// </summary>
    public class RecentFileInfo
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public string ModifiedDate { get; set; }
        public bool Exists { get; set; }
    }

    /// <summary>
    /// Main user control for the Markdown Editor addin.
    /// Uses WebView2 control to display a split-pane markdown editor with live preview.
    /// </summary>
    public partial class MarkdownEditorControl : UserControl
    {
        private const int MAX_RECENT_FILES = 30;
        private const string RECENT_FILES_KEY = "RecentFiles";

        private readonly EditorService _editorService;
        private readonly SettingsService _settingsService;
        private string _currentFilePath;
        private string _tempHtmlPath;
        private bool _isWebView2Ready = false;
        private bool _initializationStarted = false;
        private string _pendingFilePath = null;

        // Tab tracking fields
        private Dictionary<string, FileTab> _openTabs = new Dictionary<string, FileTab>();
        private string _activeTabId;
        private int _untitledCounter = 0;

        // Message filter for closing menus when clicking WebView2
        private WebView2MenuCloseFilter _menuCloseFilter;

        public MarkdownEditorControl()
        {
            InitializeComponent();
            _editorService = new EditorService();
            _settingsService = new SettingsService();

            // Install message filter to close menus when clicking inside WebView2
            _menuCloseFilter = new WebView2MenuCloseFilter(this, webView, menuStrip);
            Application.AddMessageFilter(_menuCloseFilter);

            // Close menu dropdowns when WebView2 gets focus (clicks inside WebView2 don't bubble up to close menus)
            webView.GotFocus += (s, e) => CloseMenuDropdowns();

            // Defer WebView2 initialization until the webView control has its handle
            // (not just the parent UserControl - the WebView2 itself needs a valid HWND)
            webView.HandleCreated += WebView_HandleCreated;

            // If webView handle is already created (restored from docked state), initialize
            if (webView.IsHandleCreated)
            {
                _initializationStarted = true;
                // Use BeginInvoke to defer initialization until after the control
                // is fully integrated and the message queue settles
                this.BeginInvoke(new Action(() => InitializeWebView()));
            }
        }

        private void WebView_HandleCreated(object sender, EventArgs e)
        {
            // Only initialize once
            if (!_initializationStarted)
            {
                _initializationStarted = true;
                // Use BeginInvoke to defer initialization until after the control
                // is fully integrated and the message queue settles
                this.BeginInvoke(new Action(() => InitializeWebView()));
            }
        }

        private void CloseMenuDropdowns()
        {
            foreach (ToolStripItem item in menuStrip.Items)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.DropDown.Visible)
                {
                    menuItem.DropDown.Close();
                }
            }
        }

        private async void InitializeWebView()
        {
            // Prevent double initialization
            if (_initializationStarted && _isWebView2Ready)
            {
                return;
            }
            _initializationStarted = true;

            try
            {
                System.Diagnostics.Debug.WriteLine("=== InitializeWebView START ===");

                // Initialize WebView2 directly - let EnsureCoreWebView2Async handle any errors
                System.Diagnostics.Debug.WriteLine("About to call EnsureCoreWebView2Async...");
                await webView.EnsureCoreWebView2Async(null);
                _isWebView2Ready = true;
                System.Diagnostics.Debug.WriteLine("WebView2 initialized successfully!");
                
                // Disable context menu entirely
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                // Subscribe to web messages from JavaScript
                webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                // Set up virtual host mapping to enable CDN scripts (Mermaid, etc.)
                var addinDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var resourcesPath = Path.Combine(addinDir, "Resources");
                System.Diagnostics.Debug.WriteLine($"Setting up virtual host: app.local -> {resourcesPath}");
                
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "app.local",
                    resourcesPath,
                    Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow
                );
                
                // Load markdown-editor.html via virtual host (enables CDN)
                System.Diagnostics.Debug.WriteLine("Navigating to https://app.local/markdown-editor.html");
                
                // Inject Highlight.js before navigating
                var htmlPath = Path.Combine(resourcesPath, "markdown-editor.html");
                if (File.Exists(htmlPath))
                {
                    var html = File.ReadAllText(htmlPath);
                    html = InjectHighlightJs(html);
                    
                    // Write modified HTML to temp location
                    _tempHtmlPath = Path.Combine(resourcesPath, "markdown-editor-temp.html");
                    File.WriteAllText(_tempHtmlPath, html);
                    
                    webView.CoreWebView2.Navigate("https://app.local/markdown-editor-temp.html");
                    System.Diagnostics.Debug.WriteLine("Navigated to modified HTML with Highlight.js injected");

                    // Show Start Page (or pending file) after HTML loads
                    webView.CoreWebView2.NavigationCompleted += async (s, args) =>
                    {
                        if (args.IsSuccess && _isWebView2Ready)
                        {
                            // Small delay to ensure JavaScript is fully initialized
                            await Task.Delay(100);
                            if (!string.IsNullOrEmpty(_pendingFilePath))
                            {
                                var path = _pendingFilePath;
                                _pendingFilePath = null;
                                OpenFile(path);
                            }
                            else
                            {
                                ShowStartPage();
                            }
                        }
                    };
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR: markdown-editor.html not found at {htmlPath}");
                    MessageBox.Show("markdown-editor.html not found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EXCEPTION in InitializeWebView: {ex.ToString()}");
                MessageBox.Show($"Error initializing WebView2: {ex.Message}\n\nPlease try reinstalling the WebView2 Runtime.", 
                    "WebView2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowWebView2InstallPrompt()
        {
            var result = MessageBox.Show(
                "Microsoft Edge WebView2 Runtime is required but not installed.\n\n" +
                "WebView2 provides the modern browser engine for this markdown editor.\n\n" +
                "Would you like to download it now?\n\n" +
                "Download: https://go.microsoft.com/fwlink/p/?LinkId=2124703\n" +
                "(~100MB download, system-wide install)",
                "WebView2 Runtime Required",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
            {
                try
                {
                    System.Diagnostics.Process.Start("https://go.microsoft.com/fwlink/p/?LinkId=2124703");
                }
                catch
                {
                    MessageBox.Show(
                        "Unable to open browser automatically.\n\n" +
                        "Please manually visit:\n" +
                        "https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                        "Download WebView2",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }

            // Show fallback message in the control
            var fallbackHtml = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body { 
            font-family: 'Segoe UI', Tahoma, sans-serif; 
            padding: 40px; 
            background: #f5f5f5; 
            text-align: center;
        }
        .container {
            background: white;
            padding: 40px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            max-width: 600px;
            margin: 0 auto;
        }
        h2 { color: #d32f2f; margin-bottom: 20px; }
        p { line-height: 1.6; color: #555; margin-bottom: 15px; }
        .download-link { 
            display: inline-block;
            padding: 12px 24px;
            background: #0078d4;
            color: white;
            text-decoration: none;
            border-radius: 4px;
            margin-top: 20px;
            font-weight: 500;
        }
        .download-link:hover { background: #106ebe; }
        code {
            background: #f0f0f0;
            padding: 2px 6px;
            border-radius: 3px;
            font-family: 'Consolas', monospace;
        }
    </style>
</head>
<body>
    <div class='container'>
        <h2>⚠️ WebView2 Runtime Required</h2>
        <p>The Markdown Editor requires <strong>Microsoft Edge WebView2 Runtime</strong> to function.</p>
        <p>This provides the modern browser engine for rendering markdown with syntax highlighting.</p>
        <p><strong>Most Windows 10/11 systems have this pre-installed.</strong></p>
        <p>If you're seeing this message, please download and install it:</p>
        <a href='https://go.microsoft.com/fwlink/p/?LinkId=2124703' class='download-link' target='_blank'>
            Download WebView2 Runtime (~100MB)
        </a>
        <p style='margin-top: 30px; font-size: 12px; color: #888;'>
            After installation, restart Clarion IDE and this editor will work.
        </p>
    </div>
</body>
</html>";
            
            // This will fail since webView isn't initialized, but that's okay - we're showing the message
            try
            {
                webView.NavigateToString(fallbackHtml);
            }
            catch
            {
                // Silently fail - user already got the message box
            }
        }

        private string InjectHighlightJs(string html)
        {
            var injectionLog = "INJECTION DEBUG: ";
            try
            {
                // Get the directory where the addin is deployed
                var addinDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                injectionLog += $"addinDir={addinDir}; ";
                
                // Load highlight.js CSS
                var cssPath = Path.Combine(addinDir, "Resources", "atom-one-dark.min.css");
                injectionLog += $"cssPath={cssPath}; ";
                if (File.Exists(cssPath))
                {
                    var css = File.ReadAllText(cssPath);
                    html = html.Replace("<!-- INJECT_HIGHLIGHTJS_CSS -->", $"<style>\n{css}\n    </style>");
                    injectionLog += "CSS injected; ";
                }
                else
                {
                    injectionLog += "CSS NOT FOUND; ";
                }
                
                // Load highlight.js JavaScript
                var jsPath = Path.Combine(addinDir, "Resources", "highlight.min.js");
                injectionLog += $"jsPath={jsPath}; ";
                if (File.Exists(jsPath))
                {
                    var js = File.ReadAllText(jsPath);
                    // Don't escape - the script block should handle it
                    // Just inject it as-is
                    html = html.Replace("<!-- INJECT_HIGHLIGHTJS_JS -->", $"<script>\n{js}\n    </script>");
                    injectionLog += "JS injected; ";
                }
                else
                {
                    injectionLog += "JS NOT FOUND; ";
                }
                
                // Inject Clarion language definition
                var clarionLang = GetClarionLanguageDefinition();
                html = html.Replace("<!-- INJECT_CLARION_LANG -->", $"<script>\n{clarionLang}\n    </script>");
                injectionLog += "Clarion lang injected; ";
                
                // Add diagnostic message in HTML
                html = html.Replace("</body>", $"<!-- {injectionLog} -->\n</body>");
                
                return html;
            }
            catch (Exception ex)
            {
                // If injection fails, add error comment
                injectionLog += $"EXCEPTION: {ex.Message}; ";
                html = html.Replace("</body>", $"<!-- {injectionLog} -->\n</body>");
                return html;
            }
        }

        private string GetClarionLanguageDefinition()
        {
            return @"
        // Clarion language definition for Highlight.js
        // Based on https://github.com/msarson/discourse-highlightjs-clarion
        hljs.registerLanguage('clarion', function(hljs) {
            const STRING_LITERAL = {
                className: 'string',
                begin: ""'"",
                end: ""'""
            };

            const COMMENTS = {
                className: 'comment',
                begin: '(\\!|\\|)',
                end: '$'
            };

            const NUMERIC_LITERALS = {
                className: 'number',
                begin: '\\b\\d+(\\.\\d+)?'
            };

            const LABELS = {
                className: 'label',
                begin: '^[A-Za-z_][A-Za-z0-9_]*(?::[A-Za-z0-9_]+)?',
                end: '(?=\\s)',
            };

            const CLASS_LABELS = {
                className: 'title class-',
                begin: '[A-Za-z_][A-Za-z0-9_]*\\.[A-Za-z_][A-Za-z0-9_]*',
                end: '(?=\\s)',
                relevance: 10
            };

            const BASE_TYPES = {
                className: 'type',
                begin: '\\b(?i:ANY|ASTRING|BOOL|BYTE|CSTRING|DATE|DECIMAL|DOUBLE|FLOAT4|LONG|PSTRING|REAL|SHORT|SIGNED|STRING|TIME|ULONG|UNSIGNED|USHORT)\\b'
            };

            const SPECIAL_TYPES = {
                className: 'type',
                begin: '\\b(?i:FILE|QUEUE|GROUP|ARRAY)\\b'
            };

            const ATTRIBUTES = {
                className: 'attr',
                begin: '\\b(?i:AT|AUTO|BINDABLE|BOXED|CENTER|COLOR|COLUMN|DEFAULT|DIM|DLL|DRIVER|DROP|ICON|INNER|LENGTH|MASK|NAME|ORDER|OVER|PAGE|PRIVATE|PROTECTED|PUBLIC|REQ|SCROLL|STATIC|TIP|USE|VALUE|VERTICAL|WIDTH)\\b'
            };

            const OPERATORS = {
                className: 'operator',
                begin: '(\\+|\\-|\\*|\\/|=|<>|<=|>=|~=|%|\\+=|\\-=|\\*=|\\\\=|:=:|~>|~<|\\^)'
            };

            const PUNCTUATION = {
                className: 'punctuation',
                begin: '[.,;]'
            };

            const CONSTANTS = {
                className: 'number',
                variants: [
                    { begin: '0[xX][0-9a-fA-F]+[hH]?' },
                    { begin: '[01]+[bB]' },
                    { begin: '[0-7]+[oO]' }
                ]
            };

            const PICTURE_NUMERIC_FORMAT = {
                className: 'string',
                begin: '@[Nn][\\-]?[0-9\\.]*\\~',
                end: '\\~'
            };

            const PREPROCESSOR_DIRECTIVES = {
                className: 'meta',
                begin: '#(pragma|include)',
                end: '$'
            };

            const HARD_RESERVED_KEYWORDS = {
                className: 'keyword',
                begin: '\\b(?i:ACCEPT|AND|BREAK|BY|CASE|CHOOSE|CYCLE|DO|ELSE|ELSIF|END|EXECUTE|EXIT|FUNCTION|GOTO|IF|LOOP|MEMBER|NEW|NOT|OF|OR|OROF|PARENT|PROCEDURE|PROGRAM|RETURN|ROUTINE|SELF|THEN|TIMES|TO|UNTIL|WHILE)\\b'
            };

            const SOFT_RESERVED_KEYWORDS = {
                className: 'keyword',
                begin: '\\b(?i:APPLICATION|CLASS|CODE|DATA|DETAIL|ENUM|FILE|FOOTER|FORM|GROUP|HEADER|INLINE|ITEM|JOIN|MAP|MENU|MENUBAR|MODULE|OLECONTROL|OPTION|QUEUE|RECORD|REPORT|ROW|SHEET|TAB|TABLE|TOOLBAR|VIEW|WINDOW|PROPERTY|INDEXER)\\b'
            };

            const LANG_FUNCTIONS = {
                className: 'built_in',
                begin: '\\b(?i:ADD|DISPOSE|ADDRESS|GET|PUT|OPEN|CLOSE|LOCK|UNLOCK|MESSAGE|CLEAR|FREE|SET|SEND|POST|FILEERROR|FILEERRORCODE|RANDOM|DAY|YEAR|MONTH|INSTRING|MATCH|LEN|UPPER|LOWER|LEFT|RIGHT|SUB|TODAY|FORMAT|INT|ABS)\\b'
            };

            const PROCEDURE_KEYWORD = {
                className: 'title function_',
                begin: '\\bPROCEDURE\\b'
            };

            return {
                name: 'Clarion',
                aliases: ['clarion', 'Clarion', 'CLARION', 'clw'],
                case_insensitive: true,
                contains: [
                    STRING_LITERAL,
                    COMMENTS,
                    NUMERIC_LITERALS,
                    CONSTANTS,
                    LABELS,
                    CLASS_LABELS,
                    BASE_TYPES,
                    SPECIAL_TYPES,
                    ATTRIBUTES,
                    HARD_RESERVED_KEYWORDS,
                    SOFT_RESERVED_KEYWORDS,
                    LANG_FUNCTIONS,
                    PROCEDURE_KEYWORD,
                    OPERATORS,
                    PUNCTUATION,
                    PREPROCESSOR_DIRECTIVES,
                    PICTURE_NUMERIC_FORMAT
                ]
            };
        });";
        }

        private string GetFallbackHtml()
        {
            return @"<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: Segoe UI, Arial, sans-serif; padding: 20px; }
        .error { color: #c00; }
    </style>
</head>
<body>
    <h3 class='error'>Resource not found</h3>
    <p>The markdown editor HTML resource could not be loaded.</p>
</body>
</html>";
        }

        #region Toolbar Button Handlers

        private void btnNew_Click(object sender, EventArgs e)
        {
            NewMarkdownFile();
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            OpenMarkdownFile();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveMarkdownFile();
        }

        private void btnSaveAs_Click(object sender, EventArgs e)
        {
            SaveMarkdownFileAs();
        }

        private void btnInsertToIDE_Click(object sender, EventArgs e)
        {
            InsertToIdeEditor();
        }

        private void lblFileName_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ShowFileNameContextMenu();
            }
        }

        private void ShowFileNameContextMenu()
        {
            var menu = new ContextMenuStrip();

            menu.Items.Add("Close", null, (s, ev) => btnNew_Click(s, ev));
            menu.Items.Add("Save As", null, (s, ev) => SaveMarkdownFileAs());
            menu.Items.Add(new ToolStripSeparator());

            var copyPathItem = menu.Items.Add("Copy Path");
            copyPathItem.Enabled = !string.IsNullOrEmpty(_currentFilePath);
            copyPathItem.Click += (s, ev) => {
                if (!string.IsNullOrEmpty(_currentFilePath))
                    Clipboard.SetText(_currentFilePath);
            };

            var openFolderItem = menu.Items.Add("Open Containing Folder");
            openFolderItem.Enabled = !string.IsNullOrEmpty(_currentFilePath) && File.Exists(_currentFilePath);
            openFolderItem.Click += (s, ev) => {
                if (!string.IsNullOrEmpty(_currentFilePath) && File.Exists(_currentFilePath))
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{_currentFilePath}\"");
            };

            menu.Show(Cursor.Position);
        }

        #endregion

        #region File Operations

        private void NewMarkdownFile()
        {
            var tabId = GenerateTabId();
            var fileName = GetNextUntitledName();

            var tab = new FileTab
            {
                Id = tabId,
                FilePath = null,
                FileName = fileName,
                IsDirty = false
            };

            _openTabs[tabId] = tab;
            _activeTabId = tabId;
            _currentFilePath = null;

            // Call JavaScript to add the tab
            AddTabToJs(tabId, fileName, "", null);
        }

        #region Recent Files

        private List<string> GetRecentFiles()
        {
            var stored = _settingsService.Get(RECENT_FILES_KEY);
            if (string.IsNullOrEmpty(stored)) return new List<string>();

            // Split, filter empty, and deduplicate (case-insensitive)
            var files = stored.Split('|')
                .Where(f => !string.IsNullOrEmpty(f))
                .Select(f => {
                    try { return Path.GetFullPath(f); }
                    catch { return f; }
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return files;
        }

        private void SaveRecentFiles(List<string> files)
        {
            _settingsService.Set(RECENT_FILES_KEY, string.Join("|", files));
        }

        private void AddToRecentFiles(string filePath)
        {
            // Normalize the path
            filePath = Path.GetFullPath(filePath);

            var files = GetRecentFiles();

            // Remove any existing entry (case-insensitive for Windows)
            files.RemoveAll(f => string.Equals(f, filePath, StringComparison.OrdinalIgnoreCase));

            // Insert at top
            files.Insert(0, filePath);

            if (files.Count > MAX_RECENT_FILES)
                files = files.Take(MAX_RECENT_FILES).ToList();

            SaveRecentFiles(files);
        }

        public void LoadFile(string filePath)
        {
            if (!_isWebView2Ready)
            {
                _pendingFilePath = filePath;
                return;
            }
            OpenFile(filePath);
        }

        private void OpenFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show($"File not found:\n{filePath}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                var files = GetRecentFiles();
                files.Remove(filePath);
                SaveRecentFiles(files);
                return;
            }

            // Check if file is already open in a tab
            var existingTab = _openTabs.Values.FirstOrDefault(t =>
                !string.IsNullOrEmpty(t.FilePath) &&
                string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

            if (existingTab != null)
            {
                // Switch to existing tab
                SwitchToTab(existingTab.Id);
                // Still update recent files to move to top
                AddToRecentFiles(filePath);
                _ = RefreshRecentFilesInStartPage();
                return;
            }

            // Create a new tab for this file
            var tabId = GenerateTabId();
            var fileName = Path.GetFileName(filePath);
            string content = File.ReadAllText(filePath);

            var tab = new FileTab
            {
                Id = tabId,
                FilePath = filePath,
                FileName = fileName,
                IsDirty = false
            };

            _openTabs[tabId] = tab;
            _activeTabId = tabId;
            _currentFilePath = filePath;
            _settingsService.Set("LastOpenFolder", Path.GetDirectoryName(filePath));

            // Call JavaScript to add the tab
            AddTabToJs(tabId, fileName, content, filePath);

            AddToRecentFiles(filePath);
            _ = RefreshRecentFilesInStartPage();
        }

        private void menuRecent_DropDownOpening(object sender, EventArgs e)
        {
            menuRecent.DropDownItems.Clear();
            var files = GetRecentFiles();

            if (files.Count == 0)
            {
                var noItems = new ToolStripMenuItem("(No recent files)");
                noItems.Enabled = false;
                menuRecent.DropDownItems.Add(noItems);
                return;
            }

            foreach (var file in files)
            {
                var item = new ToolStripMenuItem(Path.GetFileName(file));
                item.ToolTipText = file;
                item.Tag = file;
                item.Click += RecentFile_Click;
                menuRecent.DropDownItems.Add(item);
            }

            menuRecent.DropDownItems.Add(new ToolStripSeparator());
            var clearItem = new ToolStripMenuItem("Clear Recent Files");
            clearItem.Click += (s, ev) => { SaveRecentFiles(new List<string>()); };
            menuRecent.DropDownItems.Add(clearItem);
        }

        private void RecentFile_Click(object sender, EventArgs e)
        {
            var item = sender as ToolStripMenuItem;
            if (item?.Tag is string filePath)
            {
                OpenFile(filePath);
            }
        }

        #endregion

        private void OpenMarkdownFile()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Markdown files (*.md)|*.md|Text files (*.txt)|*.txt|All files (*.*)|*.*";
                dialog.Title = "Open Markdown File";

                var lastFolder = _settingsService.Get("LastOpenFolder");
                if (!string.IsNullOrEmpty(lastFolder) && Directory.Exists(lastFolder))
                {
                    dialog.InitialDirectory = lastFolder;
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    OpenFile(dialog.FileName);
                }
            }
        }

        private void SaveMarkdownFile()
        {
            if (string.IsNullOrEmpty(_activeTabId) || !_openTabs.TryGetValue(_activeTabId, out var activeTab))
            {
                return;
            }

            if (string.IsNullOrEmpty(activeTab.FilePath))
            {
                SaveMarkdownFileAs();
                return;
            }

            string content = GetEditorContent();
            if (content != null)
            {
                File.WriteAllText(activeTab.FilePath, content);
                activeTab.IsDirty = false;
                _currentFilePath = activeTab.FilePath;
                SendMessageToJs("fileSaved", activeTab.FilePath);
                ClearDirtyIndicatorInJs(_activeTabId);
            }
        }

        private void SaveMarkdownFileAs()
        {
            if (string.IsNullOrEmpty(_activeTabId) || !_openTabs.TryGetValue(_activeTabId, out var activeTab))
            {
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Markdown files (*.md)|*.md|Text files (*.txt)|*.txt|All files (*.*)|*.*";
                dialog.Title = "Save Markdown File";
                dialog.DefaultExt = "md";

                if (!string.IsNullOrEmpty(activeTab.FilePath))
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(activeTab.FilePath);
                    dialog.FileName = Path.GetFileName(activeTab.FilePath);
                }
                else if (!string.IsNullOrEmpty(_currentFilePath))
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(_currentFilePath);
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    string content = GetEditorContent();
                    if (content != null)
                    {
                        File.WriteAllText(dialog.FileName, content);

                        // Update tab with new file path
                        activeTab.FilePath = dialog.FileName;
                        activeTab.FileName = Path.GetFileName(dialog.FileName);
                        activeTab.IsDirty = false;

                        _currentFilePath = dialog.FileName;

                        SendMessageToJs("fileSaved", dialog.FileName);
                        UpdateTabInJs(_activeTabId, activeTab.FileName, dialog.FileName);
                        ClearDirtyIndicatorInJs(_activeTabId);

                        AddToRecentFiles(dialog.FileName);
                    }
                }
            }
        }

        private async void InsertToIdeEditor()
        {
            string content = await GetEditorContentAsync();
            if (content != null)
            {
                var result = _editorService.InsertTextAtCaret(content);
                if (!result.Success)
                {
                    MessageBox.Show($"Could not insert text: {result.ErrorMessage}",
                        "Markdown Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        #endregion

        #region JavaScript Communication

        /// <summary>
        /// Handles messages received from the WebView2 control.
        /// </summary>
        private void CoreWebView2_WebMessageReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            var message = e.WebMessageAsJson;
            HandleWebMessage(message);
        }

        /// <summary>
        /// Gets the current content from the JavaScript editor (async).
        /// </summary>
        private async Task<string> GetEditorContentAsync()
        {
            if (_isWebView2Ready)
            {
                try
                {
                    var result = await webView.ExecuteScriptAsync("getEditorContent()");
                    // Remove surrounding quotes from JSON string
                    if (result.StartsWith("\"") && result.EndsWith("\""))
                    {
                        result = result.Substring(1, result.Length - 2);
                        result = result.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\"", "\"");
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"GetEditorContentAsync error: {ex.Message}");
                    return null;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the current content from the JavaScript editor (synchronous wrapper - avoid if possible).
        /// </summary>
        private string GetEditorContent()
        {
            // Use GetAwaiter().GetResult() instead of .Wait() to avoid deadlocks
            return GetEditorContentAsync().GetAwaiter().GetResult();
        }

        private void SendContentToJs(string content, string fileName)
        {
            // Normalize line endings to \n for JavaScript
            string normalized = content.Replace("\r\n", "\n").Replace("\r", "\n");
            InvokeScript("loadContent", normalized, fileName);
        }

        private void SendMessageToJs(string messageType, string data)
        {
            InvokeScript("receiveMessage", messageType, data);
        }

        private async void InvokeScript(string functionName, params string[] args)
        {
            if (_isWebView2Ready)
            {
                try
                {
                    // Build JavaScript call with arguments - properly escape strings
                    var escapedArgs = Array.ConvertAll(args, arg => {
                        arg = arg.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
                        return $"\"{arg}\"";
                    });
                    var argsStr = string.Join(",", escapedArgs);
                    await webView.ExecuteScriptAsync($"{functionName}({argsStr})");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"InvokeScript error: {ex.Message}");
                }
            }
        }

        #endregion

        #region Tab Operations

        /// <summary>
        /// Generates a unique tab ID using a GUID substring.
        /// </summary>
        private string GenerateTabId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        /// <summary>
        /// Gets the next untitled file name (Untitled 1, Untitled 2, etc.).
        /// </summary>
        private string GetNextUntitledName()
        {
            return $"Untitled {++_untitledCounter}";
        }

        /// <summary>
        /// Switches to a specific tab by its ID.
        /// </summary>
        private void SwitchToTab(string tabId)
        {
            if (!_openTabs.TryGetValue(tabId, out var tab))
            {
                return;
            }

            _activeTabId = tabId;
            _currentFilePath = tab.FilePath;

            // Tell JavaScript to switch to this tab
            SwitchToTabInJs(tabId);
        }

        /// <summary>
        /// Closes a tab by its ID. Prompts to save if the tab is dirty.
        /// </summary>
        private async Task CloseTab(string tabId)
        {
            if (!_openTabs.TryGetValue(tabId, out var tab))
            {
                return;
            }

            // Check if dirty and prompt user
            if (tab.IsDirty)
            {
                var result = MessageBox.Show(
                    $"Save changes to {tab.FileName}?",
                    "Save Changes",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Cancel)
                {
                    return; // Don't close
                }

                if (result == DialogResult.Yes)
                {
                    // Save the file
                    if (tabId == _activeTabId)
                    {
                        if (string.IsNullOrEmpty(tab.FilePath))
                        {
                            SaveMarkdownFileAs();
                            // If user cancelled save as, don't close
                            if (string.IsNullOrEmpty(tab.FilePath))
                            {
                                return;
                            }
                        }
                        else
                        {
                            SaveMarkdownFile();
                        }
                    }
                    else
                    {
                        // Need to get content from JavaScript for this specific tab
                        string content = await GetTabContentFromJs(tabId);
                        if (content != null && !string.IsNullOrEmpty(tab.FilePath))
                        {
                            File.WriteAllText(tab.FilePath, content);
                        }
                    }
                }
            }

            // Remove the tab
            _openTabs.Remove(tabId);
            RemoveTabFromJs(tabId);

            // If we closed the active tab, switch to another
            if (tabId == _activeTabId)
            {
                if (_openTabs.Count > 0)
                {
                    // Switch to the first available tab
                    var nextTab = _openTabs.Values.First();
                    SwitchToTab(nextTab.Id);
                }
                else
                {
                    // No more tabs - switch to Start Page
                    ShowStartPage();
                }
            }
        }

        /// <summary>
        /// Closes all tabs except the specified one.
        /// </summary>
        private async Task CloseOtherTabs(string keepTabId)
        {
            var tabsToClose = _openTabs.Keys.Where(id => id != keepTabId).ToList();
            foreach (var tabId in tabsToClose)
            {
                await CloseTab(tabId);
            }
        }

        /// <summary>
        /// Closes all open tabs.
        /// </summary>
        private async Task CloseAllTabs()
        {
            var tabsToClose = _openTabs.Keys.ToList();
            foreach (var tabId in tabsToClose)
            {
                await CloseTab(tabId);
            }
        }

        /// <summary>
        /// Marks a tab as dirty (has unsaved changes).
        /// </summary>
        private void SetTabDirty(string tabId, bool isDirty)
        {
            if (_openTabs.TryGetValue(tabId, out var tab))
            {
                tab.IsDirty = isDirty;
                if (isDirty)
                {
                    SetDirtyIndicatorInJs(tabId);
                }
                else
                {
                    ClearDirtyIndicatorInJs(tabId);
                }
            }
        }

        /// <summary>
        /// Handles messages received from JavaScript (WebMessageReceived).
        /// </summary>
        public void HandleWebMessage(string message)
        {
            try
            {
                // Parse the JSON message from JavaScript using simple string parsing
                // Expected format: { "type": "messageType", "data": { ... } }
                var messageType = ExtractJsonValue(message, "type");

                switch (messageType)
                {
                    case "tabSwitched":
                        {
                            var tabId = ExtractNestedJsonValue(message, "data", "tabId");
                            if (!string.IsNullOrEmpty(tabId) && _openTabs.TryGetValue(tabId, out var tab))
                            {
                                _activeTabId = tabId;
                                _currentFilePath = tab.FilePath;
                            }
                        }
                        break;

                    case "documentClicked":
                        CloseMenuDropdowns();
                        break;

                    case "closeTabRequested":
                        {
                            var tabId = ExtractNestedJsonValue(message, "data", "tabId");
                            if (!string.IsNullOrEmpty(tabId))
                            {
                                _ = CloseTab(tabId); // Fire and forget async
                            }
                        }
                        break;

                    case "tabDirtyChanged":
                        {
                            var tabId = ExtractNestedJsonValue(message, "data", "tabId");
                            var isDirtyStr = ExtractNestedJsonValue(message, "data", "isDirty");
                            var isDirty = isDirtyStr?.ToLower() == "true";
                            if (!string.IsNullOrEmpty(tabId) && _openTabs.TryGetValue(tabId, out var tab))
                            {
                                tab.IsDirty = isDirty;
                            }
                        }
                        break;

                    case "contextMenuAction":
                        {
                            var action = ExtractNestedJsonValue(message, "data", "action");
                            var tabId = ExtractNestedJsonValue(message, "data", "tabId");
                            if (!string.IsNullOrEmpty(action) && !string.IsNullOrEmpty(tabId))
                            {
                                HandleContextMenuAction(action, tabId);
                            }
                        }
                        break;

                    case "startPageAction":
                        {
                            var action = ExtractNestedJsonValue(message, "data", "action");

                            switch (action)
                            {
                                case "newFile":
                                    NewMarkdownFile();
                                    break;

                                case "openFile":
                                    OpenMarkdownFile();
                                    break;

                                case "openRecentFile":
                                    var filePath = ExtractNestedJsonValue(message, "data", "filePath");
                                    if (!string.IsNullOrEmpty(filePath))
                                    {
                                        OpenFile(filePath);
                                    }
                                    break;

                                case "removeRecentFile":
                                    var indexStr = ExtractNestedJsonValue(message, "data", "index");
                                    if (int.TryParse(indexStr, out int index))
                                    {
                                        RemoveRecentFileByIndex(index);
                                    }
                                    break;

                                case "removeMissingFiles":
                                    RemoveMissingRecentFiles();
                                    break;
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HandleWebMessage error: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts a simple string value from a JSON object.
        /// </summary>
        private string ExtractJsonValue(string json, string key)
        {
            // Simple pattern: "key":"value" or "key": "value"
            var pattern = $"\"{key}\"\\s*:\\s*\"([^\"]+)\"";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Extracts a nested string value from a JSON object (one level deep).
        /// </summary>
        private string ExtractNestedJsonValue(string json, string parentKey, string childKey)
        {
            // Find the "data" object first
            var dataPattern = $"\"{parentKey}\"\\s*:\\s*\\{{([^}}]+)\\}}";
            var dataMatch = System.Text.RegularExpressions.Regex.Match(json, dataPattern);
            if (!dataMatch.Success) return null;

            var dataContent = dataMatch.Groups[1].Value;

            // Look for string value
            var pattern = $"\"{childKey}\"\\s*:\\s*\"([^\"]+)\"";
            var match = System.Text.RegularExpressions.Regex.Match(dataContent, pattern);
            if (match.Success) return match.Groups[1].Value;

            // Look for boolean/number value (no quotes)
            pattern = $"\"{childKey}\"\\s*:\\s*([^,}}\\s]+)";
            match = System.Text.RegularExpressions.Regex.Match(dataContent, pattern);
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Handles context menu actions from JavaScript.
        /// </summary>
        private void HandleContextMenuAction(string action, string tabId)
        {
            if (!_openTabs.TryGetValue(tabId, out var tab))
            {
                return;
            }

            switch (action)
            {
                case "Close":
                    _ = CloseTab(tabId);
                    break;

                case "CloseOthers":
                    _ = CloseOtherTabs(tabId);
                    break;

                case "CloseAll":
                    _ = CloseAllTabs();
                    break;

                case "Save":
                    if (tabId == _activeTabId)
                    {
                        SaveMarkdownFile();
                    }
                    break;

                case "SaveAs":
                    if (tabId == _activeTabId)
                    {
                        SaveMarkdownFileAs();
                    }
                    break;

                case "CopyPath":
                    if (!string.IsNullOrEmpty(tab.FilePath))
                    {
                        Clipboard.SetText(tab.FilePath);
                    }
                    break;

                case "OpenContainingFolder":
                    if (!string.IsNullOrEmpty(tab.FilePath) && File.Exists(tab.FilePath))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{tab.FilePath}\"");
                    }
                    break;
            }
        }

        #endregion

        #region Tab JavaScript Communication

        /// <summary>
        /// Calls JavaScript to add a new tab.
        /// </summary>
        private async void AddTabToJs(string id, string fileName, string content, string filePath)
        {
            if (_isWebView2Ready)
            {
                try
                {
                    // Normalize content line endings
                    string normalizedContent = content.Replace("\r\n", "\n").Replace("\r", "\n");

                    // Escape strings for JavaScript
                    string escapedId = EscapeJsString(id);
                    string escapedFileName = EscapeJsString(fileName);
                    string escapedContent = EscapeJsString(normalizedContent);
                    string escapedFilePath = filePath != null ? EscapeJsString(filePath) : "null";

                    var script = filePath != null
                        ? $"addTab(\"{escapedId}\", \"{escapedFileName}\", \"{escapedContent}\", \"{escapedFilePath}\")"
                        : $"addTab(\"{escapedId}\", \"{escapedFileName}\", \"{escapedContent}\", null)";

                    await webView.ExecuteScriptAsync(script);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AddTabToJs error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Calls JavaScript to switch to a tab.
        /// </summary>
        private async void SwitchToTabInJs(string tabId)
        {
            if (_isWebView2Ready)
            {
                try
                {
                    string escapedId = EscapeJsString(tabId);
                    await webView.ExecuteScriptAsync($"switchToTab(\"{escapedId}\")");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SwitchToTabInJs error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Calls JavaScript to remove a tab.
        /// </summary>
        private async void RemoveTabFromJs(string tabId)
        {
            if (_isWebView2Ready)
            {
                try
                {
                    string escapedId = EscapeJsString(tabId);
                    await webView.ExecuteScriptAsync($"removeTab(\"{escapedId}\")");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"RemoveTabFromJs error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Calls JavaScript to update a tab's name and path.
        /// </summary>
        private async void UpdateTabInJs(string tabId, string fileName, string filePath)
        {
            if (_isWebView2Ready)
            {
                try
                {
                    string escapedId = EscapeJsString(tabId);
                    string escapedFileName = EscapeJsString(fileName);
                    string escapedFilePath = EscapeJsString(filePath);
                    await webView.ExecuteScriptAsync($"updateTab(\"{escapedId}\", \"{escapedFileName}\", \"{escapedFilePath}\")");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"UpdateTabInJs error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Calls JavaScript to set the dirty indicator on a tab.
        /// </summary>
        private async void SetDirtyIndicatorInJs(string tabId)
        {
            if (_isWebView2Ready)
            {
                try
                {
                    string escapedId = EscapeJsString(tabId);
                    await webView.ExecuteScriptAsync($"setTabDirty(\"{escapedId}\", true)");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SetDirtyIndicatorInJs error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Calls JavaScript to clear the dirty indicator on a tab.
        /// </summary>
        private async void ClearDirtyIndicatorInJs(string tabId)
        {
            if (_isWebView2Ready)
            {
                try
                {
                    string escapedId = EscapeJsString(tabId);
                    await webView.ExecuteScriptAsync($"setTabDirty(\"{escapedId}\", false)");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ClearDirtyIndicatorInJs error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Gets the content of a specific tab from JavaScript.
        /// </summary>
        private async Task<string> GetTabContentFromJs(string tabId)
        {
            if (_isWebView2Ready)
            {
                try
                {
                    string escapedId = EscapeJsString(tabId);
                    var result = await webView.ExecuteScriptAsync($"getTabContent(\"{escapedId}\")");

                    // Remove surrounding quotes from JSON string
                    if (result.StartsWith("\"") && result.EndsWith("\""))
                    {
                        result = result.Substring(1, result.Length - 2);
                        result = result.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\"", "\"");
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"GetTabContentFromJs error: {ex.Message}");
                    return null;
                }
            }
            return null;
        }

        /// <summary>
        /// Escapes a string for safe use in JavaScript.
        /// </summary>
        private string EscapeJsString(string str)
        {
            if (str == null) return "";
            return str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        #endregion

        #region Start Page

        /// <summary>
        /// Shows the Start Page tab on initialization or when requested.
        /// </summary>
        private async void ShowStartPage()
        {
            if (_isWebView2Ready)
            {
                try
                {
                    await webView.ExecuteScriptAsync("addStartPageTab()");
                    _activeTabId = "startPage";
                    await RefreshRecentFilesInStartPage();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ShowStartPage error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Gets recent files with metadata for the Start Page.
        /// </summary>
        private List<RecentFileInfo> GetRecentFilesWithMetadata()
        {
            var files = GetRecentFiles();
            var result = new List<RecentFileInfo>();

            foreach (var filePath in files)
            {
                var info = new RecentFileInfo
                {
                    Path = filePath,
                    Name = Path.GetFileName(filePath),
                    Exists = File.Exists(filePath)
                };

                if (info.Exists)
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        info.ModifiedDate = fileInfo.LastWriteTime.ToString("MMM d, yyyy");
                    }
                    catch
                    {
                        info.ModifiedDate = "";
                    }
                }
                else
                {
                    info.ModifiedDate = "File not found";
                }

                result.Add(info);
            }

            return result;
        }

        /// <summary>
        /// Sends recent files data to the JavaScript Start Page.
        /// </summary>
        private async Task RefreshRecentFilesInStartPage()
        {
            if (!_isWebView2Ready) return;

            try
            {
                var recentFiles = GetRecentFilesWithMetadata();

                // Build JSON array for JavaScript
                var jsonItems = recentFiles.Select(f =>
                    $"{{\"path\":\"{EscapeJsString(f.Path)}\",\"name\":\"{EscapeJsString(f.Name)}\",\"modifiedDate\":\"{EscapeJsString(f.ModifiedDate)}\",\"exists\":{f.Exists.ToString().ToLower()}}}"
                );
                var jsonArray = "[" + string.Join(",", jsonItems) + "]";

                await webView.ExecuteScriptAsync($"populateRecentFiles({jsonArray})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshRecentFilesInStartPage error: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes a file from the recent files list by index.
        /// </summary>
        private void RemoveRecentFileByIndex(int index)
        {
            var files = GetRecentFiles();
            if (index >= 0 && index < files.Count)
            {
                files.RemoveAt(index);
                SaveRecentFiles(files);
                _ = RefreshRecentFilesInStartPage();
            }
        }

        /// <summary>
        /// Removes all non-existent files from the recent files list.
        /// </summary>
        private void RemoveMissingRecentFiles()
        {
            var files = GetRecentFiles();
            var existingFiles = files.Where(f => File.Exists(f)).ToList();

            if (existingFiles.Count != files.Count)
            {
                SaveRecentFiles(existingFiles);
                _ = RefreshRecentFilesInStartPage();
            }
        }

        /// <summary>
        /// Menu handler to show the Start Page.
        /// </summary>
        private void menuShowStartPage_Click(object sender, EventArgs e)
        {
            ShowStartPage();
        }

        private void menuAbout_Click(object sender, EventArgs e)
        {
            ShowAboutDialog();
        }

        private async void ShowAboutDialog()
        {
            if (_isWebView2Ready)
            {
                await webView.ExecuteScriptAsync("showAboutDialog()");
            }
        }

        /// <summary>
        /// Populates the View menu with Start Page and open tabs.
        /// </summary>
        private void menuView_DropDownOpening(object sender, EventArgs e)
        {
            // Clear all items except Start Page (first item)
            while (menuView.DropDownItems.Count > 1)
            {
                menuView.DropDownItems.RemoveAt(1);
            }

            // Check Start Page if it's active
            menuShowStartPage.Checked = (_activeTabId == "startPage" || string.IsNullOrEmpty(_activeTabId));

            // Add separator if there are open tabs
            if (_openTabs.Count > 0)
            {
                menuView.DropDownItems.Add(new ToolStripSeparator());

                // Add each open tab
                foreach (var tab in _openTabs.Values)
                {
                    var displayName = tab.FileName;
                    if (tab.IsDirty)
                    {
                        displayName = "* " + displayName;
                    }

                    var item = new ToolStripMenuItem(displayName);
                    item.Tag = tab.Id;
                    item.Checked = (tab.Id == _activeTabId);
                    item.ToolTipText = tab.FilePath ?? "Unsaved file";
                    item.Click += ViewMenuItem_Click;
                    menuView.DropDownItems.Add(item);
                }
            }
        }

        /// <summary>
        /// Handles clicking a tab item in the View menu.
        /// </summary>
        private void ViewMenuItem_Click(object sender, EventArgs e)
        {
            var item = sender as ToolStripMenuItem;
            if (item?.Tag is string tabId)
            {
                SwitchToTab(tabId);
            }
        }

        #endregion

        public void RefreshContent()
        {
            // Reload if we have a current file
            if (!string.IsNullOrEmpty(_currentFilePath) && File.Exists(_currentFilePath))
            {
                string content = File.ReadAllText(_currentFilePath);
                SendContentToJs(content, _currentFilePath);
            }
        }
    }

    /// <summary>
    /// Message filter that closes menu dropdowns when clicking inside WebView2.
    /// WebView2 swallows mouse events, so we intercept them at the message level.
    /// </summary>
    internal class WebView2MenuCloseFilter : IMessageFilter
    {
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_NCLBUTTONDOWN = 0x00A1;

        private readonly Control _parent;
        private readonly Control _webView;
        private readonly MenuStrip _menuStrip;

        public WebView2MenuCloseFilter(Control parent, Control webView, MenuStrip menuStrip)
        {
            _parent = parent;
            _webView = webView;
            _menuStrip = menuStrip;
        }

        public bool PreFilterMessage(ref Message m)
        {
            // Check for mouse down messages
            if (m.Msg == WM_LBUTTONDOWN || m.Msg == WM_RBUTTONDOWN || m.Msg == WM_NCLBUTTONDOWN)
            {
                // Check if any menu dropdown is visible
                bool menuOpen = false;
                foreach (ToolStripItem item in _menuStrip.Items)
                {
                    if (item is ToolStripMenuItem menuItem && menuItem.DropDown.Visible)
                    {
                        menuOpen = true;
                        break;
                    }
                }

                if (menuOpen)
                {
                    // Check if click is inside the WebView2 control or its children
                    Control target = Control.FromHandle(m.HWnd);
                    if (IsWebViewOrChild(target))
                    {
                        // Close all menu dropdowns
                        foreach (ToolStripItem item in _menuStrip.Items)
                        {
                            if (item is ToolStripMenuItem menuItem && menuItem.DropDown.Visible)
                            {
                                menuItem.DropDown.Close();
                            }
                        }
                    }
                }
            }

            // Don't block the message, let it continue
            return false;
        }

        private bool IsWebViewOrChild(Control control)
        {
            if (control == null) return false;
            if (control == _webView) return true;

            // Check if it's a child of WebView2 (WebView2 has internal child HWNDs)
            Control parent = control.Parent;
            while (parent != null)
            {
                if (parent == _webView) return true;
                parent = parent.Parent;
            }

            // Also check by comparing handles - WebView2's internal windows won't be in the Control hierarchy
            // but will be descendants of the WebView2 HWND
            if (_webView.IsHandleCreated && control.Handle != IntPtr.Zero)
            {
                return IsChildWindow(_webView.Handle, control.Handle);
            }

            return false;
        }

        private bool IsChildWindow(IntPtr parent, IntPtr child)
        {
            IntPtr current = child;
            while (current != IntPtr.Zero)
            {
                current = GetParent(current);
                if (current == parent) return true;
            }
            return false;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);
    }
}
