# ADR-003: 桌面端交互体验、批处理韧性与交付能力升级架构决策

- **状态**: Proposed (评审中)
- **日期**: 2026-09-16
- **关联决策**: [ADR-002: Windows 原生资源管理器内嵌与自动化长截图桌面应用架构决策](ADR-002-windows-explorer-desktop-capture-architecture.md)

---

## 1. 背景与驱动因素 (Context & Problem Statement)

在 ADR-002 完成了基于 Windows 原生 `IExplorerBrowser` COM 控件与长列表垂直无缝拼接（Scroll Stitcher）的内核验证后，用户和资深产品经理在实际测试与交付场景中提出了更高强度的工程化要求：

1. **输入效率低下**：每次选目录必须点击模态文件夹浏览框，缺乏桌面应用标配的“拖入即预览（Drag & Drop）”与“历史路径记忆（MRU）”。
2. **批处理不可控**：一旦对大项目启动递归截图，无法中途取消（Cancel）；若目录层级过深，极易产生几百张冗余截图，缺乏递归深度限制（Max Depth）。
3. **交付属性单一**：生成的截图缺少上下文元数据（路径、文件数、生成时间），且仅能写入磁盘，在频繁向文档/汇报软件（如飞书、Notion、Word）贴图时步骤繁琐（缺乏一键复制到剪贴板）。

---

## 2. 关键架构决策与技术权衡 (Key Decisions & Trade-offs)

### 2.1 决策 1：STA 线程安全的批处理中断与流控（CancellationToken + STA Message Pump）
- **问题**：`IExplorerBrowser` 属于 STA 线程 COM 控件，所有 COM 方法调用 (`BrowseToObject`, `SetFolderSettings`) 必须发生在创建控件的 UI 线程，无法随意派发给后台 `Task.Run`。
- **方案决策**：
  - 不采用强行多线程跨线程调用（避免引发 `RPC_E_WRONG_THREAD`）；
  - 保留 UI 线程驱动，采用状态机配合协作式取消标记；
  - 批量执行期间，将“开始全自动批量截图”按钮转为醒目的红色“⏹️ 停止 / 中断”，通过异步定时检查取消标记，毫秒级响应用户终止诉求。

### 2.2 决策 2：原生拖放与防误触拦截（Shell Drop Target Coexistence）
- **问题**：`IExplorerBrowser` 内部拥有独立的 OLE DropTarget，如果直接拖入文件夹，Explorer 默认会将文件系统执行复制/移动操作，可能导致用户本地文件意外迁移。
- **方案决策**：
  - 在外层容器（窗体、控制面板及边框容器）启用 `AllowDrop = true`；
  - 拦截 `DragEnter`，严格校验拖入数据格式必须包含 `DataFormats.FileDrop` 且为有效目录；
  - 触发 `DragDrop` 时提取第一个有效路径，并在 UI 层面阻断冒泡，直接调用 `SetRootFolder(path)`，确保绝对零磁盘写入风险。

### 2.3 决策 3：交付增强之“可选工程元数据页眉（Metadata Header Banner）”
- **方案决策**：
  - 增加配置开关：`[✓] 顶部附带路径与统计抬头`；
  - 在截图合成流水线末端，若开关开启，动态分配 `height + 36px` 画布，以深色优雅暗调绘制页眉条：
    - 左侧：文件夹图标 + 相对/绝对路径
    - 右侧：文件统计（如：`共 14 项`）+ 截图时间戳
  - 该功能纯 GDI+ 绘制，零第三方字体依赖，自适应高 DPI。

### 2.4 决策 4：剪贴板优先（Clipboard-First Workflow）
- **方案决策**：
  - “仅截当前”增加“复制到剪贴板”开关或一键动作；
  - 调用 `Clipboard.SetImage(bmp)` 直接将高清位图推入系统剪贴板，状态栏配合平滑提示，彻底消除干扰性极强的模态 `MessageBox` 阻断。

### 2.5 决策 5：原生左侧导航树（Tree View）与截图视口解耦
- **背景与痛点**：用户在本地频繁切换目录时，弹窗选择体验割裂；但如果在界面上显示原生左侧导航树，全幅截图又会把庞大的系统盘符与冗余节点截进交付图片中。
- **方案决策**：
  - **交互层**：启用 `IExplorerBrowser` 的左侧导航树（移除 `EBO_NOTREEVIEW`），同时挂载 `IExplorerBrowserEvents`，用户在左侧树点击任意文件夹时，顶部路径输入框与输出目录自动双向联动同步；提供一键收起/展开导航树开关。
  - **捕获层解耦**：无论左侧导航树是否显示，截图引擎（`ScrollStitcher`）通过 Win32 API 锁定右侧 `SHELLDLL_DefView` 视图矩形区域，**始终精准只截取右侧纯净文件列表**（或提供可选开关），实现“浏览像资源管理器一样畅快，截图依然纯净专业”。


---

## 3. 架构分层映射 (Architecture Evolution)

```
[Form View]
   │
   ├── Drag & Drop Handler (拦截拖拽路径，防止 COM 误操作)
   ├── MRU Path History Manager (最近使用目录轻量缓存)
   │
[Batch Pipeline Controller]
   │
   ├── Depth-limited Traversal (最大层级裁剪: 1层/2层/全量)
   └── Cooperative Cancellation (协作式毫秒级安全退出)
   │
[Scroll & Render Pipeline]
   │
   ├── ExplorerHostControl (STA 原生资源管理器呈现)
   ├── ScrollStitcher (滚动无缝缝合)
   ├── Metadata Header Decorator (可选动态页眉注入)
   └── Output Dispatcher (文件磁盘写入 / 剪贴板注入)
```

---

## 4. 后续演进路线 (Roadmap)
- v1.1: 拖拽导入 + 剪贴板注入 + 批处理取消 + 深度限制 + 元数据页眉 (当前实施)
- v1.2: 批量预检勾选清单 (Checklist Dialog)
- v2.0: 多图合并导出单一 PDF 交付手册
