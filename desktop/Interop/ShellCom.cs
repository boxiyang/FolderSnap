using System;
using System.Runtime.InteropServices;

namespace FolderSnapDesktop.Interop
{
    [ComImport]
    [Guid("dfd3b6b5-c10c-4be9-85f6-a66969f402f6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IExplorerBrowser
    {
        void Initialize(IntPtr hwndParent, [In] ref RECT prc, [In] ref FOLDERSETTINGS pfs);
        void Destroy();
        void SetRect([In, Out] ref IntPtr phdwp, RECT rcBrowser);
        void SetPropertyBag([MarshalAs(UnmanagedType.LPWStr)] string pszPropertyBag);
        void SetEmptyText([MarshalAs(UnmanagedType.LPWStr)] string pszEmptyText);
        void SetFolderSettings([In] ref FOLDERSETTINGS pfs);
        void Advise([MarshalAs(UnmanagedType.Interface)] IExplorerBrowserEvents psbe, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(EXPLORER_BROWSER_OPTIONS dwFlag);
        void GetOptions(out EXPLORER_BROWSER_OPTIONS pdwFlag);
        void BrowseToIDList(IntPtr pidl, uint uFlags);
        void BrowseToObject([MarshalAs(UnmanagedType.IUnknown)] object punk, uint uFlags);
        void FillFromObject([MarshalAs(UnmanagedType.IUnknown)] object punk, int dwFlags);
        void RemoveAll();
        void GetCurrentView([In] ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    }

    [ComImport]
    [Guid("71f96385-ddd6-48d3-a0c1-ae06e8b055fb")]
    public class ExplorerBrowserClass
    {
    }

    [ComImport]
    [Guid("361b67fb-3238-4607-b6e6-1001de7edbe8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IExplorerBrowserEvents
    {
        [PreserveSig]
        int OnNavigationPending(IntPtr pidlFolder);

        [PreserveSig]
        int OnViewCreated(IntPtr psv);

        [PreserveSig]
        int OnNavigationComplete(IntPtr pidlFolder);

        [PreserveSig]
        int OnNavigationFailed(IntPtr pidlFolder);
    }

    [ComImport]
    [Guid("e07010ec-bc17-44c0-97b0-46c7c95b9edc")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IExplorerPaneVisibility
    {
        [PreserveSig]
        int GetPaneState([In] ref Guid ep, out uint peps);
    }

    [ComImport]
    [Guid("6d5140c1-7436-11ce-8034-00aa006009fa")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IServiceProvider
    {
        [PreserveSig]
        int QueryService([In] ref Guid guidService, [In] ref Guid riid, out IntPtr ppvObject);
    }

    public static class ShellGuids
    {
        public static readonly Guid EP_NavPane = new Guid("cb316b22-25f7-42b8-8a09-540d23a43c2f");
        public static readonly Guid EP_Commands = new Guid("d9745868-ca5f-4a76-91cd-f5a129fbb076");
        public static readonly Guid EP_DetailsPane = new Guid("43abf98b-89b8-472d-b9ce-e69b8229f019");
        public static readonly Guid EP_PreviewPane = new Guid("893c63d1-45c8-4d17-be19-223be71be365");
        public static readonly Guid EP_Ribbon = new Guid("d27524a8-c9f2-4834-a106-df8889fd4f37");
        public static readonly Guid IID_IExplorerPaneVisibility = new Guid("e07010ec-bc17-44c0-97b0-46c7c95b9edc");
    }



    [ComImport]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem
    {
        void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    public enum SIGDN : uint
    {
        SIGDN_NORMALDISPLAY = 0x00000000,
        SIGDN_PARENTRELATIVEPARSING = 0x80018001,
        SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000,
        SIGDN_PARENTRELATIVEEDITING = 0x80031001,
        SIGDN_DESKTOPABSOLUTEEDITING = 0x8004c000,
        SIGDN_FILESYSPATH = 0x80058000,
        SIGDN_URL = 0x80068000,
        SIGDN_PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
        SIGDN_PARENTRELATIVE = 0x80080001
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;

        public RECT(int l, int t, int r, int b)
        {
            left = l;
            top = t;
            right = r;
            bottom = b;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FOLDERSETTINGS
    {
        public uint ViewMode;
        public uint fFlags;
    }

    public enum FOLDERVIEWMODE : uint
    {
        FVM_AUTO = 0,
        FVM_FIRST = 1,
        FVM_ICON = 1,
        FVM_SMALLICON = 2,
        FVM_LIST = 3,
        FVM_DETAILS = 4,
        FVM_THUMBNAIL = 5,
        FVM_TILE = 6,
        FVM_THUMBSTRIP = 7,
        FVM_CONTENT = 8,
        FVM_LAST = 8
    }

    public enum FOLDERFLAGS : uint
    {
        FWF_NONE = 0x00000000,
        FWF_AUTOARRANGE = 0x00000001,
        FWF_ABBREVIATEDNAMES = 0x00000002,
        FWF_SNAPTOGRID = 0x00000004,
        FWF_OWNERDATA = 0x00000008,
        FWF_BESTFITPATH = 0x00000010,
        FWF_KEYBOARDCOMMUNICATES = 0x00000020,
        FWF_TRANSPARENT = 0x00000040,
        FWF_NOCLIENTEDGE = 0x00000080,
        FWF_NOSCROLL = 0x00000100,
        FWF_ALIGNLEFT = 0x00000200,
        FWF_NOICONS = 0x00000400,
        FWF_SHOWSELALWAYS = 0x00000800,
        FWF_NOVISIBLE = 0x00001000,
        FWF_SINGLECLICKACTIVATE = 0x00002000,
        FWF_SINGLESEL = 0x00004000,
        FWF_NOHEADERINALLVIEWS = 0x00008000,
        FWF_EXTENDEDTILES = 0x00010000,
        FWF_TRICHECKSELECT = 0x00020000,
        FWF_AUTOCHECKSELECT = 0x00040000,
        FWF_NOENUMREFRESH = 0x00080000,
        FWF_HIDEHEADER = 0x00100000
    }

    [Flags]
    public enum EXPLORER_BROWSER_OPTIONS : uint
    {
        EBO_NONE = 0x00000000,
        EBO_NAVIGATEONCE = 0x00000001,
        EBO_SHOWFRAMES = 0x00000002,
        EBO_ALWAYSNAVIGATE = 0x00000004,
        EBO_NOTREEVIEW = 0x00000008,
        EBO_NOWRAPPERWINDOW = 0x00000010,
        EBO_HTMLSHAREDFOLDERVIEW = 0x00000020,
        EBO_NOBORDER = 0x00000040,
        EBO_NOPERSISTVIEWSTATE = 0x00000080
    }

    public static class ShellNative
    {
        public static readonly Guid IID_IShellItem = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
        public static readonly Guid IID_IExplorerBrowser = new Guid("dfd3b6b5-c10c-4be9-85f6-a66969f402f6");

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHGetNameFromIDList(IntPtr pidl, SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);

        [DllImport("shlwapi.dll", EntryPoint = "IUnknown_SetSite", SetLastError = true)]
        public static extern int IUnknown_SetSite([MarshalAs(UnmanagedType.IUnknown)] object punk, [MarshalAs(UnmanagedType.IUnknown)] object punkSite);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHParseDisplayName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszName,
            IntPtr pbc,
            out IntPtr ppidl,
            uint sfgaoIn,
            out uint psfgaoOut);

        [DllImport("shell32.dll")]
        public static extern void ILFree(IntPtr pidl);
    }
}

