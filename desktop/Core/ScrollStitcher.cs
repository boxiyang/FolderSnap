using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using FolderSnapDesktop.Interop;

namespace FolderSnapDesktop.Core
{
    public static class ScrollStitcher
    {
        public static Bitmap CaptureWindowHandle(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return null;
            RECT rc;
            Win32Native.GetClientRect(hwnd, out rc);
            int width = rc.right - rc.left;
            int height = rc.bottom - rc.top;
            if (width <= 0 || height <= 0) return null;

            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                IntPtr hdcDest = g.GetHdc();
                try
                {
                    bool success = Win32Native.PrintWindow(hwnd, hdcDest, Win32Native.PW_RENDERFULLCONTENT);
                    if (!success)
                    {
                        IntPtr hdcSrc = Win32Native.GetDC(hwnd);
                        if (hdcSrc != IntPtr.Zero)
                        {
                            Win32Native.BitBlt(hdcDest, 0, 0, width, height, hdcSrc, 0, 0, Win32Native.SRCCOPY);
                            Win32Native.ReleaseDC(hwnd, hdcSrc);
                        }
                    }
                }
                finally
                {
                    g.ReleaseHdc(hdcDest);
                }
            }
            return bmp;
        }

        public static Bitmap CaptureControl(Control control)
        {
            if (control == null) return null;

            // 若为 ExplorerHostControl，优先捕获右侧纯净文件列表 DefView 视图句柄，排除左侧导航树干扰
            ExplorerHostControl ehc = control as ExplorerHostControl;
            if (ehc != null)
            {
                IntPtr defView = ehc.GetDefViewHandle();
                if (defView != IntPtr.Zero && defView != control.Handle)
                {
                    Bitmap viewBmp = CaptureWindowHandle(defView);
                    if (viewBmp != null) return viewBmp;
                }
            }

            if (control.Width <= 0 || control.Height <= 0) return null;

            Bitmap bmp = new Bitmap(control.Width, control.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                IntPtr hdcDest = g.GetHdc();
                try
                {
                    // 优先调用 PrintWindow 捕获完整客户区
                    bool success = Win32Native.PrintWindow(control.Handle, hdcDest, Win32Native.PW_RENDERFULLCONTENT);
                    if (!success)
                    {
                        // 降级使用 BitBlt 抓取屏幕 DC
                        IntPtr hdcSrc = Win32Native.GetDC(control.Handle);
                        if (hdcSrc != IntPtr.Zero)
                        {
                            Win32Native.BitBlt(hdcDest, 0, 0, control.Width, control.Height, hdcSrc, 0, 0, Win32Native.SRCCOPY);
                            Win32Native.ReleaseDC(control.Handle, hdcSrc);
                        }
                    }
                }
                finally
                {
                    g.ReleaseHdc(hdcDest);
                }
            }
            return bmp;
        }


        public static Bitmap CaptureStitchedList(ExplorerHostControl hostControl, Action<string> logCallback = null)
        {
            if (hostControl == null) return null;

            IntPtr listHwnd = hostControl.GetFileListHandle();
            Win32Native.SCROLLINFO si = new Win32Native.SCROLLINFO();
            si.cbSize = (uint)Marshal.SizeOf(si);
            si.fMask = Win32Native.SIF_ALL;

            bool hasScroll = Win32Native.GetScrollInfo(listHwnd, Win32Native.SB_VERT, ref si);
            int maxRange = si.nMax - si.nMin + 1;

            if (logCallback != null)
            {
                logCallback(string.Format("调试: listHwnd=0x{0:X}, hasScroll={1}, nMin={2}, nMax={3}, nPage={4}, nPos={5}, maxRange={6}",
                    listHwnd.ToInt64(), hasScroll, si.nMin, si.nMax, si.nPage, si.nPos, maxRange));
            }

            // 如果没有垂直滚动条，或者一页完全容纳所有条目，直接单帧快照
            if (!hasScroll || si.nMax <= 0 || si.nPage == 0 || si.nPage >= (uint)maxRange)
            {
                if (logCallback != null) logCallback("目录条目已完全在可视区域内，执行单帧捕获");
                return CaptureControl(hostControl);
            }

            if (logCallback != null)
            {
                logCallback(string.Format("检测到长列表滚动条 (范围: 0-{0}, 每页: {1})，启动无缝滚动拼图引擎...", si.nMax, si.nPage));
            }

            // 1. 先回到顶部
            Win32Native.SendMessage(listHwnd, Win32Native.WM_VSCROLL, (IntPtr)Win32Native.SB_TOP, IntPtr.Zero);
            for (int i = 0; i < 8; i++) { Application.DoEvents(); Thread.Sleep(15); }

            List<Bitmap> frames = new List<Bitmap>();
            List<int> positions = new List<int>();

            int lastPos = -1;
            int maxLoops = 50; // 防止异常死循环
            int loopCount = 0;

            // 获取当前位置并截第一帧
            while (loopCount < maxLoops)
            {
                si.cbSize = (uint)Marshal.SizeOf(si);
                si.fMask = Win32Native.SIF_POS | Win32Native.SIF_RANGE | Win32Native.SIF_PAGE;
                Win32Native.GetScrollInfo(listHwnd, Win32Native.SB_VERT, ref si);

                if (si.nPos == lastPos && frames.Count > 0)
                {
                    // 已经触底，无法再向下滚动
                    break;
                }

                lastPos = si.nPos;
                positions.Add(lastPos);

                // 捕获当前帧
                Bitmap currentFrame = CaptureControl(hostControl);
                frames.Add(currentFrame);

                // 向下翻页滚动
                Win32Native.SendMessage(listHwnd, Win32Native.WM_VSCROLL, (IntPtr)Win32Native.SB_PAGEDOWN, IntPtr.Zero);
                for (int i = 0; i < 8; i++) { Application.DoEvents(); Thread.Sleep(20); } // 等待资源管理器视图完成渲染重绘

                loopCount++;
            }

            // 恢复回顶部
            Win32Native.SendMessage(listHwnd, Win32Native.WM_VSCROLL, (IntPtr)Win32Native.SB_TOP, IntPtr.Zero);
            for (int i = 0; i < 5; i++) { Application.DoEvents(); Thread.Sleep(10); }

            if (frames.Count == 0) return CaptureControl(hostControl);
            if (frames.Count == 1) return frames[0];

            // 2. 进行垂直多帧智能缝合
            return StitchVerticalFrames(frames, positions, (int)si.nPage, hostControl);
        }

        private static Bitmap StitchVerticalFrames(List<Bitmap> frames, List<int> positions, int pageSize, ExplorerHostControl hostControl)
        {
            if (frames.Count == 0) return null;
            if (frames.Count == 1) return frames[0];

            int width = frames[0].Width;
            int viewHeight = frames[0].Height;
            int headerHeight = hostControl.GetHeaderHeight();
            int itemHeight = hostControl.GetItemHeight();

            // 计算可视区域中文件项内容区的高度
            int contentAreaHeight = Math.Max(50, viewHeight - headerHeight);
            double pixelsPerItem = itemHeight > 0 ? (double)itemHeight : (pageSize > 0 ? (double)contentAreaHeight / pageSize : 22.0);

            int totalHeight = viewHeight;
            List<int> sliceHeights = new List<int>();
            for (int i = 1; i < frames.Count; i++)
            {
                int deltaItems = Math.Max(1, positions[i] - positions[i - 1]);
                int sliceH = (int)Math.Round(deltaItems * pixelsPerItem);
                sliceH = Math.Min(sliceH, contentAreaHeight);
                sliceHeights.Add(sliceH);
                totalHeight += sliceH;
            }

            Bitmap stitched = new Bitmap(width, totalHeight, PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(stitched))
            {
                g.Clear(Color.White);

                // 画第一帧（包含完整列头和顶部数据）
                g.DrawImage(frames[0], 0, 0);
                int currentY = viewHeight;

                for (int i = 1; i < frames.Count; i++)
                {
                    Bitmap frame = frames[i];
                    int sliceH = sliceHeights[i - 1];

                    // 每一页向下滚动后，最新露出的条目位于视口底部！
                    // 从当前帧视口底部截取新增区域：[viewHeight - sliceH, sliceH]
                    Rectangle srcRect = new Rectangle(0, Math.Max(0, viewHeight - sliceH), width, sliceH);
                    Rectangle destRect = new Rectangle(0, currentY, width, sliceH);

                    g.DrawImage(frame, destRect, srcRect, GraphicsUnit.Pixel);
                    currentY += sliceH;

                    frame.Dispose();
                }
            }

            frames[0].Dispose();
            return stitched;
        }

        public static Bitmap AttachMetadataHeader(Bitmap srcBmp, string folderPath, string rootPath, int itemCount = -1)
        {
            if (srcBmp == null) return null;

            int headerHeight = 36;
            int totalWidth = srcBmp.Width;
            int totalHeight = srcBmp.Height + headerHeight;

            Bitmap result = new Bitmap(totalWidth, totalHeight, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                // 1. 绘制深色页眉背景
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(24, 24, 27)))
                {
                    g.FillRectangle(bgBrush, 0, 0, totalWidth, headerHeight);
                }

                // 底部 1px 细微分割线
                using (Pen borderPen = new Pen(Color.FromArgb(63, 63, 70), 1))
                {
                    g.DrawLine(borderPen, 0, headerHeight - 1, totalWidth, headerHeight - 1);
                }

                // 2. 计算展示路径 (纯文本，杜绝 Emoji 在 GDI+ 字体回退时退化成方框)
                string displayPath;
                if (string.Equals(folderPath.TrimEnd('\\', '/'), rootPath.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                {
                    displayPath = "[根目录] " + System.IO.Path.GetFileName(rootPath);
                }
                else if (folderPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                {
                    string rel = folderPath.Substring(rootPath.Length).TrimStart('\\', '/');
                    displayPath = "/" + rel.Replace('\\', '/');
                }
                else
                {
                    displayPath = folderPath;
                }

                // 绘制左侧精致矢量文件夹图标 (尺寸约 16x12，金黄琥珀色)
                int iconX = 14;
                int iconY = 12;
                Point[] backPts = new Point[] {
                    new Point(iconX, iconY + 1),
                    new Point(iconX + 6, iconY + 1),
                    new Point(iconX + 8, iconY + 3),
                    new Point(iconX + 16, iconY + 3),
                    new Point(iconX + 16, iconY + 13),
                    new Point(iconX, iconY + 13)
                };
                using (SolidBrush backBrush = new SolidBrush(Color.FromArgb(217, 119, 6))) // 琥珀金
                {
                    g.FillPolygon(backBrush, backPts);
                }
                Point[] frontPts = new Point[] {
                    new Point(iconX, iconY + 4),
                    new Point(iconX + 16, iconY + 4),
                    new Point(iconX + 16, iconY + 13),
                    new Point(iconX, iconY + 13)
                };
                using (SolidBrush frontBrush = new SolidBrush(Color.FromArgb(251, 191, 36))) // 亮明黄
                {
                    g.FillPolygon(frontBrush, frontPts);
                }
                using (Pen folderPen = new Pen(Color.FromArgb(180, 83, 9), 1))
                {
                    g.DrawPolygon(folderPen, frontPts);
                }

                // 绘制左侧路径文案 (位于小文件夹图标右侧)
                using (Font pathFont = new Font("Segoe UI", 9.5F, FontStyle.Bold))
                using (SolidBrush pathBrush = new SolidBrush(Color.FromArgb(244, 244, 245)))
                {
                    g.DrawString(displayPath, pathFont, pathBrush, iconX + 22, 8);
                }

                // 绘制右侧条目统计与时间戳
                string countText = itemCount >= 0 ? string.Format("共 {0} 项  •  ", itemCount) : "";
                string timeText = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                string metaRight = countText + timeText;

                using (Font metaFont = new Font("Segoe UI", 8.5F, FontStyle.Regular))
                using (SolidBrush metaBrush = new SolidBrush(Color.FromArgb(161, 161, 170)))
                {
                    SizeF size = g.MeasureString(metaRight, metaFont);
                    float x = totalWidth - size.Width - 14;
                    g.DrawString(metaRight, metaFont, metaBrush, Math.Max(10, x), 10);
                }

                // 3. 将原截图排版在页眉下方
                g.DrawImage(srcBmp, 0, headerHeight);
            }

            srcBmp.Dispose();
            return result;
        }
    }
}

