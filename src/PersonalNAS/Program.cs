using System;
using System.Threading;
using System.Windows.Forms;

namespace PersonalNAS
{
    internal static class Program
    {
        private const string MutexName = @"Local\PersonalNAS.TrayLauncher";

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool createdNew;
            using (Mutex instanceMutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "Personal NAS is already running. Use its tray icon to manage the server.",
                        "Personal NAS",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    Application.Run(new TrayApplicationContext());
                }
                finally
                {
                    instanceMutex.ReleaseMutex();
                }
            }
        }
    }
}
