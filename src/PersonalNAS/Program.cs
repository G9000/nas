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

                TrayApplicationContext applicationContext = null;
                try
                {
                    applicationContext = new TrayApplicationContext();
                    applicationContext.StartInitially();
                    Application.Run(applicationContext);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Personal NAS could not start.\r\n\r\n" + ex.Message,
                        "Personal NAS",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                finally
                {
                    if (applicationContext != null)
                    {
                        try
                        {
                            applicationContext.CleanupAfterRun();
                        }
                        catch (Exception cleanupException)
                        {
                            MessageBox.Show(
                                "Personal NAS could not fully stop its server.\r\n\r\n" + cleanupException.Message,
                                "Personal NAS",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    }

                    instanceMutex.ReleaseMutex();
                }
            }
        }
    }
}
