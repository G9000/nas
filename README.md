# 📁 Personal NAS & Cloud Storage (Local & Online)

A personal network storage solution running on your PC. It lets you:
1. **Share files locally** across your Wi-Fi (laptop, desktop, phone, or tablet).
2. **Access files in Windows File Explorer** through an SMB share.
3. **Browse files with a web interface** with media streaming, drag-and-drop uploads, and shareable links.
4. **Access the web interface remotely** through a separate Cloudflare Tunnel workflow.

The bundled File Browser version is archived upstream and no longer receives security fixes. The tunnel provides an HTTPS connection, but it does not make the unmaintained application safe for sensitive data. Avoid exposing sensitive files through the public tunnel.

---

## 🚀 Quick Start

| Action | How to Run |
| :--- | :--- |
| **Start NAS Server (recommended)** | Double-click `PersonalNAS.exe` in this project folder. |
| **Start NAS Server (fallback)** | Double-click `Start_NAS.bat`, or run `scripts\start-nas.ps1` in PowerShell. |
| **Stop fallback server** | Double-click `Stop_NAS.bat`, or run `scripts\stop-nas.ps1` in PowerShell. |
| **Online Internet Access** | Double-click `Start_Online_Access.bat` (separate public tunnel flow). |
| **Enable Windows Explorer (SMB)** | Double-click `Enable_Windows_File_Sharing.bat`. |

If `PersonalNAS.exe` is not in the project folder yet, open PowerShell in the project folder and run `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-launcher.ps1` once. The build script downloads and verifies the pinned File Browser release if needed and creates the EXE. Building requires the Windows x64 .NET Framework C# compiler at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`; if it is missing, install the .NET Framework 4.x developer tools.

Keep the project folder writable. `PersonalNAS.exe` uses the adjacent `data\` folder for its database and logs and the adjacent `storage\` folder for your files. Keep both folders beside the EXE; they hold your existing NAS data and settings.

---

## 🖥️ Start and control the NAS

When you double-click `PersonalNAS.exe`, it starts the local and LAN web server, opens `http://localhost:8080` in your browser, and adds a NAS icon to the Windows notification area (system tray).

While the server is running, the tray menu provides:

- **Open NAS** — open the local web page on this computer.
- **Copy LAN address** — copy the current LAN web address for another device on the same Wi-Fi. The address can change when your network adapter or IP changes, so use this action instead of a saved IP address.
- **Stop server** — stop the server started by this launcher; the tray app stays available.
- **Exit** — stop the server started by this launcher and close the tray app.

After stopping the server, choose **Start server** in the tray menu to start it again, or **Exit** to close the app.

If **Copy LAN address** gives you `http://127.0.0.1:8080`, Windows did not report a usable LAN address. Check that Wi-Fi or Ethernet is connected and active; `127.0.0.1` only works on this computer.

The legacy `Stop_NAS.bat` / `scripts\stop-nas.ps1` fallback stops every process named `filebrowser` and `cloudflared`, including processes started separately. The tray app's **Stop server** and **Exit** only stop the server instance started by that tray app.

### Default Login Credentials

- **Username**: `admin`
- **Password**: `NasAdmin2026!`

You can change the password in **Settings** in the web interface.

---

## 🌐 1. Local Wi-Fi Access (Web UI)

- **On this computer**: Visit [http://localhost:8080](http://localhost:8080), or choose **Open NAS** in the tray menu.
- **On another device on the same Wi-Fi**: choose **Copy LAN address** from the tray menu, then open the copied address on that device.

---

## 🖥️ 2. Windows File Explorer (Native SMB Share)

To access the storage folder as a network location in Windows Explorer:

1. Double-click `Enable_Windows_File_Sharing.bat` and approve the Administrator prompt.
2. On another Windows PC on the same network, press `Win + R` and enter `\\ANIMAOS\NAS` (replace `ANIMAOS` with this computer's name if needed).
3. If the computer name does not resolve, choose **Copy LAN address** in the tray menu and use its IP in the SMB path, for example `\\<IP>\NAS` (remove `http://` and `:8080` from the copied web address).
4. To map a permanent drive, open **This PC** in File Explorer, choose **Map network drive**, select a drive letter, and enter the same SMB path.

The SMB setup shares the adjacent `storage\` folder. SMB access is separate from the web server and the online tunnel.

The SMB setup grants `Everyone` full control of the share and storage files. Use it only on a trusted private network: anyone who can reach the share may change or delete its files.

---

## ☁️ 3. Online Access (Cloudflare Tunnel)

This is a separate workflow from the tray launcher and exposes the web interface through a public URL:

1. Start the NAS locally and change the initial admin password in the web interface before making the server public.
2. Double-click `Start_Online_Access.bat` to start the tunnel.
3. Open the generated `https://…trycloudflare.com` address on your remote device.
4. Press **Enter** in the tunnel window when you want to stop online access.

The browser's public HTTPS connection terminates at Cloudflare, which forwards traffic to this PC through its encrypted tunnel. The bundled File Browser release is archived and no longer receives security fixes. Do not use this public tunnel for sensitive files.

---

## 📂 Storage Directory

Your files remain in the `storage\` folder beside `PersonalNAS.exe`:

- Put files in `storage\` manually or upload them through the web interface.
- Existing subfolders include `Documents\`, `Photos\`, `Videos\`, and `Desktop_Share\`.

The existing database and logs remain in the adjacent `data\` folder. Keep the project folder writable so the NAS can update its files.

---

## 🔄 Optional: Sync with Google Drive

To synchronize the NAS storage folder with your Google Drive account:

- Run this in PowerShell from the project folder:
  ```powershell
  .\scripts\setup-gdrive-sync.ps1
  ```
- Follow the instructions to link `rclone` to your Google Drive account.

The setup guide prints `rclone sync` examples. `sync` mirrors deletions too, so preview the changes and keep a backup before running them. Use `rclone copy` instead if you do not want the destination to delete files.
