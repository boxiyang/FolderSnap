using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace FolderSnapDesktop.UI
{
    public class AboutDialog : Form
    {
        public AboutDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "关于 FolderSnap Desktop";
            this.Size = new Size(520, 520);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.White;
            this.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(134)));

            // 窗口图标加载
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app_icon.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
            }
            catch { }

            // 主垂直容器
            Panel contentPanel = new Panel();
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.Padding = new Padding(24, 16, 24, 16);

            // 1. 顶部图标
            PictureBox picIcon = new PictureBox();
            picIcon.Size = new Size(64, 64);
            picIcon.SizeMode = PictureBoxSizeMode.Zoom;
            picIcon.Location = new Point((this.ClientSize.Width - 64) / 2, 16);

            try
            {
                string[] possiblePaths = new string[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app_icon.png"),
                    Path.Combine(Application.StartupPath, "Resources", "app_icon.png"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "desktop", "Resources", "app_icon.png"),
                    Path.Combine(Environment.CurrentDirectory, "desktop", "Resources", "app_icon.png")
                };
                foreach (string p in possiblePaths)
                {
                    if (File.Exists(p))
                    {
                        picIcon.Image = Image.FromFile(p);
                        break;
                    }
                }
            }
            catch { }

            // 2. 产品标题 (给予充裕的 36px 高度与充足的字距，彻底避免高 DPI 下文字下部被裁切)
            Label lblTitle = new Label();
            lblTitle.Text = "FolderSnap Desktop";
            lblTitle.Font = new Font("Microsoft YaHei UI", 13.5F, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(24, 24, 27);
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            lblTitle.Location = new Point(0, 88);
            lblTitle.Size = new Size(this.ClientSize.Width, 36);

            // 3. 版本号标签
            Label lblVersion = new Label();
            lblVersion.Text = "v1.3.0  •  发布日期: 2026-09-16";
            lblVersion.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular);
            lblVersion.ForeColor = Color.FromArgb(71, 85, 105);
            lblVersion.TextAlign = ContentAlignment.MiddleCenter;
            lblVersion.Location = new Point(0, 126);
            lblVersion.Size = new Size(this.ClientSize.Width, 22);

            // 4. 作者与版权
            Label lblAuthor = new Label();
            lblAuthor.Text = "作者: boxiyang  •  Copyright © 2026 boxiyang. MIT License.";
            lblAuthor.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular);
            lblAuthor.ForeColor = Color.FromArgb(113, 113, 122);
            lblAuthor.TextAlign = ContentAlignment.MiddleCenter;
            lblAuthor.Location = new Point(0, 148);
            lblAuthor.Size = new Size(this.ClientSize.Width, 20);

            // 4.1 GitHub 链接
            LinkLabel lnkGithub = new LinkLabel();
            lnkGithub.Text = "GitHub: https://github.com/boxiyang/FolderSnap";
            lnkGithub.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular);
            lnkGithub.LinkColor = Color.FromArgb(37, 99, 235);
            lnkGithub.ActiveLinkColor = Color.FromArgb(29, 78, 216);
            lnkGithub.VisitedLinkColor = Color.FromArgb(37, 99, 235);
            lnkGithub.TextAlign = ContentAlignment.MiddleCenter;
            lnkGithub.Location = new Point(0, 168);
            lnkGithub.Size = new Size(this.ClientSize.Width, 20);
            lnkGithub.Cursor = Cursors.Hand;
            lnkGithub.LinkClicked += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start("https://github.com/boxiyang/FolderSnap");
                }
                catch { }
            };

            // 5. 功能介绍特性卡片 (Feature Card)
            Panel featureCard = new Panel();
            featureCard.Location = new Point(24, 196);
            featureCard.Size = new Size(this.ClientSize.Width - 48, 198);
            featureCard.BackColor = Color.FromArgb(248, 250, 252);
            featureCard.Padding = new Padding(18, 12, 18, 12);
            featureCard.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(226, 232, 240), 1))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    Rectangle rect = new Rectangle(0, 0, featureCard.Width - 1, featureCard.Height - 1);
                    e.Graphics.DrawRectangle(pen, rect);
                }
            };

            TableLayoutPanel tableFeatures = new TableLayoutPanel();
            tableFeatures.Dock = DockStyle.Fill;
            tableFeatures.RowCount = 4;
            tableFeatures.ColumnCount = 1;
            tableFeatures.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            tableFeatures.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            tableFeatures.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            tableFeatures.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));

            string[] features = new string[]
            {
                "✦ Windows Shell 深度集成：真实系统图标与排版 1:1 精准还原",
                "✦ 智能长列表滚动拼图：自动感知垂直滚动条，像素级平滑拼接",
                "✦ 全自动递归批量截图：支持自定义扫描深度，一键全自动生成",
                "✦ 分辨率与列宽自适应：按需扩展视口，彻底杜绝文字与尺寸截断"
            };

            for (int i = 0; i < features.Length; i++)
            {
                Label item = new Label();
                item.Text = features[i];
                item.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
                item.ForeColor = Color.FromArgb(51, 65, 85);
                item.Dock = DockStyle.Fill;
                item.TextAlign = ContentAlignment.MiddleLeft;
                item.AutoEllipsis = true;
                tableFeatures.Controls.Add(item, 0, i);
            }
            featureCard.Controls.Add(tableFeatures);

            // 6. 底部关闭按钮
            Button btnClose = new Button();
            btnClose.Text = "确定";
            btnClose.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            btnClose.Size = new Size(110, 36);
            btnClose.Location = new Point((this.ClientSize.Width - 110) / 2, 412);
            btnClose.BackColor = Color.FromArgb(37, 99, 235);
            btnClose.ForeColor = Color.White;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += (s, e) => this.Close();

            // 快捷键支持
            this.AcceptButton = btnClose;
            this.CancelButton = btnClose;

            contentPanel.Controls.Add(picIcon);
            contentPanel.Controls.Add(lblTitle);
            contentPanel.Controls.Add(lblVersion);
            contentPanel.Controls.Add(lblAuthor);
            contentPanel.Controls.Add(lnkGithub);
            contentPanel.Controls.Add(featureCard);
            contentPanel.Controls.Add(btnClose);

            this.Controls.Add(contentPanel);
        }
    }
}
