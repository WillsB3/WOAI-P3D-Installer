using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace WOAI_P3D_Installer
{
    public partial class Main : Form
    {
        public Main()
        {
            InitializeComponent();
        }

        private void btnChooseFolder_Click(object sender, EventArgs e)
        {
            if (fbdPath.ShowDialog() == DialogResult.OK)
            {
                txtPath.Text = fbdPath.SelectedPath;
            }
        }

        private bool checkFolderStructure()
        {
            string sourceRootDirectory = this.getSourceRootDirectory();
            string extractedPackagesDirectory = this.getExtractedPackagesDirectory();
            string outputRootDirectory = this.getOutputRootDirectory();

            // Check that "<ROOT>\Source\Extracted Packages" exists
            if (File.Exists(extractedPackagesDirectory))
            {
                DialogResult dialogResult = MessageBox.Show("Could not find directory: " + extractedPackagesDirectory +
                    "\nWould you like me to create it for you?", "Missing Directory", MessageBoxButtons.OKCancel, 
                    MessageBoxIcon.Question);

                switch (dialogResult) {
                    case DialogResult.OK:
                        Directory.CreateDirectory(extractedPackagesDirectory);
                        break;
                    case DialogResult.Cancel:
                        MessageBox.Show("One or more directory checks failed. Please create the proper directory " + 
                            "structure manually and then retry", "Process Aborted", MessageBoxButtons.OK, 
                            MessageBoxIcon.Information);
                        return false;
                }
            }

            // Check that the output folder exists and is empty.
            if (Directory.Exists(extractedPackagesDirectory))
            {
                if (Directory.EnumerateFileSystemEntries(outputRootDirectory).Any())
                {
                    DialogResult dialogResult = MessageBox.Show("The Output directory is not empty. Continuing may " +
                        "overwrite previously processed files. Are you sure you want to continue?", "Question", 
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

                    if (dialogResult == DialogResult.Cancel)
                    {
                        MessageBox.Show("OK - No files have been overwritten. Please rerun the process once you are" +
                                " happy that the directory is empty or that the contents may be overwritten", 
                                "Process Aborted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return false;
                    }
                }
                
            } else {
                MessageBox.Show("Extracted Packages directory does not exist.", "Error", MessageBoxButtons.OK, 
                    MessageBoxIcon.Error);
            }

            return true;
        }

        private string getExtractedPackagesDirectory() {
            return Path.Combine(this.txtPath.Text, "Source\\Extracted Packages");
        }

        private string getSourceRootDirectory() {
            return Path.Combine(this.txtPath.Text, "Source");
        }

        private string getOutputRootDirectory() {
            return Path.Combine(this.txtPath.Text, "Output");
        }

        private void copyGlobalTextures()
        {
            string extractedPackagesDir = this.getExtractedPackagesDirectory();
            DirectoryInfo[] packageDirs = new DirectoryInfo(extractedPackagesDir).GetDirectories();

            // Create destination folder for SimObjects.


            foreach (DirectoryInfo package in packageDirs) {
                Console.WriteLine("Processing package: " + package.Name);

                DirectoryInfo[] modelDirs = new DirectoryInfo(Path.Combine(package.FullName, "aircraft")).GetDirectories();

                foreach (DirectoryInfo model in modelDirs) {
                    string destDir = Path.Combine(this.getOutputRootDirectory(), "SimObjects", "Airplanes", model.Name);
                    Console.WriteLine("Copy model " + model + " to " + destDir);
                }
            }
        }

        private void moveAircraft()
        {

        }

        private void copyBgls()
        {

        }

        private void resetUi(bool retainProgress) {
            this.unlockUi();

            if (!retainProgress) {
                pbProgress.Value = 0;
            }
        }

        private void btnGo_Click(object sender, EventArgs e)
        {
            bool folderCheckOk;

            this.lockUi();
            this.updateProgress(1);

            folderCheckOk =  this.checkFolderStructure();

            if (!folderCheckOk) {
                this.resetUi(false);
                return;
            }

            this.copyGlobalTextures();

            this.updateProgress(5);
            this.updateProgress(50);
        }

        private void lockUi()
        {
            this.txtPath.Enabled = false;
            this.btnChooseFolder.Enabled = false;
            this.btnGo.Enabled = false;
        }

        private void unlockUi()
        {
            this.txtPath.Enabled = true;
            this.btnChooseFolder.Enabled = true;
            this.btnGo.Enabled = true;
        }

        private void updateProgress(int value)
        {
            this.pbProgress.Value = value;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void aboutToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            MessageBox.Show("WOAI Installer for P3D - v1.0\nCreated by " + 
                "Wills Bithrey\nPlease file issues on GitHub at " + 
                "https://github.com/WillsB3/WOAI-P3D-Installer", "About", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
