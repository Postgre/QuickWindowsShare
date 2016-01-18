using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;
using System.Security.Permissions;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using Tulpep.ActiveDirectoryObjectPicker;

namespace SelectShare
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            radioButton3.Checked = true;
        }
        private void btnOpenFiles_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                foreach (string filename in openFileDialog.FileNames)
                    lbFiles.Items.Add((filename));
            }
        }

        private void btnADOP_Click(object sender, EventArgs e)
        {
            DirectoryObjectPickerDialog picker = new DirectoryObjectPickerDialog()
            {
                AllowedObjectTypes = ObjectTypes.Groups | ObjectTypes.Users,
                DefaultObjectTypes = ObjectTypes.Groups | ObjectTypes.Users,
                AllowedLocations = Locations.All,
                DefaultLocations = Locations.JoinedDomain,
                MultiSelect = true,
                ShowAdvancedView = true
            };

            try
            {
                if (picker.ShowDialog() == DialogResult.OK)
                {
                    foreach (var sel in picker.SelectedObjects)
                    {
                        lbGroups.Items.Add(sel.Name);
                    }
                }
            }
            catch (Exception) { }
        }

        private void remFiles_Click(object sender, EventArgs e)
        {
            for (int x = lbFiles.SelectedIndices.Count - 1; x >= 0; x--)
            {
                int idx = lbFiles.SelectedIndices[x];
                lbFiles.Items.RemoveAt(idx);
            }
        }

        private void remUGs_Click(object sender, EventArgs e)
        {
            for (int x = lbGroups.SelectedIndices.Count - 1; x >= 0; x--)
            {
                int idx = lbGroups.SelectedIndices[x];
                lbGroups.Items.RemoveAt(idx);
            }
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private ManagementObject genUGSec(string ugname)
        {
            NTAccount ntAccount = new NTAccount(ugname);
            SecurityIdentifier oGrpSID = (SecurityIdentifier)ntAccount.Translate(typeof(SecurityIdentifier));
            byte[] utenteSIDArray = new byte[oGrpSID.BinaryLength];
            oGrpSID.GetBinaryForm(utenteSIDArray, 0);
            ManagementObject oGrpTrustee = new ManagementClass(new ManagementPath("Win32_Trustee"), null);
            oGrpTrustee["Name"] = ugname;
            oGrpTrustee["SID"] = utenteSIDArray;
            ManagementObject oGrpACE = new ManagementClass(new ManagementPath("Win32_Ace"), null);
            oGrpACE["AccessMask"] = 2032127;//Full access
            oGrpACE["AceFlags"] = AceFlags.ObjectInherit | AceFlags.ContainerInherit; //propagate the AccessMask to the subfolders
            oGrpACE["AceType"] = AceType.AccessAllowed;
            oGrpACE["Trustee"] = oGrpTrustee;
            return oGrpACE;
        }

        private void share_Click(object sender, EventArgs e)
        {
            string sharepath = "C:\\Shares\\";
            if (lbFiles.Items.Count == 0)
            {
                MessageBox.Show("You must select files to share.", "SelectShare", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (lbGroups.Items.Count == 0)
            {
                MessageBox.Show("You must select users/groups with whom to share.", "SelectShare", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (textBox1.Text == "")
            {
                MessageBox.Show("You must select a unique share name.", "SelectShare", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string sname = sharepath + textBox1.Text;
            if(Directory.Exists(sname)) {
                MessageBox.Show("Chosen share name is in use. Please choose another.", "SelectShare", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            Directory.CreateDirectory(sname);

            var choice = groupBox1.Controls.OfType<RadioButton>().FirstOrDefault(r => r.Checked);
            foreach (var fn in lbFiles.Items)
            {
                if (choice == radioButton3)
                    File.Copy(fn.ToString(), sname+"\\"+Path.GetFileName(fn.ToString())); 
                    
                if(choice == radioButton4)
                    File.Move(fn.ToString(), sname + "\\" + Path.GetFileName(fn.ToString()));
            }
            try
            {
                ManagementObject oGrpSecurityDescriptor = new ManagementClass(new ManagementPath("Win32_SecurityDescriptor"), null);
                oGrpSecurityDescriptor["ControlFlags"] = 4; //SE_DACL_PRESENT
                var otmp = new object[lbGroups.Items.Count];
                int i = 0;
                foreach (var ug in lbGroups.Items)
                {
                    otmp[i] = genUGSec(ug.ToString());
                    i++;
                }
                oGrpSecurityDescriptor["DACL"] = otmp;
                ManagementClass mc = new ManagementClass("Win32_Share");
                ManagementBaseObject inParams = mc.GetMethodParameters("Create");
                ManagementBaseObject outParams;
                inParams["Description"] = textBox1.Text;
                inParams["Name"] = textBox1.Text;
                inParams["Path"] = sname;
                inParams["Type"] = 0x0; // Disk Drive\ inParams["MaximumAllowed"] = null;
                inParams["Password"] = null;
                inParams["Access"] = oGrpSecurityDescriptor; 
                outParams = mc.InvokeMethod("Create", inParams, null);
                if ((uint)(outParams.Properties["ReturnValue"].Value) != 0)
                {
                    throw new Exception("Unable to share directory.");
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }
        }

    }
}
