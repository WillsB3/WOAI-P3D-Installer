using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Squirrel;
using NLog;

namespace WOAI_P3D_Installer
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Task.Run(async () => {
                try {
                    using (var mgr = UpdateManager.GitHubUpdateManager("https://github.com/WillsB3/WOAI-P3D-Installer")) {
                        // Note, in most of these scenarios, the app exits after this method completes!
                        SquirrelAwareApp.HandleEvents(
                            v => {
                                mgr.Result.CreateShortcutForThisExe();
                                MessageBox.Show("OnInitialInstall: " + v);
                            },
                            v => {
                                mgr.Result.CreateShortcutForThisExe();
                                MessageBox.Show("OnAppUpdate: " + v);
                            },
                            v => MessageBox.Show("OnAppObsoleted: " + v),
                            v => {
                                mgr.Result.RemoveShortcutForThisExe();
                                MessageBox.Show("OnAppUninstall: " + v);
                            },
                            () => MessageBox.Show("OnFirstRun"));

                        await mgr.Result.UpdateApp();
                    }
                } catch (Exception ex) {
                    Console.WriteLine("Update check failed" + ex);
                }
            });

            NLog.GlobalDiagnosticsContext.Set("logName", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Main());
        }

    }
}
