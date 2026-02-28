        // Initialize Mermaid
        if (typeof mermaid !== 'undefined') {
            mermaid.initialize({ 
                startOnLoad: false, 
                theme: 'default',  // 'default' is more colorful than 'dark'
                themeVariables: {
                    primaryColor: '#4a90e2',
                    primaryTextColor: '#fff',
                    primaryBorderColor: '#2e5c8a',
                    lineColor: '#6c757d',
                    secondaryColor: '#82ca9d',
                    tertiaryColor: '#ffc658'
                },
                securityLevel: 'loose'
            });
        }

        // Minimal marked.js implementation for markdown parsing
        var marked = (function() {
            function escape(html) {
                return html.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
            }

            function parse(src) {
                var out = '';
                var lines = src.split('\n');
                var inCode = false, inList = false, listType = '';
                var codeBlock = '', codeLanguage = '', codeFence = '';

                for (var i = 0; i < lines.length; i++) {
                    var line = lines[i];

                    // Fenced code blocks (support 3+ backticks at any indentation)
                    var codeMatch = line.match(/^(\s*)(```+)(\w*)/);
                    if (codeMatch) {
                        var fence = codeMatch[2]; // Get the backticks (```, ````, etc.)
                        if (!inCode) {
                            inCode = true;
                            codeFence = fence; // Remember how many backticks opened it
                            codeLanguage = codeMatch[3]; // Get language from match
                            codeBlock = '';
                        } else if (fence === codeFence) {
                            // Only close if same number of backticks
                            // Check for mermaid diagrams
                            if (codeLanguage === 'mermaid') {
                                out += '<div class="mermaid">' + escape(codeBlock.slice(0,-1)) + '</div>\n';
                            } else {
                                out += '<pre><code class="' + (codeLanguage ? 'language-' + codeLanguage : '') + '">' + escape(codeBlock.slice(0,-1)) + '</code></pre>\n';
                            }
                            inCode = false;
                            codeBlock = '';
                            codeLanguage = '';
                            codeFence = '';
                        } else {
                            // Different number of backticks - treat as content
                            codeBlock += line + '\n';
                        }
                        continue;
                    }
                    if (inCode) { codeBlock += line + '\n'; continue; }

                    // Close list if empty line
                    if (inList && line.trim() === '') {
                        out += '</' + listType + '>\n';
                        inList = false;
                    }

                    // Headers
                    if (line.match(/^#{1,6}\s/)) {
                        var level = line.match(/^#+/)[0].length;
                        var text = line.replace(/^#+\s*/, '');
                        out += '<h' + level + '>' + inline(text) + '</h' + level + '>\n';
                        continue;
                    }

                    // Horizontal rule
                    if (line.match(/^(-{3,}|\*{3,}|_{3,})$/)) {
                        out += '<hr>\n';
                        continue;
                    }

                    // Blockquote
                    if (line.match(/^>\s?/)) {
                        out += '<blockquote>' + inline(line.replace(/^>\s?/, '')) + '</blockquote>\n';
                        continue;
                    }

                    // Unordered list
                    if (line.match(/^[\*\-\+]\s/)) {
                        if (!inList || listType !== 'ul') {
                            if (inList) out += '</' + listType + '>\n';
                            out += '<ul>\n';
                            inList = true;
                            listType = 'ul';
                        }
                        out += '<li>' + inline(line.replace(/^[\*\-\+]\s/, '')) + '</li>\n';
                        continue;
                    }

                    // Ordered list
                    if (line.match(/^\d+\.\s/)) {
                        if (!inList || listType !== 'ol') {
                            if (inList) out += '</' + listType + '>\n';
                            out += '<ol>\n';
                            inList = true;
                            listType = 'ol';
                        }
                        out += '<li>' + inline(line.replace(/^\d+\.\s/, '')) + '</li>\n';
                        continue;
                    }

                    // Paragraph
                    if (line.trim() !== '') {
                        out += '<p>' + inline(line) + '</p>\n';
                    }
                }

                if (inCode) out += '<pre><code>' + escape(codeBlock) + '</code></pre>\n';
                if (inList) out += '</' + listType + '>\n';

                return out;
            }

            function inline(text) {
                // Images
                text = text.replace(/!\[([^\]]*)\]\(([^)]+)\)/g, '<img src="$2" alt="$1">');
                // Links
                text = text.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2">$1</a>');
                // Bold
                text = text.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
                text = text.replace(/__([^_]+)__/g, '<strong>$1</strong>');
                // Italic
                text = text.replace(/\*([^*]+)\*/g, '<em>$1</em>');
                text = text.replace(/_([^_]+)_/g, '<em>$1</em>');
                // Inline code
                text = text.replace(/`([^`]+)`/g, '<code>$1</code>');
                return text;
            }

            return { parse: parse };
        })();

        var editor = document.getElementById('editor');
        var preview = document.getElementById('preview');
        var statusEl = document.getElementById('status');
        var statsEl = document.getElementById('stats');
        var editorPane = document.querySelector('.editor-pane');
        var previewPane = document.getElementById('previewPane');
        var toggleBtn = document.getElementById('toggleBtn');
        var formatToolbar = document.querySelector('.format-toolbar');
        var isDirty = false;
        var isFullscreen = false;
        var isStartPageActive = false;  // Track if Start Page is currently shown

        // ====================================
        // Tab Management System
        // ====================================
        var tabs = {};  // { tabId: { content, isDirty, filePath, fileName } }
        var activeTabId = null;
        var contextMenuTabId = null;
        var tabBar = document.getElementById('tabBar');
        var tabContextMenu = document.getElementById('tabContextMenu');

        // Find tab by file path
        function findTabByFilePath(filePath) {
            for (var id in tabs) {
                if (tabs[id].filePath === filePath) {
                    return id;
                }
            }
            return null;
        }

        // Add a new tab or switch to existing one with same file path
        function addTab(tabId, fileName, content, filePath) {
            // Check if a tab with this file path already exists
            var existingTabId = findTabByFilePath(filePath);
            if (existingTabId) {
                switchTab(existingTabId);
                return existingTabId;
            }

            // Save current tab content before adding new one
            if (activeTabId && tabs[activeTabId]) {
                tabs[activeTabId].content = editor.value;
            }

            // Create tab data
            tabs[tabId] = {
                content: content || '',
                isDirty: false,
                filePath: filePath || '',
                fileName: fileName || 'Untitled'
            };

            // Create tab element
            var tabEl = document.createElement('div');
            tabEl.className = 'tab';
            tabEl.setAttribute('data-tab-id', tabId);
            tabEl.innerHTML = '<span class="tab-dirty" style="display:none;">*</span>' +
                              '<span class="tab-title">' + escapeHtml(fileName) + '</span>' +
                              '<span class="tab-close" title="Close">&times;</span>';

            // Tab click handler
            tabEl.addEventListener('click', function(e) {
                if (e.target.classList.contains('tab-close')) {
                    // Close button clicked
                    e.stopPropagation();
                    notifyCloseTabRequested(tabId);
                } else {
                    switchTab(tabId);
                }
            });

            // Right-click context menu
            tabEl.addEventListener('contextmenu', function(e) {
                e.preventDefault();
                showTabContextMenu(tabId, e.clientX, e.clientY);
            });

            tabBar.appendChild(tabEl);

            // Switch to the new tab
            switchTab(tabId);

            return tabId;
        }

        // Switch to a specific tab
        function switchTab(tabId) {
            // Handle Start Page tab specially
            if (tabId === 'startPage') {
                switchToStartPage();
                return;
            }

            // If switching away from Start Page, hide it
            if (isStartPageActive) {
                hideStartPage();
            }

            if (!tabs[tabId]) return;

            // Save current tab content before switching
            if (activeTabId && tabs[activeTabId] && activeTabId !== tabId && !tabs[activeTabId].isStartPage) {
                tabs[activeTabId].content = editor.value;
            }

            // Update active tab visually
            var allTabs = tabBar.querySelectorAll('.tab');
            allTabs.forEach(function(tab) {
                tab.classList.remove('active');
            });
            var newActiveTab = tabBar.querySelector('[data-tab-id="' + tabId + '"]');
            if (newActiveTab) {
                newActiveTab.classList.add('active');
                // Scroll tab into view if needed
                newActiveTab.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'nearest' });
            }

            // Load the tab's content
            activeTabId = tabId;
            editor.value = tabs[tabId].content;
            isDirty = tabs[tabId].isDirty;
            updatePreview();

            // Notify C#
            notifyTabSwitched(tabId);
        }

        // Close a tab
        function closeTab(tabId) {
            if (!tabs[tabId]) return;

            // Don't allow closing the Start Page
            if (tabId === 'startPage') return;

            // Remove tab element
            var tabEl = tabBar.querySelector('[data-tab-id="' + tabId + '"]');
            if (tabEl) {
                tabEl.remove();
            }

            // Remove tab data
            delete tabs[tabId];

            // If closing the active tab, switch to another
            if (activeTabId === tabId) {
                // Get remaining tabs excluding Start Page
                var remainingTabIds = Object.keys(tabs).filter(function(id) {
                    return id !== 'startPage';
                });
                if (remainingTabIds.length > 0) {
                    switchTab(remainingTabIds[remainingTabIds.length - 1]);
                } else {
                    // No more file tabs - switch to Start Page
                    switchToStartPage();
                }
            }
        }

        // Update dirty indicator for a tab
        function updateTabDirty(tabId, dirty) {
            if (!tabs[tabId]) return;
            tabs[tabId].isDirty = dirty;

            var tabEl = tabBar.querySelector('[data-tab-id="' + tabId + '"]');
            if (tabEl) {
                var dirtyIndicator = tabEl.querySelector('.tab-dirty');
                if (dirtyIndicator) {
                    dirtyIndicator.style.display = dirty ? 'inline' : 'none';
                }
            }

            // Update global isDirty if this is active tab
            if (tabId === activeTabId) {
                isDirty = dirty;
            }
        }

        // Alias for C# compatibility
        function setTabDirty(tabId, dirty) {
            updateTabDirty(tabId, dirty);
        }

        // Get content of the active tab
        function getActiveTabContent() {
            if (activeTabId && tabs[activeTabId]) {
                // Return current editor value (which is the active tab's content)
                return editor.value();
            }
            return '';
        }

        // Get active tab ID
        function getActiveTabId() {
            return activeTabId;
        }

        // Get tab info
        function getTabInfo(tabId) {
            return tabs[tabId] || null;
        }

        // Get content of a specific tab
        function getTabContent(tabId) {
            if (tabId === activeTabId) {
                // For active tab, return current editor value
                return editor.value();
            }
            if (tabs[tabId]) {
                return tabs[tabId].content;
            }
            return '';
        }

        // Show tab context menu
        function showTabContextMenu(tabId, x, y) {
            contextMenuTabId = tabId;
            tabContextMenu.style.left = x + 'px';
            tabContextMenu.style.top = y + 'px';
            tabContextMenu.classList.add('visible');
        }

        // Hide tab context menu
        function hideTabContextMenu() {
            tabContextMenu.classList.remove('visible');
            contextMenuTabId = null;
        }

        // Handle context menu item click
        function handleContextMenuAction(action) {
            if (!contextMenuTabId) return;

            var tabId = contextMenuTabId;
            hideTabContextMenu();

            switch (action) {
                case 'close':
                    notifyCloseTabRequested(tabId);
                    break;
                case 'closeOthers':
                    notifyContextMenuAction('CloseOthers', tabId);
                    break;
                case 'closeAll':
                    notifyContextMenuAction('CloseAll', tabId);
                    break;
                case 'save':
                    notifyContextMenuAction('Save', tabId);
                    break;
                case 'saveAs':
                    notifyContextMenuAction('SaveAs', tabId);
                    break;
                case 'copyPath':
                    notifyContextMenuAction('CopyPath', tabId);
                    break;
                case 'openFolder':
                    notifyContextMenuAction('OpenContainingFolder', tabId);
                    break;
            }
        }

        // Context menu click handler
        tabContextMenu.addEventListener('click', function(e) {
            var action = e.target.getAttribute('data-action');
            if (action) {
                handleContextMenuAction(action);
            }
        });

        // Hide context menu when clicking elsewhere and notify C# to close menu dropdowns
        document.addEventListener('click', function(e) {
            if (!tabContextMenu.contains(e.target)) {
                hideTabContextMenu();
            }
            notifyDocumentClicked();
        });

        // Hide context menu on escape key
        document.addEventListener('keydown', function(e) {
            if (e.key === 'Escape') {
                hideTabContextMenu();
            }
        });

        // C# notification helpers
        function notifyTabSwitched(tabId) {
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({ type: 'tabSwitched', data: { tabId: tabId } });
            }
        }

        function notifyCloseTabRequested(tabId) {
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({ type: 'closeTabRequested', data: { tabId: tabId } });
            }
        }

        function notifyContextMenuAction(action, tabId) {
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({ type: 'contextMenuAction', data: { action: action, tabId: tabId } });
            }
        }

        function notifyDocumentClicked() {
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({ type: 'documentClicked', data: {} });
            }
        }

        // About dialog functions
        function showAboutDialog() {
            document.getElementById('aboutModal').classList.add('visible');
        }

        function hideAboutDialog() {
            document.getElementById('aboutModal').classList.remove('visible');
        }

        // Close about dialog when clicking overlay or pressing Escape
        var aboutModalEl = document.getElementById('aboutModal');
        if (aboutModalEl) {
            aboutModalEl.addEventListener('click', function(e) {
                if (e.target === this) {
                    hideAboutDialog();
                }
            });
        }

        document.addEventListener('keydown', function(e) {
            var modal = document.getElementById('aboutModal');
            if (e.key === 'Escape' && modal && modal.classList.contains('visible')) {
                hideAboutDialog();
            }
        });

        // HTML escape helper
        function escapeHtml(text) {
            var div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }

        // Update tab content without marking as dirty (for loading/saving)
        function setTabContent(tabId, content) {
            if (!tabs[tabId]) return;
            tabs[tabId].content = content;
            if (tabId === activeTabId) {
                editor.value = content;
                // Update preview without triggering dirty
                preview.innerHTML = marked.parse(content);
                if (typeof hljs !== 'undefined') {
                    var blocks = preview.querySelectorAll('pre code');
                    blocks.forEach(function(block) {
                        block.removeAttribute('data-highlighted');
                        try { hljs.highlightElement(block); } catch (err) {}
                    });
                }
                if (typeof mermaid !== 'undefined') {
                    var mermaidDivs = preview.querySelectorAll('.mermaid');
                    if (mermaidDivs.length > 0) {
                        requestAnimationFrame(function() {
                            try { mermaid.run({ nodes: mermaidDivs }); } catch (err) {}
                        });
                    }
                }
                updateStats();
            }
        }

        // Get all tabs info (for C# to query state)
        function getAllTabs() {
            var result = [];
            for (var id in tabs) {
                result.push({
                    tabId: id,
                    fileName: tabs[id].fileName,
                    filePath: tabs[id].filePath,
                    isDirty: tabs[id].isDirty
                });
            }
            return result;
        }

        // Update tab file info (e.g., after Save As)
        function updateTabInfo(tabId, fileName, filePath) {
            if (!tabs[tabId]) return;
            tabs[tabId].fileName = fileName;
            tabs[tabId].filePath = filePath;

            // Update tab element
            var tabEl = tabBar.querySelector('[data-tab-id="' + tabId + '"]');
            if (tabEl) {
                var titleEl = tabEl.querySelector('.tab-title');
                if (titleEl) {
                    titleEl.textContent = fileName;
                }
            }
        }

        // Expose tab functions to window for C# access
        window.addTab = addTab;
        window.switchTab = switchTab;
        window.switchToTab = switchTab;  // Alias for C# compatibility
        window.closeTab = closeTab;
        window.removeTab = closeTab;     // Alias for C# compatibility
        window.setTabDirty = setTabDirty;
        window.updateTabDirty = updateTabDirty;
        window.getActiveTabContent = getActiveTabContent;
        window.getActiveTabId = getActiveTabId;
        window.getTabInfo = getTabInfo;
        window.getTabContent = getTabContent;
        window.setTabContent = setTabContent;
        window.getAllTabs = getAllTabs;
        window.updateTabInfo = updateTabInfo;
        window.updateTab = updateTabInfo; // Alias for C# compatibility

        // ====================================
        // End Tab Management System
        // ====================================

        // ====================================
        // Start Page System
        // ====================================

        function initStartPage() {
            // Called after page load - nothing needed currently
        }

        function showStartPage() {
            var startPageEl = document.getElementById('startPage');
            var editorContainer = document.querySelector('.editor-container');
            var formatToolbar = document.querySelector('.format-toolbar');

            if (startPageEl && editorContainer) {
                startPageEl.style.display = 'flex';
                editorContainer.style.display = 'none';
                if (formatToolbar) formatToolbar.style.display = 'none';
                isStartPageActive = true;
            }
        }

        function hideStartPage() {
            var startPageEl = document.getElementById('startPage');
            var editorContainer = document.querySelector('.editor-container');
            var formatToolbar = document.querySelector('.format-toolbar');

            if (startPageEl && editorContainer) {
                startPageEl.style.display = 'none';
                editorContainer.style.display = 'flex';
                if (formatToolbar) formatToolbar.style.display = 'flex';
                isStartPageActive = false;
            }
        }

        function addStartPageTab() {
            // Check if Start Page tab already exists
            if (tabs['startPage']) {
                switchToStartPage();
                return;
            }

            // Create Start Page tab data
            tabs['startPage'] = {
                content: '',
                isDirty: false,
                filePath: '',
                fileName: 'Start Page',
                isStartPage: true
            };

            // Create tab element (no close button - Start Page cannot be closed)
            var tabEl = document.createElement('div');
            tabEl.className = 'tab';
            tabEl.setAttribute('data-tab-id', 'startPage');
            tabEl.innerHTML = '<span class="tab-title">Start Page</span>';

            // Tab click handler
            tabEl.addEventListener('click', function(e) {
                switchToStartPage();
            });

            // Insert at beginning of tab bar
            var tabBarEl = document.getElementById('tabBar');
            if (tabBarEl.firstChild) {
                tabBarEl.insertBefore(tabEl, tabBarEl.firstChild);
            } else {
                tabBarEl.appendChild(tabEl);
            }

            switchToStartPage();
        }

        function switchToStartPage() {
            // Save current tab content before switching
            if (activeTabId && tabs[activeTabId] && !tabs[activeTabId].isStartPage) {
                tabs[activeTabId].content = editor.value;
            }

            // Update active tab visually
            var tabBarEl = document.getElementById('tabBar');
            var allTabs = tabBarEl.querySelectorAll('.tab');
            allTabs.forEach(function(tab) {
                tab.classList.remove('active');
            });
            var startPageTab = tabBarEl.querySelector('[data-tab-id="startPage"]');
            if (startPageTab) {
                startPageTab.classList.add('active');
                startPageTab.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'nearest' });
            }

            activeTabId = 'startPage';
            showStartPage();
        }

        function populateRecentFiles(recentFiles) {
            var listEl = document.getElementById('recentFilesList');
            var emptyEl = document.getElementById('recentEmpty');

            if (!listEl) return;

            listEl.innerHTML = '';

            if (!recentFiles || recentFiles.length === 0) {
                listEl.style.display = 'none';
                if (emptyEl) emptyEl.style.display = 'block';
                return;
            }

            listEl.style.display = 'flex';
            if (emptyEl) emptyEl.style.display = 'none';

            recentFiles.forEach(function(file, index) {
                var itemEl = document.createElement('div');
                itemEl.className = 'recent-file-item';
                itemEl.setAttribute('data-file-path', file.path);

                var existsClass = file.exists ? '' : ' style="opacity: 0.5;"';

                itemEl.innerHTML =
                    '<div class="recent-file-info"' + existsClass + '>' +
                        '<div class="recent-file-name">' + escapeHtml(file.name) + '</div>' +
                        '<div class="recent-file-path">' + escapeHtml(file.path) + '</div>' +
                    '</div>' +
                    '<div class="recent-file-date">' + escapeHtml(file.modifiedDate || '') + '</div>' +
                    '<button class="recent-file-remove" title="Remove from list">&times;</button>';

                // Click to open file
                itemEl.addEventListener('click', function(e) {
                    if (!e.target.classList.contains('recent-file-remove')) {
                        startPageOpenRecentFile(file.path);
                    }
                });

                // Remove button click
                var removeBtn = itemEl.querySelector('.recent-file-remove');
                removeBtn.addEventListener('click', function(e) {
                    e.stopPropagation();
                    startPageRemoveRecentFile(index);
                });

                listEl.appendChild(itemEl);
            });
        }

        // Start Page action handlers
        function startPageNewFile() {
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({ type: 'startPageAction', data: { action: 'newFile' } });
            }
        }

        function startPageOpenFile() {
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({ type: 'startPageAction', data: { action: 'openFile' } });
            }
        }

        function startPageOpenRecentFile(filePath) {
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({
                    type: 'startPageAction',
                    data: { action: 'openRecentFile', filePath: filePath }
                });
            }
        }

        function startPageRemoveRecentFile(index) {
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({
                    type: 'startPageAction',
                    data: { action: 'removeRecentFile', index: index.toString() }
                });
            }
        }

        function startPageRemoveMissingFiles() {
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({
                    type: 'startPageAction',
                    data: { action: 'removeMissingFiles' }
                });
            }
        }

        // Expose Start Page functions to window for C# access
        window.addStartPageTab = addStartPageTab;
        window.showStartPage = showStartPage;
        window.hideStartPage = hideStartPage;
        window.populateRecentFiles = populateRecentFiles;
        window.switchToStartPage = switchToStartPage;
        window.initStartPage = initStartPage;
        // ====================================
        // End Start Page System
        // ====================================

        // Debouncing for preview updates to prevent system lockup
        var updateTimeout = null;
        var renderGeneration = 0;
        var DEBOUNCE_MS = 150;

        function debouncedUpdatePreview() {
            if (updateTimeout) {
                clearTimeout(updateTimeout);
            }
            updateTimeout = setTimeout(function() {
                updatePreview();
                // Mark dirty only on actual user edits (not tab switches or loads)
                isDirty = true;
                if (activeTabId && tabs[activeTabId]) {
                    tabs[activeTabId].isDirty = true;
                    var tabEl = tabBar.querySelector('[data-tab-id="' + activeTabId + '"]');
                    if (tabEl) {
                        var dirtyIndicator = tabEl.querySelector('.tab-dirty');
                        if (dirtyIndicator) {
                            dirtyIndicator.style.display = 'inline';
                        }
                    }
                    if (window.chrome && window.chrome.webview) {
                        window.chrome.webview.postMessage({ type: 'contentChanged', tabId: activeTabId });
                    }
                }
            }, DEBOUNCE_MS);
        }

        function toggleFullscreen() {
            isFullscreen = !isFullscreen;
            if (isFullscreen) {
                editorPane.classList.add('hidden');
                previewPane.classList.add('fullscreen');
                formatToolbar.classList.add('hidden');
                toggleBtn.textContent = 'Split';
                toggleBtn.title = 'Show editor and preview side by side';
            } else {
                editorPane.classList.remove('hidden');
                previewPane.classList.remove('fullscreen');
                formatToolbar.classList.remove('hidden');
                toggleBtn.textContent = 'Expand';
                toggleBtn.title = 'Toggle fullscreen preview';
            }
        }

        var isHorizontalSplit = false;
        function toggleSplitDirection() {
            isHorizontalSplit = !isHorizontalSplit;
            var editorContainer = document.querySelector('.editor-container');
            var splitBtn = document.getElementById('splitBtn');

            if (isHorizontalSplit) {
                editorContainer.classList.add('horizontal-split');
                splitBtn.textContent = '⬍ Horizontal';
                splitBtn.title = 'Switch to vertical split (side by side)';
            } else {
                editorContainer.classList.remove('horizontal-split');
                splitBtn.textContent = '⬌ Vertical';
                splitBtn.title = 'Switch to horizontal split (stacked)';
            }
        }

        function updatePreview() {
            var currentGeneration = ++renderGeneration;
            var text = editor.value;
            preview.innerHTML = marked.parse(text);

            // Apply syntax highlighting to all code blocks
            var statusMsg = 'Ready';
            if (typeof hljs !== 'undefined') {
                var blocks = preview.querySelectorAll('pre code');
                if (blocks.length > 0) {
                    blocks.forEach(function(block) {
                        // Remove any existing highlighting
                        block.removeAttribute('data-highlighted');
                        try {
                            hljs.highlightElement(block);
                            statusMsg = 'Highlighted ' + blocks.length + ' code block(s)';
                        } catch (err) {
                            statusMsg = 'Highlight error: ' + err.message;
                        }
                    });
                }
            } else {
                statusMsg = 'WARNING: hljs not defined!';
            }

            // Render Mermaid diagrams asynchronously to prevent blocking
            if (typeof mermaid !== 'undefined') {
                var mermaidDivs = preview.querySelectorAll('.mermaid');
                if (mermaidDivs.length > 0) {
                    var diagramCount = mermaidDivs.length;
                    // Use requestAnimationFrame to avoid blocking the UI thread
                    requestAnimationFrame(function() {
                        // Cancel if a newer render has started
                        if (currentGeneration !== renderGeneration) return;
                        try {
                            mermaid.run({
                                nodes: mermaidDivs
                            });
                        } catch (err) {
                            console.error('Mermaid error:', err);
                        }
                    });
                    statusMsg += ' | ' + diagramCount + ' diagram(s)';
                }
            }

            setStatus(statusMsg);
            updateStats();
        }

        function updateStats() {
            var text = editor.value;
            var lines = text.split('\n').length;
            var words = text.trim() ? text.trim().split(/\s+/).length : 0;
            statsEl.textContent = 'Lines: ' + lines + ' | Words: ' + words;
        }
        
        // Scroll synchronization
        var syncingScroll = false;
        var scrollSyncEnabled = true; // Default enabled
        
        function syncScroll(source, target) {
            if (syncingScroll || !scrollSyncEnabled) return;

            syncingScroll = true;
            var percentage = source.scrollTop / (source.scrollHeight - source.clientHeight);
            if (isNaN(percentage)) percentage = 0;
            target.scrollTop = percentage * (target.scrollHeight - target.clientHeight);

            setTimeout(function() {
                syncingScroll = false;
            }, 10);
        }
        
        function toggleScrollSync() {
            var checkbox = document.getElementById('syncScrollCheckbox');
            scrollSyncEnabled = checkbox.checked;
            
            // When enabling sync, align editor to preview's current position
            if (scrollSyncEnabled) {
                syncScroll(preview, editor);
            }
        }
        
        // Sync scroll handlers - must be set after page load (WebView2 timing issue)
        setTimeout(function() {
            if (editor) {
                editor.onscroll = function() {
                    if (scrollSyncEnabled) {
                        syncScroll(editor, preview);
                    }
                };
            }
            if (preview) {
                preview.onscroll = function() {
                    if (scrollSyncEnabled) {
                        syncScroll(preview, editor);
                    }
                };
            }
        }, 500);
        
        function toggleDarkMode() {
            document.body.classList.toggle('dark-mode');
            var isDark = document.body.classList.contains('dark-mode');

            // Update button text
            var btn = document.getElementById('darkModeBtn');
            if (btn) {
                btn.textContent = isDark ? '☀️ Light Mode' : '🌓 Dark Mode';
            }

            // Notify C# (though we're now handling it in JS)
            if (window.external && window.external.SetDarkMode) {
                window.external.SetDarkMode(isDark);
            }
        }
        
        function setDarkMode(enabled) {
            // Convert string to boolean if needed
            if (typeof enabled === 'string') {
                enabled = enabled === 'true';
            }
            
            if (enabled) {
                document.body.classList.add('dark-mode');
            } else {
                document.body.classList.remove('dark-mode');
            }
            
            // Update button text
            var btn = document.getElementById('darkModeBtn');
            if (btn) {
                btn.textContent = enabled ? '☀️ Light' : '🌓 Dark';
            }
        }

        function setStatus(msg) {
            statusEl.textContent = msg;
            setTimeout(function() { statusEl.textContent = 'Ready'; }, 3000);
        }

        // Called from C# to get editor content
        function getEditorContent() {
            return editor.value;
        }

        // Called from C# to load content (legacy - for backward compatibility)
        // Prefer using addTab for multi-file support
        function loadContent(content, filename) {
            editor.value = content;
            setStatus('Loading: ' + filename);

            // Update preview without marking as dirty (this is a load, not an edit)
            var text = editor.value;
            preview.innerHTML = marked.parse(text);

            // Apply syntax highlighting
            if (typeof hljs !== 'undefined') {
                var blocks = preview.querySelectorAll('pre code');
                blocks.forEach(function(block) {
                    block.removeAttribute('data-highlighted');
                    try { hljs.highlightElement(block); } catch (err) {}
                });
            }

            // Render Mermaid diagrams
            if (typeof mermaid !== 'undefined') {
                var mermaidDivs = preview.querySelectorAll('.mermaid');
                if (mermaidDivs.length > 0) {
                    requestAnimationFrame(function() {
                        try { mermaid.run({ nodes: mermaidDivs }); } catch (err) {}
                    });
                }
            }

            updateStats();
            isDirty = false;
        }

        // Called from C# for messages
        function receiveMessage(type, data) {
            if (type === 'fileSaved') {
                isDirty = false;
                setStatus('Saved: ' + data);
            }
        }

        // Formatting helpers
        function insertBold() { wrapSelection('**', '**'); }
        function insertItalic() { wrapSelection('*', '*'); }
        function insertCode() { wrapSelection('`', '`'); }
        function insertCodeBlock() { wrapSelection('\n```\n', '\n```\n'); }
        function insertH1() { insertAtLineStart('# '); }
        function insertH2() { insertAtLineStart('## '); }
        function insertH3() { insertAtLineStart('### '); }
        function insertBulletList() { insertAtLineStart('- '); }
        function insertNumberList() { insertAtLineStart('1. '); }
        function insertQuote() { insertAtLineStart('> '); }
        function insertHr() { insertText('\n---\n'); }

        function insertLink() {
            var sel = getSelection();
            var url = prompt('Enter URL:', 'https://');
            if (url) {
                insertText('[' + (sel || 'link text') + '](' + url + ')');
            }
        }

        function insertImage() {
            var url = prompt('Enter image URL:', 'https://');
            if (url) {
                var alt = prompt('Enter alt text:', 'image');
                insertText('![' + (alt || 'image') + '](' + url + ')');
            }
        }

        function insertTable() {
            var table = '\n| Header 1 | Header 2 | Header 3 |\n|----------|----------|----------|\n| Cell 1   | Cell 2   | Cell 3   |\n| Cell 4   | Cell 5   | Cell 6   |\n';
            insertText(table);
        }

        function wrapSelection(before, after) {
            var start = editor.selectionStart;
            var end = editor.selectionEnd;
            var text = editor.value;
            var sel = text.substring(start, end) || 'text';
            editor.value = text.substring(0, start) + before + sel + after + text.substring(end);
            editor.selectionStart = start + before.length;
            editor.selectionEnd = start + before.length + sel.length;
            editor.focus();
            updatePreview();
        }

        function insertAtLineStart(prefix) {
            var start = editor.selectionStart;
            var text = editor.value;
            // Find line start
            var lineStart = text.lastIndexOf('\n', start - 1) + 1;
            editor.value = text.substring(0, lineStart) + prefix + text.substring(lineStart);
            editor.selectionStart = editor.selectionEnd = start + prefix.length;
            editor.focus();
            updatePreview();
        }

        function getSelection() {
            return editor.value.substring(editor.selectionStart, editor.selectionEnd);
        }

        function insertText(text) {
            var start = editor.selectionStart;
            var end = editor.selectionEnd;
            var val = editor.value;
            editor.value = val.substring(0, start) + text + val.substring(end);
            editor.selectionStart = editor.selectionEnd = start + text.length;
            editor.focus();
            updatePreview();
        }

        // Keyboard shortcuts
        editor.addEventListener('keydown', function(e) {
            if (e.ctrlKey) {
                if (e.key === 'b' || e.key === 'B') { e.preventDefault(); insertBold(); }
                else if (e.key === 'i' || e.key === 'I') { e.preventDefault(); insertItalic(); }
                else if (e.key === 's' || e.key === 'S') {
                    e.preventDefault();
                    if (window.chrome && window.chrome.webview) {
                        window.chrome.webview.postMessage({ type: 'saveRequested' });
                    }
                }
            }
        });

        // Initialize
        updatePreview();
