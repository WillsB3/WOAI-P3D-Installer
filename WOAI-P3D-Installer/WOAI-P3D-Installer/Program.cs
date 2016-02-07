using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Squirrel;

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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Main());

            Task.Run(async () =>
            {
                using (var mgr = UpdateManager.GitHubUpdateManager("https://github.com/WillsB3/WOAI-P3D-Installer")) {
                    await mgr.Result.UpdateApp();
                }
            });
        }

    }
}
