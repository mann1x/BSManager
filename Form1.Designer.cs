
using System.Reflection;
using System.Windows.Forms;

namespace BSManager
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.notifyIcon1 = new System.Windows.Forms.NotifyIcon(this.components);
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripMenuItemBS = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItemDisco = new System.Windows.Forms.ToolStripMenuItem();
            this.hMDToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItemHmd = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripRunAtStartup = new System.Windows.Forms.ToolStripMenuItem();
            this.RuntimeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem4 = new System.Windows.Forms.ToolStripMenuItem();
            this.bSManagerVersionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripDebugLog = new System.Windows.Forms.ToolStripMenuItem();
            this.steamVRLHDBToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.SteamDBToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.documentationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.licenseToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createDesktopShortcutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.quitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // notifyIcon1
            // 
            this.notifyIcon1.BalloonTipText = "BSManager";
            this.notifyIcon1.BalloonTipTitle = "BSManager";
            this.notifyIcon1.ContextMenuStrip = this.contextMenuStrip1;
            this.notifyIcon1.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon1.Icon")));
            this.notifyIcon1.Text = "BSManager";
            this.notifyIcon1.Visible = true;
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemBS,
            this.hMDToolStripMenuItem,
            this.toolStripRunAtStartup,
            this.RuntimeToolStripMenuItem,
            this.toolStripMenuItem4,
            this.quitToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(181, 158);
            // 
            // toolStripMenuItemBS
            // 
            this.toolStripMenuItemBS.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ToolStripMenuItemDisco});
            this.toolStripMenuItemBS.Name = "toolStripMenuItemBS";
            this.toolStripMenuItemBS.Size = new System.Drawing.Size(180, 22);
            this.toolStripMenuItemBS.Text = "Base Stations";
            // 
            // ToolStripMenuItemDisco
            // 
            this.ToolStripMenuItemDisco.Name = "ToolStripMenuItemDisco";
            this.ToolStripMenuItemDisco.Size = new System.Drawing.Size(144, 22);
            this.ToolStripMenuItemDisco.Text = "Discovered: 0";
            // 
            // hMDToolStripMenuItem
            // 
            this.hMDToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ToolStripMenuItemHmd});
            this.hMDToolStripMenuItem.Name = "hMDToolStripMenuItem";
            this.hMDToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.hMDToolStripMenuItem.Text = "HMD";
            // 
            // ToolStripMenuItemHmd
            // 
            this.ToolStripMenuItemHmd.Name = "ToolStripMenuItemHmd";
            this.ToolStripMenuItemHmd.Size = new System.Drawing.Size(95, 22);
            this.ToolStripMenuItemHmd.Text = "OFF";
            // 
            // toolStripRunAtStartup
            // 
            this.toolStripRunAtStartup.Name = "toolStripRunAtStartup";
            this.toolStripRunAtStartup.Size = new System.Drawing.Size(180, 22);
            this.toolStripRunAtStartup.Text = "Run at Startup";
            this.toolStripRunAtStartup.Click += new System.EventHandler(this.toolStripRunAtStartup_Click);
            // 
            // RuntimeToolStripMenuItem
            // 
            this.RuntimeToolStripMenuItem.Name = "RuntimeToolStripMenuItem";
            this.RuntimeToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.RuntimeToolStripMenuItem.Text = "Manage Runtime";
            this.RuntimeToolStripMenuItem.Click += new System.EventHandler(this.RuntimeToolStripMenuItem_Click);
            // 
            // toolStripMenuItem4
            // 
            this.toolStripMenuItem4.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.bSManagerVersionToolStripMenuItem,
            this.toolStripDebugLog,
            this.steamVRLHDBToolStripMenuItem,
            this.documentationToolStripMenuItem,
            this.licenseToolStripMenuItem,
            this.createDesktopShortcutToolStripMenuItem});
            this.toolStripMenuItem4.Name = "toolStripMenuItem4";
            this.toolStripMenuItem4.Size = new System.Drawing.Size(180, 22);
            this.toolStripMenuItem4.Text = "Help and Info";
            // 
            // bSManagerVersionToolStripMenuItem
            // 
            this.bSManagerVersionToolStripMenuItem.Name = "bSManagerVersionToolStripMenuItem";
            this.bSManagerVersionToolStripMenuItem.Size = new System.Drawing.Size(200, 22);
            this.bSManagerVersionToolStripMenuItem.Text = "BSManager Version";
            // 
            // toolStripDebugLog
            // 
            this.toolStripDebugLog.Name = "toolStripDebugLog";
            this.toolStripDebugLog.Size = new System.Drawing.Size(200, 22);
            this.toolStripDebugLog.Text = "Debug Log";
            this.toolStripDebugLog.Click += new System.EventHandler(this.toolStripDebugLog_Click);
            // 
            // steamVRLHDBToolStripMenuItem
            // 
            this.steamVRLHDBToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.SteamDBToolStripMenuItem});
            this.steamVRLHDBToolStripMenuItem.Name = "steamVRLHDBToolStripMenuItem";
            this.steamVRLHDBToolStripMenuItem.Size = new System.Drawing.Size(200, 22);
            this.steamVRLHDBToolStripMenuItem.Text = "SteamVR LH DB";
            // 
            // SteamDBToolStripMenuItem
            // 
            this.SteamDBToolStripMenuItem.Name = "SteamDBToolStripMenuItem";
            this.SteamDBToolStripMenuItem.Size = new System.Drawing.Size(96, 22);
            this.SteamDBToolStripMenuItem.Text = "N/D";
            // 
            // documentationToolStripMenuItem
            // 
            this.documentationToolStripMenuItem.Name = "documentationToolStripMenuItem";
            this.documentationToolStripMenuItem.Size = new System.Drawing.Size(200, 22);
            this.documentationToolStripMenuItem.Text = "Documentation";
            this.documentationToolStripMenuItem.Click += new System.EventHandler(this.documentationToolStripMenuItem_Click);
            // 
            // licenseToolStripMenuItem
            // 
            this.licenseToolStripMenuItem.Name = "licenseToolStripMenuItem";
            this.licenseToolStripMenuItem.Size = new System.Drawing.Size(200, 22);
            this.licenseToolStripMenuItem.Text = "License";
            this.licenseToolStripMenuItem.Click += new System.EventHandler(this.licenseToolStripMenuItem_Click);
            // 
            // createDesktopShortcutToolStripMenuItem
            // 
            this.createDesktopShortcutToolStripMenuItem.Name = "createDesktopShortcutToolStripMenuItem";
            this.createDesktopShortcutToolStripMenuItem.Size = new System.Drawing.Size(200, 22);
            this.createDesktopShortcutToolStripMenuItem.Text = "Create desktop shortcut";
            this.createDesktopShortcutToolStripMenuItem.Click += new System.EventHandler(this.createDesktopShortcutToolStripMenuItem_Click);
            // 
            // quitToolStripMenuItem
            // 
            this.quitToolStripMenuItem.Name = "quitToolStripMenuItem";
            this.quitToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.quitToolStripMenuItem.Text = "Quit";
            this.quitToolStripMenuItem.Click += new System.EventHandler(this.quitToolStripMenuItem_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Form1";
            this.Text = "BSManager";
            this.WindowState = System.Windows.Forms.FormWindowState.Minimized;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form1_FormClosed);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.NotifyIcon notifyIcon1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private ToolStripMenuItem toolStripMenuItemBS;
        private ToolStripMenuItem ToolStripMenuItemDisco;
        private ToolStripMenuItem hMDToolStripMenuItem;
        private ToolStripMenuItem ToolStripMenuItemHmd;
        private ToolStripMenuItem toolStripRunAtStartup;
        private ToolStripMenuItem toolStripMenuItem4;
        private ToolStripMenuItem bSManagerVersionToolStripMenuItem;
        private ToolStripMenuItem steamVRLHDBToolStripMenuItem;
        private ToolStripMenuItem SteamDBToolStripMenuItem;
        private ToolStripMenuItem documentationToolStripMenuItem;
        private ToolStripMenuItem licenseToolStripMenuItem;
        private ToolStripMenuItem createDesktopShortcutToolStripMenuItem;
        private ToolStripMenuItem toolStripDebugLog;
        private ToolStripMenuItem quitToolStripMenuItem;
        private ToolStripMenuItem RuntimeToolStripMenuItem;
    }
}

