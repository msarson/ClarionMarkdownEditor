using System;
using System.Windows.Forms;
using ICSharpCode.Core;
using ICSharpCode.SharpDevelop.Gui;

namespace ClarionMarkdownEditor
{
    /// <summary>
    /// Tools menu command: prompt for a URL and open the resulting Markdown
    /// in the editor pad. Thin wrapper around MarkdownEditorApi.OpenUrl.
    /// </summary>
    public class OpenMarkdownFromUrlCommand : AbstractMenuCommand
    {
        public override void Run()
        {
            try
            {
                IWin32Window owner = Form.ActiveForm;
                var url = UrlPromptDialog.Show(owner);
                if (string.IsNullOrWhiteSpace(url)) return;

                MarkdownEditorApi.OpenUrl(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error opening URL: " + ex.Message,
                    "Markdown Editor",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
