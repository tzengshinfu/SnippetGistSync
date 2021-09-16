
namespace SnippetGistSync {
    partial class SnippetGistSyncOptionForm {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnToGitHub = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.txtUserPAT = new System.Windows.Forms.TextBox();
            this.txtUserName = new System.Windows.Forms.TextBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.btnUploadAll = new System.Windows.Forms.Button();
            this.btnDownloadAll = new System.Windows.Forms.Button();
            this.btnAutoSync = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.btnToGitHub);
            this.groupBox1.Controls.Add(this.btnSave);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.txtUserPAT);
            this.groupBox1.Controls.Add(this.txtUserName);
            this.groupBox1.Location = new System.Drawing.Point(12, 15);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(404, 112);
            this.groupBox1.TabIndex = 2;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "GitHub 使用者憑證";
            // 
            // btnToGitHub
            // 
            this.btnToGitHub.Location = new System.Drawing.Point(302, 33);
            this.btnToGitHub.Name = "btnToGitHub";
            this.btnToGitHub.Size = new System.Drawing.Size(75, 23);
            this.btnToGitHub.TabIndex = 5;
            this.btnToGitHub.Text = "到 GitHub";
            this.btnToGitHub.UseVisualStyleBackColor = true;
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(302, 74);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(75, 23);
            this.btnSave.TabIndex = 4;
            this.btnSave.Text = "儲存";
            this.btnSave.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(10, 60);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(111, 12);
            this.label2.TabIndex = 3;
            this.label2.Text = "Personal Access Token";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(10, 20);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(65, 12);
            this.label1.TabIndex = 2;
            this.label1.Text = "使用者名稱";
            // 
            // txtUserPAT
            // 
            this.txtUserPAT.Location = new System.Drawing.Point(38, 75);
            this.txtUserPAT.Name = "txtUserPAT";
            this.txtUserPAT.PasswordChar = '*';
            this.txtUserPAT.Size = new System.Drawing.Size(258, 22);
            this.txtUserPAT.TabIndex = 1;
            // 
            // txtUserName
            // 
            this.txtUserName.Location = new System.Drawing.Point(38, 35);
            this.txtUserName.Name = "txtUserName";
            this.txtUserName.Size = new System.Drawing.Size(258, 22);
            this.txtUserName.TabIndex = 0;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.btnUploadAll);
            this.groupBox2.Controls.Add(this.btnDownloadAll);
            this.groupBox2.Controls.Add(this.btnAutoSync);
            this.groupBox2.Location = new System.Drawing.Point(12, 133);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(404, 58);
            this.groupBox2.TabIndex = 3;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "同步動作";
            // 
            // btnUploadAll
            // 
            this.btnUploadAll.Location = new System.Drawing.Point(38, 21);
            this.btnUploadAll.Name = "btnUploadAll";
            this.btnUploadAll.Size = new System.Drawing.Size(98, 23);
            this.btnUploadAll.TabIndex = 7;
            this.btnUploadAll.Text = "覆蓋遠端";
            this.btnUploadAll.UseVisualStyleBackColor = true;
            // 
            // btnDownloadAll
            // 
            this.btnDownloadAll.Location = new System.Drawing.Point(159, 21);
            this.btnDownloadAll.Name = "btnDownloadAll";
            this.btnDownloadAll.Size = new System.Drawing.Size(98, 23);
            this.btnDownloadAll.TabIndex = 6;
            this.btnDownloadAll.Text = "覆蓋本機";
            this.btnDownloadAll.UseVisualStyleBackColor = true;
            // 
            // btnAutoSync
            // 
            this.btnAutoSync.Location = new System.Drawing.Point(279, 21);
            this.btnAutoSync.Name = "btnAutoSync";
            this.btnAutoSync.Size = new System.Drawing.Size(98, 23);
            this.btnAutoSync.TabIndex = 5;
            this.btnAutoSync.Text = "啟用同步";
            this.btnAutoSync.UseVisualStyleBackColor = true;
            // 
            // SnippetGistSyncOptionForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(428, 206);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.groupBox2);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SnippetGistSyncOptionForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "SnippetGistSync 選項";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button btnToGitHub;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtUserPAT;
        private System.Windows.Forms.TextBox txtUserName;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button btnUploadAll;
        private System.Windows.Forms.Button btnDownloadAll;
        private System.Windows.Forms.Button btnAutoSync;
    }
}