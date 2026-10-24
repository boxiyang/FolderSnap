# FolderSnap Web（免安装体验版）

纯前端的文件夹遍历与卡片截图工具，浏览器（Chrome / Edge）直接打开 `index.html` 即可，无需服务端。

- 📂 递归遍历（`showDirectoryPicker()`，回退 `webkitdirectory`）
- 🎨 macOS 毛玻璃卡片 + 彩色文件类型徽章
- 📦 批量 2x 截图并打包 ZIP（`html2canvas` + `jszip` 已离线内置于 `libs/`）
- 🛡️ 默认过滤 `.git` / `node_modules` 等噪音

```bash
# 可选：本地静态服务器
python -m http.server 5173
```

> 追求原生体验与长截图拼接，请使用仓库根目录介绍的 **Windows 桌面版**。
