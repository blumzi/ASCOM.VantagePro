using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ASCOM.Utilities;
using ASCOM.VantagePro;

namespace ASCOM.VantagePro
{
    [ComVisible(false)]					// Form not registered for COM!

    public partial class SetupDialogForm : Form
    {
        private VantagePro vantagePro;
        private SerialPortFetcher serialPortFetcher;
        private SocketFetcher socketFetcher;
        private FileFetcher fileFetcher;

        public SetupDialogForm()
        {
            vantagePro = new VantagePro();
            serialPortFetcher = new SerialPortFetcher();
            socketFetcher = new SocketFetcher();
            fileFetcher = new FileFetcher();

            vantagePro.ReadProfile();
            serialPortFetcher.ReadLowerProfile();
            socketFetcher.ReadLowerProfile();
            fileFetcher.ReadLowerProfile();

            InitializeComponent();
            // Initialise current values of user settings from the ASCOM Profile
            InitUI();

            this.Text = $"VantagePro Setup v{VantagePro.AssemblyVersion}";
        }

        private void cmdOK_Click(object sender, EventArgs e) // OK button event handler
        {
            string[] ports = System.IO.Ports.SerialPort.GetPortNames();
            List<string> portsList = new List<string>(ports);
            string selectedComPort = (string)comboBoxComPort.SelectedItem;

            if (portsList.Contains(selectedComPort)) {
                serialPortFetcher.ComPort = selectedComPort;
            }

            if (System.IO.File.Exists(textBoxReportFile.Text)) {
                FileFetcher.DataFile = textBoxReportFile.Text;
            }

            socketFetcher.Address = textBoxIPAddress.Text;
            socketFetcher.Port = Convert.ToUInt16(textBoxIPPort.Text);


            if (radioButtonNone.Checked)
                VantagePro.OperationalMode = VantagePro.OpMode.None;
            else if (radioButtonIP.Checked)
                VantagePro.OperationalMode = VantagePro.OpMode.IP;
            else if (radioButtonReportFile.Checked)
                VantagePro.OperationalMode = VantagePro.OpMode.File;
            else if (radioButtonSerialPort.Checked)
                VantagePro.OperationalMode = VantagePro.OpMode.Serial;

            vantagePro.Tracing = ObservingConditions.tl.Enabled = chkTrace.Checked;
            VantagePro.interval = TimeSpan.FromSeconds(Convert.ToInt32(textBoxInterval.Text));

            vantagePro.WriteProfile();
            fileFetcher.WriteLowerProfile();
            socketFetcher.WriteLowerProfile();
            serialPortFetcher.WriteLowerProfile();

            serialPortFetcher = null;
            socketFetcher = null;
            fileFetcher = null;
            vantagePro = null;
        }

        private void cmdCancel_Click(object sender, EventArgs e) // Cancel button event handler
        {
            Close();
        }

        private void BrowseToAscom(object sender, EventArgs e) // Click on ASCOM logo event handler
        {
            try
            {
                System.Diagnostics.Process.Start("http://ascom-standards.org/");
            }
            catch (System.ComponentModel.Win32Exception noBrowser)
            {
                if (noBrowser.ErrorCode == -2147467259)
                    MessageBox.Show(noBrowser.Message);
            }
            catch (System.Exception other)
            {
                MessageBox.Show(other.Message);
            }
        }

        private void InitUI()
        {
            chkTrace.Checked = ObservingConditions.tl.Enabled;

            comboBoxComPort.Items.Clear();
            comboBoxComPort.Items.AddRange(System.IO.Ports.SerialPort.GetPortNames());      // use System.IO because it's static

            if (serialPortFetcher.ComPort != null && comboBoxComPort.Items.Contains(serialPortFetcher.ComPort))
            {
                comboBoxComPort.SelectedItem = serialPortFetcher.ComPort;
            }

            switch (VantagePro.OperationalMode)
            {
                case VantagePro.OpMode.None:
                    radioButtonNone.Checked = true;
                    break;
                case VantagePro.OpMode.File:
                    radioButtonReportFile.Checked = true;
                    break;
                case VantagePro.OpMode.Serial:
                    radioButtonSerialPort.Checked = true;
                    break;
                case VantagePro.OpMode.IP:
                    radioButtonIP.Checked = true;
                    break;
            }

            textBoxReportFile.Text = FileFetcher.DataFile;
            chkTrace.Checked = vantagePro.Tracing;
            labelTracePath.Text = chkTrace.Checked ? VantagePro.traceLogFile : "";
            textBoxIPAddress.Text = socketFetcher.Address;
            textBoxInterval.Text = Convert.ToInt32(VantagePro.interval.TotalSeconds).ToString();
        }

        private void buttonChooser_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(FileFetcher.DataFile))
                openFileDialogReportFile.InitialDirectory = System.IO.Path.GetDirectoryName(FileFetcher.DataFile);
            var result = openFileDialogReportFile.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                FileFetcher.DataFile = openFileDialogReportFile.FileName;
                textBoxReportFile.Text = FileFetcher.DataFile;
            }
        }

        private void openFileDialogReportFile_FileOk(object sender, CancelEventArgs e)
        {
            textBoxReportFile.Text = (sender as OpenFileDialog).FileName;
        }

        private void radioButtonReportFile_CheckedChanged(object sender, EventArgs e)
        {
            if ((sender as RadioButton).Checked)
            {
                foreach (var control in new List<Control> { textBoxReportFile, buttonChooser })
                    control.Enabled = true;
                foreach (var control in new List<Control> { comboBoxComPort, textBoxIPAddress, textBoxIPPort })
                    control.Enabled = false;
            }
        }

        private void radioButtonSerialPort_CheckedChanged(object sender, EventArgs e)
        {
            if ((sender as RadioButton).Checked)
            {
                foreach (var control in new List<Control> { comboBoxComPort })
                    control.Enabled = true;
                foreach (var control in new List<Control> { textBoxReportFile, buttonChooser, textBoxIPAddress, textBoxIPPort })
                    control.Enabled = false;
            }
        }

        private void radioButtonIP_CheckedChanged(object sender, EventArgs e)
        {
            if ((sender as RadioButton).Checked)
            {
                foreach (var control in new List<Control> { textBoxIPAddress, textBoxIPPort })
                    control.Enabled = true;
                foreach (var control in new List<Control> { comboBoxComPort, textBoxReportFile, buttonChooser })
                    control.Enabled = false;
            }
        }

        private void radioButtonNone_CheckedChanged(object sender, EventArgs e)
        {
            foreach (var control in new List<Control> { textBoxIPAddress, textBoxIPPort, comboBoxComPort, textBoxReportFile, buttonChooser })
                control.Enabled = false;
        }

        private void chkTrace_CheckedChanged(object sender, EventArgs e)
        {
            labelTracePath.Text = (sender as CheckBox).Checked ? VantagePro.traceLogFile : "";
        }

        private void buttonTest_Click(object sender, EventArgs e)
        {
            string result = null;
            Color resultColor = (sender as Button).ForeColor;

            if (radioButtonNone.Checked)
            {
                MessageBox.Show("Please select an operational mode other than \"None\"!", "Failure", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            VantagePro.tl.Enabled = chkTrace.Checked;
            buttonTest.Text = "Testing ...";
            if (radioButtonReportFile.Checked)
            {
                fileFetcher.Test(textBoxReportFile.Text, ref result, ref resultColor);
            }
            else if (radioButtonSerialPort.Checked)
            {
                serialPortFetcher.Test(comboBoxComPort.Text, ref result, ref resultColor);
            }
            else if (radioButtonIP.Checked)
            {
                socketFetcher.Test(textBoxIPAddress.Text, textBoxIPPort.Text, ref result, ref resultColor);
            }

            if (resultColor == VantagePro.colorGood)
            {
                MessageBox.Show(result, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (resultColor == VantagePro.colorWarning)
            {
                MessageBox.Show(result, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (resultColor == VantagePro.colorError)
            {
                MessageBox.Show(result, "Failure", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            buttonTest.Text = "Test configuration";
        }
    }
}