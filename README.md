# Torr

Torr is a fast and simple Windows app to open and explore `.torrent` files. It lets you see what is inside a torrent before downloading it—including files, folders, file sizes, and torrent details.

It has no ads, no trackers, no extra installation steps, and takes almost no computer memory to run.

---

## What It Can Do

- **Browse Files and Folders**: Look through all the files in a torrent just like Windows Explorer, with a folder tree on the left and files on the right.
- **Light and Dark Modes**: Matches your Windows theme automatically, or you can switch manually anytime (`Ctrl + D`).
- **Torrent Details**: See the torrent name, total download size, creation date, comments, and web sources at a glance.
- **File Details**: Click on any file to check its size, type, and how much space it takes up in the torrent.
- **Drag and Drop**: Simply drag any `.torrent` file into the window to open it immediately.
- **Quick Copies**: Copy file names, file sizes, magnet links, or torrent hashes with a single click.
- **Fast and Tiny**: Opens instantly and runs smoothly even on torrents with tens of thousands of files.

---

## How to Build

Torr does not need Visual Studio or any extra software to build—Windows already has everything built in.

1. Double-click `build.bat` (or run it in Command Prompt):
   ```cmd
   build.bat
   ```
2. That's it! It creates `torr.exe` inside the `bin\` folder.

---

## How to Use

Double-click `bin\torr.exe` to start the app, or open a torrent directly from the command line:

```cmd
bin\torr.exe "path\to\file.torrent"
```

You can also drag and drop any `.torrent` file straight into the app window.

---

## Keyboard Shortcuts

| Shortcut | What It Does |
| :--- | :--- |
| `Ctrl + O` | Open a torrent file |
| `Ctrl + W` | Close the current torrent |
| `Ctrl + D` | Switch between Dark and Light mode |
| `Ctrl + M` | Copy magnet link |
| `Ctrl + H` | Copy torrent hash |
| `Ctrl + C` | Copy selected item name |
| `Ctrl + Shift + S` | Copy selected item file size |
| `Ctrl + A` | Select all items in the folder |
| `Backspace` / `Alt + Up` | Go up one folder |
| `Enter` | Open selected folder |
| `F5` | Refresh current folder |
| `Ctrl + 1` | Show or hide torrent info sidebar |
| `Ctrl + 2` | Show or hide file details sidebar |
| `Ctrl + 3` | Hide both sidebars for full-width file browsing |
| `Ctrl + R` | Reset layout to default |
| `Ctrl + E` | Expand all folders in the tree |
| `Ctrl + K` | Collapse all folders in the tree |
| `F1` | About Torr |
| `Alt + F4` | Exit |

---

## Files in This Project

- `build.bat` — Simple build script to compile the app
- `bin/` — Contains the compiled executable (`torr.exe`)
- `src/` — Application source files:
  - `Program.cs` — Starts the application and handles high-resolution screens
  - `MainForm.cs` — The main window, menus, sidebars, and file browser
  - `Strings.cs` — Stores all text, labels, and messages used in the app
  - `ThemeManager.cs` — Handles light and dark themes
  - `ShellIconHelper.cs` — Fetches real Windows file icons
  - `TorrentModel.cs` — Organizes torrent folders and files
  - `Bencode.cs` — Reads and decodes torrent files
