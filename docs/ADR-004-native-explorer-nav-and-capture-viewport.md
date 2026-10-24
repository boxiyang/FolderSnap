# ADR-004: Windows 原生导航窗格统一、自动展开同步与截图视口自适应

## 状态
已通过 (Accepted)

## 上下文与问题陈述
FolderSnap Desktop 在引入左侧目录树后，用户提出了四项关键缺陷与改进需求：
1. **导航栏重复**：界面上同时存在 WinForms 自绘的“快速导航目录”与 Windows 资源管理器的“原生导航窗格”，造成视觉冗余与混乱；
2. **目录同步不一致**：原生导航窗格选中的目录与右侧列表区未保持精准一致；
3. **截图过窄与列截断**：原生导航窗格开启时占据 240~280px，右侧列表区被压缩，导致截图直接截取缩窄的列表区，文件名过长及日期/类型/大小等多列信息被挤压或省略，且截出图片尺寸过小；
4. **页眉横幅方框符号**：截图页眉横幅中的路径以方框字符（□）开头，原因是 GDI+ 使用 `Segoe UI` 绘制 Unicode Emoji `📁` 时缺乏彩色字体回退，退化为缺失字符方框。

## 架构决策

### 1. 废弃自绘 TreeView，全量归一至 Windows 原生资源管理器
- 彻底移除 `MainForm.cs` 中的自绘 `TreeView`、相关 `SplitContainer`、图标列表与递归枚举逻辑，精简近 300 行代码；
- `ExplorerHostControl` 直接作为工作区唯一主体控件铺满界面；
- 顶部保留“📂 显示导航窗格”复选框，直接由 `ExplorerHostControl.ShowTreeView` 控制 Windows 官方原生导航窗格的显隐。

### 2. 原生导航栏双向同步与自动展开
- 在注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\NavPaneExpandToCurrentFolder` 保持开启状态，通知 Shell 在导航时自动展开并定位到当前打开的文件夹；
- 通过已验证的 `IExplorerBrowserEvents::OnNavigationComplete` 回调，用户在原生导航树中点击任何目录，均实时回填主界面顶部路径与状态栏。

### 3. 截图视口智能自适应（Full-Width Viewport Snapshot）
- 截图引擎在调用 `CaptureControl` 或 `CaptureStitchedList` 之前，临时检测是否开启了导航窗格；
- 若开启导航窗格，在后台瞬态临时收起原生导航窗格（或保证列表区拥有 1000px+ 完整宽度以展开所有列），完成单帧或拼接截图后毫秒级恢复；
- 保证用户平时有导航窗格可用，截出来的图片多列信息完整、高分辨率、绝不截断。

### 4. GDI+ 矢量文件夹图标替换 Unicode Emoji
- 移除 `displayPath = "📁 " + ...` 中的 Emoji 字符；
- 在页眉左侧使用 GDI+ 绘制 14×14 矢量渐变文件夹图标（暖黄渐变前盖 + 细致边框），彻底根治在任何 Windows 版本与字体下的“豆腐块方框”问题。
