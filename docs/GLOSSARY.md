# 术语表 (Glossary)

本文档记录 **FolderSnap** 文件夹列表截图应用的核心领域概念与统一语言（Ubiquitous Language）。

---

### 1. 核心领域实体 (Domain Entities)

- **Directory Node (目录节点)**：
  表示文件系统中的一个文件夹单元，包含其相对路径、层级深度、子文件夹列表、直属文件列表、总大小与文件数统计。
- **File Entry (文件条目)**：
  单个文件的元数据对象，包含文件名、扩展名、分类类型（文档/代码/图片/音视频/归档/系统等）、文件字节大小、格式化大小、最后修改时间（如有）。
- **Snapshot Card (快照卡片)**：
  以单个目录节点为边界渲染的精致视觉组件，采用 macOS 窗口质感、磨砂玻璃背景、交通灯窗口控件、面包屑路径和汇总统计栏，可独立生成截图。
- **Panorama Canvas (全景画布)**：
  将整个已遍历目录树按层级拓扑或平铺流水无缝组合成一幅完整的大图/长图。

---

### 2. 交互与功能术语 (Interaction & Features)

- **Directory Picker (目录选择器)**：
  优先采用 W3C File System Access API（`window.showDirectoryPicker`）进行低内存占用的原生异步遍历；若浏览器不支持则自动回退至 `<input type="file" webkitdirectory>`。
- **Noise Filter (智能噪音过滤)**：
  自动屏蔽软件工程与操作系统产生的冗余临时目录（例如 `.git`, `.svn`, `node_modules`, `.DS_Store`, `Thumbs.db` 等），提供一键切换开关。
- **High-DPI Capture (高倍率渲染捕获)**：
  截图导出时启用 2x / 3x 设备像素比（Device Pixel Ratio），确保在高分辨率屏幕或打印归档时文字极其清晰锐利。
- **Bundle Exporter (批量打包器)**：
  基于 JSZip 与异步流水线，将所有子文件夹的快照卡片无缝截取并按目录层级/文件夹名称归档打包为 `.zip` 文件下载。

---

### 3. Windows 桌面与资源管理器架构术语 (Desktop & Explorer Integration)

- **Explorer Shell Host (资源管理器宿主)**：
  负责托管或调度 Windows 资源管理器界面。分为两种实现方式：
  1. *外部进程式*：通过操作系统调用直接唤起 `explorer.exe` 独立窗口；
  2. *内嵌组件式*：利用 COM 接口 `IExplorerBrowser` / `IShellView` 将真实资源管理器视图无缝嵌入本桌面程序内。
- **Window Capture Pipeline (窗口图形捕获流水线)**：
  基于 Windows Win32 API (`PrintWindow` / `BitBlt` / Desktop Duplication API / Windows.Graphics.Capture) 对目标资源管理器窗口或内嵌 Shell 面板进行像素级位图采集。
- **Batch Explorer Traversal (批量目录轮转控制器)**：
  针对包含大量子文件夹的复杂目录，自动化驱动资源管理器依次切换/展示各个子文件夹，并配合渲染就绪检测（等待图标加载完成）后触发截屏，最后自动归档输出。
- **Scroll Stitcher (滚动拼图引擎)**：
  针对单一文件夹内文件数量过多、超出可视窗口高度时，通过 UI Automation 模拟向下滚动并执行多次重叠截图，消除滚动条拼接成一张完整长图的能力。
- **Shell Drop Interceptor (Shell 拖拽拦截器)**：
  在原生内嵌资源管理器宿主上区分“文件系统移动/复制”与“应用级路径导入”事件，确保用户将目录拖入主窗口时安全解析为目标路径，防止意外触发文件物理转移。
- **STA Batch Pipeline & Cancellation (STA 批处理管线与安全中断)**：
  针对 COM 控件要求单一线程单元（STA）的强约束，在 UI 主线程通过受控异步消息泵配合 `CancellationToken` 驱动的非阻塞批处理轮转流程，支持随时安全中止。
- **Metadata Header Banner (元数据页眉横幅)**：
  在截取出的资源管理器视口上方动态附加的工程信息横幅，包含目录相对路径、文件统计、时间戳与环境标识，提升图片在工程交付与架构审查时的辨识度。
- **Max Traversal Depth (最大遍历深度限制)**：
  批处理递归扫描时设置的层级硬限制（如仅根目录、1层、2层或无限制），避免意外遍历系统深层目录导致任务膨胀。
- **Adaptive Column Fitting (自适应列宽动态伸展)**：
  在详细信息视图下，依据当前目录下最长文件名及可视区宽度，动态计算并分配各列（名称、修改日期、类型、大小）最佳宽度，根除长文本省略号截断（`...`）与多列被挤出可视区的问题。
- **Resolution-Aware Viewport (分辨率感知视口)**：
  依据主显示器物理分辨率（1080P/2K/4K）与系统 DPI 缩放比例，动态计算并设定窗体与列表区的最佳几何尺寸，确保在大屏上拥有充足展示宽度，在小屏上不溢出越界。


