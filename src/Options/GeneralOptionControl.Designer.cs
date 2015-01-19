namespace NuGet.Options
{
    partial class GeneralOptionControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GeneralOptionControl));
            this.checkForUpdate = new System.Windows.Forms.CheckBox();
            this.clearPackageCacheButton = new System.Windows.Forms.Button();
            this.browsePackageCacheButton = new System.Windows.Forms.Button();
            this.UpdateHeaderDivider = new System.Windows.Forms.GroupBox();
            this.UpdateHeader = new System.Windows.Forms.Label();
            this.PackagesCacheHeaderDivider = new System.Windows.Forms.GroupBox();
            this.PackagesCacheHeader = new System.Windows.Forms.Label();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.updatePanel = new System.Windows.Forms.Panel();
            this.PackageRestoreHeader = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.packageRestoreConsentCheckBox = new System.Windows.Forms.CheckBox();
            this.packageRestoreAutomaticCheckBox = new System.Windows.Forms.CheckBox();
            this.tableLayoutPanel1.SuspendLayout();
            this.updatePanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // checkForUpdate
            // 
            resources.ApplyResources(this.checkForUpdate, "checkForUpdate");
            this.checkForUpdate.Name = "checkForUpdate";
            this.checkForUpdate.UseVisualStyleBackColor = true;
            // 
            // clearPackageCacheButton
            // 
            resources.ApplyResources(this.clearPackageCacheButton, "clearPackageCacheButton");
            this.clearPackageCacheButton.Name = "clearPackageCacheButton";
            this.clearPackageCacheButton.UseVisualStyleBackColor = true;
            this.clearPackageCacheButton.Click += new System.EventHandler(this.OnClearPackageCacheClick);
            // 
            // browsePackageCacheButton
            // 
            resources.ApplyResources(this.browsePackageCacheButton, "browsePackageCacheButton");
            this.browsePackageCacheButton.Name = "browsePackageCacheButton";
            this.browsePackageCacheButton.UseVisualStyleBackColor = true;
            this.browsePackageCacheButton.Click += new System.EventHandler(this.OnBrowsePackageCacheClick);
            // 
            // UpdateHeaderDivider
            // 
            resources.ApplyResources(this.UpdateHeaderDivider, "UpdateHeaderDivider");
            this.UpdateHeaderDivider.Name = "UpdateHeaderDivider";
            this.UpdateHeaderDivider.TabStop = false;
            // 
            // UpdateHeader
            // 
            resources.ApplyResources(this.UpdateHeader, "UpdateHeader");
            this.UpdateHeader.Name = "UpdateHeader";
            // 
            // PackagesCacheHeaderDivider
            // 
            resources.ApplyResources(this.PackagesCacheHeaderDivider, "PackagesCacheHeaderDivider");
            this.PackagesCacheHeaderDivider.Name = "PackagesCacheHeaderDivider";
            this.PackagesCacheHeaderDivider.TabStop = false;
            // 
            // PackagesCacheHeader
            // 
            resources.ApplyResources(this.PackagesCacheHeader, "PackagesCacheHeader");
            this.PackagesCacheHeader.Name = "PackagesCacheHeader";
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(this.clearPackageCacheButton, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.browsePackageCacheButton, 1, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // updatePanel
            // 
            this.updatePanel.Controls.Add(this.checkForUpdate);
            this.updatePanel.Controls.Add(this.UpdateHeader);
            this.updatePanel.Controls.Add(this.UpdateHeaderDivider);
            resources.ApplyResources(this.updatePanel, "updatePanel");
            this.updatePanel.Name = "updatePanel";
            // 
            // PackageRestoreHeader
            // 
            resources.ApplyResources(this.PackageRestoreHeader, "PackageRestoreHeader");
            this.PackageRestoreHeader.Name = "PackageRestoreHeader";
            // 
            // groupBox1
            // 
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // packageRestoreConsentCheckBox
            // 
            resources.ApplyResources(this.packageRestoreConsentCheckBox, "packageRestoreConsentCheckBox");
            this.packageRestoreConsentCheckBox.Name = "packageRestoreConsentCheckBox";
            this.packageRestoreConsentCheckBox.UseVisualStyleBackColor = true;
            this.packageRestoreConsentCheckBox.CheckedChanged += new System.EventHandler(this.packageRestoreConsentCheckBox_CheckedChanged);
            // 
            // packageRestoreAutomaticCheckBox
            // 
            resources.ApplyResources(this.packageRestoreAutomaticCheckBox, "packageRestoreAutomaticCheckBox");
            this.packageRestoreAutomaticCheckBox.Name = "packageRestoreAutomaticCheckBox";
            this.packageRestoreAutomaticCheckBox.UseVisualStyleBackColor = true;
            // 
            // GeneralOptionControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.PackagesCacheHeader);
            this.Controls.Add(this.PackagesCacheHeaderDivider);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Controls.Add(this.PackageRestoreHeader);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.packageRestoreConsentCheckBox);
            this.Controls.Add(this.packageRestoreAutomaticCheckBox);
            this.Controls.Add(this.updatePanel);
            this.Name = "GeneralOptionControl";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.updatePanel.ResumeLayout(false);
            this.updatePanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox checkForUpdate;
        private System.Windows.Forms.Button clearPackageCacheButton;
        private System.Windows.Forms.Button browsePackageCacheButton;
        private System.Windows.Forms.GroupBox UpdateHeaderDivider;
        private System.Windows.Forms.Label UpdateHeader;
        private System.Windows.Forms.GroupBox PackagesCacheHeaderDivider;
        private System.Windows.Forms.Label PackagesCacheHeader;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Panel updatePanel;
        private System.Windows.Forms.Label PackageRestoreHeader;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckBox packageRestoreConsentCheckBox;
        private System.Windows.Forms.CheckBox packageRestoreAutomaticCheckBox;
    }
}
