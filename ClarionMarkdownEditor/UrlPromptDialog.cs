using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClarionMarkdownEditor
{
    /// <summary>
    /// Small modal dialog that prompts the user for a single URL.
    /// Returns the trimmed entered URL on OK, or null on Cancel.
    /// </summary>
    internal static class UrlPromptDialog
    {
        public static string Show(IWin32Window owner, string defaultText = null)
        {
            using (var form = new Form())
            {
                form.Text = "Open Markdown from URL";
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.StartPosition = FormStartPosition.CenterParent;
                form.ClientSize = new Size(500, 130);

                var label = new Label
                {
                    AutoSize = true,
                    Location = new Point(12, 12),
                    Text = "Enter a Markdown URL or GitHub repo URL:"
                };
                form.Controls.Add(label);

                var textBox = new TextBox
                {
                    Location = new Point(12, 40),
                    Width = form.ClientSize.Width - 24,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                    Text = defaultText ?? TryClipboardUrl() ?? string.Empty
                };
                form.Controls.Add(textBox);

                var hint = new Label
                {
                    AutoSize = true,
                    ForeColor = SystemColors.GrayText,
                    Location = new Point(12, 68),
                    Text = "Supports raw URLs, github.com repo/blob URLs."
                };
                form.Controls.Add(hint);

                var okButton = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Location = new Point(form.ClientSize.Width - 178, form.ClientSize.Height - 34),
                    Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                    Enabled = !string.IsNullOrWhiteSpace(textBox.Text)
                };
                var cancelButton = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(form.ClientSize.Width - 90, form.ClientSize.Height - 34),
                    Anchor = AnchorStyles.Right | AnchorStyles.Bottom
                };
                form.Controls.Add(okButton);
                form.Controls.Add(cancelButton);
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                textBox.TextChanged += (s, e) => okButton.Enabled = !string.IsNullOrWhiteSpace(textBox.Text);
                textBox.SelectAll();

                return form.ShowDialog(owner) == DialogResult.OK
                    ? textBox.Text.Trim()
                    : null;
            }
        }

        private static string TryClipboardUrl()
        {
            try
            {
                if (!Clipboard.ContainsText()) return null;
                var t = Clipboard.GetText()?.Trim();
                if (string.IsNullOrEmpty(t)) return null;
                if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    return t;
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
