using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using FolderSnapDesktop.Core;
using FolderSnapDesktop.Interop;

namespace FolderSnapDesktop.UI
{
    public class MainForm : Form
    {
        private Panel _controlPanel;
        private TextBox _txtRootPath;
        private Button _btnBrowseRoot;
        private TextBox _txtOutputPath;
        private Button _btnBrowseOutput;

        private ComboBox _cmbViewMode;
        private CheckBox _chkShowTreeView;
        private ComboBox _cmbMaxDepth;
        private CheckBox _chkIncludeHeader;
        private CheckBox _chkStitchLongList;
        private CheckBox _chkFilterBlacklist;

        private Button _btnStartBatch;
        private Button _btnCaptureCurrent;

        private Panel _explorerContainer;
        private ExplorerHostControl _explorerHost;

        private StatusStrip _statusStrip;
        private ToolStripProgressBar _progressBar;
        private ToolStripStatusLabel _statusLabel;

        private bool _isBatchRunning = false;
        private volatile bool _isCancelRequested = false;
        private bool _isNavigating = false;


        public MainForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "FolderSnap Desktop - Windows 原生资源管理器长截图工具";
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0)));
            // 依据当前物理屏幕分辨率与工作区动态计算最佳窗口比例与居中位置
            Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
            int targetWidth = Math.Max(1120, Math.Min((int)(workingArea.Width * 0.78), 1920));
            int targetHeight = Math.Max(760, Math.Min((int)(workingArea.Height * 0.82), 1200));
            this.Size = new Size(targetWidth, targetHeight);
            this.MinimumSize = new Size(950, 620);
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(
                workingArea.Left + Math.Max(0, (workingArea.Width - targetWidth) / 2),
                workingArea.Top + Math.Max(0, (workingArea.Height - targetHeight) / 2)
            );
            this.BackColor = Color.FromArgb(243, 244, 246);

            // 加载精致专属应用图标
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app_icon.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
            }
            catch
            {
            }

            // 1. 控制参数卡片面板 (自适应高 DPI，保证 150%/200% 缩放下绝对不折行、不截断)
            _controlPanel = new Panel();
            _controlPanel.Dock = DockStyle.Top;
            _controlPanel.AutoSize = true;
            _controlPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _controlPanel.BackColor = Color.White;
            _controlPanel.Padding = new Padding(20, 12, 20, 12);

            TableLayoutPanel mainTable = new TableLayoutPanel();
            mainTable.Dock = DockStyle.Top;
            mainTable.AutoSize = true;
            mainTable.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            mainTable.ColumnCount = 3;
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 自适应标签宽度，彻底避免“目标文/件夹:”换行
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 浏览按钮自适应

            // 行 0: 目标文件夹
            Label lblRoot = new Label();
            lblRoot.Text = "目标文件夹:";
            lblRoot.AutoSize = true;
            lblRoot.Anchor = AnchorStyles.Right;
            lblRoot.ForeColor = Color.FromArgb(63, 63, 70);
            lblRoot.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblRoot.Margin = new Padding(0, 6, 8, 6);

            _txtRootPath = new TextBox();
            _txtRootPath.Dock = DockStyle.Fill;
            _txtRootPath.Font = new Font("Segoe UI", 9.5F);
            _txtRootPath.BorderStyle = BorderStyle.FixedSingle;
            _txtRootPath.Margin = new Padding(0, 4, 8, 4);

            _btnBrowseRoot = new Button();
            _btnBrowseRoot.Text = "浏览...";
            _btnBrowseRoot.AutoSize = true;
            _btnBrowseRoot.Padding = new Padding(12, 3, 12, 3);
            _btnBrowseRoot.Height = 28;
            _btnBrowseRoot.FlatStyle = FlatStyle.Flat;
            _btnBrowseRoot.BackColor = Color.FromArgb(244, 244, 245);
            _btnBrowseRoot.FlatAppearance.BorderColor = Color.FromArgb(212, 212, 216);
            _btnBrowseRoot.Cursor = Cursors.Hand;
            _btnBrowseRoot.Margin = new Padding(0, 3, 0, 3);
            _btnBrowseRoot.Click += BtnBrowseRoot_Click;

            mainTable.Controls.Add(lblRoot, 0, 0);
            mainTable.Controls.Add(_txtRootPath, 1, 0);
            mainTable.Controls.Add(_btnBrowseRoot, 2, 0);

            // 行 1: 截图保存至
            Label lblOutput = new Label();
            lblOutput.Text = "截图保存至:";
            lblOutput.AutoSize = true;
            lblOutput.Anchor = AnchorStyles.Right;
            lblOutput.ForeColor = Color.FromArgb(63, 63, 70);
            lblOutput.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblOutput.Margin = new Padding(0, 6, 8, 6);

            _txtOutputPath = new TextBox();
            _txtOutputPath.Dock = DockStyle.Fill;
            _txtOutputPath.Font = new Font("Segoe UI", 9.5F);
            _txtOutputPath.BorderStyle = BorderStyle.FixedSingle;
            _txtOutputPath.Margin = new Padding(0, 4, 8, 4);

            _btnBrowseOutput = new Button();
            _btnBrowseOutput.Text = "浏览...";
            _btnBrowseOutput.AutoSize = true;
            _btnBrowseOutput.Padding = new Padding(12, 3, 12, 3);
            _btnBrowseOutput.Height = 28;
            _btnBrowseOutput.FlatStyle = FlatStyle.Flat;
            _btnBrowseOutput.BackColor = Color.FromArgb(244, 244, 245);
            _btnBrowseOutput.FlatAppearance.BorderColor = Color.FromArgb(212, 212, 216);
            _btnBrowseOutput.Cursor = Cursors.Hand;
            _btnBrowseOutput.Margin = new Padding(0, 3, 0, 3);
            _btnBrowseOutput.Click += BtnBrowseOutput_Click;

            mainTable.Controls.Add(lblOutput, 0, 1);
            mainTable.Controls.Add(_txtOutputPath, 1, 1);
            mainTable.Controls.Add(_btnBrowseOutput, 2, 1);

            // 行 2: 选项配置工具栏 (流式自动折行排版，功能丰富，空间从容)
            FlowLayoutPanel optionsRow = new FlowLayoutPanel();
            optionsRow.Dock = DockStyle.Fill;
            optionsRow.AutoSize = true;
            optionsRow.FlowDirection = FlowDirection.LeftToRight;
            optionsRow.WrapContents = true;
            optionsRow.Margin = new Padding(0, 6, 0, 4);
            optionsRow.Padding = new Padding(0);

            Label lblView = new Label();
            lblView.Text = "视图排版:";
            lblView.AutoSize = true;
            lblView.Anchor = AnchorStyles.Left;
            lblView.ForeColor = Color.FromArgb(63, 63, 70);
            lblView.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblView.Margin = new Padding(0, 6, 4, 6);

            _cmbViewMode = new ComboBox();
            _cmbViewMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbViewMode.Items.AddRange(new object[] { "详细信息 (Details)", "大图标 (Icons)", "列表 (List)", "平铺 (Tiles)" });
            _cmbViewMode.SelectedIndex = 0;
            _cmbViewMode.Width = 175;
            _cmbViewMode.Anchor = AnchorStyles.Left;
            _cmbViewMode.Margin = new Padding(0, 4, 16, 4);
            _cmbViewMode.SelectedIndexChanged += CmbViewMode_SelectedIndexChanged;

            Label lblDepth = new Label();
            lblDepth.Text = "扫描深度:";
            lblDepth.AutoSize = true;
            lblDepth.Anchor = AnchorStyles.Left;
            lblDepth.ForeColor = Color.FromArgb(63, 63, 70);
            lblDepth.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblDepth.Margin = new Padding(0, 6, 4, 6);

            _cmbMaxDepth = new ComboBox();
            _cmbMaxDepth.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbMaxDepth.Items.AddRange(new object[] { "全部递归 (无限制)", "仅限 1 层 (直属子目录)", "仅限 2 层", "仅限 3 层" });
            _cmbMaxDepth.SelectedIndex = 0;
            _cmbMaxDepth.Width = 175;
            _cmbMaxDepth.Anchor = AnchorStyles.Left;
            _cmbMaxDepth.Margin = new Padding(0, 4, 20, 4);

            _chkShowTreeView = new CheckBox();
            _chkShowTreeView.Text = "📂 显示导航窗格";
            _chkShowTreeView.Checked = true;
            _chkShowTreeView.AutoSize = true;
            _chkShowTreeView.Anchor = AnchorStyles.Left;
            _chkShowTreeView.ForeColor = Color.FromArgb(39, 39, 42);
            _chkShowTreeView.Margin = new Padding(0, 6, 16, 6);
            _chkShowTreeView.CheckedChanged += (s, e) => {
                _explorerHost.ShowTreeView = _chkShowTreeView.Checked;
            };

            _chkIncludeHeader = new CheckBox();
            _chkIncludeHeader.Text = "🏷️ 附带工程页眉";
            _chkIncludeHeader.Checked = true;
            _chkIncludeHeader.AutoSize = true;
            _chkIncludeHeader.Anchor = AnchorStyles.Left;
            _chkIncludeHeader.ForeColor = Color.FromArgb(39, 39, 42);
            _chkIncludeHeader.Margin = new Padding(0, 6, 16, 6);

            _chkStitchLongList = new CheckBox();
            _chkStitchLongList.Text = "↕️ 滚动拼长图";
            _chkStitchLongList.Checked = true;
            _chkStitchLongList.AutoSize = true;
            _chkStitchLongList.Anchor = AnchorStyles.Left;
            _chkStitchLongList.ForeColor = Color.FromArgb(39, 39, 42);
            _chkStitchLongList.Margin = new Padding(0, 6, 16, 6);

            _chkFilterBlacklist = new CheckBox();
            _chkFilterBlacklist.Text = "🛡️ 忽略依赖 (.git 等)";
            _chkFilterBlacklist.Checked = true;
            _chkFilterBlacklist.AutoSize = true;
            _chkFilterBlacklist.Anchor = AnchorStyles.Left;
            _chkFilterBlacklist.ForeColor = Color.FromArgb(39, 39, 42);
            _chkFilterBlacklist.Margin = new Padding(0, 6, 0, 6);

            optionsRow.Controls.Add(lblView);
            optionsRow.Controls.Add(_cmbViewMode);
            optionsRow.Controls.Add(lblDepth);
            optionsRow.Controls.Add(_cmbMaxDepth);
            optionsRow.Controls.Add(_chkShowTreeView);
            optionsRow.Controls.Add(_chkIncludeHeader);
            optionsRow.Controls.Add(_chkStitchLongList);
            optionsRow.Controls.Add(_chkFilterBlacklist);

            mainTable.SetColumnSpan(optionsRow, 3);
            mainTable.Controls.Add(optionsRow, 0, 2);

            // 行 3: 操作按钮栏 (右侧吸附，左侧提示，主次分明，空间充裕不被挤切)
            TableLayoutPanel actionBar = new TableLayoutPanel();
            actionBar.Dock = DockStyle.Fill;
            actionBar.AutoSize = true;
            actionBar.ColumnCount = 2;
            actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actionBar.Margin = new Padding(0, 6, 0, 0);

            Label hintLabel = new Label();
            hintLabel.Text = "💡 提示：支持文件夹直接拖拽放入 • 左侧目录树点击可即时穿梭 • 截图自动复制到剪贴板";
            hintLabel.ForeColor = Color.FromArgb(113, 113, 122);
            hintLabel.Font = new Font("Segoe UI", 8.5F);
            hintLabel.AutoSize = true;

            hintLabel.Anchor = AnchorStyles.Left;

            FlowLayoutPanel buttonGroup = new FlowLayoutPanel();
            buttonGroup.AutoSize = true;
            buttonGroup.FlowDirection = FlowDirection.LeftToRight;
            buttonGroup.WrapContents = false;
            buttonGroup.Anchor = AnchorStyles.Right;
            buttonGroup.Margin = new Padding(0);

            _btnCaptureCurrent = new Button();
            _btnCaptureCurrent.Text = "📸 仅截当前";
            _btnCaptureCurrent.Font = new Font("Segoe UI", 9F);
            _btnCaptureCurrent.AutoSize = true;
            _btnCaptureCurrent.Padding = new Padding(14, 4, 14, 4);
            _btnCaptureCurrent.Height = 32;
            _btnCaptureCurrent.FlatStyle = FlatStyle.Flat;
            _btnCaptureCurrent.BackColor = Color.FromArgb(244, 244, 245);
            _btnCaptureCurrent.FlatAppearance.BorderColor = Color.FromArgb(212, 212, 216);
            _btnCaptureCurrent.Cursor = Cursors.Hand;
            _btnCaptureCurrent.Margin = new Padding(0, 0, 12, 0);
            _btnCaptureCurrent.Click += BtnCaptureCurrent_Click;

            _btnStartBatch = new Button();
            _btnStartBatch.Text = "⚡ 开始全自动批量截图";
            _btnStartBatch.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _btnStartBatch.AutoSize = true;
            _btnStartBatch.Padding = new Padding(18, 4, 18, 4);
            _btnStartBatch.Height = 32;
            _btnStartBatch.BackColor = Color.FromArgb(37, 99, 235); // 鲜明现代蓝
            _btnStartBatch.ForeColor = Color.White;
            _btnStartBatch.FlatStyle = FlatStyle.Flat;
            _btnStartBatch.FlatAppearance.BorderSize = 0;
            _btnStartBatch.Cursor = Cursors.Hand;
            _btnStartBatch.Margin = new Padding(0);
            _btnStartBatch.Click += BtnStartBatch_Click;

            buttonGroup.Controls.Add(_btnCaptureCurrent);
            buttonGroup.Controls.Add(_btnStartBatch);

            actionBar.Controls.Add(hintLabel, 0, 0);
            actionBar.Controls.Add(buttonGroup, 1, 0);

            mainTable.SetColumnSpan(actionBar, 3);
            mainTable.Controls.Add(actionBar, 0, 3);

            _controlPanel.Controls.Add(mainTable);

            // 底部 1px 细微边界线，增强卡片层次感
            Panel dividerLine = new Panel();
            dividerLine.Dock = DockStyle.Top;
            dividerLine.Height = 1;
            dividerLine.BackColor = Color.FromArgb(228, 228, 231);

            // 3. 资源管理器内嵌容器 (直接承载原生 Explorer 控件，铺满主工作区)
            _explorerContainer = new Panel();
            _explorerContainer.Dock = DockStyle.Fill;
            _explorerContainer.Padding = new Padding(16, 10, 16, 10);
            _explorerContainer.BackColor = Color.FromArgb(244, 245, 247);

            Panel explorerBorderPanel = new Panel();
            explorerBorderPanel.Dock = DockStyle.Fill;
            explorerBorderPanel.BackColor = Color.FromArgb(212, 212, 216);
            explorerBorderPanel.Padding = new Padding(1);

            _explorerHost = new ExplorerHostControl();
            _explorerHost.Dock = DockStyle.Fill;
            _explorerHost.ShowTreeView = _chkShowTreeView.Checked;
            _explorerHost.FolderNavigated += ExplorerHost_FolderNavigated;
            explorerBorderPanel.Controls.Add(_explorerHost);

            _explorerContainer.Controls.Add(explorerBorderPanel);

            // 4. 底部状态栏
            _statusStrip = new StatusStrip();
            _statusStrip.BackColor = Color.White;
            _statusStrip.Height = 30;

            _statusLabel = new ToolStripStatusLabel();
            _statusLabel.Text = "就绪 - 请选择目标文件夹";
            _statusLabel.Spring = true;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;

            _progressBar = new ToolStripProgressBar();
            _progressBar.Size = new Size(180, 16);
            _progressBar.Visible = false;

            ToolStripButton btnOpenFolder = new ToolStripButton();
            btnOpenFolder.Text = "📁 打开截图目录";
            btnOpenFolder.Click += BtnOpenFolder_Click;

            ToolStripStatusLabel btnAbout = new ToolStripStatusLabel();
            btnAbout.Text = "ℹ️ 关于 FolderSnap";
            btnAbout.IsLink = true;
            btnAbout.LinkColor = Color.FromArgb(71, 85, 105);
            btnAbout.ActiveLinkColor = Color.FromArgb(37, 99, 235);
            btnAbout.VisitedLinkColor = Color.FromArgb(71, 85, 105);
            btnAbout.Margin = new Padding(8, 1, 10, 2);
            btnAbout.Click += (s, e) => {
                using (AboutDialog dlg = new AboutDialog())
                {
                    dlg.ShowDialog(this);
                }
            };

            _statusStrip.Items.Add(_statusLabel);
            _statusStrip.Items.Add(_progressBar);
            _statusStrip.Items.Add(btnOpenFolder);
            _statusStrip.Items.Add(btnAbout);

            // 添加主控件布局 (严格遵循正确的 WinForms Dock 叠加顺序)
            this.Controls.Add(_explorerContainer);
            this.Controls.Add(dividerLine);
            this.Controls.Add(_controlPanel);
            this.Controls.Add(_statusStrip);

            // 全窗体支持直接拖入文件夹（Drag & Drop）
            this.AllowDrop = true;
            this.DragEnter += Form_DragEnter;
            this.DragDrop += Form_DragDrop;
            _controlPanel.AllowDrop = true;
            _controlPanel.DragEnter += Form_DragEnter;
            _controlPanel.DragDrop += Form_DragDrop;
            _explorerContainer.AllowDrop = true;
            _explorerContainer.DragEnter += Form_DragEnter;
            _explorerContainer.DragDrop += Form_DragDrop;

            // 配置系统原生资源管理器导航栏在浏览时自动展开到当前目录
            ConfigureNavPaneExpand();

            // 默认加载当前工作区目录
            string initialPath = @"d:\code\ts\foldersnap";
            if (Directory.Exists(initialPath))
            {
                SetRootFolder(initialPath);
            }
        }

        private void ExplorerHost_FolderNavigated(string path)
        {
            if (_isBatchRunning || _isNavigating) return;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                string normCurrent = string.IsNullOrEmpty(_txtRootPath.Text) ? "" : Path.GetFullPath(_txtRootPath.Text).TrimEnd('\\');
                string normPath = Path.GetFullPath(path).TrimEnd('\\');
                if (!string.Equals(normCurrent, normPath, StringComparison.OrdinalIgnoreCase))
                {
                    _txtRootPath.Text = path;
                    string defaultOutput = Path.Combine(path, "_FolderSnap_Screenshots");
                    _txtOutputPath.Text = defaultOutput;
                    _statusLabel.Text = "当前正在浏览: " + path;
                    ScheduleAutoFitColumns();
                }
            }
        }

        private void Form_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0 && Directory.Exists(files[0]))
                {
                    e.Effect = DragDropEffects.Copy;
                    return;
                }
            }
            e.Effect = DragDropEffects.None;
        }

        private void Form_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0 && Directory.Exists(files[0]))
                {
                    SetRootFolder(files[0]);
                    _statusLabel.Text = "已通过拖拽载入目录: " + files[0];
                }
            }
        }

        private void SetRootFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;

            string normPath = Path.GetFullPath(path);
            _txtRootPath.Text = normPath;
            string defaultOutput = Path.Combine(normPath, "_FolderSnap_Screenshots");
            _txtOutputPath.Text = defaultOutput;
            _explorerHost.Navigate(normPath);
            _statusLabel.Text = "当前正在预览: " + normPath;
            ScheduleAutoFitColumns();
        }

        private void ScheduleAutoFitColumns(int delayMs = 180)
        {
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = delayMs;
            t.Tick += (s, e) =>
            {
                t.Stop();
                t.Dispose();
                if (_explorerHost != null && !this.IsDisposed)
                {
                    _explorerHost.AutoFitColumns();
                }
            };
            t.Start();
        }

        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            if (_explorerHost != null && !this.IsDisposed)
            {
                _explorerHost.AutoFitColumns();
            }
        }

        /// <summary>
        /// 配置 Windows Shell 原生导航窗格自动展开到当前选中的目录
        /// 对应注册表: HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\NavPaneExpandToCurrentFolder
        /// </summary>
        private void ConfigureNavPaneExpand()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("NavPaneExpandToCurrentFolder");
                        if (val == null || Convert.ToInt32(val) != 1)
                        {
                            key.SetValue("NavPaneExpandToCurrentFolder", 1, Microsoft.Win32.RegistryValueKind.DWord);
                        }
                    }
                }
            }
            catch
            {
                // 忽略非特权或受限环境下的注册表写入异常，不影响主程序运行
            }
        }

        /// <summary>
        /// 智能按需自适应截图捕获：
        /// 1. 截图前调用 AutoFitColumns() 确保名称列和核心信息列（修改日期 155px、类型 115px、大小 95px）自然舒展；
        /// 2. 检查当前列表区视口宽度是否足以完整容纳全部列（无横向滚动截断）：
        ///    - 若能完整呈现：直接按当前紧凑尺寸截图，绝不强制拉伸，严格控制截图文件大小；
        ///    - 若开启了左侧导航窗格或窗口较窄导致出现横向溢出截断：
        ///      依据所需总列宽精确计算差值 deltaWidth，临时微调自适应加宽视口，
        ///      确保修改日期、类型、大小 100% 完整展示，彻底消除横向滚动条，截图完毕后即时平滑复原。
        /// 3. 支持长列表智能识别并无缝滚动拼接。
        /// </summary>
        private Bitmap CaptureExplorerView(bool stitchLongList, Action<string> statusCallback = null)
        {
            _explorerHost.AutoFitColumns();
            Application.DoEvents();

            int requiredWidth = _explorerHost.GetColumnsRequiredWidth();
            int currentDefWidth = _explorerHost.GetDefViewWidth();
            int deltaWidth = 0;

            if (requiredWidth > 0 && currentDefWidth > 0 && currentDefWidth < requiredWidth)
            {
                deltaWidth = requiredWidth - currentDefWidth;
            }

            try
            {
                if (deltaWidth > 0)
                {
                    this.Width += deltaWidth;
                    for (int frame = 0; frame < 6; frame++)
                    {
                        Application.DoEvents();
                        Thread.Sleep(20);
                    }
                    _explorerHost.AutoFitColumns();
                    Application.DoEvents();
                }

                if (stitchLongList)
                {
                    return ScrollStitcher.CaptureStitchedList(_explorerHost, statusCallback);
                }
                else
                {
                    return ScrollStitcher.CaptureControl(_explorerHost);
                }
            }
            finally
            {
                if (deltaWidth > 0)
                {
                    this.Width -= deltaWidth;
                    Application.DoEvents();
                    _explorerHost.AutoFitColumns();
                }
            }
        }


        private void BtnBrowseRoot_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "请选择需要扫描并批量截图的根文件夹";
                if (!string.IsNullOrEmpty(_txtRootPath.Text) && Directory.Exists(_txtRootPath.Text))
                {
                    fbd.SelectedPath = _txtRootPath.Text;
                }
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    SetRootFolder(fbd.SelectedPath);
                }
            }
        }

        private void BtnBrowseOutput_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "请选择截图文件保存目录";
                if (!string.IsNullOrEmpty(_txtOutputPath.Text))
                {
                    fbd.SelectedPath = _txtOutputPath.Text;
                }
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    _txtOutputPath.Text = fbd.SelectedPath;
                }
            }
        }

        private void CmbViewMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (_cmbViewMode.SelectedIndex)
            {
                case 0:
                    _explorerHost.CurrentViewMode = FOLDERVIEWMODE.FVM_DETAILS;
                    break;
                case 1:
                    _explorerHost.CurrentViewMode = FOLDERVIEWMODE.FVM_ICON;
                    break;
                case 2:
                    _explorerHost.CurrentViewMode = FOLDERVIEWMODE.FVM_LIST;
                    break;
                case 3:
                    _explorerHost.CurrentViewMode = FOLDERVIEWMODE.FVM_TILE;
                    break;
            }
        }

        private void BtnCaptureCurrent_Click(object sender, EventArgs e)
        {
            string currentFolder = _explorerHost.CurrentPath;
            if (string.IsNullOrEmpty(currentFolder) || !Directory.Exists(currentFolder))
            {
                MessageBox.Show("当前目录无效，请先浏览有效文件夹！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string outDir = _txtOutputPath.Text.Trim();
            if (string.IsNullOrEmpty(outDir))
            {
                outDir = Path.Combine(currentFolder, "_FolderSnap_Screenshots");
                _txtOutputPath.Text = outDir;
            }
            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            string fileName = "Capture_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
            string filePath = Path.Combine(outDir, fileName);

            try
            {
                Bitmap bmp = CaptureExplorerView(_chkStitchLongList.Checked, s => _statusLabel.Text = s);

                if (bmp != null)
                {
                    if (_chkIncludeHeader.Checked)
                    {
                        int count = _explorerHost.GetItemCount();
                        bmp = ScrollStitcher.AttachMetadataHeader(bmp, currentFolder, currentFolder, count);
                    }

                    bmp.Save(filePath, ImageFormat.Png);

                    // 剪贴板优先：直接写入系统剪贴板，彻底消除模态对话框打扰
                    try
                    {
                        Clipboard.SetImage(bmp);
                    }
                    catch
                    {
                    }

                    bmp.Dispose();
                    _statusLabel.Text = "✅ 截图已保存至 " + fileName + "，并已复制到系统剪贴板 (可直接 Ctrl+V 粘贴)！";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存截图失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnStartBatch_Click(object sender, EventArgs e)
        {
            if (_isBatchRunning)
            {
                RequestCancelBatch();
                return;
            }

            string rootDir = _txtRootPath.Text.Trim();
            if (string.IsNullOrEmpty(rootDir) || !Directory.Exists(rootDir))
            {
                MessageBox.Show("请先选择有效的目标根文件夹！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string outDir = _txtOutputPath.Text.Trim();
            if (string.IsNullOrEmpty(outDir))
            {
                outDir = Path.Combine(rootDir, "_FolderSnap_Screenshots");
                _txtOutputPath.Text = outDir;
            }
            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            // 提取扫描深度限制参数
            int maxDepth = -1;
            if (_cmbMaxDepth.SelectedIndex == 1) maxDepth = 1;
            else if (_cmbMaxDepth.SelectedIndex == 2) maxDepth = 2;
            else if (_cmbMaxDepth.SelectedIndex == 3) maxDepth = 3;

            // 启动全自动批量流水线（自动排除输出目录，避免嵌套死循环）
            BatchScanner scanner = new BatchScanner(_chkFilterBlacklist.Checked ? null : new string[0]);
            List<string> targetFolders = scanner.ScanDirectories(rootDir, true, outDir, maxDepth);

            if (targetFolders.Count == 0)
            {
                MessageBox.Show("未找到任何可扫描的文件夹！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult dr = MessageBox.Show(
                string.Format("即将对选定目录下的 {0} 个文件夹依次进行资源管理器呈现与截图。\n扫描深度: {1}\n输出目录: {2}\n\n是否立即开始？",
                    targetFolders.Count, _cmbMaxDepth.SelectedItem, outDir),
                "确认批量任务",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (dr != DialogResult.Yes) return;

            _isCancelRequested = false;
            SetControlsBatchState(true);
            _progressBar.Minimum = 0;
            _progressBar.Maximum = targetFolders.Count;
            _progressBar.Value = 0;
            _progressBar.Visible = true;

            int successCount = 0;
            try
            {
                for (int i = 0; i < targetFolders.Count; i++)
                {
                    if (_isCancelRequested)
                    {
                        break;
                    }

                    string folder = targetFolders[i];
                    _statusLabel.Text = string.Format("正在处理 ({0}/{1}): {2}", i + 1, targetFolders.Count, Path.GetFileName(folder));
                    _progressBar.Value = i + 1;
                    Application.DoEvents();

                    if (_isCancelRequested) break;

                    // 1. 驱动内嵌 Explorer 导航至当前文件夹
                    _explorerHost.Navigate(folder);
                    Application.DoEvents();

                    // 充分泵送 Windows 消息循环，确保 Shell 资源管理器完全加载文件项与系统图标并绘制完成
                    DateTime waitStart = DateTime.Now;
                    while ((DateTime.Now - waitStart).TotalMilliseconds < 600)
                    {
                        if (_isCancelRequested) break;
                        Application.DoEvents();
                        Thread.Sleep(30);
                    }

                    if (_isCancelRequested) break;

                    // 2. 执行智能全宽截图（长列表滚动拼图或标准捕获，自动消除导航窗格挤压）
                    Bitmap bmp = CaptureExplorerView(_chkStitchLongList.Checked);

                    if (bmp != null)
                    {
                        if (_chkIncludeHeader.Checked)
                        {
                            int count = _explorerHost.GetItemCount();
                            bmp = ScrollStitcher.AttachMetadataHeader(bmp, folder, rootDir, count);
                        }

                        string safeFileName = string.Format("{0:D3}_{1}", i + 1, BatchScanner.GenerateSafeFileName(rootDir, folder));
                        string outPath = Path.Combine(outDir, safeFileName);
                        bmp.Save(outPath, ImageFormat.Png);
                        bmp.Dispose();
                        successCount++;
                    }

                    Thread.Sleep(40);
                    Application.DoEvents();
                }

                if (_isCancelRequested)
                {
                    _statusLabel.Text = string.Format("⏹️ 任务已由用户手动停止！共安全生成 {0} 张图片。", successCount);
                    MessageBox.Show(string.Format("批处理已安全中止。\n已完成并保存 {0} 个文件夹的高清截图。", successCount), "任务已中止", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    _statusLabel.Text = string.Format("批量任务圆满完成！共成功截取 {0} 张图片。", successCount);
                    DialogResult openDr = MessageBox.Show(
                        string.Format("批量截图完成！\n已成功生成 {0} 个文件夹的高清截图。\n\n是否立即打开输出目录？", successCount),
                        "任务完成",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (openDr == DialogResult.Yes)
                    {
                        OpenFolder(outDir);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("批量截图执行中断: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetControlsBatchState(false);
                _progressBar.Visible = false;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && _isBatchRunning)
            {
                RequestCancelBatch();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void RequestCancelBatch()
        {
            if (_isBatchRunning && !_isCancelRequested)
            {
                _isCancelRequested = true;
                _btnStartBatch.Text = "正在安全停止...";
                _btnStartBatch.Enabled = false;
                _statusLabel.Text = "收到停止指令，正在等待当前文件夹截图保存完毕后退出...";
            }
        }

        private void SetControlsBatchState(bool isRunning)
        {
            _isBatchRunning = isRunning;
            if (isRunning)
            {
                _btnStartBatch.Text = "⏹️ 停止处理 (ESC)";
                _btnStartBatch.BackColor = Color.FromArgb(220, 38, 38); // 红色
                _btnStartBatch.Enabled = true;
            }
            else
            {
                _btnStartBatch.Text = "⚡ 开始全自动批量截图";
                _btnStartBatch.BackColor = Color.FromArgb(37, 99, 235); // 经典蓝
                _btnStartBatch.Enabled = true;
            }

            _btnCaptureCurrent.Enabled = !isRunning;
            _btnBrowseRoot.Enabled = !isRunning;
            _btnBrowseOutput.Enabled = !isRunning;
            _cmbViewMode.Enabled = !isRunning;
            _cmbMaxDepth.Enabled = !isRunning;
            _chkShowTreeView.Enabled = !isRunning;
            _chkIncludeHeader.Enabled = !isRunning;
            _chkStitchLongList.Enabled = !isRunning;
            _chkFilterBlacklist.Enabled = !isRunning;
        }


        private void BtnOpenFolder_Click(object sender, EventArgs e)
        {
            string outDir = _txtOutputPath.Text.Trim();
            if (!string.IsNullOrEmpty(outDir) && Directory.Exists(outDir))
            {
                OpenFolder(outDir);
            }
            else
            {
                MessageBox.Show("输出目录尚未创建或不存在！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static void OpenFolder(string path)
        {
            try
            {
                Process.Start("explorer.exe", path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开目录失败: " + ex.Message);
            }
        }
    }
}
