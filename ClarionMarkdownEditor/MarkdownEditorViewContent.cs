using System;
using System.IO;
using System.Windows.Forms;
using ICSharpCode.SharpDevelop.Gui;

namespace ClarionMarkdownEditor
{
    /// <summary>
    /// ViewContent for Markdown Editor that allows docking in the main document area.
    /// This enables the editor to be opened as a main window (like source files)
    /// rather than just as a tool pad.
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
        /// Returns true if this editor instance has the given file open as a tab.
        /// </summary>
        public bool HasFileOpen(string filePath)
        {
            return _control?.HasFileOpen(filePath) ?? false;
        }

        /// <summary>
        /// Switches the internal editor to the tab for the given file and activates this window.
        /// </summary>
        public void SwitchToFile(string filePath)
        {
            _control?.SwitchToFileTab(filePath);
            WorkbenchWindow?.SelectWindow();
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

        /// <summary>
        /// Event raised when dirty state changes.
        /// </summary>
        protected virtual void OnDirtyChanged(EventArgs e)
        {
            // Notify the workbench that dirty state has changed
        }

        /// <summary>
        /// Event raised when file name changes.
        /// </summary>
        protected virtual void OnFileNameChanged(EventArgs e)
        {
            // Notify the workbench that file name has changed
        }
    }
}
