using System;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Gui;

namespace ClarionMarkdownEditor
{
    /// <summary>
    /// Registers the Markdown Editor as the handler for .md files.
    /// When a user opens a .md file in the IDE, this binding creates a
    /// MarkdownEditorViewContent instead of the default text editor.
    /// </summary>
    public class MarkdownDisplayBinding : IDisplayBinding
    {
        public bool CanCreateContentForFile(string fileName)
        {
            return fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase);
        }

        public IViewContent CreateContentForFile(string fileName)
        {
            var content = new MarkdownEditorViewContent(fileName);
            content.Load(fileName);
            return content;
        }

        public bool CanCreateContentForLanguage(string languageName)
        {
            return false;
        }

        public IViewContent CreateContentForLanguage(string languageName, string content)
        {
            return null;
        }
    }
}
