using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowsFormsApp3
{
    public partial class Form1 : Form
    {
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int DragThresholdSq = 100;

        private const uint WmKeydown = 0x0100;
        private const uint WmKeyup = 0x0101;

        private bool _leftDown;
        private Point _downScreen;
        private Point _downLocation;
        private bool _dragging;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("user32.dll")]
        private static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint KeyeventfKeyup = 0x0002;
        private const byte VkReturn = 0x0D;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        public Form1()
        {
            TopMost = true;

            InitializeComponent();
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TransparencyKey = Color.Empty;

            var menu = new ContextMenuStrip();
            menu.RenderMode = ToolStripRenderMode.System;
            var exitItem = new ToolStripMenuItem("关闭程序");
            exitItem.Click += (_, __) => Close();
            menu.Items.Add(exitItem);
            pictureBox1.ContextMenuStrip = menu;

            pictureBox1.MouseDown += PictureBox1_MouseDown;
            pictureBox1.MouseMove += PictureBox1_MouseMove;
            pictureBox1.MouseUp += PictureBox1_MouseUp;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW;
                cp.ExStyle |= WS_EX_NOACTIVATE;
                return cp;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            BackgroundImage = Image.FromFile("background.jpg");
            BackgroundImageLayout = ImageLayout.Stretch;

            var transparentPictureBox = new TransparentPictureBox
            {
                Image = Image.FromFile("example.png"),
                Size = new Size(100, 100),
                Location = new Point(50, 50)
            };
            Controls.Add(transparentPictureBox);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.Fuchsia;
            TransparencyKey = Color.Fuchsia;
            AllowTransparency = true;
            TopMost = true;
            LoadEnterKeyImage();
        }

        /// <summary>从程序目录加载 enter_key_modern.png；可替换该文件自定义外观。</summary>
        private void LoadEnterKeyImage()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "enter_key_modern.png");
                if (!File.Exists(path))
                    return;
                var old = pictureBox1.Image;
                pictureBox1.Image = Image.FromFile(path);
                old?.Dispose();
            }
            catch
            {
                // 保留设计器默认（无图）即可
            }
        }

        private bool IsOurWindowTree(IntPtr h)
        {
            if (h == IntPtr.Zero)
                return false;
            return h == Handle || IsChild(Handle, h);
        }

        /// <summary>前台线程里真正拥有键盘焦点的控件；比仅取前台窗体更适合记事本、浏览器等。</summary>
        private static IntPtr GetKeyboardMessageTarget()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero)
                return IntPtr.Zero;
            uint pid;
            uint tid = GetWindowThreadProcessId(fg, out pid);
            var gi = new GUITHREADINFO();
            gi.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));
            if (GetGUIThreadInfo(tid, ref gi) && gi.hwndFocus != IntPtr.Zero)
                return gi.hwndFocus;
            return fg;
        }

        private void SendEnterToOtherApp()
        {
            IntPtr target = GetKeyboardMessageTarget();
            if (target != IntPtr.Zero && !IsOurWindowTree(target))
            {
                // lParam 高 32 位含 0xC0000001 等标志时，按 int 运算会溢出；必须用 uint 再转为 IntPtr。
                const int scanEnter = 0x1C;
                IntPtr lParamDown = new IntPtr(1 | (scanEnter << 16));
                uint lpUp = 0xC0000001u | ((uint)scanEnter << 16);
                IntPtr lParamUp = new IntPtr(unchecked((int)lpUp));
                PostMessage(target, WmKeydown, (IntPtr)VkReturn, lParamDown);
                PostMessage(target, WmKeyup, (IntPtr)VkReturn, lParamUp);
                return;
            }

            keybd_event(VkReturn, 0, 0, UIntPtr.Zero);
            keybd_event(VkReturn, 0, KeyeventfKeyup, UIntPtr.Zero);
        }

        private void PictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            _leftDown = true;
            _dragging = false;
            _downScreen = MousePosition;
            _downLocation = Location;
            pictureBox1.VisualPressed = true;
            pictureBox1.Capture = true;
        }

        private void PictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_leftDown)
                return;
            int dx = MousePosition.X - _downScreen.X;
            int dy = MousePosition.Y - _downScreen.Y;
            if (!_dragging && (dx * dx + dy * dy) >= DragThresholdSq)
            {
                _dragging = true;
                pictureBox1.VisualPressed = false;
            }
            if (_dragging)
                Location = new Point(_downLocation.X + dx, _downLocation.Y + dy);
        }

        private void PictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            pictureBox1.Capture = false;
            pictureBox1.VisualPressed = false;
            bool wasDrag = _dragging;
            _leftDown = false;
            _dragging = false;
            if (!wasDrag)
                SendEnterToOtherApp();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
        }
    }
}
