<div align="center">

<img src="docs/assets/icon.png" width="96" alt="FolderSnap" />

# FolderSnap 📸

**Turn any folder into a deliverable screenshot — native Windows Explorer long-capture & batch archiving.**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
![Platform](https://img.shields.io/badge/platform-Windows-0078D6)
![Size](https://img.shields.io/badge/size-~95KB-success)

[简体中文](README.md) · [English](README.en.md) · [⬇️ Download](../../releases/latest)

</div>

## Why

Documenting a directory tree means screenshotting, stitching and annotating by hand. FolderSnap embeds the **real** Windows Explorer view (`IExplorerBrowser`) and automates the rest.

## Features

- 🪟 **Real Explorer, not a mock** — genuine icons, columns, context menus
- 📜 **Seamless long screenshots** — auto-scroll & stitch hundreds of files into one image
- ⚡ **Batch mode** — recurse sub-folders (1/2/3 levels or all), cancel anytime with `ESC`
- 📋 **Clipboard-first** — saved PNG is also on your clipboard, just `Ctrl+V`
- 🏷️ **Delivery header** — vector path, item count and timestamp baked into the image
- 🔍 **Smart column auto-fit** — no more `...` truncation
- 👀 **4 views** — Details / Icons / List / Tiles
- 🛡️ **Ignores `.git`, `node_modules`, `bin`, `obj`** by default
- 🧳 **~95KB single portable exe**, zero dependencies

## Use cases

Project hand-over & acceptance, audit evidence, design asset archives, tutorials & docs, "where did I put that file" messages.

## Quick start

1. Download `FolderSnapDesktop.exe` from [Releases](../../releases/latest)
2. Drag a folder in (or pick from the tree)
3. Click **📸 Capture current** or **⚡ Batch capture**

Requires Windows 10/11 x64.

## Build

```bat
desktop\build.bat
```
Uses the C# compiler shipped with Windows (`csc.exe`).

## Web version

A browser-only demo lives in [`web/`](web/).

## License

[MIT](LICENSE)
