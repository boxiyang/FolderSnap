# ADR-002: Windows 原生资源管理器内嵌与自动化长截图桌面应用架构决策

- **状态**: Accepted (已采纳)
- **日期**: 2026-09-15
- **决策结论**: A（内嵌 Explorer 视图） + B（长列表滚动拼图） + 模式 1（全自动递归批量） + 选项 A（C# .NET Windows Desktop 原生架构）

---

## 1. 背景与核心目标 (Context & Goals)

在 Web 端完成快速文件夹目录卡片可视化的基础上，用户需要一个 **Windows 原生桌面应用**：
1. **调用 Windows 原生资源管理器（Explorer）呈现真实文件列表**：要求具备 Windows 操作系统完全一致的文件图标、列头排列（名称、修改日期、类型、大小等）、分组视图及系统渲染质感；
2. **长列表自动滚动无缝拼图 (Scroll Stitching)**：单一文件夹超出当前窗口高度时，支持自动步进滚动并剔除重复与滚动条，合成为一张完整无缝的高清长图；
3. **全自动递归批量截屏 (Batch Recursive Traversal)**：选定根文件夹后，自动递归提取所有子文件夹队列，驱动内嵌资源管理器依次导航呈现、等待就绪、滚动截屏拼图，并导出到指定输出目录；
4. **纯原生 Windows 桌面架构**：基于 C# (.NET / Win32 Shell API)，打造不频繁弹窗、不抢占用户屏幕焦点的稳定桌面客户端。

---

## 2. 核心技术决策 (Architecture Decisions)

### 2.1 资源管理器呈现层：内嵌式 `IExplorerBrowser` COM 控件
- **决策**: 不采用外部 `explorer.exe` 频繁弹窗模式，采用 Windows 官方 Shell 接口 **`IExplorerBrowser`**（`CLSID_ExplorerBrowser` / `IID_IExplorerBrowser`）。
- **技术要点**:
  - `IExplorerBrowser` 是 Windows Vista/7/10/11 资源管理器在系统内部公开的 COM 托管控件；
  - 宿主容器可配置无边框、移除左侧导航树窗格（仅保留文件列表主视图 `FOLDERVIEWMODE_DETAILS` 或用户指定模式）；
  - 导航方式通过 `NavigateToPIDL` 或 `SHCreateItemFromParsingName` 异步或同步导航，毫秒级就绪，完全运行在应用专属容器内。

### 2.2 视图滚动与长图拼接引擎 (Scroll Stitcher Pipeline)
- **决策**:
  - 当文件条目超出控件可视区域（检测到垂直滚动条最大值 `GetScrollInfo(SB_VERT)` > 可视高度）时：
    1. 获取单次步进位移与列表行高（通常每行约 22~28px）；
    2. 第一帧：截取初始顶部视口；
    3. 循环步进：通过 `SendMessage(hwndList, WM_VSCROLL, SB_PAGEDOWN/SB_LINEDOWN, ...)` 或 UI Automation 滚动精准像素；
    4. 截取每次滚动的增量区域并去除滚动条杂边；
    5. 拼接器（Bitmap GDI+ / Direct2D）在内存中动态分配总高度的大画布，按偏移量无缝合成最终超长长图。

### 2.3 窗口图形捕获管线 (Window Capture Pipeline)
- **决策**:
  - 优先调用 **`PrintWindow`**（配置标志 `PW_RENDERFULLCONTENT = 2`），支持即便部分被遮挡仍能正确提取后台像素；
  - 辅以 **`BitBlt`** 针对前台内嵌 ShellView 句柄 (`SHELLDLL_DefView` / `SysListView32` / `DirectUIHWND`) 进行内存 DC 高保真位图拷贝；
  - 统一支持高 DPI 缩放感知 (`SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)`)，杜绝模糊与文字发虚。

### 2.4 批量递归与调度控制 (Batch Automation Pipeline)
- **决策**:
  - 用户选定输入目录与输出目录，并可配置包含深度、黑名单目录（如 `.git`, `node_modules`）；
  - 建立目录任务队列 `Queue<DirectoryInfo>`；
  - 单线程 UI 消息泵流水线循环：
    - `UpdateFolder(path)` ➡️ 等待 Shell 接收并渲染完成（监听 `ExplorerBrowserViewEvents.ViewCreated` 或短时就绪自检） ➡️ 执行截图/拼图 ➡️ 写入输出目录（如 `output/subfolder_name.png`） ➡️ 更新进度条 UI ➡️ 下一个文件夹。

---

## 3. 系统架构与分层设计 (Layered Architecture)

```
┌────────────────────────────────────────────────────────┐
│                   Desktop UI Layer                     │
│  (Modern Windows Window: WPF / WinForms Host Form)     │
│  - 路径选择与输出配置 / 进度条 / 过滤项开关 / 开始截图按钮     │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│          Batch Automation Controller                   │
│  - 递归遍历器 (Recursive Directory Walker)               │
│  - 任务队列与状态机 (Task Queue & Status Pipeline)        │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│          Explorer Shell Host Manager                   │
│  - IExplorerBrowser COM 封装与生命周期管理               │
│  - 视图选项配置 (FolderViewMode: 详细信息/图标/列表)        │
│  - 导航事件监听与就绪检测 (Navigation Ready Detector)     │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│          Capture & Scroll Stitching Engine             │
│  - Win32 PrintWindow / BitBlt 句柄截屏                  │
│  - 垂直滚动位移计算与增量拼接器 (Bitmap Canvas Stitcher) │
│  - PNG / JPEG 编码输出与归档管理器                       │
└────────────────────────────────────────────────────────┘
```

---

## 4. 实施阶段计划 (Next Steps)

1. **阶段 1：核心 COM 原型与 Shell Host 封装**
   - 编写 `IExplorerBrowser` 的 C# P/Invoke COM 接口定义；
   - 验证指定本地路径导航与资源管理器在窗体上的成功挂载与显示。
2. **阶段 2：Win32 高清截图与长图拼图算法**
   - 实现无边框列表捕获；
   - 验证超出行数时的滚动计算与无缝长图合成。
3. **阶段 3：递归批量流水线与 UI 控制台**
   - 包含目录选择、智能过滤、并行/串行队列调度及进度反馈。
4. **阶段 4：编译产物打包与端到端验证**
   - 编译为独立轻量 `.exe`，提供完备的使用文档与示例。
