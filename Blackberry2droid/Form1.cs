using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Xml;

namespace Blackberry2droid
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Adds attributes to an sms XMLElement
        /// </summary>
        /// <param name="sms"></param>
        /// <param name="smsMessage"></param>
        private void addSMStoFile(XmlElement sms, ReadIPD.IPD_SMSRecord smsMessage )
        {
            DateTime smsDate = DateTime.MinValue;
            int messageType = 0;
            long date = 0L;
            if( smsMessage.WasSent )
            {
                smsDate = smsMessage.Sent;
                date = smsMessage.SentEpoch;
                messageType = 2; // Sent Message
            }
            else
            {
                messageType = 1; // Recieved message
                smsDate = smsMessage.Received;
                date = smsMessage.ReceivedEpoch;
            }

            sms.SetAttribute("protocol", "0");
            sms.SetAttribute("address", smsMessage.Number);
            sms.SetAttribute("date", date.ToString());
            sms.SetAttribute("type", messageType.ToString());
            sms.SetAttribute("subject", "null");
            sms.SetAttribute("body", smsMessage.Message);

            sms.SetAttribute("toa", "null");
            sms.SetAttribute("sc_toa", "null");
            sms.SetAttribute("service_center", "null");
            sms.SetAttribute("read", "1");
            sms.SetAttribute("status", "-1");
            sms.SetAttribute("locked", "0");
            sms.SetAttribute("readable_date", smsDate.ToString("MMM dd, yyyy h:mm tt"));
            sms.SetAttribute("contact_name", "(Unknown)");
            sms.SetAttribute("locked", "0");
        }

        /// <summary>
        /// Function to process conversion to SMS backup & Restoe format.
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <param name="destinationFile"></param>
        /// <param name="databaseId">Optional Database ID to use</param>
        private void convertFile(string sourceFile, string destinationFile, int databaseId)
        {
            bool foundSMSTable = false;

            using (ReadIPD ipdParser = new ReadIPD(sourceFile))
            {
                if (ipdParser.fileVersion == 0 || ipdParser.fileDbcount == 0)
                {
                    MessageBox.Show("Invalid Backup File. Please provide a backup created by the \"Blackberry Destkop Software\".", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                lblStatus.Text = "Loaded Blackberry Backup";

                //
                // Get name of the SMS database.
                string[] dbList = ipdParser.getDatabaseNames();
                int smsID = 0;
                if (databaseId > 0)
                {
                    // use the optional database ID specified.
                    smsID = databaseId;
                    foundSMSTable = true;
                }
                else
                {
                    // Iterate over listed databases.
                    foreach (string s in dbList)
                    {
                        if (s == "SMS Messages\0")
                        {
                            foundSMSTable = true;
                            break;
                        }
                        smsID++;
                    }
                }
                
                // If we failed to locate the required database:
                if (!foundSMSTable)
                {
                    lblStatus.Text = "Failed to locate SMS messages";
                    MessageBox.Show("Could not locate any SMS messages in this backup", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                lblStatus.Text = "Located SMS message table";


                //
                // Locate the SMS messages.
                List<object> smsMessages = ipdParser.getDatabaseRecords(smsID, ReadIPD.RecordTypes.SMS);
                lblStatus.Text = "Found " + smsMessages.Count + " SMS messages";
                if (smsMessages.Count <= 0)
                {
                    lblStatus.Text = "Complete - No SMS messages present";
                    MessageBox.Show("No SMS messages are present in this backup", "Conversion complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                progConversion.Maximum = smsMessages.Count;
                lblStatus.Text = "Converting messages";

                XmlDocument xmlDoc = new XmlDocument();
                XmlElement smses = xmlDoc.CreateElement("smses");
                xmlDoc.AppendChild(smses);
                smses.SetAttribute("count", smsMessages.Count.ToString());

                //
                // Extract message and convert.
                foreach (object smsMessage in smsMessages)
                {

                    XmlElement sms = xmlDoc.CreateElement("sms");

                    addSMStoFile(sms, (ReadIPD.IPD_SMSRecord)smsMessage);
                    smses.AppendChild(sms);
                    progConversion.Value += 1;
                }


                // Output
                xmlDoc.Save(destinationFile);

                // All done.
                lblStatus.Text = "Complete";
                btnSave.Enabled = false; // Prevent clicking convert again until a file is chosen once more.
                
            }
        }


        /// <summary>
        /// Choose file button clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnChooseFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog fd = new OpenFileDialog();
            fd.InitialDirectory = txtSourceFile.Text;
            fd.ShowDialog();
            txtSourceFile.Text = fd.FileName;
            fd.Dispose();

            // Setup progress bar.
            progConversion.Maximum = 100;
            progConversion.Minimum = 0;
            progConversion.Value = 0;
            progConversion.Style = ProgressBarStyle.Continuous;
            lblStatus.Text = "Ready";

            // Enable conversion  button.
            btnSave.Enabled = true;
        }


        /// <summary>
        /// Save button clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSave_Click(object sender, EventArgs e)
        {
            string szDatabasePath = txtSourceFile.Text;
            int dDatabaseId = 0;    // Optional - Not needed for IPD files, but used for BBB format.
            ExtractBBBFile objExtractBBB = null;

            if (szDatabasePath.EndsWith(".bbb"))
            {
                objExtractBBB = new ExtractBBBFile(szDatabasePath, "SMS Messages");
                szDatabasePath = objExtractBBB.DatabasePath;
                dDatabaseId = objExtractBBB.DatabaseId;
            }

            if (!System.IO.File.Exists(szDatabasePath ))
            {
                MessageBox.Show("You must select a valid file to convert", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            SaveFileDialog sd = new SaveFileDialog();
            sd.InitialDirectory = txtSourceFile.Text;
            sd.Filter = "SMS Backup And Restore|*.xml";
            sd.ShowDialog();

            convertFile(szDatabasePath, sd.FileName, dDatabaseId);

            // Needed to remove the temp files from the system.
            if (objExtractBBB != null)
            {
                objExtractBBB.Dispose();
            }

            sd.Dispose();
        }


        /// <summary>
        /// HOPE.MX Hyperlink clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start( "http://hope.mx" );
        }

    }
}
