using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsFormsApp3
{
    public class TransparentPictureBox : PictureBox
    {
        public TransparentPictureBox()
        {
            // 设置控件样式支持透明背景
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // 禁用背景绘制
            // base.OnPaintBackground(pevent);
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            // 绘制父容器的背景
            if (Parent != null)
            {
                using (var bmp = new Bitmap(Parent.ClientSize.Width, Parent.ClientSize.Height))
                {
                    Parent.DrawToBitmap(bmp, Parent.ClientRectangle);
                    pe.Graphics.DrawImage(bmp, -Left, -Top);
                }
            }

            // 绘制图片
            base.OnPaint(pe);
        }
    }
}