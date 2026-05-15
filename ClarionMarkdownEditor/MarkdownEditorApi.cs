using System;
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
        /// Opens a Markdown document from a URL in the editor pad, bringing
        /// the pad to the front. Fire-and-forget: errors surface as message
        /// boxes inside the editor, not exceptions back to the caller.
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
                var control = EnsurePadVisible();
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

        private static MarkdownEditorControl EnsurePadVisible()
        {
            var workbench = WorkbenchSingleton.Workbench;
            if (workbench == null) return null;

            // GetPad / BringPadToFront via reflection — same pattern the existing
            // ShowMarkdownEditorCommand uses for IDE-version compatibility.
            var getPad = workbench.GetType().GetMethod("GetPad", new[] { typeof(Type) });
            var padDescriptor = getPad?.Invoke(workbench, new object[] { typeof(MarkdownEditorPad) });
            if (padDescriptor == null) return null;

            padDescriptor.GetType().GetMethod("BringPadToFront")?.Invoke(padDescriptor, null);

            // PadContent gives us the actual MarkdownEditorPad instance; .Control is the control.
            var padContent = padDescriptor.GetType().GetProperty("PadContent")?.GetValue(padDescriptor, null)
                             as MarkdownEditorPad;
            return padContent?.Control as MarkdownEditorControl;
        }
    }
}
