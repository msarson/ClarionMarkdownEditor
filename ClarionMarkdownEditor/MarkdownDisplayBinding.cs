using System;
using System.IO;
using System.Linq;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Gui;

namespace ClarionMarkdownEditor
{
    /// <summary>
    /// Registers the Markdown Editor as the handler for .md files.
    /// When a user opens a .md file in the IDE, this binding routes it into
    /// the existing Markdown Editor instance (as a new tab) rather than
    /// creating a separate editor per file.
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
            // Reuse the existing Markdown Editor instance if one is already open
            var existing = WorkbenchSingleton.Workbench.ViewContentCollection
                .OfType<MarkdownEditorViewContent>()
                .FirstOrDefault();

            if (existing != null)
            {
                existing.Load(fileName);
                return existing;
            }

            // No existing instance — create a new one
            var content = new MarkdownEditorViewContent();
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
