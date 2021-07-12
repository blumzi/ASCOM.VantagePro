namespace ASCOM.VantagePro
{
    partial class SetupDialogForm
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.cmdOK = new System.Windows.Forms.Button();
            this.cmdCancel = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.picASCOM = new System.Windows.Forms.PictureBox();
            this.label2 = new System.Windows.Forms.Label();
            this.chkTrace = new System.Windows.Forms.CheckBox();
            this.comboBoxComPort = new System.Windows.Forms.ComboBox();
            this.radioButtonReportFile = new System.Windows.Forms.RadioButton();
            this.radioButtonSerialPort = new System.Windows.Forms.RadioButton();
            this.groupBoxOpMode = new System.Windows.Forms.GroupBox();
            this.radioButtonIP = new System.Windows.Forms.RadioButton();
            this.radioButtonNone = new System.Windows.Forms.RadioButton();
            this.label3 = new System.Windows.Forms.Label();
            this.textBoxReportFile = new System.Windows.Forms.TextBox();
            this.openFileDialogReportFile = new System.Windows.Forms.OpenFileDialog();
            this.buttonChooser = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.textBoxIPAddress = new System.Windows.Forms.TextBox();
            this.textBoxIPPort = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.labelTracePath = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.picASCOM)).BeginInit();
            this.groupBoxOpMode.SuspendLayout();
            this.SuspendLayout();
            // 
            // cmdOK
            // 
            this.cmdOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdOK.CausesValidation = false;
            this.cmdOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.cmdOK.Location = new System.Drawing.Point(538, 157);
            this.cmdOK.Name = "cmdOK";
            this.cmdOK.Size = new System.Drawing.Size(59, 24);
            this.cmdOK.TabIndex = 0;
            this.cmdOK.Text = "OK";
            this.cmdOK.UseVisualStyleBackColor = true;
            this.cmdOK.Click += new System.EventHandler(this.cmdOK_Click);
            // 
            // cmdCancel
            // 
            this.cmdCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cmdCancel.Location = new System.Drawing.Point(538, 187);
            this.cmdCancel.Name = "cmdCancel";
            this.cmdCancel.Size = new System.Drawing.Size(59, 25);
            this.cmdCancel.TabIndex = 1;
            this.cmdCancel.Text = "Cancel";
            this.cmdCancel.UseVisualStyleBackColor = true;
            this.cmdCancel.Click += new System.EventHandler(this.cmdCancel_Click);
            // 
            // label1
            // 
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(175, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(277, 60);
            this.label1.TabIndex = 2;
            this.label1.Text = "An ASCOM ObservingConditions driver\r\nfor the VantagePro2 weather station\r\nby Davi" +
    "s Systems";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // picASCOM
            // 
            this.picASCOM.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.picASCOM.Cursor = System.Windows.Forms.Cursors.Hand;
            this.picASCOM.Image = global::ASCOM.VantagePro.Properties.Resources.ASCOM;
            this.picASCOM.Location = new System.Drawing.Point(549, 9);
            this.picASCOM.Name = "picASCOM";
            this.picASCOM.Size = new System.Drawing.Size(48, 56);
            this.picASCOM.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.picASCOM.TabIndex = 3;
            this.picASCOM.TabStop = false;
            this.picASCOM.Click += new System.EventHandler(this.BrowseToAscom);
            this.picASCOM.DoubleClick += new System.EventHandler(this.BrowseToAscom);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(145, 137);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(58, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "Comm Port";
            // 
            // chkTrace
            // 
            this.chkTrace.AutoSize = true;
            this.chkTrace.Location = new System.Drawing.Point(37, 200);
            this.chkTrace.Name = "chkTrace";
            this.chkTrace.Size = new System.Drawing.Size(69, 17);
            this.chkTrace.TabIndex = 6;
            this.chkTrace.Text = "Trace on";
            this.chkTrace.UseVisualStyleBackColor = true;
            this.chkTrace.CheckedChanged += new System.EventHandler(this.chkTrace_CheckedChanged);
            // 
            // comboBoxComPort
            // 
            this.comboBoxComPort.FormattingEnabled = true;
            this.comboBoxComPort.Location = new System.Drawing.Point(209, 134);
            this.comboBoxComPort.Name = "comboBoxComPort";
            this.comboBoxComPort.Size = new System.Drawing.Size(90, 21);
            this.comboBoxComPort.TabIndex = 7;
            // 
            // radioButtonReportFile
            // 
            this.radioButtonReportFile.AutoSize = true;
            this.radioButtonReportFile.Location = new System.Drawing.Point(22, 42);
            this.radioButtonReportFile.Name = "radioButtonReportFile";
            this.radioButtonReportFile.Size = new System.Drawing.Size(76, 17);
            this.radioButtonReportFile.TabIndex = 8;
            this.radioButtonReportFile.Text = "Report File";
            this.radioButtonReportFile.UseVisualStyleBackColor = true;
            this.radioButtonReportFile.CheckedChanged += new System.EventHandler(this.radioButtonReportFile_CheckedChanged);
            // 
            // radioButtonSerialPort
            // 
            this.radioButtonSerialPort.AutoSize = true;
            this.radioButtonSerialPort.Location = new System.Drawing.Point(22, 65);
            this.radioButtonSerialPort.Name = "radioButtonSerialPort";
            this.radioButtonSerialPort.Size = new System.Drawing.Size(73, 17);
            this.radioButtonSerialPort.TabIndex = 9;
            this.radioButtonSerialPort.Text = "Serial Port";
            this.radioButtonSerialPort.UseVisualStyleBackColor = true;
            this.radioButtonSerialPort.CheckedChanged += new System.EventHandler(this.radioButtonSerialPort_CheckedChanged);
            // 
            // groupBoxOpMode
            // 
            this.groupBoxOpMode.Controls.Add(this.radioButtonIP);
            this.groupBoxOpMode.Controls.Add(this.radioButtonNone);
            this.groupBoxOpMode.Controls.Add(this.radioButtonReportFile);
            this.groupBoxOpMode.Controls.Add(this.radioButtonSerialPort);
            this.groupBoxOpMode.Location = new System.Drawing.Point(15, 72);
            this.groupBoxOpMode.Name = "groupBoxOpMode";
            this.groupBoxOpMode.Size = new System.Drawing.Size(120, 121);
            this.groupBoxOpMode.TabIndex = 10;
            this.groupBoxOpMode.TabStop = false;
            this.groupBoxOpMode.Text = " Operational mode ";
            // 
            // radioButtonIP
            // 
            this.radioButtonIP.AutoSize = true;
            this.radioButtonIP.Location = new System.Drawing.Point(22, 89);
            this.radioButtonIP.Name = "radioButtonIP";
            this.radioButtonIP.Size = new System.Drawing.Size(35, 17);
            this.radioButtonIP.TabIndex = 11;
            this.radioButtonIP.Text = "IP";
            this.radioButtonIP.UseVisualStyleBackColor = true;
            this.radioButtonIP.CheckedChanged += new System.EventHandler(this.radioButtonIP_CheckedChanged);
            // 
            // radioButtonNone
            // 
            this.radioButtonNone.AutoSize = true;
            this.radioButtonNone.Checked = true;
            this.radioButtonNone.Location = new System.Drawing.Point(22, 21);
            this.radioButtonNone.Name = "radioButtonNone";
            this.radioButtonNone.Size = new System.Drawing.Size(51, 17);
            this.radioButtonNone.TabIndex = 10;
            this.radioButtonNone.TabStop = true;
            this.radioButtonNone.Text = "None";
            this.radioButtonNone.UseVisualStyleBackColor = true;
            this.radioButtonNone.CheckedChanged += new System.EventHandler(this.radioButtonNone_CheckedChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(145, 114);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(58, 13);
            this.label3.TabIndex = 11;
            this.label3.Text = "Report File";
            // 
            // textBoxReportFile
            // 
            this.textBoxReportFile.Location = new System.Drawing.Point(209, 111);
            this.textBoxReportFile.Name = "textBoxReportFile";
            this.textBoxReportFile.Size = new System.Drawing.Size(322, 20);
            this.textBoxReportFile.TabIndex = 12;
            // 
            // openFileDialogReportFile
            // 
            this.openFileDialogReportFile.DefaultExt = "htm";
            this.openFileDialogReportFile.Title = "WeatherLink report file";
            this.openFileDialogReportFile.FileOk += new System.ComponentModel.CancelEventHandler(this.openFileDialogReportFile_FileOk);
            // 
            // buttonChooser
            // 
            this.buttonChooser.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonChooser.CausesValidation = false;
            this.buttonChooser.Location = new System.Drawing.Point(538, 110);
            this.buttonChooser.Name = "buttonChooser";
            this.buttonChooser.Size = new System.Drawing.Size(59, 25);
            this.buttonChooser.TabIndex = 14;
            this.buttonChooser.Text = "Choose";
            this.buttonChooser.UseVisualStyleBackColor = true;
            this.buttonChooser.Click += new System.EventHandler(this.buttonChooser_Click);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(145, 163);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(45, 13);
            this.label4.TabIndex = 15;
            this.label4.Text = "Address";
            // 
            // textBoxIPAddress
            // 
            this.textBoxIPAddress.Location = new System.Drawing.Point(209, 160);
            this.textBoxIPAddress.Name = "textBoxIPAddress";
            this.textBoxIPAddress.Size = new System.Drawing.Size(90, 20);
            this.textBoxIPAddress.TabIndex = 16;
            // 
            // textBoxIPPort
            // 
            this.textBoxIPPort.Location = new System.Drawing.Point(340, 160);
            this.textBoxIPPort.Name = "textBoxIPPort";
            this.textBoxIPPort.Size = new System.Drawing.Size(38, 20);
            this.textBoxIPPort.TabIndex = 18;
            this.textBoxIPPort.Text = "22222";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(313, 163);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(26, 13);
            this.label5.TabIndex = 17;
            this.label5.Text = "Port";
            // 
            // labelTracePath
            // 
            this.labelTracePath.Location = new System.Drawing.Point(112, 201);
            this.labelTracePath.Name = "labelTracePath";
            this.labelTracePath.Size = new System.Drawing.Size(419, 16);
            this.labelTracePath.TabIndex = 19;
            // 
            // SetupDialogForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(607, 224);
            this.Controls.Add(this.labelTracePath);
            this.Controls.Add(this.textBoxIPPort);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.textBoxIPAddress);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.buttonChooser);
            this.Controls.Add(this.textBoxReportFile);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.groupBoxOpMode);
            this.Controls.Add(this.comboBoxComPort);
            this.Controls.Add(this.chkTrace);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.picASCOM);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.cmdCancel);
            this.Controls.Add(this.cmdOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SetupDialogForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "VantagePro Setup";
            ((System.ComponentModel.ISupportInitialize)(this.picASCOM)).EndInit();
            this.groupBoxOpMode.ResumeLayout(false);
            this.groupBoxOpMode.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button cmdOK;
        private System.Windows.Forms.Button cmdCancel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.PictureBox picASCOM;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckBox chkTrace;
        private System.Windows.Forms.ComboBox comboBoxComPort;
        private System.Windows.Forms.RadioButton radioButtonReportFile;
        private System.Windows.Forms.RadioButton radioButtonSerialPort;
        private System.Windows.Forms.GroupBox groupBoxOpMode;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBoxReportFile;
        private System.Windows.Forms.RadioButton radioButtonNone;
        private System.Windows.Forms.OpenFileDialog openFileDialogReportFile;
        private System.Windows.Forms.Button buttonChooser;
        private System.Windows.Forms.RadioButton radioButtonIP;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textBoxIPAddress;
        private System.Windows.Forms.TextBox textBoxIPPort;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label labelTracePath;
    }
}