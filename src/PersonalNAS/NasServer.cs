using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

namespace PersonalNAS
{
    internal sealed class NasServer : IDisposable
    {
        private const int ServerPort = 8080;
        private const int StartupTimeoutMilliseconds = 15000;
        private const int StopTimeoutMilliseconds = 5000;
        private const int CommandTimeoutMilliseconds = 30000;
        private const string RuntimeVersion = "2.63.23";

        private readonly object syncRoot = new object();
        private readonly string launcherDirectory;
        private readonly string dataDirectory;
        private readonly string storageDirectory;
        private readonly string databasePath;
        private readonly string logPath;
        private readonly StringBuilder standardOutput = new StringBuilder();
        private readonly StringBuilder standardError = new StringBuilder();
        private Process serverProcess;
        private bool disposed;

        internal event EventHandler StateChanged;

        internal NasServer()
        {
            launcherDirectory = AppDomain.CurrentDomain.BaseDirectory;
            dataDirectory = Path.Combine(launcherDirectory, "data");
            storageDirectory = Path.Combine(launcherDirectory, "storage");
            databasePath = Path.Combine(dataDirectory, "filebrowser.db");
            logPath = Path.Combine(dataDirectory, "nas.log");
        }

        internal bool IsRunning
        {
            get
            {
                lock (syncRoot)
                {
                    return IsProcessRunning(serverProcess);
                }
            }
        }

        internal void Start()
        {
            Process exitedProcess = null;
            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (IsProcessRunning(serverProcess))
                {
                    return;
                }

                exitedProcess = serverProcess;
                serverProcess = null;
                standardOutput.Length = 0;
                standardError.Length = 0;
            }

            if (exitedProcess != null)
            {
                try { exitedProcess.WaitForExit(); } catch { }
                DetachAndDispose(exitedProcess);
            }

            PrepareUserDirectories();
            string executablePath = ExtractFileBrowserIfMissing();
            InitializeDatabaseIfMissing(executablePath);
            EnsurePortAvailable();

            Process ownedProcess = CreateServerProcess(executablePath);
            lock (syncRoot)
            {
                ThrowIfDisposed();
                serverProcess = ownedProcess;
            }

            try
            {
                bool started;
                try
                {
                    started = ownedProcess.Start();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("File Browser could not be started: " + ex.Message, ex);
                }

                if (!started)
                {
                    throw new InvalidOperationException("Windows did not start the File Browser process.");
                }

                ownedProcess.BeginOutputReadLine();
                ownedProcess.BeginErrorReadLine();
                WaitForServerReady(ownedProcess);
            }
            catch
            {
                StopFailedStart(ownedProcess);
                throw;
            }

            RaiseStateChanged();
        }

        internal void Stop()
        {
            Process ownedProcess;
            lock (syncRoot)
            {
                ownedProcess = serverProcess;
            }

            if (ownedProcess == null)
            {
                return;
            }

            try
            {
                if (IsProcessRunning(ownedProcess))
                {
                    ownedProcess.Kill();
                    if (!ownedProcess.WaitForExit(StopTimeoutMilliseconds))
                    {
                        throw new InvalidOperationException("File Browser did not stop within five seconds.");
                    }
                }

                ownedProcess.WaitForExit();
            }
            catch (Exception ex)
            {
                if (IsProcessRunning(ownedProcess))
                {
                    throw new InvalidOperationException("The owned File Browser process could not be stopped: " + ex.Message, ex);
                }
            }

            ReleaseProcess(ownedProcess);
            RaiseStateChanged();
        }

        public void Dispose()
        {
            Stop();
            lock (syncRoot)
            {
                disposed = true;
            }
        }

        private void PrepareUserDirectories()
        {
            EnsureDirectory(dataDirectory);
            EnsureDirectory(storageDirectory);
            ProbeDirectory(dataDirectory);
            ProbeDirectory(storageDirectory);
        }

        private static void EnsureDirectory(string directoryPath)
        {
            try
            {
                Directory.CreateDirectory(directoryPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("The NAS directory could not be created or accessed: " + directoryPath + Environment.NewLine + ex.Message, ex);
            }
        }

        private static void ProbeDirectory(string directoryPath)
        {
            string probePath = Path.Combine(directoryPath, ".personalnas-write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
            Exception failure = null;

            try
            {
                using (FileStream stream = new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.WriteByte(0);
                }

                File.Delete(probePath);
            }
            catch (Exception ex)
            {
                failure = ex;
                try
                {
                    if (File.Exists(probePath))
                    {
                        File.Delete(probePath);
                    }
                }
                catch
                {
                    // Keep the original probe failure as the useful startup diagnostic.
                }
            }

            if (failure != null)
            {
                throw new InvalidOperationException("The NAS directory is not writable: " + directoryPath + Environment.NewLine + failure.Message, failure);
            }
        }

        private static string ExtractFileBrowserIfMissing()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (String.IsNullOrEmpty(localAppData))
            {
                throw new InvalidOperationException("Windows did not provide a Local AppData directory for the File Browser runtime.");
            }

            string runtimeDirectory = Path.Combine(localAppData, Path.Combine("PersonalNAS", Path.Combine("Runtime", RuntimeVersion)));
            string executablePath = Path.Combine(runtimeDirectory, "filebrowser.exe");

            if (File.Exists(executablePath))
            {
                return executablePath;
            }

            try
            {
                Directory.CreateDirectory(runtimeDirectory);
                using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("PersonalNAS.FileBrowser.exe"))
                {
                    if (resource == null)
                    {
                        throw new InvalidOperationException("The embedded File Browser 2.63.23 executable is missing from PersonalNAS.exe.");
                    }

                    string temporaryPath = executablePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        using (FileStream output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            resource.CopyTo(output);
                        }

                        try
                        {
                            File.Move(temporaryPath, executablePath);
                        }
                        catch (IOException)
                        {
                            if (!File.Exists(executablePath))
                            {
                                throw;
                            }
                        }
                    }
                    finally
                    {
                        if (File.Exists(temporaryPath))
                        {
                            File.Delete(temporaryPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("The File Browser runtime could not be extracted to: " + executablePath + Environment.NewLine + ex.Message, ex);
            }

            return executablePath;
        }

        private void InitializeDatabaseIfMissing(string executablePath)
        {
            if (File.Exists(databasePath))
            {
                return;
            }

            RunInitializationCommand(executablePath, "database initialization", new string[]
            {
                "config", "init",
                "-d", databasePath,
                "-a", "0.0.0.0",
                "-p", "8080",
                "-r", storageDirectory,
                "--branding.name", "My Personal NAS",
                "--minimumPasswordLength", "4"
            });

            RunInitializationCommand(executablePath, "initial administrator creation", new string[]
            {
                "users", "add", "admin", "NasAdmin2026!",
                "-d", databasePath,
                "--perm.admin"
            });
        }

        private void RunInitializationCommand(string executablePath, string operation, string[] arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = BuildArguments(arguments),
                WorkingDirectory = launcherDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (Process command = new Process())
            {
                command.StartInfo = startInfo;
                StringBuilder output = new StringBuilder();
                StringBuilder errors = new StringBuilder();
                command.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        lock (output) { output.AppendLine(e.Data); }
                    }
                };
                command.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        lock (errors) { errors.AppendLine(e.Data); }
                    }
                };

                try
                {
                    if (!command.Start())
                    {
                        throw new InvalidOperationException("Windows did not start the File Browser command.");
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("File Browser could not run " + operation + ". " + ex.Message, ex);
                }

                command.BeginOutputReadLine();
                command.BeginErrorReadLine();

                if (!command.WaitForExit(CommandTimeoutMilliseconds))
                {
                    try { command.Kill(); } catch { }
                    command.WaitForExit();
                    throw new InvalidOperationException("File Browser timed out while performing " + operation + ".");
                }

                command.WaitForExit();
                if (command.ExitCode != 0)
                {
                    string details;
                    lock (output)
                    {
                        lock (errors)
                        {
                            details = FormatCommandOutput(output.ToString(), errors.ToString());
                        }
                    }

                    throw new InvalidOperationException(
                        "File Browser failed during " + operation + " (exit code " + command.ExitCode.ToString() + ")." +
                        (details.Length == 0 ? String.Empty : Environment.NewLine + details));
                }
            }
        }

        private static string FormatCommandOutput(string output, string errors)
        {
            StringBuilder details = new StringBuilder();
            if (!String.IsNullOrWhiteSpace(output))
            {
                details.Append("Output: ").Append(output.Trim());
            }

            if (!String.IsNullOrWhiteSpace(errors))
            {
                if (details.Length > 0)
                {
                    details.AppendLine();
                }

                details.Append("Error: ").Append(errors.Trim());
            }

            return details.ToString();
        }

        private void EnsurePortAvailable()
        {
            TcpListener probe = new TcpListener(IPAddress.Any, ServerPort);
            try
            {
                probe.Start();
            }
            catch (SocketException ex)
            {
                throw new InvalidOperationException("Port 8080 is already in use. Close the other service using that port, then start Personal NAS again.", ex);
            }
            finally
            {
                probe.Stop();
            }
        }

        private Process CreateServerProcess(string executablePath)
        {
            Process ownedProcess = new Process();
            ownedProcess.StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = BuildArguments(new string[]
                {
                    "-d", databasePath,
                    "-r", storageDirectory,
                    "-a", "0.0.0.0",
                    "-p", "8080",
                    "--log", logPath
                }),
                WorkingDirectory = launcherDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            ownedProcess.EnableRaisingEvents = true;
            ownedProcess.OutputDataReceived += OnServerOutputReceived;
            ownedProcess.ErrorDataReceived += OnServerErrorReceived;
            ownedProcess.Exited += OnServerProcessExited;
            return ownedProcess;
        }

        private static string BuildArguments(string[] arguments)
        {
            StringBuilder commandLine = new StringBuilder();
            for (int index = 0; index < arguments.Length; index++)
            {
                if (index > 0)
                {
                    commandLine.Append(' ');
                }

                commandLine.Append(QuoteArgument(arguments[index]));
            }

            return commandLine.ToString();
        }

        private static string QuoteArgument(string argument)
        {
            if (argument.Length > 0 && argument.IndexOfAny(new char[] { ' ', '\t', '\n', '\v', '"' }) < 0)
            {
                return argument;
            }

            StringBuilder quoted = new StringBuilder();
            quoted.Append('"');
            int backslashes = 0;
            for (int index = 0; index < argument.Length; index++)
            {
                char current = argument[index];
                if (current == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (current == '"')
                {
                    quoted.Append('\\', backslashes * 2 + 1);
                    quoted.Append('"');
                    backslashes = 0;
                    continue;
                }

                if (backslashes > 0)
                {
                    quoted.Append('\\', backslashes);
                    backslashes = 0;
                }

                quoted.Append(current);
            }

            if (backslashes > 0)
            {
                quoted.Append('\\', backslashes * 2);
            }

            quoted.Append('"');
            return quoted.ToString();
        }

        private void WaitForServerReady(Process ownedProcess)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(StartupTimeoutMilliseconds);
            Thread.Sleep(100);
            while (DateTime.UtcNow < deadline)
            {
                if (!IsProcessRunning(ownedProcess))
                {
                    throw CreateProcessExitException(ownedProcess);
                }

                if (IsPortAcceptingConnections())
                {
                    if (!IsProcessRunning(ownedProcess))
                    {
                        throw CreateProcessExitException(ownedProcess);
                    }

                    return;
                }

                Thread.Sleep(100);
            }

            if (!IsProcessRunning(ownedProcess))
            {
                throw CreateProcessExitException(ownedProcess);
            }

            string capturedOutput;
            lock (syncRoot)
            {
                capturedOutput = FormatCommandOutput(standardOutput.ToString(), standardError.ToString());
            }

            throw new InvalidOperationException("File Browser did not begin listening on port 8080 within 15 seconds." +
                (capturedOutput.Length == 0 ? String.Empty : Environment.NewLine + capturedOutput));
        }

        private static bool IsPortAcceptingConnections()
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    client.Connect(IPAddress.Loopback, ServerPort);
                    return client.Connected;
                }
            }
            catch (SocketException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        private InvalidOperationException CreateProcessExitException(Process ownedProcess)
        {
            int exitCode = -1;
            try
            {
                ownedProcess.WaitForExit();
                exitCode = ownedProcess.ExitCode;
            }
            catch { }

            string capturedOutput;
            lock (syncRoot)
            {
                capturedOutput = FormatCommandOutput(standardOutput.ToString(), standardError.ToString());
            }

            if (IsPortAcceptingConnections())
            {
                return new InvalidOperationException("File Browser could not bind port 8080 because another service is using it." +
                    (capturedOutput.Length == 0 ? String.Empty : Environment.NewLine + capturedOutput));
            }

            return new InvalidOperationException("File Browser exited during startup (exit code " + exitCode.ToString() + ")." +
                (capturedOutput.Length == 0 ? String.Empty : Environment.NewLine + capturedOutput));
        }

        private void StopFailedStart(Process ownedProcess)
        {
            try
            {
                if (IsProcessRunning(ownedProcess))
                {
                    ownedProcess.Kill();
                    ownedProcess.WaitForExit(StopTimeoutMilliseconds);
                }
            }
            catch
            {
                // Preserve the startup failure that caused this cleanup.
            }

            if (!IsProcessRunning(ownedProcess))
            {
                ReleaseProcess(ownedProcess);
            }
        }

        private void OnServerOutputReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                lock (syncRoot) { standardOutput.AppendLine(e.Data); }
            }
        }

        private void OnServerErrorReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                lock (syncRoot) { standardError.AppendLine(e.Data); }
            }
        }

        private void OnServerProcessExited(object sender, EventArgs e)
        {
            RaiseStateChanged();
        }

        private void RaiseStateChanged()
        {
            EventHandler handler = StateChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void ReleaseProcess(Process ownedProcess)
        {
            bool released = false;
            lock (syncRoot)
            {
                if (Object.ReferenceEquals(serverProcess, ownedProcess))
                {
                    serverProcess = null;
                    released = true;
                }
            }

            if (released)
            {
                DetachAndDispose(ownedProcess);
            }
        }

        private void DetachAndDispose(Process process)
        {
            process.OutputDataReceived -= OnServerOutputReceived;
            process.ErrorDataReceived -= OnServerErrorReceived;
            process.Exited -= OnServerProcessExited;
            process.Dispose();
        }

        private static bool IsProcessRunning(Process process)
        {
            if (process == null)
            {
                return false;
            }

            try
            {
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException("NasServer");
            }
        }
    }
}
