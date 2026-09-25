# Personal NAS Tray Launcher Design

## Goal

Let the user start the existing Windows NAS with one double-click on a standalone launcher executable. Keep the existing storage and online access workflows intact.

## Recommended approach

Build a small Windows tray application as a single self-contained `PersonalNAS.exe`. Embed the repository's existing File Browser server executable as a resource and extract it to a versioned runtime directory under the current Windows user's Local AppData when needed. This removes the launcher's dependency on PowerShell and a separate `bin/filebrowser.exe` at runtime.

The launcher operates on the `data` and `storage` directories beside its own executable. This preserves existing NAS files and settings and keeps future uploads outside the executable. The distribution folder therefore remains writable and must travel with those directories; the executable itself is a single file, not a full installer that relocates user data.

## Startup and tray behavior

- On double-click, initialize the existing File Browser database and default admin account if no database exists, then start the server quietly on the existing port `8080` and open `http://localhost:8080` in the default browser.
- Show a tray icon with a status tooltip and menu actions: **Open NAS**, **Copy LAN address**, **Stop server**, and **Exit**.
- Stopping ends only the server process started by this launcher. Exiting stops that process and removes the tray icon.
- Prevent a second launcher instance from starting another server; show a short message directing the user to the existing tray controls.
- Report startup failures in a readable dialog. In particular, explain if port `8080` is already occupied or if the adjacent data/storage directories cannot be written.
- Keep online tunnel and SMB configuration as separate existing workflows; this launcher starts only the local/LAN web server.

## Packaging and repository changes

- Add focused C# source for the tray launcher and a Windows build script that creates the single-file executable from the existing `bin/filebrowser.exe`.
- Include the File Browser license notice with the distribution.
- Place the generated launcher under `dist/` and update ignore rules so generated binaries remain untracked while the source and build script are tracked.
- Update the README with the new double-click flow, tray controls, writable folder requirement, and the separate online/SMB actions.
- Keep current batch and PowerShell launch/stop scripts available as fallback workflows.

## Acceptance criteria

- The produced EXE starts the bundled server without showing a command window and opens the local web UI.
- Existing `data` and `storage` content is used without moving or overwriting user files.
- Tray actions open the web UI, copy the current LAN URL, stop only the server owned by the launcher, and exit cleanly.
- A failed or conflicting startup produces a useful message and does not kill unrelated File Browser processes.
- The legacy scripts and separate tunnel workflow remain available.

## Risks and constraints

- The File Browser project and this bundled release are archived upstream; packaging does not add upstream security fixes.
- Existing File Browser credentials and database behavior are preserved. Credential redesign is outside this launcher's scope.
- Because the storage and database remain beside the EXE, users should run it from a writable folder rather than a protected installation directory.
- LAN access still depends on the Windows firewall being configured as in the current setup.

