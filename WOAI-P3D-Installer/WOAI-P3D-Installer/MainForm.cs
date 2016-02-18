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
    public partial class MainForm : Form 
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public MainForm() {
            InitializeComponent();
            logger.Info("Main form loaded.");

            // Display the version number in the status bar.
            this.tsslVersion.Text = this.getVersion();
        }

        private async Task update() {
            logger.Info("Starting update check…");

            this.tsslStatus.Text = "Checking for Updates…";

            using (var mgr = UpdateManager.GitHubUpdateManager("https://github.com/WillsB3/WOAI-P3D-Installer")) {
                var updates = await mgr.Result.CheckForUpdate();

                if (updates.ReleasesToApply.Any()) {
                    var latestVersion = updates.ReleasesToApply.OrderBy(x => x.Version).Last();
                    logger.Info("Update available. Current Version {0}, New Version {1}", mgr.Result.CurrentlyInstalledVersion(), latestVersion.Version.ToString());

                    DialogResult dialogResult = MessageBox.Show("There is an update available. Do you want to " +
                        "install it now?", "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (dialogResult == DialogResult.Yes) {
                        logger.Info("User accepted update.");
                        this.lockUi();

                        try {
                            this.tsslStatus.Text = "Downloading updates…";
                            await mgr.Result.DownloadReleases(updates.ReleasesToApply);
                        } catch (Exception ex) {
                            logger.Error(ex, "Error trying to download releases.");
                            MessageBox.Show("There was an error trying to download updates.", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            this.resetUi();
                        }

                        try {
                            this.tsslStatus.Text = "Applying updates…";
                            await mgr.Result.ApplyReleases(updates);
                        } catch (Exception ex) {
                            logger.Error(ex, "Error while trying to apply updates.");
                            MessageBox.Show("There was an error trying to apply updates.", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            this.resetUi();
                        }

                        try {
                            await mgr.Result.UpdateApp();
                        } catch (Exception ex) {
                            logger.Error(ex, "Error calling updateApp.");
                            MessageBox.Show("There was an error to update the application.", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            this.resetUi();
                        }

                        //try {
                        //    await mgr.Result.CreateUninstallerRegistryEntry();
                        //} catch (Exception ex) {
                        //    logger.Error(ex, "Error while trying to create uninstaller registry entry.");
                        //    MessageBox.Show("There was an error trying to create the relevant registry entries for the update. " +
                        //        "Please try reinstalling the latest full version of the application.", "Error",
                        //        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        //    this.resetUi();
                        //}

                        string latestExe = Path.Combine(mgr.Result.RootAppDirectory, string.Concat("app-", latestVersion.Version.Version.Major, ".", latestVersion.Version.Version.Minor, ".", latestVersion.Version.Version.Build, ".", latestVersion.Version.Version.Revision), "WOAI_P3D_Installer.exe");
                        logger.Info("Updates Applied successfully.");

                        MessageBox.Show("Nearly there. The application will now restart to complete the update.", "Update Downloaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        logger.Info("Restarting App to complete update.");
                        // UpdateManager.RestartApp(latestExe);
                    } else {
                        logger.Info("User deferred installing update.");
                        this.resetUi();
                    }
                } else {
                    logger.Info("No updates available.");
                    this.resetUi();
                }
            }
        }

        public static void OnInitialInstall(Version version) {
            logger.Info("Squirrel Event: OnInitialInstall");

            var exePath = Assembly.GetEntryAssembly().Location;
            string appName = Path.GetFileName(exePath);

            using (var mgr = UpdateManager.GitHubUpdateManager("https://github.com/WillsB3/WOAI-P3D-Installer")) {
                // Create Desktop and Start Menu shortcuts
                mgr.Result.CreateShortcutsForExecutable(appName, ShortcutLocation.StartMenu | ShortcutLocation.Desktop, false);
            }
        }

        public static void OnAppUpdate(Version version) {
            logger.Info("Squirrel Event: OnAppUpdate");
        }

        public static void OnAppUninstall(Version version) {
            logger.Info("Squirrel Event: OnAppUninstall");

            var exePath = Assembly.GetEntryAssembly().Location;
            string appName = Path.GetFileName(exePath);

            using (var mgr = UpdateManager.GitHubUpdateManager("https://github.com/WillsB3/WOAI-P3D-Installer")) {
                // Remove Desktop and Start Menu shortcuts
                mgr.Result.RemoveShortcutsForExecutable(appName, ShortcutLocation.StartMenu | ShortcutLocation.Desktop);
            }
        }

        private void btnChooseFolder_Click(object sender, EventArgs e) {
            if (fbdPath.ShowDialog() == DialogResult.OK) {
                txtPath.Text = fbdPath.SelectedPath;
            }
        }

        private bool checkFolderStructure() {
            logger.Info("Starting folder structure check.");
            string sourceRootDirectory = this.getSourceRootDirectory();
            string extractedPackagesDirectory = this.getExtractedPackagesDirectory();
            string outputRootDirectory = this.getOutputRootDirectory();
            this.tsslStatus.Text = "Checking Folder Structure…";
            this.statusStrip.Refresh();

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
            return Path.Combine(this.txtPath.Text, @"Source\Extracted Packages");
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
            string extractedPackagesDir = this.getExtractedPackagesDirectory();
            DirectoryInfo[] packageDirs = new DirectoryInfo(extractedPackagesDir).GetDirectories();
            int numPackages = packageDirs.Length;
            float progress = 0.0f;
            float singlePackagePerc = 100.0f / numPackages;
            logger.Info("About to process {0} packages.", numPackages);
            this.tsslStatus.Text = "Processing…";
            this.statusStrip.Refresh();

            // Create destination folder for SimObjects.
            foreach (DirectoryInfo package in packageDirs) {
                logger.Info("Processing package: {0}", package.Name);

                string srcBglPath = Path.Combine(package.FullName, "scenery", "world", "scenery");
                string srcModelPath = Path.Combine(package.FullName, "aircraft");
                string srcTexturePath = Path.Combine(package.FullName, "Texture");
                string destBglPath = Path.Combine(this.getOutputRootDirectory(), "WOAI Traffic", "scenery");
                string destModelPath = Path.Combine(this.getOutputRootDirectory(), "SimObjects", "Airplanes");
                string destTexturePath = Path.Combine(this.getOutputRootDirectory(), "SimObjects", "Airplanes", "WOAI_Base", "Texture_Fallback");
                bool needsTextureFallback = false;
                bool textureFallbackDirExists = Directory.Exists(destTexturePath);

                // Copy Models to <OUTPUT_ROOT>/SimObjects/<model_name>
                DirectoryInfo[] modelDirs = new DirectoryInfo(srcModelPath).GetDirectories();

                // Copy global textures from <model_name>/Texture to <OUTPUT_ROOT>/SimObjects/Airplanes/WOAI_Fallback
                if (Directory.Exists(srcTexturePath)) {
                    FileInfo[] textureFiles = new DirectoryInfo(srcTexturePath).GetFiles();
                    needsTextureFallback = true;

                    if (!textureFallbackDirExists) {
                        CreateDirectory(new DirectoryInfo(destTexturePath));
                    }

                    foreach (FileInfo textureFile in textureFiles) {
                        string dest = Path.Combine(destTexturePath, textureFile.Name);
                        logger.Info("Copying texture file: {0} --> {1}", textureFile.FullName, dest);
                        File.Copy(textureFile.FullName, dest, true);
                    }
                }

                foreach (DirectoryInfo model in modelDirs) {
                    string modelDestDir = Path.Combine(destModelPath, model.Name);
                    logger.Info("Copying model: {0} --> {1}", model.FullName, modelDestDir);
                    copyDirectory(model.FullName, modelDestDir);

                    if (needsTextureFallback) {
                        // For each texture  folder in <OUTPUT_ROOT>/SimObjects/Airplanes/<model_name>/
                        DirectoryInfo[] allDirs = new DirectoryInfo(modelDestDir).GetDirectories();
                        DirectoryInfo[] textureDirs = Array.FindAll(allDirs, dir => dir.Name.ToLower().StartsWith("texture."));

                        foreach (DirectoryInfo texture in textureDirs) {
                            string textureCfgPath = Path.Combine(texture.FullName, "texture.cfg");
                            string[] lines = {
                                "[fltsim]",
                                @"fallback.1=..\..\WOAI_Base\Texture_Fallback"
                            };

                            logger.Info("Writing texture.cfg to {0}", textureCfgPath);
                            System.IO.File.WriteAllLines(textureCfgPath, lines);
                        }
                    }
                }

                // Copy Scenery to <OUTPUT_ROOT>/WOAI Traffic/scenery
                // Create <OUTPUT_ROOT>/WOAI Traffic/scenery directory
                CreateDirectory(new DirectoryInfo(this.getSceneryOutputDirectory()));
                FileInfo[] sceneryFiles = new DirectoryInfo(srcBglPath).GetFiles();

                foreach (FileInfo sceneryFile in sceneryFiles) {
                    string dest = Path.Combine(destBglPath, sceneryFile.Name);
                    logger.Info("Copying scenery file: {0} --> {1}", sceneryFile.FullName, dest);
                    File.Copy(sceneryFile.FullName, dest, true);
                }

                // Update progress.
                progress = progress + singlePackagePerc;
                this.updateProgress((int)progress);
            }

            logger.Info("Processing complete.");
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

        private void btnGo_Click(object sender, EventArgs e) {
            bool folderCheckOk;

            logger.Info("Processing started…");

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

        private void resetUi() {
            this.resetUi(false);
        }

        private void resetUi(bool retainProgress) {
            this.unlockUi();

            if (!retainProgress) {
                pbProgress.Value = 0;
            }

            this.tsslStatus.Text = "Ready";
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
            logger.Debug("Updating progress to {0}", value);
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

        private void checkForUpdatesToolStripMenuItem_Click(object sender, EventArgs e) {
            logger.Info("Manual update check initiated.");
            var t = this.update();
            t.Wait();
        }
    }
}
