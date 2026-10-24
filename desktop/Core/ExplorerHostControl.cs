using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FolderSnapDesktop.Interop;

namespace FolderSnapDesktop.Core
{
    public class ExplorerHostControl : UserControl
    {
        private IExplorerBrowser _explorerBrowser;
        private string _currentPath;
        private FOLDERVIEWMODE _currentViewMode = FOLDERVIEWMODE.FVM_DETAILS;
        private bool _showTreeView = true;
        private uint _eventsCookie = 0;
        private ExplorerBrowserEventsSink _eventsSink;

        public event Action<string> FolderNavigated;

        public string CurrentPath
        {
            get { return _currentPath; }
        }

        public bool ShowTreeView
        {
            get { return _showTreeView; }
            set
            {
                if (_showTreeView != value)
                {
                    _showTreeView = value;
                    ApplyOptions();
                }
            }
        }

        public FOLDERVIEWMODE CurrentViewMode
        {
            get { return _currentViewMode; }
            set
            {
                _currentViewMode = value;
                ApplyFolderSettings();
            }
        }

        public ExplorerHostControl()
        {
            this.SetStyle(ControlStyles.UserPaint, false);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            InitExplorerBrowser();
        }

        private void InitExplorerBrowser()
        {
            if (_explorerBrowser != null) return;

            try
            {
                _explorerBrowser = (IExplorerBrowser)new ExplorerBrowserClass();

                // 挂载 Shell 事件与窗格可见性服务监听器
                // 注意：必须在 Initialize 之前 Advise，ExplorerBrowser 在初始化创建窗格布局时才会查询 IExplorerPaneVisibility
                _eventsSink = new ExplorerBrowserEventsSink(this);
                _explorerBrowser.Advise(_eventsSink, out _eventsCookie);

                // 必须置位 EBO_SHOWFRAMES，第三方宿主程序才能激活 Explorer 的导航树与窗格布局体系
                EXPLORER_BROWSER_OPTIONS options =
                    EXPLORER_BROWSER_OPTIONS.EBO_SHOWFRAMES |
                    EXPLORER_BROWSER_OPTIONS.EBO_NOBORDER |
                    EXPLORER_BROWSER_OPTIONS.EBO_ALWAYSNAVIGATE;

                if (!_showTreeView)
                {
                    options |= EXPLORER_BROWSER_OPTIONS.EBO_NOTREEVIEW;
                }

                _explorerBrowser.SetOptions(options);

                RECT rect = new RECT(0, 0, this.Width, this.Height);
                FOLDERSETTINGS settings = new FOLDERSETTINGS();
                settings.ViewMode = (uint)_currentViewMode;
                settings.fFlags = (uint)(FOLDERFLAGS.FWF_AUTOARRANGE | FOLDERFLAGS.FWF_SHOWSELALWAYS);

                _explorerBrowser.Initialize(this.Handle, ref rect, ref settings);

                if (!string.IsNullOrEmpty(_currentPath))
                {
                    Navigate(_currentPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("初始化 Windows 资源管理器控件失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnInternalNavigated(string path)
        {
            _currentPath = path;
            if (FolderNavigated != null)
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action<string>(p => FolderNavigated(p)), path);
                }
                else
                {
                    FolderNavigated(path);
                }
            }
        }

        public void ApplyOptions()
        {
            if (_explorerBrowser == null) return;
            try
            {
                EXPLORER_BROWSER_OPTIONS options =
                    EXPLORER_BROWSER_OPTIONS.EBO_SHOWFRAMES |
                    EXPLORER_BROWSER_OPTIONS.EBO_NOBORDER |
                    EXPLORER_BROWSER_OPTIONS.EBO_ALWAYSNAVIGATE;

                if (!_showTreeView)
                {
                    options |= EXPLORER_BROWSER_OPTIONS.EBO_NOTREEVIEW;
                }

                _explorerBrowser.SetOptions(options);

            }
            catch
            {
            }
        }

        public bool Navigate(string path)
        {
            _currentPath = path;
            if (_explorerBrowser == null || !this.IsHandleCreated) return false;

            try
            {
                IShellItem item;
                ShellNative.SHCreateItemFromParsingName(path, IntPtr.Zero, ShellNative.IID_IShellItem, out item);
                if (item != null)
                {
                    try
                    {
                        _explorerBrowser.BrowseToObject(item, 0);
                        return true;
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(item);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("导航目录异常: " + ex.Message);
            }
            return false;
        }

        public void ApplyFolderSettings()
        {
            if (_explorerBrowser == null) return;
            try
            {
                FOLDERSETTINGS settings = new FOLDERSETTINGS();
                settings.ViewMode = (uint)_currentViewMode;
                settings.fFlags = (uint)(FOLDERFLAGS.FWF_AUTOARRANGE | FOLDERFLAGS.FWF_SHOWSELALWAYS);
                _explorerBrowser.SetFolderSettings(ref settings);
            }
            catch
            {
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_explorerBrowser != null && this.IsHandleCreated)
            {
                IntPtr phdwp = IntPtr.Zero;
                RECT rect = new RECT(0, 0, this.Width, this.Height);
                _explorerBrowser.SetRect(ref phdwp, rect);
            }
        }

        public IntPtr GetDefViewHandle()
        {
            IntPtr target = IntPtr.Zero;
            Win32Native.EnumChildWindows(this.Handle, (hwnd, lParam) =>
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
                Win32Native.GetClassName(hwnd, sb, sb.Capacity);
                if (sb.ToString() == "SHELLDLL_DefView")
                {
                    target = hwnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            return target != IntPtr.Zero ? target : this.Handle;
        }

        public IntPtr GetFileListHandle()
        {
            IntPtr defView = GetDefViewHandle();
            IntPtr target = IntPtr.Zero;

            // 优先在 SHELLDLL_DefView 内部寻找真正的文件列表宿主 SysListView32
            if (defView != IntPtr.Zero && defView != this.Handle)
            {
                Win32Native.EnumChildWindows(defView, (hwnd, lParam) =>
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
                    Win32Native.GetClassName(hwnd, sb, sb.Capacity);
                    string cls = sb.ToString();
                    if (cls == "SysListView32")
                    {
                        target = hwnd;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);

                if (target != IntPtr.Zero) return target;

                // 若未找到 SysListView32，再寻找 defView 直属的 DirectUIHWND
                Win32Native.EnumChildWindows(defView, (hwnd, lParam) =>
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
                    Win32Native.GetClassName(hwnd, sb, sb.Capacity);
                    string cls = sb.ToString();
                    if (cls == "DirectUIHWND")
                    {
                        target = hwnd;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);

                if (target != IntPtr.Zero) return target;
                return defView;
            }

            // 全局保底降级查找
            Win32Native.EnumChildWindows(this.Handle, (hwnd, lParam) =>
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
                Win32Native.GetClassName(hwnd, sb, sb.Capacity);
                string cls = sb.ToString();
                if (cls == "SysListView32")
                {
                    target = hwnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            return target != IntPtr.Zero ? target : this.Handle;
        }

        public int GetHeaderHeight()
        {
            IntPtr header = IntPtr.Zero;
            IntPtr defView = GetDefViewHandle();
            IntPtr searchRoot = defView != IntPtr.Zero ? defView : this.Handle;

            Win32Native.EnumChildWindows(searchRoot, (hwnd, lParam) =>
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
                Win32Native.GetClassName(hwnd, sb, sb.Capacity);
                if (sb.ToString() == "SysHeader32")
                {
                    header = hwnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            if (header != IntPtr.Zero)
            {
                RECT r;
                Win32Native.GetWindowRect(header, out r);
                int h = r.bottom - r.top;
                if (h > 0 && h < 100) return h;
            }
            return 24;
        }

        public int GetItemCount()
        {
            IntPtr listHwnd = GetFileListHandle();
            if (listHwnd != IntPtr.Zero)
            {
                IntPtr count = Win32Native.SendMessage(listHwnd, (uint)Win32Native.LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
                int val = count.ToInt32();
                if (val >= 0) return val;
            }
            return -1;
        }

        public int GetItemHeight()
        {
            IntPtr listHwnd = GetFileListHandle();
            if (listHwnd != IntPtr.Zero)
            {
                RECT rect = new RECT();
                rect.left = 0; // LVIR_BOUNDS
                IntPtr pRect = Marshal.AllocHGlobal(Marshal.SizeOf(rect));
                try
                {
                    Marshal.StructureToPtr(rect, pRect, false);
                    IntPtr res = Win32Native.SendMessage(listHwnd, (uint)Win32Native.LVM_GETITEMRECT, IntPtr.Zero, pRect);
                    if (res != IntPtr.Zero)
                    {
                        rect = (RECT)Marshal.PtrToStructure(pRect, typeof(RECT));
                        int h = rect.bottom - rect.top;
                        if (h > 12 && h < 120) return h;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(pRect);
                }
            }
            return 22; // 经典 Windows 详细视图默认行高
        }

        public int GetDefViewWidth()
        {
            IntPtr defView = GetDefViewHandle();
            if (defView != IntPtr.Zero)
            {
                RECT rc;
                if (Win32Native.GetClientRect(defView, out rc))
                {
                    return rc.right - rc.left;
                }
            }
            return 0;
        }

        public int GetColumnsRequiredWidth()
        {
            if (_currentViewMode != FOLDERVIEWMODE.FVM_DETAILS) return 0;
            IntPtr listHwnd = GetFileListHandle();
            if (listHwnd == IntPtr.Zero) return 0;

            int total = 0;
            for (int c = 0; c < 4; c++)
            {
                IntPtr w = Win32Native.SendMessage(listHwnd, (uint)Win32Native.LVM_GETCOLUMNWIDTH, (IntPtr)c, IntPtr.Zero);
                int cw = w.ToInt32();
                if (cw > 0) total += cw;
            }
            return total > 0 ? total + 25 : 0;
        }

        /// <summary>
        /// 智能自适应列宽：在详细信息视图下，依据当前目录最长文件名自动撑开名称列，
        /// 并按科学比例保全修改日期、类型、大小列，根除文件名省略号截断（...）与列头文字被截断。
        /// </summary>
        public void AutoFitColumns()
        {
            if (_currentViewMode != FOLDERVIEWMODE.FVM_DETAILS) return;

            try
            {
                IntPtr listHwnd = GetFileListHandle();

                // 若为经典 SysListView32，直接发送 Win32 消息精细化分配各列
                System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
                Win32Native.GetClassName(listHwnd, sb, sb.Capacity);
                string cls = sb.ToString();

                if (cls == "SysListView32")
                {
                    // 列 0（名称）：基于内容自适应展开，保底 220px，上限 450px
                    Win32Native.SendMessage(listHwnd, (uint)Win32Native.LVM_SETCOLUMNWIDTH, 0, Win32Native.LVSCW_AUTOSIZE);
                    IntPtr currentW = Win32Native.SendMessage(listHwnd, (uint)Win32Native.LVM_GETCOLUMNWIDTH, 0, 0);
                    int w = currentW.ToInt32();
                    if (w < 220)
                    {
                        Win32Native.SendMessage(listHwnd, (uint)Win32Native.LVM_SETCOLUMNWIDTH, 0, 220);
                    }
                    else if (w > 450)
                    {
                        Win32Native.SendMessage(listHwnd, (uint)Win32Native.LVM_SETCOLUMNWIDTH, 0, 450);
                    }

                    // 列 1（修改日期）：固定 155px，防止截断成“修改E”并完整容纳"yyyy/M/d HH:mm"
                    Win32Native.SendMessage(listHwnd, (uint)Win32Native.LVM_SETCOLUMNWIDTH, 1, 155);
                    // 列 2（类型）：保底 115px，保证能完整展示类型全名
                    Win32Native.SendMessage(listHwnd, (uint)Win32Native.LVM_SETCOLUMNWIDTH, 2, 115);
                    // 列 3（大小）：保底 95px，保证能完整展示数字及单位 KB/MB
                    Win32Native.SendMessage(listHwnd, (uint)Win32Native.LVM_SETCOLUMNWIDTH, 3, 95);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 检测当前列表区是否存在横向溢出截断（即是否需要横向滚动才能看全各列）
        /// </summary>
        public bool IsHorizontalScrollNeeded()
        {
            try
            {
                IntPtr listHwnd = GetFileListHandle();
                if (listHwnd == IntPtr.Zero) return false;

                Win32Native.SCROLLINFO si = new Win32Native.SCROLLINFO();
                si.cbSize = (uint)Marshal.SizeOf(typeof(Win32Native.SCROLLINFO));
                si.fMask = (uint)Win32Native.SIF_ALL;

                if (Win32Native.GetScrollInfo(listHwnd, Win32Native.SB_HORZ, ref si))
                {
                    // 当 nMax > nPage 且 nPage > 0 时，说明横向有内容被挤出可视区
                    if (si.nPage > 0 && si.nMax > (int)si.nPage)
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions]
        [System.Security.SecurityCritical]
        protected override void Dispose(bool disposing)
        {
            if (disposing && _explorerBrowser != null)
            {
                try
                {
                    if (_eventsCookie != 0)
                    {
                        try { _explorerBrowser.Unadvise(_eventsCookie); } catch { }
                        _eventsCookie = 0;
                    }
                    try { _explorerBrowser.Destroy(); } catch { }
                    try { Marshal.ReleaseComObject(_explorerBrowser); } catch { }
                    _explorerBrowser = null;
                }
                catch
                {
                }
            }
            base.Dispose(disposing);
        }

        private class ExplorerBrowserEventsSink : IExplorerBrowserEvents, IExplorerPaneVisibility, FolderSnapDesktop.Interop.IServiceProvider
        {
            private readonly ExplorerHostControl _owner;
            public ExplorerBrowserEventsSink(ExplorerHostControl owner)
            {
                _owner = owner;
            }

            public int OnNavigationPending(IntPtr pidlFolder) { return 0; }
            public int OnViewCreated(IntPtr psv) { return 0; }
            public int OnNavigationComplete(IntPtr pidlFolder)
            {
                if (pidlFolder != IntPtr.Zero)
                {
                    string path;
                    int hr = ShellNative.SHGetNameFromIDList(pidlFolder, SIGDN.SIGDN_FILESYSPATH, out path);
                    if (hr == 0 && !string.IsNullOrEmpty(path))
                    {
                        _owner.OnInternalNavigated(path);
                    }
                }
                return 0;
            }
            public int OnNavigationFailed(IntPtr pidlFolder) { return 0; }

            public int GetPaneState(ref Guid ep, out uint peps)
            {
                if (ep == ShellGuids.EP_NavPane)
                {
                    peps = _owner.ShowTreeView ? 0x00020001u : 0x00020002u; // EPS_DEFAULT_ON/OFF | EPS_FORCE
                    return 0;
                }

                // 强制关闭命令栏、详情窗格、预览窗格与功能区等干扰元素，保持界面干净原生
                peps = 0x00020002u; // EPS_DEFAULT_OFF | EPS_FORCE
                return 0;
            }

            public int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject)
            {
                if (guidService == ShellGuids.IID_IExplorerPaneVisibility || riid == ShellGuids.IID_IExplorerPaneVisibility)
                {
                    ppvObject = Marshal.GetComInterfaceForObject(this, typeof(IExplorerPaneVisibility));
                    return 0;
                }
                ppvObject = IntPtr.Zero;
                return -2147467262; // E_NOINTERFACE (0x80004002)
            }
        }
    }
}

