namespace ClarionMarkdownEditor
{
    partial class MarkdownEditorControl
    {
        private System.ComponentModel.IContainer components = null;
        private Microsoft.Web.WebView2.WinForms.WebView2 webView;
        private System.Windows.Forms.MenuStrip menuStrip;
        private System.Windows.Forms.ToolStripMenuItem menuFile;
        private System.Windows.Forms.ToolStripMenuItem menuNew;
        private System.Windows.Forms.ToolStripMenuItem menuOpen;
        private System.Windows.Forms.ToolStripMenuItem menuRecent;
        private System.Windows.Forms.ToolStripMenuItem menuSave;
        private System.Windows.Forms.ToolStripMenuItem menuSaveAs;
        private System.Windows.Forms.ToolStripMenuItem menuAbout;
        private System.Windows.Forms.ToolStripMenuItem menuView;
        private System.Windows.Forms.ToolStripMenuItem menuShowStartPage;
        private System.Windows.Forms.ToolStripSeparator menuViewSeparator;
        private System.Windows.Forms.ToolStripMenuItem menuDarkMode;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                OnCustomDispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        partial void OnCustomDispose();

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.menuStrip = new System.Windows.Forms.MenuStrip();
            this.menuFile = new System.Windows.Forms.ToolStripMenuItem();
            this.menuNew = new System.Windows.Forms.ToolStripMenuItem();
            this.menuOpen = new System.Windows.Forms.ToolStripMenuItem();
            this.menuRecent = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSave = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSaveAs = new System.Windows.Forms.ToolStripMenuItem();
            this.menuAbout = new System.Windows.Forms.ToolStripMenuItem();
            this.menuView = new System.Windows.Forms.ToolStripMenuItem();
            this.menuShowStartPage = new System.Windows.Forms.ToolStripMenuItem();
            this.menuViewSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.menuDarkMode = new System.Windows.Forms.ToolStripMenuItem();
            this.webView = new Microsoft.Web.WebView2.WinForms.WebView2();
            ((System.ComponentModel.ISupportInitialize)(this.webView)).BeginInit();
            this.menuStrip.SuspendLayout();
            this.SuspendLayout();
            //
            // menuStrip
            //
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.menuFile,
                this.menuView
            });
            this.menuStrip.Location = new System.Drawing.Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Size = new System.Drawing.Size(600, 24);
            this.menuStrip.TabIndex = 0;
            //
            // menuFile
            //
            this.menuFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.menuNew,
                this.menuOpen,
                this.menuRecent,
                new System.Windows.Forms.ToolStripSeparator(),
                this.menuSave,
                this.menuSaveAs,
                new System.Windows.Forms.ToolStripSeparator(),
                this.menuAbout
            });
            this.menuFile.Name = "menuFile";
            this.menuFile.Size = new System.Drawing.Size(37, 20);
            this.menuFile.Text = "&File";
            //
            // menuNew
            //
            this.menuNew.Name = "menuNew";
            this.menuNew.Size = new System.Drawing.Size(180, 22);
            this.menuNew.Text = "&New";
            this.menuNew.Click += new System.EventHandler(this.btnNew_Click);
            //
            // menuOpen
            //
            this.menuOpen.Name = "menuOpen";
            this.menuOpen.Size = new System.Drawing.Size(180, 22);
            this.menuOpen.Text = "&Open...";
            this.menuOpen.Click += new System.EventHandler(this.btnOpen_Click);
            //
            // menuRecent
            //
            this.menuRecent.Name = "menuRecent";
            this.menuRecent.Size = new System.Drawing.Size(180, 22);
            this.menuRecent.Text = "&Recent Files";
            this.menuRecent.DropDownOpening += new System.EventHandler(this.menuRecent_DropDownOpening);
            //
            // menuSave
            //
            this.menuSave.Name = "menuSave";
            this.menuSave.Size = new System.Drawing.Size(180, 22);
            this.menuSave.Text = "&Save";
            this.menuSave.Click += new System.EventHandler(this.btnSave_Click);
            //
            // menuSaveAs
            //
            this.menuSaveAs.Name = "menuSaveAs";
            this.menuSaveAs.Size = new System.Drawing.Size(180, 22);
            this.menuSaveAs.Text = "Save &As...";
            this.menuSaveAs.Click += new System.EventHandler(this.btnSaveAs_Click);
            //
            // menuAbout
            //
            this.menuAbout.Name = "menuAbout";
            this.menuAbout.Size = new System.Drawing.Size(180, 22);
            this.menuAbout.Text = "&About...";
            this.menuAbout.Click += new System.EventHandler(this.menuAbout_Click);
            //
            // menuView
            //
            this.menuView.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.menuShowStartPage,
                this.menuViewSeparator,
                this.menuDarkMode});
            this.menuView.Name = "menuView";
            this.menuView.Size = new System.Drawing.Size(44, 20);
            this.menuView.Text = "&View";
            this.menuView.DropDownOpening += new System.EventHandler(this.menuView_DropDownOpening);
            //
            // menuShowStartPage
            //
            this.menuShowStartPage.Name = "menuShowStartPage";
            this.menuShowStartPage.Size = new System.Drawing.Size(180, 22);
            this.menuShowStartPage.Text = "Start Page";
            this.menuShowStartPage.Click += new System.EventHandler(this.menuShowStartPage_Click);
            // 
            // menuViewSeparator
            // 
            this.menuViewSeparator.Name = "menuViewSeparator";
            this.menuViewSeparator.Size = new System.Drawing.Size(177, 6);
            // 
            // menuDarkMode
            // 
            this.menuDarkMode.Name = "menuDarkMode";
            this.menuDarkMode.Size = new System.Drawing.Size(180, 22);
            this.menuDarkMode.Text = "Dark Mode";
            this.menuDarkMode.CheckOnClick = false;
            this.menuDarkMode.Click += new System.EventHandler(this.menuDarkMode_Click);
            //
            // webView
            //
            this.webView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.webView.Location = new System.Drawing.Point(0, 24);
            this.webView.Name = "webView";
            this.webView.Size = new System.Drawing.Size(600, 376);
            this.webView.TabIndex = 1;
            //
            // MarkdownEditorControl
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.webView);
            this.Controls.Add(this.menuStrip);
            this.Name = "MarkdownEditorControl";
            this.Size = new System.Drawing.Size(600, 400);
            ((System.ComponentModel.ISupportInitialize)(this.webView)).EndInit();
            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion
    }
}
