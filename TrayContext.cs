using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TaskbarMediaWidget
{
    public class TrayContext : ApplicationContext
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private readonly Config _config;
        private readonly MediaEngine _engine;
        private readonly WidgetForm _form;
        private readonly NotifyIcon _trayIcon;
        private Icon _currentIcon;

        public TrayContext(Config config, MediaEngine engine)
        {
            Program.Log("TrayContext ctor start");
            _config = config;
            _engine = engine;

            _form = new WidgetForm(_engine, _config);
            Program.Log("WidgetForm created");
            _form.Show();
            Program.Log("WidgetForm.Show called");

            _trayIcon = new NotifyIcon();
            _currentIcon = GenerateTrayIcon(_engine.CurrentInfo.IsPlaying);
            _trayIcon.Icon = _currentIcon;
            _trayIcon.Text = "Медиаплеер на панели задач";
            _trayIcon.Visible = true;
            Program.Log("TrayIcon visible");

            _trayIcon.ContextMenuStrip = _form.BuildContextMenu();
            _trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    _trayIcon.ContextMenuStrip = _form.BuildContextMenu();
                }
            };
            _trayIcon.DoubleClick += (s, e) => _engine.ActivatePlayerWindow();

            _engine.OnMediaChanged += () =>
            {
                if (_engine.CurrentInfo.HasTrack)
                {
                    string txt = _engine.CurrentInfo.Artist + " - " + _engine.CurrentInfo.Title;
                    if (txt.Length > 63) txt = txt.Substring(0, 60) + "...";
                    _trayIcon.Text = txt;
                }
                else
                {
                    _trayIcon.Text = "Медиаплеер на панели задач";
                }

                UpdateTrayIcon(_engine.CurrentInfo.IsPlaying);
            };

            Program.Log("TrayContext ctor completed");
        }

        private void UpdateTrayIcon(bool isPlaying)
        {
            try
            {
                Icon old = _currentIcon;
                _currentIcon = GenerateTrayIcon(isPlaying);
                _trayIcon.Icon = _currentIcon;
                if (old != null) old.Dispose();
            }
            catch { }
        }

        private static Icon GenerateTrayIcon(bool isPlaying)
        {
            using (Bitmap bmp = new Bitmap(32, 32, PixelFormat.Format32bppPArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    // 1. Dark frosted glass squircle background
                    using (GraphicsPath baseP = RoundedRect(1f, 1f, 30f, 30f, 6.5f))
                    {
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 18, 20, 26))) g.FillPath(b, baseP);
                        using (Pen p = new Pen(Color.FromArgb(50, 255, 255, 255), 1f)) g.DrawPath(p, baseP);
                    }

                    // 2. Headband arc (Electric cyan to vivid blue gradient)
                    using (LinearGradientBrush lgb = new LinearGradientBrush(new PointF(7, 6), new PointF(25, 22),
                        Color.FromArgb(255, 60, 225, 255), Color.FromArgb(255, 90, 140, 255)))
                    using (Pen bandPen = new Pen(lgb, 2.4f))
                    {
                        bandPen.StartCap = LineCap.Round;
                        bandPen.EndCap = LineCap.Round;
                        g.DrawArc(bandPen, 7f, 6f, 18f, 16f, 180, 180);
                    }

                    // 3. Top headband cushion
                    using (GraphicsPath cushion = RoundedRect(11.5f, 4.5f, 9f, 2.5f, 1.2f))
                    {
                        using (SolidBrush cb = new SolidBrush(Color.FromArgb(255, 35, 40, 52))) g.FillPath(cb, cushion);
                        using (Pen cp = new Pen(Color.FromArgb(120, 80, 215, 255), 0.8f)) g.DrawPath(cp, cushion);
                    }

                    // 4. Ear cups
                    using (SolidBrush cupBrush = new SolidBrush(Color.FromArgb(255, 255, 255, 255)))
                    {
                        using (GraphicsPath leftCup = RoundedRect(5.5f, 13.5f, 4.5f, 10.5f, 2.2f)) g.FillPath(cupBrush, leftCup);
                        using (GraphicsPath rightCup = RoundedRect(22f, 13.5f, 4.5f, 10.5f, 2.2f)) g.FillPath(cupBrush, rightCup);
                    }

                    // 4b. Inner cushion accent line
                    using (Pen cupLine = new Pen(Color.FromArgb(200, 40, 45, 58), 1f))
                    {
                        g.DrawLine(cupLine, 7.7f, 16f, 7.7f, 21.5f);
                        g.DrawLine(cupLine, 24.3f, 16f, 24.3f, 21.5f);
                    }

                    // 5. Sound waves between ear cups when playing
                    if (isPlaying)
                    {
                        using (LinearGradientBrush dotBrush = new LinearGradientBrush(new PointF(13, 17), new PointF(19, 23),
                            Color.FromArgb(255, 60, 230, 255), Color.FromArgb(255, 120, 100, 255)))
                        {
                            using (GraphicsPath b1 = RoundedRect(12.5f, 18f, 1.8f, 4f, 0.9f)) g.FillPath(dotBrush, b1);
                            using (GraphicsPath b2 = RoundedRect(15.1f, 16f, 1.8f, 8f, 0.9f)) g.FillPath(dotBrush, b2);
                            using (GraphicsPath b3 = RoundedRect(17.7f, 18f, 1.8f, 4f, 0.9f)) g.FillPath(dotBrush, b3);
                        }
                    }
                    else
                    {
                        // Subtle resting play glyph when paused
                        using (SolidBrush playBrush = new SolidBrush(Color.FromArgb(180, 160, 170, 185)))
                        {
                            PointF[] playTriangle = new PointF[]
                            {
                                new PointF(14.5f, 17f),
                                new PointF(18.5f, 19.5f),
                                new PointF(14.5f, 22f)
                            };
                            g.FillPolygon(playBrush, playTriangle);
                        }
                    }
                }

                IntPtr hIcon = bmp.GetHicon();
                Icon icon = (Icon)Icon.FromHandle(hIcon).Clone();
                DestroyIcon(hIcon);
                return icon;
            }
        }

        private static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
        {
            GraphicsPath path = new GraphicsPath();
            float d = r * 2f;
            if (d > w) d = w;
            if (d > h) d = h;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            Program.Log("TrayContext.Dispose called, disposing=" + disposing);
            if (disposing)
            {
                if (_currentIcon != null)
                {
                    _currentIcon.Dispose();
                }
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
                if (_form != null)
                {
                    _form.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
