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
using System.Reflection;
using NLog;
using Squirrel;

namespace WOAI_P3D_Installer
{
    public partial class Main : Form 
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private Task<UpdateManager> updateManager = UpdateManager.GitHubUpdateManager("https://github.com/WillsB3/WOAI-P3D-Installer");

        public Main() {
            InitializeComponent();
            logger.Info("Main form loaded.");

            // Display the version number in the status bar.
            this.tsslVersion.Text = this.getVersion();

            Task update = new Task(checkForUpdate);
            update.Start();
            update.Wait();
        }

        private async void checkForUpdate() {
            logger.Debug("Starting update check...");

            Task<UpdateInfo> updates = this.checkUpdate();
            await updates;
                     
            if (updates.Result.ReleasesToApply.Count != 0) {
                logger.Info("Update available");

                DialogResult dialogResult = MessageBox.Show("There is an update available. Do you want to " + 
                    "install it now?", "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (dialogResult == DialogResult.Yes) {
                    logger.Info("User accepted update.");
                    Task update = this.doUpdate(updates.Result);
                    await update;
                } else {
                    logger.Info("User deferred installing update.");
                }
            } else {
                logger.Info("No updates available");
            }
        }

        private async Task doUpdate(UpdateInfo u, Action<int> progress = null) {
            try {
                await this.updateManager.Result.DownloadReleases(u.ReleasesToApply, x => progress(x / 3 + 33));
            } catch (Exception ex) {
                logger.Error(ex, "Error trying to download releases.");
            } 

            try {
                await this.updateManager.Result.ApplyReleases(u, x => progress(x / 3 + 66));
            } catch (Exception ex) {
                logger.Error(ex, "Error while trying to apply releases.");
            }

            try {
                await this.updateManager.Result.CreateUninstallerRegistryEntry();
            } catch (Exception ex) {
                logger.Error(ex, "Error while trying to create uninstaller registry entry.");
            }
        }

        private async Task<UpdateInfo> checkUpdate() {
            // Note, in most of these scenarios, the app exits after this method completes!
            SquirrelAwareApp.HandleEvents(
                onInitialInstall: v => {
                    updateManager.Result.CreateShortcutForThisExe();
                    logger.Debug("Squirrel:OnInitialInstall: " + v);
                },
                onAppUpdate: v => {
                    updateManager.Result.CreateShortcutForThisExe();
                    logger.Debug("Squirrel:OnAppUpdate: " + v);
                },
                onAppUninstall: v => {
                    updateManager.Result.RemoveShortcutForThisExe();
                    logger.Debug("Squirrel:OnAppUninstall: " + v);
                },
                onFirstRun: () => {
                    logger.Debug("Squirrel:OnFirstRun");
                }
            );

            try {
                UpdateInfo u = await updateManager.Result.CheckForUpdate(false);
                return u;
            } catch (Exception ex) {
                logger.Error(ex, "Error while trying to check for updates.");
                return null;
            }
        }

        private void btnChooseFolder_Click(object sender, EventArgs e) {
            if (fbdPath.ShowDialog() == DialogResult.OK) {
                txtPath.Text = fbdPath.SelectedPath;
            }
        }

        private bool checkFolderStructure() {
            logger.Trace("Starting folder structure check.");
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

        private string getSceneryOutputDirectory() {
            return Path.Combine(this.getOutputRootDirectory(), "WOAI Traffic", "scenery");
        }

        private int calculateTaskProgress(string task, float progress) {
            float result;

            switch (task) {
                case "COPY_AIRCRAFT":
                    result = 0.8f * progress;
                    break;
                case "COPY_TEXTURES":
                    result = 0.3f;
                    break;
                case "CHECK_DIRS":
                    result = 0.1f * progress;
                    break;
                default:
                    result = 0;
                    break;
            }

            return (int)result;
        }

        private void copyGlobalTextures() {
            logger.Trace("Starting main processing.");
            string extractedPackagesDir = this.getExtractedPackagesDirectory();
              DirectoryInfo[] packageDirs = new DirectoryInfo(extractedPackagesDir).GetDirectories();
            int numPackages = packageDirs.Length;
            float progress = 0.0f;
            float singlePackagePerc = 100.0f / numPackages;
            logger.Trace("About to process {0} packages.", numPackages);

            // Create destination folder for SimObjects.
            foreach (DirectoryInfo package in packageDirs) {
                Console.WriteLine("Processing package: " + package.Name);

                string srcBglPath = Path.Combine(package.FullName, "scenery", "world", "scenery");
                string srcModelPath = Path.Combine(package.FullName, "aircraft");
                string srcTexturePath = Path.Combine(package.FullName, "Texture");
                string destBglPath = Path.Combine(this.getOutputRootDirectory(), "WOAI Traffic", "scenery");
                string destModelPath = Path.Combine(this.getOutputRootDirectory(), "SimObjects", "Airplanes");
                string destTexturePath = Path.Combine(this.getOutputRootDirectory(), "SimObjects", "Airplanes", "WOAI_Base", "Texture_Fallback");
                bool textureFallbackDirExists = Directory.Exists(destTexturePath);

                // Copy Models to <OUTPUT_ROOT>/SimObjects/<model_name>
                DirectoryInfo[] modelDirs = new DirectoryInfo(srcModelPath).GetDirectories();

                foreach (DirectoryInfo model in modelDirs) {
                    string destDir = Path.Combine(destModelPath, model.Name);
                    Console.WriteLine("Copying model: " + model.FullName + " --> " + destDir);
                    copyDirectory(model.FullName, destDir);
                }

                // Copy Scenery to <OUTPUT_ROOT>/WOAI Traffic/scenery
                // Create <OUTPUT_ROOT>/WOAI Traffic/scenery directory
                CreateDirectory(new DirectoryInfo(this.getSceneryOutputDirectory()));
                FileInfo[] sceneryFiles = new DirectoryInfo(srcBglPath).GetFiles();

                foreach (FileInfo sceneryFile in sceneryFiles) {
                    string dest = Path.Combine(destBglPath, sceneryFile.Name);
                    Console.WriteLine("Copying scenery file: " + sceneryFile.FullName + " --> " + dest);
                    File.Copy(sceneryFile.FullName, dest, true);
                }

                // Copy textures from <model_name>/Texture to <OUTPUT_ROOT>/SimObjects/Airplanes/WOAI_Fallback
                if (Directory.Exists(srcTexturePath)) {
                    FileInfo[] textureFiles = new DirectoryInfo(srcTexturePath).GetFiles();

                    if (!textureFallbackDirExists) {
                        CreateDirectory(new DirectoryInfo(destTexturePath));
                    }

                    foreach (FileInfo textureFile in textureFiles) {
                        string dest = Path.Combine(destTexturePath, textureFile.Name);
                        Console.WriteLine("Copying texture file: " + textureFile.FullName + " --> " + dest);
                        File.Copy(textureFile.FullName, dest, true);
                    }
                }

                // Update progress.
                progress = progress + singlePackagePerc;
                Console.WriteLine("Updating progress to " + progress);
                this.updateProgress((int)progress);
            }

            logger.Trace("Processing complete.");
        }

        public static void CreateDirectory(DirectoryInfo directory) {
            if (!directory.Parent.Exists) {
                CreateDirectory(directory.Parent);
            }
            directory.Create();
        }

        private static void copyDirectory(string sourcePath, string destPath) {
            if (!Directory.Exists(destPath)) {
                Directory.CreateDirectory(destPath);
            }

            foreach (string file in Directory.GetFiles(sourcePath)) {
                string dest = Path.Combine(destPath, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }

            foreach (string folder in Directory.GetDirectories(sourcePath)) {
                string dest = Path.Combine(destPath, Path.GetFileName(folder));
                copyDirectory(folder, dest);
            }
        }

        private void resetUi(bool retainProgress) {
            this.unlockUi();

            if (!retainProgress) {
                pbProgress.Value = 0;
            }
        }

        private void btnGo_Click(object sender, EventArgs e) {
            bool folderCheckOk;

            this.updateProgress(0);
            this.lockUi();
            this.updateProgress(1);

            folderCheckOk =  this.checkFolderStructure();
            this.updateProgress(this.calculateTaskProgress("CHECK_DIRS", 100.0f));

            if (!folderCheckOk) {
                this.resetUi(false);
                return;
            }

            this.updateProgress(this.calculateTaskProgress("DIRECTORY_CHECK", 100.0f));

            this.copyGlobalTextures();

            this.done();
        }

        private void lockUi() {
            this.txtPath.Enabled = false;
            this.btnChooseFolder.Enabled = false;
            this.btnGo.Enabled = false;
        }

        private void unlockUi() {
            this.txtPath.Enabled = true;
            this.btnChooseFolder.Enabled = true;
            this.btnGo.Enabled = true;
        }

        private void updateProgress(int value) {
            this.pbProgress.Value = value;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e) {
            Application.Exit();
        }

        private void done() {
            MessageBox.Show("Done! ", "Processing Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.resetUi(true);
        }

        private void aboutToolStripMenuItem1_Click(object sender, EventArgs e) {
            MessageBox.Show("WOAI Installer for P3D - v" + this.getVersion() + "\nCreated by " + 
                "Wills Bithrey\nPlease file issues on GitHub at " + 
                "https://github.com/WillsB3/WOAI-P3D-Installer", "About", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string getVersion() {
            Assembly assembly = Assembly.GetExecutingAssembly();
            return assembly.GetName().Version.ToString(4);
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e) {
            this.updateManager.Dispose();
        }
    }
}
