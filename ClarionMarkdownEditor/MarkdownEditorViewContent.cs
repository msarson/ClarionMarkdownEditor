using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ICSharpCode.SharpDevelop.Gui;

namespace ClarionMarkdownEditor
{
    /// <summary>
    /// ViewContent for the Markdown Editor: the single editor window, opened as a document tab
    /// in the main document area like source files. One instance holds all open Markdown tabs;
    /// it can be dragged to sit beside other documents or floated like any document tab.
    /// </summary>
    public class MarkdownEditorViewContent : AbstractViewContent
    {
        private MarkdownEditorControl _control;
        private string _fileName;
        private bool _isDirty;

        public MarkdownEditorViewContent()
        {
            _control = new MarkdownEditorControl();
            TitleName = "Markdown Editor";
        }

        public MarkdownEditorViewContent(string fileName) : this()
        {
            if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
            {
                _fileName = fileName;
            }
        }

        /// <summary>
        /// The open Markdown Editor instance, or null when there is none.
        /// Skips an instance that is still listed in ViewContentCollection after its window
        /// was closed and its control disposed. Reusing that one makes every later .md open fail
        /// with the IDE's "Cannot load file ... check the file permission", and makes
        /// View > Tools > Markdown Editor do nothing, until the IDE is restarted.
        /// </summary>
        internal static MarkdownEditorViewContent FindOpenInstance()
        {
            var workbench = WorkbenchSingleton.Workbench;
            if (workbench == null) return null;

            return workbench.ViewContentCollection
                .OfType<MarkdownEditorViewContent>()
                .FirstOrDefault(v => v.IsAlive);
        }

        /// <summary>True while this instance has a window and an undisposed control.</summary>
        internal bool IsAlive => _control != null && !_control.IsDisposed && WorkbenchWindow != null;

        /// <summary>
        /// Gets the main control for this view content.
        /// </summary>
        public override Control Control
        {
            get { return _control; }
        }

        /// <summary>
        /// Gets or sets whether this content has unsaved changes.
        /// </summary>
        public override bool IsDirty
        {
            get { return _isDirty; }
            set
            {
                if (_isDirty != value)
                {
                    _isDirty = value;
                    OnDirtyChanged(EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets or sets the file name associated with this content.
        /// </summary>
        public override string FileName
        {
            get { return _fileName; }
            set
            {
                if (_fileName != value)
                {
                    _fileName = value;
                    TitleName = "Markdown Editor";
                    OnFileNameChanged(EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Loads content from a file.
        /// </summary>
        public override void Load(string fileName)
        {
            _fileName = fileName;
            TitleName = "Markdown Editor";
            if (_control != null && File.Exists(fileName))
                _control.LoadFile(fileName);
            OnFileNameChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Saves content to the current file.
        /// </summary>
        public override void Save(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName))
            {
                _fileName = fileName;
                TitleName = "Markdown Editor";
                IsDirty = false;
                OnFileNameChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Disposes of the control.
        /// </summary>
        public override void Dispose()
        {
            if (_control != null)
            {
                _control.Dispose();
                _control = null;
            }
            base.Dispose();
        }

        /// <summary>
        /// Refreshes the content.
        /// </summary>
        public override void RedrawContent()
        {
            _control?.RefreshContent();
        }

        protected override void OnDirtyChanged(EventArgs e)
        {
            base.OnDirtyChanged(e);
        }

        protected override void OnFileNameChanged(EventArgs e)
        {
            base.OnFileNameChanged(e);
        }
    }
}
