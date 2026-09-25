using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PersonalNAS
{
    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private const string LocalNasUrl = "http://localhost:8080";

        private readonly NasServer server;
        private readonly Control uiDispatcher;
        private readonly ContextMenuStrip contextMenu;
        private readonly NotifyIcon trayIcon;
        private bool exiting;
        private bool shutdownComplete;

        public TrayApplicationContext()
        {
            server = new NasServer();

            uiDispatcher = new Control();
            if (uiDispatcher.Handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("The tray application's UI dispatcher could not be created.");
            }

            contextMenu = new ContextMenuStrip();
            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "Personal NAS is stopped",
                ContextMenuStrip = contextMenu,
                Visible = true
            };
            trayIcon.DoubleClick += delegate { OpenNas(); };
            server.StateChanged += OnServerStateChanged;

            RefreshTrayState();
            StartServer(true);
        }

        protected override void ExitThreadCore()
        {
            if (shutdownComplete)
            {
                base.ExitThreadCore();
                return;
            }

            exiting = true;
            try
            {
                server.Stop();
                if (server.IsRunning)
                {
                    throw new InvalidOperationException("The server is still running.");
                }

                server.Dispose();
            }
            catch (Exception ex)
            {
                exiting = false;
                RefreshTrayState();
                MessageBox.Show(
                    "The NAS server could not be stopped, so Personal NAS will stay in the tray.\r\n\r\n" + ex.Message,
                    "Personal NAS",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            server.StateChanged -= OnServerStateChanged;
            trayIcon.Visible = false;
            trayIcon.Dispose();
            contextMenu.Dispose();
            uiDispatcher.Dispose();
            shutdownComplete = true;

            base.ExitThreadCore();
        }

        private void OnServerStateChanged(object sender, EventArgs e)
        {
            RunOnUiThread(delegate
            {
                if (!exiting && !shutdownComplete)
                {
                    RefreshTrayState();
                }
            });
        }

        private void RunOnUiThread(MethodInvoker action)
        {
            if (shutdownComplete || uiDispatcher.IsDisposed)
            {
                return;
            }

            try
            {
                if (uiDispatcher.InvokeRequired)
                {
                    uiDispatcher.BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch (ObjectDisposedException)
            {
                // The message loop may already be closing.
            }
            catch (InvalidOperationException)
            {
                // The message loop may already be closing.
            }
        }

        private void StartServer(bool openBrowserWhenStarted)
        {
            if (exiting || shutdownComplete)
            {
                return;
            }

            try
            {
                server.Start();
                if (!server.IsRunning)
                {
                    throw new InvalidOperationException("The server did not enter the running state.");
                }

                RefreshTrayState();
                if (openBrowserWhenStarted)
                {
                    OpenNas();
                }
            }
            catch (Exception ex)
            {
                RefreshTrayState();
                MessageBox.Show(
                    "Personal NAS could not start the server.\r\n\r\n" + ex.Message,
                    "Personal NAS",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void StopServer()
        {
            try
            {
                server.Stop();
                RefreshTrayState();
            }
            catch (Exception ex)
            {
                RefreshTrayState();
                MessageBox.Show(
                    "Personal NAS could not stop the server.\r\n\r\n" + ex.Message,
                    "Personal NAS",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OpenNas()
        {
            if (!server.IsRunning)
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(LocalNasUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Personal NAS could not open the local web page.\r\n\r\n" + ex.Message,
                    "Personal NAS",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void CopyLanAddress()
        {
            RunOnUiThread(delegate
            {
                if (!server.IsRunning)
                {
                    return;
                }

                try
                {
                    Clipboard.SetText(NetworkAddress.GetLanUrl());
                    trayIcon.ShowBalloonTip(
                        2000,
                        "Personal NAS",
                        "LAN address copied to the clipboard.",
                        ToolTipIcon.Info);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Personal NAS could not copy the LAN address.\r\n\r\n" + ex.Message,
                        "Personal NAS",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            });
        }

        private void RefreshTrayState()
        {
            bool isRunning = server.IsRunning;
            RebuildMenu(isRunning);

            if (isRunning)
            {
                trayIcon.Text = "Personal NAS is running at " + NetworkAddress.GetLanUrl().Replace("http://", "");
            }
            else
            {
                trayIcon.Text = "Personal NAS is stopped";
            }
        }

        private void RebuildMenu(bool isRunning)
        {
            while (contextMenu.Items.Count > 0)
            {
                ToolStripItem item = contextMenu.Items[0];
                contextMenu.Items.RemoveAt(0);
                item.Dispose();
            }

            if (isRunning)
            {
                contextMenu.Items.Add("Open NAS", null, delegate { OpenNas(); });
                contextMenu.Items.Add("Copy LAN address", null, delegate { CopyLanAddress(); });
                contextMenu.Items.Add("Stop server", null, delegate { StopServer(); });
            }
            else
            {
                contextMenu.Items.Add("Start server", null, delegate { StartServer(false); });
            }

            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, delegate { ExitThread(); });
        }
    }
}
