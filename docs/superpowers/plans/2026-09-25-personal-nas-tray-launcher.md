# Personal NAS Tray Launcher Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a double-click Windows tray app that starts the existing NAS server and provides simple open, copy-address, stop, restart, and exit controls.

**Architecture:** A C# WinForms application compiled as a Windows GUI executable. The launcher embeds the existing File Browser Windows binary, extracts it to the current user's Local AppData, and keeps the existing database and storage folders beside `PersonalNAS.exe` at the project root.

**Tech Stack:** C# targeting the Windows .NET Framework 4.x runtime, WinForms, the Windows Framework C# compiler, and the existing File Browser 2.63.23 binary.

---

## File map

- Create `src/PersonalNAS/Program.cs` — single-instance startup and top-level error handling.
- Create `src/PersonalNAS/NasServer.cs` — initialize the existing database, extract the embedded server, and own its process lifecycle.
- Create `src/PersonalNAS/TrayApplicationContext.cs` — tray icon, status, menu actions, and browser/clipboard interactions.
- Create `src/PersonalNAS/NetworkAddress.cs` — choose the current usable LAN IPv4 address.
- Create `scripts/build-launcher.ps1` — invoke the installed Windows C# compiler, embed `bin/filebrowser.exe`, copy the license next to the output, and write `PersonalNAS.exe` at the project root.
- Modify `.gitignore` — ignore the generated license copy; `*.exe` already ignores the generated launcher.
- Modify `README.md` — document the EXE-first local/LAN workflow and retain the separate tunnel and SMB instructions.

## Task 1: Add a tray application shell and single-instance behavior

**Files:**
- Create: `src/PersonalNAS/Program.cs`
- Create: `src/PersonalNAS/TrayApplicationContext.cs`

- [ ] **Step 1: Create the WinForms application entry point**

  Use `[STAThread]`, call `Application.EnableVisualStyles()`, acquire a named local `Mutex`, and run one `ApplicationContext`. If another instance owns the mutex, show a brief information dialog and exit without launching another server.

- [ ] **Step 2: Add the tray icon and status-dependent menu**

  Create the `NotifyIcon` and menus. While running, expose **Open NAS**, **Copy LAN address**, **Stop server**, and **Exit**. While stopped, expose **Start server** and **Exit**. Update the tooltip and available menu items after each state transition.

- [ ] **Step 3: Handle open, copy, and exit actions**

  Open `http://localhost:8080` with the default browser only while the server is running. Copy the selected LAN URL through `Clipboard.SetText` on the WinForms UI thread. Exit must stop an owned server process before disposing the tray icon and message loop.

## Task 2: Manage the embedded File Browser process and persistent NAS paths

**Files:**
- Create: `src/PersonalNAS/NasServer.cs`
- Modify: `src/PersonalNAS/TrayApplicationContext.cs`

- [ ] **Step 1: Resolve data paths and prepare directories**

  Resolve the launcher directory using `AppDomain.CurrentDomain.BaseDirectory`. Use adjacent `data` and `storage` directories and the existing `data/filebrowser.db` and `data/nas.log` names. Create missing directories without changing or deleting existing content. Before database initialization, create and remove a uniquely named temporary probe file in each directory; if either probe fails, show a clear message naming the unwritable directory and stop startup.

- [ ] **Step 2: Extract the embedded server to a versioned Local AppData directory**

  Add an assembly resource named `PersonalNAS.FileBrowser.exe`. Write it to `%LOCALAPPDATA%\PersonalNAS\Runtime\2.63.23\filebrowser.exe` only when it is missing. Do not extract or overwrite the user's database or storage files.

- [ ] **Step 3: Initialize the database only on first launch**

  If the adjacent database does not exist, run the File Browser `config init` and `users add` commands with the same address, port, root, branding, password length, username, and initial password currently used by `scripts/start-nas.ps1`. Capture command output and report a useful error if either command fails.

- [ ] **Step 4: Start and stop only the owned server process**

  Start File Browser hidden with the existing database, storage root, `0.0.0.0` bind address, port `8080`, and log path. Keep the returned `Process` object in `NasServer`; implement bounded startup readiness checks and surface process-exit/port-conflict errors. Stop only this owned process, wait for exit, and keep the tray app alive so **Start server** works again.

- [ ] **Step 5: Connect server state to tray actions**

  Wire start and stop to the menu, open the browser after successful initial startup, and refresh menu/tooltip state after success or failure. Surface unwritable-directory, database-init, port-conflict, and process-start errors through readable dialogs. Startup errors must leave unrelated server processes untouched.

## Task 3: Add LAN address discovery

**Files:**
- Create: `src/PersonalNAS/NetworkAddress.cs`
- Modify: `src/PersonalNAS/TrayApplicationContext.cs`

- [ ] **Step 1: Select a usable local IPv4 address**

  Enumerate active network interfaces and choose a non-loopback IPv4 address that is not APIPA. Fall back to `127.0.0.1` if no LAN address is available.

- [ ] **Step 2: Use the address in tray feedback**

  Build `http://<address>:8080`, show it in the tray tooltip or a short confirmation after copying, and keep **Open NAS** on `localhost` for the host computer.

## Task 4: Build the single-file launcher at the project root

**Files:**
- Create: `scripts/build-launcher.ps1`
- Modify: `.gitignore`

- [ ] **Step 1: Validate build inputs and locate the Windows compiler**

  Require the existing `bin/filebrowser.exe` and `bin/LICENSE`. Locate `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, and stop with a clear message if a required input is unavailable.

- [ ] **Step 2: Compile a GUI executable with the server embedded**

  Invoke the compiler with `-target:winexe`, `-platform:x64`, references to `System`, `System.Core`, `System.Drawing`, and `System.Windows.Forms`, all launcher source files, and the named File Browser resource. Output `PersonalNAS.exe` at the project root so its directory contains `data` and `storage`.

- [ ] **Step 3: Copy the third-party license beside the executable**

  Copy `bin/LICENSE` to `PersonalNAS-FileBrowser-LICENSE.txt` next to the executable. Add that generated license copy to `.gitignore`; preserve the existing executable ignore rule.

- [ ] **Step 4: Build the deliverable**

  Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-launcher.ps1`. Confirm it exits successfully and produces a non-empty root `PersonalNAS.exe` and the adjacent license file.

## Task 5: Document the new launch flow

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Make the EXE the primary quick start**

  Document that the launcher is double-clicked from the project root, starts the local/LAN server, opens the local web page, and keeps `data` and `storage` beside the EXE. State that this folder must be writable.

- [ ] **Step 2: Document tray controls and retain existing workflows**

  Explain Open, Copy LAN address, Stop, Start, and Exit controls. Keep the batch/PowerShell fallback, online tunnel, and SMB setup instructions distinct and available.

- [ ] **Step 3: Review the final changes and build artifact**

  Review `git diff --check`, source/config changes, and the generated EXE/license presence. Do not start the NAS automatically as part of the build step.
