using System;
using System.Linq;
using ICSharpCode.Core;
using ICSharpCode.SharpDevelop.Gui;

namespace ClarionMarkdownEditor
{
    /// <summary>
    /// Command to show the MarkdownEditor as a main window (document view).
    /// This allows the editor to be docked in the main document area.
    /// </summary>
    public class ShowMarkdownEditorWindowCommand : AbstractMenuCommand
    {
        public override void Run()
        {
            try
            {
                var workbench = WorkbenchSingleton.Workbench;
                if (workbench == null) return;

                // If an instance already exists, switch to it instead of creating a new one
                var existing = workbench.ViewContentCollection
                    .OfType<MarkdownEditorViewContent>()
                    .FirstOrDefault();

                if (existing != null)
                {
                    existing.WorkbenchWindow?.SelectWindow();
                    return;
                }

                // No existing instance — create one and show it
                var viewContent = new MarkdownEditorViewContent();
                
                var showViewMethod = workbench.GetType().GetMethod("ShowView", 
                    new Type[] { typeof(IViewContent) });
                
                if (showViewMethod != null)
                {
                    showViewMethod.Invoke(workbench, new object[] { viewContent });
                }
                else
                {
                    var viewContentsProp = workbench.GetType().GetProperty("ViewContentCollection");
                    if (viewContentsProp != null)
                    {
                        var collection = viewContentsProp.GetValue(workbench, null);
                        if (collection != null)
                        {
                            var addMethod = collection.GetType().GetMethod("Add", 
                                new Type[] { typeof(IViewContent) });
                            addMethod?.Invoke(collection, new object[] { viewContent });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    "Error opening Markdown Editor window: " + ex.Message,
                    "Markdown Editor",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
    }
}
