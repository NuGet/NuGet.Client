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
            this.UpdateHeaderDivider = new System.Windows.Forms.GroupBox();
            this.UpdateHeader = new System.Windows.Forms.Label();
            this.updatePanel = new System.Windows.Forms.Panel();
            this.PackageRestoreHeader = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.packageRestoreConsentCheckBox = new System.Windows.Forms.CheckBox();
            this.packageRestoreAutomaticCheckBox = new System.Windows.Forms.CheckBox();
            this.updatePanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // checkForUpdate
            // 
            resources.ApplyResources(this.checkForUpdate, "checkForUpdate");
            this.checkForUpdate.Name = "checkForUpdate";
            this.checkForUpdate.UseVisualStyleBackColor = true;
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
            this.Controls.Add(this.PackageRestoreHeader);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.packageRestoreConsentCheckBox);
            this.Controls.Add(this.packageRestoreAutomaticCheckBox);
            this.Controls.Add(this.updatePanel);
            this.Name = "GeneralOptionControl";
            this.updatePanel.ResumeLayout(false);
            this.updatePanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox checkForUpdate;
        private System.Windows.Forms.GroupBox UpdateHeaderDivider;
        private System.Windows.Forms.Label UpdateHeader;
        private System.Windows.Forms.Panel updatePanel;
        private System.Windows.Forms.Label PackageRestoreHeader;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckBox packageRestoreConsentCheckBox;
        private System.Windows.Forms.CheckBox packageRestoreAutomaticCheckBox;
    }
}
