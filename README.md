# 📁 Personal NAS & Cloud Storage (Local & Online)

A full-featured personal network storage solution running on your PC. It lets you:
1. **Share files locally** across your Wi-Fi (Laptop ↔ Desktop, iPhone, Android, iPad).
2. **Access files in Windows File Explorer** natively via SMB (`\\ANIMAOS\NAS`).
3. **Browse files with a Google Drive-like Web UI** with media streaming, drag-and-drop uploads, and shareable links.
4. **Access your files anywhere online** using a secure HTTPS Cloudflare Tunnel without any port forwarding.

---

## 🚀 Quick Start

| Action | How to Run |
| :--- | :--- |
| **Start NAS Server** | Double-click `Start_NAS.bat` |
| **Stop NAS Server** | Double-click `Stop_NAS.bat` |
| **Online Internet Access** | Double-click `Start_Online_Access.bat` |
| **Enable Windows Explorer (SMB)** | Double-click `Enable_Windows_File_Sharing.bat` |

---

## 🌐 1. Local Wi-Fi Access (Web UI)

Once `Start_NAS.bat` is running:
- **On this computer**: Visit [http://localhost:8080](http://localhost:8080)
- **On your Desktop PC, Phone, or Tablet** (connected to the same Wi-Fi):
  Visit: `http://192.168.100.130:8080`

### Default Login Credentials:
- **Username**: `admin`
- **Password**: `NasAdmin2026!`
*(You can change your password anytime by clicking `Settings` in the web interface).*

---

## 🖥️ 2. Windows File Explorer (Native SMB Share)

If you want your Desktop PC to access files directly as a folder or mapped drive in Windows Explorer:

1. Double-click `Enable_Windows_File_Sharing.bat` (click "Yes" when prompted for Administrator).
2. On your Desktop PC (connected to same Wi-Fi):
   - Press `Win + R`, type:
     ```text
     \\ANIMAOS\NAS
     ```
     *(or `\\192.168.100.130\NAS`)* and press **Enter**.
3. **Map as a permanent drive (e.g. Z: Drive)**:
   - In File Explorer, right-click **This PC** -> **Map network drive**.
   - Choose Drive **Z:** and enter folder: `\\192.168.100.130\NAS`.
   - Check **Reconnect at sign-in** and click **Finish**.

Now you can drag and drop, open, and edit files on your laptop directly from your desktop as if they were local!

---

## ☁️ 3. Online Access ("Google Drive" from Anywhere)

To access your files from anywhere in the world (on 4G/5G, work Wi-Fi, coffee shop):

1. Double-click `Start_Online_Access.bat`.
2. A secure public HTTPS URL will be generated (e.g. `https://random-words.trycloudflare.com`).
3. Open this link on your smartphone or remote computer to view, upload, or download your files.
4. When finished, press `Enter` in the window to stop the tunnel.

---

## 📂 Storage Directory

All files are stored in:
```text
c:\Users\leoca\OneDrive\Desktop\sharing\storage
```
- Put files into `storage\` manually or upload them through the web browser.
- Subfolders included:
  - `Documents\`
  - `Photos\`
  - `Videos\`
  - `Desktop_Share\`

---

## 🔄 Optional: Sync with Google Drive

If you also want to synchronize your NAS folder with your actual Google Drive account:
- Run PowerShell:
  ```powershell
  .\scripts\setup-gdrive-sync.ps1
  ```
- Follow the instructions to link `rclone` to your Google Drive account.
