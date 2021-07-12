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
        private VantagePro vantagePro = VantagePro.Instance;

        public SetupDialogForm()
        {
            vantagePro.ReadProfile();

            InitializeComponent();
            // Initialise current values of user settings from the ASCOM Profile
            InitUI();
        }

        private void cmdOK_Click(object sender, EventArgs e) // OK button event handler
        {
            bool ok = true;

            if (radioButtonNone.Checked)
                return;

            if (radioButtonSerialPort.Checked)
            {
                string[] ports = System.IO.Ports.SerialPort.GetPortNames();
                List<string> portsList = new List<string>(ports);

                if (portsList.Contains((string)comboBoxComPort.SelectedItem))
                {
                    vantagePro.SerialPortName = (string)comboBoxComPort.SelectedItem;
                    vantagePro.OperationalMode = VantagePro.OpMode.Serial;
                }
            }
            else if (radioButtonReportFile.Checked)
            {
                if (System.IO.File.Exists(textBoxReportFile.Text))
                {
                    vantagePro.DataFile = textBoxReportFile.Text;
                    vantagePro.OperationalMode = VantagePro.OpMode.File;
                }
            }
            else if (radioButtonIP.Checked)
            {
                vantagePro.IPAddress = textBoxIPAddress.Text;
                vantagePro.IPPort = Convert.ToInt16(textBoxIPPort.Text);
                vantagePro.OperationalMode = VantagePro.OpMode.IP;
            }
            else
                ok = false;

            ObservingConditions.tl.Enabled = chkTrace.Checked;
            vantagePro.Tracing = chkTrace.Checked;

            if (ok)
                vantagePro.WriteProfile();
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

            if (comboBoxComPort.Items.Contains(vantagePro.SerialPortName))
            {
                comboBoxComPort.SelectedItem = vantagePro.SerialPortName;
            }

            switch (vantagePro.OperationalMode)
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

            textBoxReportFile.Text = vantagePro.DataFile;
        }

        private void buttonChooser_Click(object sender, EventArgs e)
        {
            openFileDialogReportFile.ShowDialog();
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
    }
}