using System;
using System.Linq;
using System.Windows.Forms;
using ICSharpCode.SharpDevelop.Gui;

namespace ClarionMarkdownEditor
{
    /// <summary>
    /// Public entry points for cross-addin invocation. Other Clarion IDE addins
    /// can call these via reflection without taking a hard reference on
    /// ClarionMarkdownEditor.dll:
    ///
    /// <code>
    /// var t = Type.GetType("ClarionMarkdownEditor.MarkdownEditorApi, ClarionMarkdownEditor");
    /// t?.GetMethod("OpenUrl")?.Invoke(null, new object[] { url });
    /// </code>
    /// </summary>
    public static class MarkdownEditorApi
    {
        /// <summary>
        /// Opens a Markdown document from a URL in the editor's document window,
        /// reusing an existing window if one is open. Fire-and-forget: errors
        /// surface as message boxes inside the editor, not exceptions back to
        /// the caller.
        /// Supported URL forms:
        /// - raw.githubusercontent.com URLs (passed through)
        /// - github.com/owner/repo/blob/branch/path.md (rewritten to raw)
        /// - github.com/owner/repo (probes main, falls back to master)
        /// </summary>
        public static void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            try
            {
                var control = EnsureWindowVisible();
                if (control == null) return;

                // LoadUrlAsync is awaitable but we run it fire-and-forget; the
                // method surfaces failures via MessageBox internally.
                _ = control.LoadUrlAsync(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error opening URL: " + ex.Message,
                    "Markdown Editor",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Opens (or refocuses) a MarkdownEditorViewContent document window and
        // returns its underlying control. Mirrors ShowMarkdownEditorWindowCommand
        // so new entry points land in the full-page editor rather than the
        // legacy dockable pad.
        private static MarkdownEditorControl EnsureWindowVisible()
        {
            var workbench = WorkbenchSingleton.Workbench;
            if (workbench == null) return null;

            var existing = workbench.ViewContentCollection
                .OfType<MarkdownEditorViewContent>()
                .FirstOrDefault();

            if (existing != null)
            {
                existing.WorkbenchWindow?.SelectWindow();
                return existing.Control as MarkdownEditorControl;
            }

            var viewContent = new MarkdownEditorViewContent();
            var showView = workbench.GetType().GetMethod("ShowView", new[] { typeof(IViewContent) });
            if (showView != null)
            {
                showView.Invoke(workbench, new object[] { viewContent });
            }
            else
            {
                var viewContentsProp = workbench.GetType().GetProperty("ViewContentCollection");
                var collection = viewContentsProp?.GetValue(workbench, null);
                var addMethod = collection?.GetType().GetMethod("Add", new[] { typeof(IViewContent) });
                addMethod?.Invoke(collection, new object[] { viewContent });
            }

            return viewContent.Control as MarkdownEditorControl;
        }
    }
}
