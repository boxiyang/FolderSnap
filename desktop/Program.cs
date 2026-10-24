using System;
using System.Windows.Forms;
using FolderSnapDesktop.Interop;
using FolderSnapDesktop.UI;

namespace FolderSnapDesktop
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    System.IO.File.WriteAllText("desktop_crash.log", e.ExceptionObject.ToString());
                    MessageBox.Show(e.ExceptionObject.ToString(), "Unhandled Exception");
                };

                Application.ThreadException += (s, e) =>
                {
                    System.IO.File.WriteAllText("desktop_crash.log", e.Exception.ToString());
                    MessageBox.Show(e.Exception.ToString(), "Thread Exception");
                };

                // 启用高 DPI 缩放感知支持，确保在 100%、125%、150%、200% 缩放屏幕上文字边缘锐利
                Win32Native.EnableHighDpi();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText("desktop_crash.log", ex.ToString());
                MessageBox.Show(ex.ToString(), "Main Catch Exception");
            }
        }
    }
}
