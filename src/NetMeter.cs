using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NetMeter
{
    // ---------- 主题 ----------
    static class T
    {
        public static readonly Color Bg = Color.FromArgb(13, 16, 22);
        public static readonly Color Card = Color.FromArgb(21, 25, 33);
        public static readonly Color CardHi = Color.FromArgb(28, 33, 43);
        public static readonly Color Line = Color.FromArgb(38, 45, 58);
        public static readonly Color Text = Color.FromArgb(232, 236, 244);
        public static readonly Color Muted = Color.FromArgb(132, 142, 162);
        public static readonly Color Accent = Color.FromArgb(86, 146, 255);
        public static readonly Color Down = Color.FromArgb(64, 220, 155);
        public static readonly Color Up = Color.FromArgb(255, 178, 46);
        public static readonly Color Warn = Color.FromArgb(255, 108, 108);

        public static readonly Font FTitle = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
        public static readonly Font FBig = new Font("Consolas", 20f, FontStyle.Bold);
        public static readonly Font FMid = new Font("Microsoft YaHei UI", 10f);
        public static readonly Font FSmall = new Font("Microsoft YaHei UI", 8.5f);
        public static readonly Font FTiny = new Font("Microsoft YaHei UI", 7.5f);
        public static readonly Font FNum = new Font("Consolas", 10f, FontStyle.Bold);

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // ---------- 绘制辅助 ----------
    static class D
    {
        const TextFormatFlags FL = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine;
        const TextFormatFlags FR = TextFormatFlags.Right | TextFormatFlags.Top | TextFormatFlags.SingleLine;
        const TextFormatFlags FC = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;

        public static void Txt(Graphics g, string s, Font f, Rectangle r, Color c) { TextRenderer.DrawText(g, s, f, r, c, FL); }
        public static void Right(Graphics g, string s, Font f, Rectangle r, Color c) { TextRenderer.DrawText(g, s, f, r, c, FR); }
        public static void Center(Graphics g, string s, Font f, Rectangle r, Color c) { TextRenderer.DrawText(g, s, f, r, c, FC); }

        // 用 GDI+ 画单行文字：不裁剪、不加边距，居中时按字体实际尺寸算
        public static void Mid(Graphics g, string s, Font f, Rectangle r, Color c)
        {
            using (var sf = new StringFormat())
            using (var br = new SolidBrush(c))
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                var size = g.MeasureString(s, f);
                var box = new RectangleF(
                    r.X + (r.Width - size.Width) / 2f,
                    r.Y + (r.Height - size.Height) / 2f,
                    size.Width,
                    size.Height);
                g.DrawString(s, f, br, box, sf);
            }
        }
    }

    // ---------- 卡片 ----------
    class Card : Panel
    {
        public int Radius = 14;
        public Color Fill = T.Card;
        public Color BorderColor = T.Line;
        public Card() { DoubleBuffered = true; BackColor = T.Bg; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = T.Round(r, Radius))
            {
                using (var b = new SolidBrush(Fill)) g.FillPath(b, path);
                using (var pen = new Pen(BorderColor)) g.DrawPath(pen, path);
            }
            PaintContent(g);
            base.OnPaint(e);
        }

        protected virtual void PaintContent(Graphics g) { }
    }

    // ---------- 按钮 ----------
    // 直接从 Control 派生：Button 会先画一层系统背景，圆角外面就会露出黑块
    class Pill : Control
    {
        public Color Fill = T.CardHi;
        public Color TextColor = T.Text;
        public Color HoverFill = Color.FromArgb(40, 47, 60);
        public Color Backdrop = T.Bg;
        public bool Accent;
        bool hover, down;

        public Pill()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.StandardClick, true);
            TabStop = false;
            Font = T.FSmall;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Backdrop);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);

            Color fill = Accent
                ? (hover ? Color.FromArgb(108, 158, 255) : (down ? Color.FromArgb(72, 128, 235) : T.Accent))
                : (hover ? HoverFill : (down ? Color.FromArgb(52, 60, 76) : Fill));

            using (var path = T.Round(r, Height / 2))
            using (var b = new SolidBrush(fill))
            {
                g.FillPath(b, path);
                if (!Accent) using (var pen = new Pen(T.Line)) g.DrawPath(pen, path);
            }
            D.Mid(g, Text, Font, r, Accent ? Color.White : TextColor);
        }
    }

    // ---------- 迷你曲线 ----------
    class Spark : Card
    {
        public List<double> Down = new List<double>();
        public List<double> Up = new List<double>();
        public bool ShowBars;
        public long[] Hours;

        protected override void PaintContent(Graphics g)
        {
            var area = new Rectangle(12, 10, Width - 24, Height - 20);
            if (area.Width < 20 || area.Height < 20) return;
            double max = 1;
            foreach (var v in Down) if (v > max) max = v;
            foreach (var v in Up) if (v > max) max = v;
            if (ShowBars && Hours != null)
            {
                long hmax = 1;
                for (int i = 0; i < 24; i++) if (Hours[i] > hmax) hmax = Hours[i];
                int n = 24, gap = 4;
                int bw = Math.Max(4, (area.Width - gap * (n - 1)) / n);
                for (int i = 0; i < n; i++)
                {
                    int bh = (int)((double)Hours[i] / hmax * (area.Height - 16));
                    var r = new Rectangle(area.X + i * (bw + gap), area.Y + area.Height - bh, bw, bh);
                    using (var b = new SolidBrush(i == DateTime.Now.Hour ? T.Accent : Color.FromArgb(52, 64, 84)))
                        g.FillRectangle(b, r);
                }
                return;
            }
            DrawSeries(g, Down, max, area, T.Down, true);
            DrawSeries(g, Up, max, area, T.Up, false);
        }

        void DrawSeries(Graphics g, List<double> data, double max, Rectangle area, Color color, bool fill)
        {
            if (data.Count < 2) return;
            var pts = new PointF[data.Count];
            float step = (float)(area.Width - 2) / (data.Count - 1);
            for (int i = 0; i < data.Count; i++)
            {
                float y = area.Bottom - (float)(Math.Min(1, data[i] / max) * (area.Height - 4));
                pts[i] = new PointF(area.X + i * step, y);
            }
            if (fill)
            {
                var poly = new List<PointF>(pts);
                poly.Add(new PointF(area.Right, area.Bottom));
                poly.Add(new PointF(area.X, area.Bottom));
                using (var b = new LinearGradientBrush(new Rectangle(area.X, area.Y, area.Width, area.Height),
                    Color.FromArgb(70, color), Color.FromArgb(0, color), 90f))
                    g.FillPolygon(b, poly.ToArray());
            }
            using (var pen = new Pen(color, 1.6f)) g.DrawLines(pen, pts);
        }
    }

    // ---------- 按程序统计（需要管理员） ----------
    public class AppStat
    {
        public long Rx;
        public long Tx;
        public long Total { get { return Rx + Tx; } }
    }

    public static class PerApp
    {
        [StructLayout(LayoutKind.Sequential)]
        struct MIB_TCPROW
        {
            public uint state;
            public uint localAddr;
            public uint localPort;
            public uint remoteAddr;
            public uint remotePort;
            public uint owningPid;
        }

        class Conn { public long In, Out; public string Pid; public int Miss; }

        [DllImport("iphlpapi.dll")]
        static extern uint GetExtendedTcpTable(IntPtr p, ref int len, bool sort, int af, int cls, int res);

        [DllImport("iphlpapi.dll")]
        static extern uint GetPerTcpConnectionEStats(ref MIB_TCPROW row, int type,
            IntPtr rw, int rwVer, int rwSize, IntPtr ros, int rosVer, int rosSize,
            IntPtr rod, int rodVer, int rodSize);

        [DllImport("iphlpapi.dll")]
        static extern uint SetPerTcpConnectionEStats(ref MIB_TCPROW row, int type,
            IntPtr rw, int rwVer, int rwSize, IntPtr ros, int rosVer, int rosSize,
            IntPtr rod, int rodVer, int rodSize);

        static readonly Dictionary<string, Conn> conns = new Dictionary<string, Conn>();
        static readonly Dictionary<int, string> pidName = new Dictionary<int, string>();
        public static readonly Dictionary<string, AppStat> Procs = new Dictionary<string, AppStat>(StringComparer.OrdinalIgnoreCase);
        public static bool Available;
        public static string Error = "";
        public static string Status = "";
        static string file;
        static string logFile;
        static int logBudget = 6;
        const int RodSize = 96;   // sizeof(TCP_ESTATS_DATA_ROD_v0)
        static readonly IntPtr rod = Marshal.AllocHGlobal(160);

        [StructLayout(LayoutKind.Sequential)]
        struct LUID { public uint LowPart; public int HighPart; }
        [StructLayout(LayoutKind.Sequential)]
        struct LUID_AND_ATTRIBUTES { public LUID Luid; public uint Attributes; }
        [StructLayout(LayoutKind.Sequential)]
        struct TOKEN_PRIVILEGES { public uint PrivilegeCount; public LUID_AND_ATTRIBUTES Privilege; }

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool LookupPrivilegeValue(string system, string name, out LUID luid);
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES newState, int bufLen, IntPtr prev, IntPtr retLen);
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr h);

        static void EnableDebugPrivilege()
        {
            try
            {
                IntPtr token;
                if (!OpenProcessToken(System.Diagnostics.Process.GetCurrentProcess().Handle, 0x0020 | 0x0008, out token)) return;
                try
                {
                    LUID luid;
                    if (!LookupPrivilegeValue(null, "SeDebugPrivilege", out luid)) return;
                    var tp = new TOKEN_PRIVILEGES();
                    tp.PrivilegeCount = 1;
                    tp.Privilege.Luid = luid;
                    tp.Privilege.Attributes = 0x00000002;
                    AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
                }
                finally { CloseHandle(token); }
            }
            catch { }
        }

        static void Log(string text)
        {
            if (logBudget <= 0 || logFile == null) return;
            logBudget--;
            try { File.AppendAllText(logFile, DateTime.Now.ToString("HH:mm:ss") + " " + text + Environment.NewLine, Encoding.UTF8); }
            catch { }
        }

        public static void Init(string dir)
        {
            file = Path.Combine(dir, "apps.csv");
            logFile = Path.Combine(dir, "perapp.log");
            if (MainForm.IsElevated) { EnableDebugPrivilege(); Log("elevated=true"); }
            try
            {
                if (!File.Exists(file)) return;
                string today = DateTime.Today.ToString("yyyy-MM-dd");
                foreach (var line in File.ReadAllLines(file, Encoding.UTF8))
                {
                    var p = line.Split(',');
                    if (p.Length < 4 || p[0] != today) continue;
                    AppStat a;
                    if (!Procs.TryGetValue(p[1], out a)) { a = new AppStat(); Procs[p[1]] = a; }
                    long v;
                    if (long.TryParse(p[2], out v)) a.Rx = v;
                    if (long.TryParse(p[3], out v)) a.Tx = v;
                }
            }
            catch { }
        }

        public static void Save()
        {
            if (file == null) return;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("date,app,rx,tx");
                string today = DateTime.Today.ToString("yyyy-MM-dd");
                foreach (var kv in Procs)
                    sb.AppendLine(string.Format("{0},{1},{2},{3}", today, kv.Key.Replace(",", " "), kv.Value.Rx, kv.Value.Tx));
                File.WriteAllText(file + ".tmp", sb.ToString(), Encoding.UTF8);
                if (File.Exists(file)) File.Delete(file);
                File.Move(file + ".tmp", file);
            }
            catch { }
        }

        static string NameOf(int pid)
        {
            string n;
            if (pidName.TryGetValue(pid, out n)) return n;
            try { n = System.Diagnostics.Process.GetProcessById(pid).ProcessName; }
            catch { n = pid == 0 ? "System" : ("pid " + pid); }
            pidName[pid] = n;
            return n;
        }

        static void Add(string name, long rx, long tx)
        {
            AppStat a;
            if (!Procs.TryGetValue(name, out a)) { a = new AppStat(); Procs[name] = a; }
            a.Rx += rx; a.Tx += tx;
        }

        public static bool Sample()
        {
            if (!MainForm.IsElevated) return false;
            try
            {
                int len = 0;
                GetExtendedTcpTable(IntPtr.Zero, ref len, false, 2, 5, 0);
                if (len <= 0) { Error = "枚举连接失败"; return false; }
                IntPtr buf = IntPtr.Zero;
                uint rc = 0;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    buf = Marshal.AllocHGlobal(len);
                    rc = GetExtendedTcpTable(buf, ref len, false, 2, 5, 0);
                    if (rc != 122) break; // 122 = ERROR_INSUFFICIENT_BUFFER，表长大了，重来
                    Marshal.FreeHGlobal(buf);
                    buf = IntPtr.Zero;
                }
                if (buf == IntPtr.Zero || rc != 0)
                {
                    Error = "枚举连接失败 rc=" + rc;
                    Log("table rc=" + rc);
                    return false;
                }
                try
                {
                    int n = Marshal.ReadInt32(buf);
                    int size = 24;
                    int established = 0;
                    var seen = new HashSet<string>();
                    for (int i = 0; i < n; i++)
                    {
                        IntPtr p = new IntPtr(buf.ToInt64() + 4 + (long)i * size);
                        var row = (MIB_TCPROW)Marshal.PtrToStructure(p, typeof(MIB_TCPROW));
                        if (row.state != 5) continue;
                        established++;
                        string key = row.localAddr + ":" + row.localPort + "-" + row.remoteAddr + ":" + row.remotePort;
                        seen.Add(key);
                        Conn c;
                        if (!conns.TryGetValue(key, out c))
                        {
                            c = new Conn();
                            c.Pid = NameOf((int)row.owningPid);
                            conns[key] = c;
                            uint setRc = Enable(ref row);
                            uint getRc = Read(ref row, c, true);
                            if (established <= 3) Log("set=" + setRc + " get=" + getRc + " pid=" + row.owningPid);
                        }
                        else
                        {
                            Read(ref row, c, false);
                        }
                    }
                    var drop = new List<string>();
                    foreach (var kv in conns)
                    {
                        if (!seen.Contains(kv.Key)) { kv.Value.Miss++; if (kv.Value.Miss > 3) drop.Add(kv.Key); }
                        else kv.Value.Miss = 0;
                    }
                    foreach (var k in drop) conns.Remove(k);
                    Available = true;
                    Error = "";
                    Status = established + " 条已建立连接，" + Procs.Count + " 个程序";
                    return true;
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            catch (Exception ex) { Error = ex.Message; Log("ex " + ex.Message); return false; }
        }

        static uint Enable(ref MIB_TCPROW row)
        {
            IntPtr rw = Marshal.AllocHGlobal(4);
            try
            {
                Marshal.WriteByte(rw, 0, 1);
                return SetPerTcpConnectionEStats(ref row, 1, rw, 0, 1, IntPtr.Zero, 0, 0, IntPtr.Zero, 0, 0);
            }
            finally { Marshal.FreeHGlobal(rw); }
        }

        static uint Read(ref MIB_TCPROW row, Conn c, bool baseline)
        {
            for (int k = 0; k < RodSize; k++) Marshal.WriteByte(rod, k, 0);
            // RodSize 必须正好是 TCP_ESTATS_DATA_ROD_v0 的大小（96 字节），少一个字节就返回 1784
            uint rc = GetPerTcpConnectionEStats(ref row, 1, IntPtr.Zero, 0, 0, IntPtr.Zero, 0, 0, rod, 0, RodSize);
            if (rc != 0) return rc;
            long outB = Marshal.ReadInt64(rod, 0);
            long inB = Marshal.ReadInt64(rod, 16);
            if (baseline) { c.Out = outB; c.In = inB; return 0; }
            long dIn = inB - c.In;
            long dOut = outB - c.Out;
            if (dIn < 0) dIn = 0;
            if (dOut < 0) dOut = 0;
            c.In = inB;
            c.Out = outB;
            if (dIn > 0 || dOut > 0) Add(c.Pid, dIn, dOut);
            return 0;
        }
    }

    // ---------- 自定义表格（避免系统滚动条的白底） ----------
    class Table : Panel
    {
        public readonly List<string[]> Rows = new List<string[]>();
        public string[] Headers = new string[] { "日期", "合计", "代理开", "直连" };
        public int[] ColX = new int[] { 16, 176, 336, 486 };
        int offset;
        const int HeadH = 30, RowH = 30;

        public Table()
        {
            DoubleBuffered = true;
            BackColor = T.Card;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(T.Card);
            int w = Width;
            using (var b = new SolidBrush(T.CardHi)) g.FillRectangle(b, 0, 0, w, HeadH);
            string[] head = Headers;
            int[] colX = ColX;
            for (int i = 0; i < head.Length; i++)
                D.Txt(g, head[i], T.FTiny, new Rectangle(colX[i], 0, 150, HeadH), T.Muted);
            using (var pen = new Pen(T.Line)) g.DrawLine(pen, 0, HeadH, w, HeadH);

            int visible = Math.Max(1, (Height - HeadH) / RowH);
            int maxOff = Math.Max(0, Rows.Count - visible);
            if (offset > maxOff) offset = maxOff;
            if (offset < 0) offset = 0;

            for (int r = 0; r < visible && offset + r < Rows.Count; r++)
            {
                var row = Rows[offset + r];
                int y = HeadH + r * RowH;
                if (r % 2 == 1)
                    using (var b = new SolidBrush(Color.FromArgb(25, 30, 39))) g.FillRectangle(b, 0, y, w, RowH);
                for (int i = 0; i < row.Length && i < colX.Length; i++)
                    D.Txt(g, row[i], T.FSmall, new Rectangle(colX[i], y + 7, 150, 18), i == 0 ? T.Muted : T.Text);
            }

            // 细滚动条
            if (Rows.Count > visible)
            {
                int trackH = Height - HeadH - 8;
                int thumbH = Math.Max(24, trackH * visible / Rows.Count);
                int thumbY = HeadH + 4 + (int)((trackH - thumbH) * (double)offset / maxOff);
                using (var b = new SolidBrush(Color.FromArgb(44, 52, 66))) g.FillRectangle(b, w - 5, HeadH + 4, 3, trackH);
                using (var b = new SolidBrush(T.Accent)) g.FillRectangle(b, w - 5, thumbY, 3, thumbH);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int visible = Math.Max(1, (Height - HeadH) / RowH);
            int maxOff = Math.Max(0, Rows.Count - visible);
            offset -= (e.Delta / 120) * 3;
            if (offset < 0) offset = 0;
            if (offset > maxOff) offset = maxOff;
            Invalidate();
        }
    }

    // ---------- 数据 ----------
    public class DayRec
    {
        public string Date;
        public long Rx, Tx, PRx, PTx;
        public long Total { get { return Rx + Tx; } }
        public long Proxy { get { return PRx + PTx; } }
        public long Direct { get { return Total - Proxy; } }
    }

    public static class Store
    {
        public static readonly Dictionary<string, DayRec> Days = new Dictionary<string, DayRec>();
        static string file;
        public static void Init(string dir)
        {
            file = Path.Combine(dir, "history.csv");
            try
            {
                if (!File.Exists(file)) return;
                foreach (var line in File.ReadAllLines(file, Encoding.UTF8))
                {
                    var p = line.Split(',');
                    if (p.Length < 5 || p[0].Length < 8) continue;
                    var r = new DayRec();
                    r.Date = p[0].Trim();
                    long.TryParse(p[1], out r.Rx);
                    long.TryParse(p[2], out r.Tx);
                    long.TryParse(p[3], out r.PRx);
                    long.TryParse(p[4], out r.PTx);
                    Days[r.Date] = r;
                }
            }
            catch { }
        }
        public static DayRec Today()
        {
            string d = DateTime.Today.ToString("yyyy-MM-dd");
            DayRec r;
            if (!Days.TryGetValue(d, out r)) { r = new DayRec(); r.Date = d; Days[d] = r; }
            return r;
        }
        public static void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("date,rx,tx,proxy_rx,proxy_tx");
                var keys = new List<string>(Days.Keys);
                keys.Sort();
                foreach (var k in keys)
                {
                    var r = Days[k];
                    sb.AppendLine(string.Format("{0},{1},{2},{3},{4}", r.Date, r.Rx, r.Tx, r.PRx, r.PTx));
                }
                string tmp = file + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
                if (File.Exists(file)) File.Delete(file);
                File.Move(tmp, file);
            }
            catch { }
        }
        public static void Sum(string prefix, out long total, out long proxy)
        {
            total = 0; proxy = 0;
            foreach (var r in Days.Values)
                if (r.Date.StartsWith(prefix)) { total += r.Total; proxy += r.Proxy; }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // 离屏截图模式：--shot 输出文件 [main|stats]，用于自检界面，不参与单实例
            bool shot = args != null && args.Length > 0 && args[0] == "--shot";
            if (!shot)
            {
                var mutex0 = new Mutex(false, "Local\\NetMeter_SingleInstance_v1");
                bool own0 = false;
                for (int i = 0; i < 60 && !own0; i++)
                {
                    try { own0 = mutex0.WaitOne(i == 0 ? 0 : 200); }
                    catch (AbandonedMutexException) { own0 = true; }
                }
                if (!own0) return;
                RunApp(mutex0);
                return;
            }

            try
            {
                MainForm.IsElevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string outFile = args.Length > 1 ? args[1] : "shot.png";
            string which = args.Length > 2 ? args[2] : "main";
            if (which == "stats")
            {
                var mf = new MainForm();
                mf.CreateControl();
                var sf = new StatsForm(mf);
                sf.Show();
                sf.Refresh();
                Application.DoEvents();
                System.Threading.Thread.Sleep(300);
                Application.DoEvents();
                using (var bmp = new Bitmap(sf.Width, sf.Height))
                {
                    sf.DrawToBitmap(bmp, new Rectangle(0, 0, sf.Width, sf.Height));
                    bmp.Save(outFile, System.Drawing.Imaging.ImageFormat.Png);
                }
                sf.Close();
                mf.Dispose();
            }
            else
            {
                var mf = new MainForm();
                mf.Show();
                mf.Refresh();
                Application.DoEvents();
                System.Threading.Thread.Sleep(300);
                Application.DoEvents();
                using (var bmp = new Bitmap(mf.Width, mf.Height))
                {
                    mf.DrawToBitmap(bmp, new Rectangle(0, 0, mf.Width, mf.Height));
                    bmp.Save(outFile, System.Drawing.Imaging.ImageFormat.Png);
                }
                mf.Dispose();
            }
            Application.Exit();
        }

        static void RunApp(Mutex mutex)
        {
            try
            {
                MainForm.IsElevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { }
            using (mutex)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try { Application.Run(new MainForm()); }
                catch (Exception ex)
                {
                    try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "NetMeter-crash.log"), DateTime.Now + " " + ex + Environment.NewLine); } catch { }
                    MessageBox.Show("NetMeter 启动失败：" + ex.Message);
                }
            }
        }
    }

    // ---------- 主窗口 ----------
    public class MainForm : Form
    {
        const string AppName = "NetMeter";
        const int WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 2;

        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

        readonly string dataDir, stateFile, eventFile;
        System.Windows.Forms.Timer timer;
        NotifyIcon tray;
        ContextMenuStrip menu;
        ToolStripMenuItem miAuto;

        Card head, speedCard, todayCard, footCard;
        Spark spark;
        Pill btnStats, btnMin, btnQuit;
        StatsForm stats;

        long lastRx, lastTx;
        DateTime lastTick;
        long todayRx, todayTx, todayProxyRx, todayProxyTx;
        long[] hourRx = new long[24];
        string todayKey;
        bool proxyOn;
        string proxyDetail = "直连";
        string nodeName = "", nodeRate = "1";
        int tick;
        bool firstSample = true;
        double curDown, curUp;

        readonly List<double> downQ = new List<double>();
        readonly List<double> upQ = new List<double>();

        public static bool IsElevated;

        public long TodayTotal { get { return todayRx + todayTx; } }
        public long TodayProxy { get { return todayProxyRx + todayProxyTx; } }
        public long[] Hours { get { return hourRx; } }
        public bool ProxyOnNow { get { return proxyOn; } }
        public string ProxyDetail { get { return proxyDetail; } }
        public string NodeName { get { return nodeName; } }
        public string NodeRate { get { return nodeRate; } }
        public List<double> DownQ { get { return downQ; } }
        public List<double> UpQ { get { return upQ; } }

        public MainForm()
        {
            dataDir = ResolveDataDir();
            try { Directory.CreateDirectory(dataDir); } catch { }
            stateFile = Path.Combine(dataDir, "state.ini");
            eventFile = Path.Combine(dataDir, "events.log");
            todayKey = DateTime.Today.ToString("yyyy-MM-dd");
            Store.Init(dataDir);
            PerApp.Init(dataDir);
            LoadState();
            BuildUi();
            UpdateNode();

            var cur = SampleTraffic();
            lastRx = cur[0]; lastTx = cur[1];
            lastTick = DateTime.Now;
            proxyOn = IsProxyOn(out proxyDetail);

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 1000;
            timer.Tick += OnTick;
            timer.Start();
            if (IsElevated) TryElevateTasks();
            OnTick(null, EventArgs.Empty);
        }

        static string ResolveDataDir()
        {
            string[] candidates = new string[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName),
                Path.Combine(Path.GetTempPath(), AppName),
                Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), AppName + "-data")
            };
            foreach (var c in candidates) { try { Directory.CreateDirectory(c); return c; } catch { } }
            return Path.GetTempPath();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int p = 2; DwmSetWindowAttribute(Handle, 33, ref p, 4); } catch { }
        }

        void BuildUi()
        {
            Text = "NetMeter";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            var wa = Screen.PrimaryScreen.WorkingArea;
            ClientSize = new Size(420, 428);
            Location = new Point(wa.Right - 440, wa.Bottom - 460);
            BackColor = T.Bg;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            MouseDown += DragWindow;

            head = new Card();
            head.Radius = 0;
            head.Fill = T.Bg;
            head.BorderColor = T.Bg;
            head.SetBounds(0, 0, 420, 50);
            head.MouseDown += DragWindow;
            head.Paint += delegate(object s, PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(T.Accent)) g.FillEllipse(b, 16, 21, 8, 8);
                D.Txt(g, "NetMeter", T.FTitle, new Rectangle(34, 8, 220, 22), T.Text);
                D.Txt(g, "流量监控", T.FTiny, new Rectangle(34, 30, 220, 18), T.Muted);
            };

            var btnClose = new Pill();
            btnClose.Text = "×";
            btnClose.SetBounds(382, 13, 30, 24);
            btnClose.Click += delegate { Quit(); };

            var btnHide = new Pill();
            btnHide.Text = "—";
            btnHide.SetBounds(348, 13, 30, 24);
            btnHide.Click += delegate { Hide(); };

            speedCard = new Card();
            speedCard.SetBounds(12, 58, 396, 96);
            speedCard.Paint += PaintSpeed;

            spark = new Spark();
            spark.SetBounds(12, 162, 396, 54);

            todayCard = new Card();
            todayCard.SetBounds(12, 224, 396, 96);
            todayCard.Paint += PaintToday;

            footCard = new Card();
            footCard.SetBounds(12, 328, 396, 52);
            footCard.Paint += PaintFoot;

            btnStats = new Pill();
            btnStats.Text = "统计 / 曲线";
            btnStats.Accent = true;
            btnStats.SetBounds(12, 388, 130, 30);
            btnStats.Click += delegate { OpenStats(); };

            var btnDir = new Pill();
            btnDir.Text = "数据目录";
            btnDir.SetBounds(150, 388, 96, 30);
            btnDir.Click += delegate { System.Diagnostics.Process.Start("explorer.exe", dataDir); };

            var btnTray = new Pill();
            btnTray.Text = "收起";
            btnTray.SetBounds(254, 388, 76, 30);
            btnTray.Click += delegate { Hide(); };

            var btnExit = new Pill();
            btnExit.Text = "退出";
            btnExit.SetBounds(338, 388, 74, 30);
            btnExit.Click += delegate { Quit(); };

            Controls.AddRange(new Control[] { head, speedCard, spark, todayCard, footCard, btnStats, btnDir, btnTray, btnExit, btnHide, btnClose });
            btnHide.BringToFront();
            btnClose.BringToFront();

            menu = new ContextMenuStrip();
            var miShow = new ToolStripMenuItem("显示窗口");
            miShow.Click += delegate { Show(); Activate(); };
            var miStats = new ToolStripMenuItem("统计 / 曲线");
            miStats.Click += delegate { OpenStats(); };
            miAuto = new ToolStripMenuItem("开机自启");
            miAuto.CheckOnClick = true;
            miAuto.Checked = AutoStartEnabled();
            miAuto.Click += delegate { SetAutoStart(miAuto.Checked); };
            var miDir = new ToolStripMenuItem("打开数据目录");
            miDir.Click += delegate { System.Diagnostics.Process.Start("explorer.exe", dataDir); };
            var miQuit = new ToolStripMenuItem("退出");
            miQuit.Click += delegate { Quit(); };
            menu.Items.Add(miShow);
            menu.Items.Add(miStats);
            menu.Items.Add(miAuto);
            menu.Items.Add(miDir);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miQuit);

            tray = new NotifyIcon();
            tray.Icon = MakeIcon();
            tray.Text = "NetMeter";
            tray.Visible = true;
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { Show(); Activate(); };
        }

        void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }

        // 托盘图标：深色圆底 + 绿色下行 / 橙色上行箭头
        static Icon MakeIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var b = new SolidBrush(Color.FromArgb(22, 27, 36))) g.FillEllipse(b, 0, 0, 15, 15);
                using (var pen = new Pen(T.Down, 1.4f))
                {
                    pen.EndCap = LineCap.Round; pen.StartCap = LineCap.Round;
                    g.DrawLine(pen, 5f, 3f, 5f, 11f);
                    g.DrawLine(pen, 5f, 12f, 3f, 9.2f);
                    g.DrawLine(pen, 5f, 12f, 7f, 9.2f);
                }
                using (var pen = new Pen(T.Up, 1.4f))
                {
                    pen.EndCap = LineCap.Round; pen.StartCap = LineCap.Round;
                    g.DrawLine(pen, 11f, 13f, 11f, 5f);
                    g.DrawLine(pen, 11f, 4f, 9f, 6.8f);
                    g.DrawLine(pen, 11f, 4f, 13f, 6.8f);
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        void PaintSpeed(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            D.Txt(g, "↓  " + MainForm.Speed(curDown) + "/s", T.FBig, new Rectangle(16, 14, 250, 34), T.Down);
            D.Txt(g, "↑  " + MainForm.Speed(curUp) + "/s", T.FBig, new Rectangle(16, 54, 250, 34), T.Up);

            D.Right(g, "代理", T.FTiny, new Rectangle(272, 16, 110, 18), T.Muted);
            D.Right(g, proxyOn ? "开" : "关", T.FMid, new Rectangle(272, 36, 110, 24), proxyOn ? T.Down : T.Muted);
            D.Right(g, proxyDetail, T.FTiny, new Rectangle(272, 64, 110, 18), T.Muted);
        }

        void PaintToday(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            D.Txt(g, "今日", T.FSmall, new Rectangle(16, 10, 60, 20), T.Muted);
            D.Txt(g, MainForm.Fmt(todayRx + todayTx), T.FBig, new Rectangle(16, 30, 260, 36), T.Text);
            D.Txt(g, "代理开 " + MainForm.Fmt(todayProxyRx + todayProxyTx), T.FTiny, new Rectangle(16, 70, 180, 18), T.Muted);
            D.Txt(g, "直连 " + MainForm.Fmt(todayRx + todayTx - todayProxyRx - todayProxyTx), T.FTiny, new Rectangle(196, 70, 190, 18), T.Muted);

            long tot = Math.Max(1, todayRx + todayTx);
            int w = 364;
            int pw = (int)(w * (double)(todayProxyRx + todayProxyTx) / tot);
            using (var b = new SolidBrush(T.CardHi)) g.FillRectangle(b, new Rectangle(16, 90, w, 4));
            if (pw > 0)
                using (var b = new SolidBrush(T.Accent)) g.FillRectangle(b, new Rectangle(16, 90, pw, 4));
        }

        void PaintFoot(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            D.Txt(g, "本月 " + MainForm.Fmt(MonthTotal()), T.FTiny, new Rectangle(16, 7, 190, 18), T.Muted);
            D.Txt(g, "本年 " + MainForm.Fmt(YearTotal()), T.FTiny, new Rectangle(16, 27, 190, 18), T.Muted);

            double mult = ParseRate();
            bool warn = mult >= 3;
            string badge = string.IsNullOrEmpty(nodeName) ? "节点未知" : nodeName;
            var size = TextRenderer.MeasureText(badge, T.FTiny);
            int bw = Math.Min(190, size.Width + 20);
            var r = new Rectangle(386 - bw, 12, bw, 26);
            using (var path = T.Round(r, 13))
            using (var b = new SolidBrush(warn ? Color.FromArgb(44, 25, 25) : Color.FromArgb(22, 30, 44)))
            using (var pen = new Pen(warn ? T.Warn : T.Accent))
            {
                g.FillPath(b, path);
                g.DrawPath(pen, path);
            }
            D.Center(g, badge, T.FTiny, r, warn ? T.Warn : T.Accent);
        }

        void OpenStats()
        {
            if (stats == null || stats.IsDisposed) stats = new StatsForm(this);
            stats.Show();
            stats.Activate();
        }

        // ---------- 采样 ----------
        static readonly string[] VirtualKeys = new string[] {
            "mihomo", "wintun", "tap-windows", "wi-fi direct", "bluetooth", "vethernet",
            "vmware", "virtualbox", "hyper-v", "teredo", "isatap", "本地连接*", "loopback"
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct MIB_IF_ROW2
        {
            public ulong InterfaceLuid;
            public uint InterfaceIndex;
            public Guid InterfaceGuid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] public string Alias;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] public string Description;
            public uint PhysicalAddressLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] PhysicalAddress;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] PermanentPhysicalAddress;
            public uint Mtu;
            public uint Type;
            public uint TunnelType;
            public uint MediaType;
            public uint PhysicalMediumType;
            public uint AccessType;
            public uint DirectionType;
            public byte InterfaceAndOperStatusFlags;
            public uint OperStatus;
            public uint AdminStatus;
            public uint MediaConnectState;
            public Guid NetworkGuid;
            public uint ConnectionType;
            public ulong TransmitLinkSpeed;
            public ulong ReceiveLinkSpeed;
            public ulong InOctets;
            public ulong OutOctets;
            public ulong InUcastPkts;
            public ulong InNUcastPkts;
            public ulong InDiscards;
            public ulong InErrors;
            public ulong InUnknownProtos;
            public ulong InUcastOctets;
            public ulong InMulticastOctets;
            public ulong InBroadcastOctets;
            public ulong OutUcastPkts;
            public ulong OutNUcastPkts;
            public ulong OutDiscards;
            public ulong OutErrors;
            public ulong OutUcastOctets;
            public ulong OutMulticastOctets;
            public ulong OutBroadcastOctets;
            public ulong OutQLen;
        }

        [DllImport("iphlpapi.dll")] static extern int GetIfTable2(out IntPtr table);
        [DllImport("iphlpapi.dll")] static extern void FreeMibTable(IntPtr table);

        static bool SkipName(string key)
        {
            foreach (var v in VirtualKeys) { if (key.Contains(v)) return true; }
            return false;
        }

        static long[] SampleTraffic()
        {
            long rx = 0, tx = 0;
            var realNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try { foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()) realNames.Add(ni.Name); } catch { }
            IntPtr table = IntPtr.Zero;
            try
            {
                if (GetIfTable2(out table) == 0 && table != IntPtr.Zero)
                {
                    int n = Marshal.ReadInt32(table);
                    int size = Marshal.SizeOf(typeof(MIB_IF_ROW2));
                    for (int i = 0; i < n; i++)
                    {
                        IntPtr p = new IntPtr(table.ToInt64() + 8 + (long)i * size);
                        var row = (MIB_IF_ROW2)Marshal.PtrToStructure(p, typeof(MIB_IF_ROW2));
                        if (row.OperStatus != 1) continue;
                        if (row.Type == 131 || row.Type == 24) continue;
                        string alias = row.Alias ?? "";
                        if (!realNames.Contains(alias)) continue;
                        string key = (alias + " " + (row.Description ?? "")).ToLowerInvariant();
                        if (SkipName(key)) continue;
                        rx += (long)row.InOctets;
                        tx += (long)row.OutOctets;
                    }
                    return new long[] { rx, tx };
                }
            }
            catch { }
            finally { if (table != IntPtr.Zero) FreeMibTable(table); }

            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    try
                    {
                        if (ni.OperationalStatus != OperationalStatus.Up) continue;
                        var t = ni.NetworkInterfaceType;
                        if (t == NetworkInterfaceType.Loopback || t == NetworkInterfaceType.Tunnel) continue;
                        if (SkipName((ni.Name + " " + ni.Description).ToLowerInvariant())) continue;
                        var s = ni.GetIPv4Statistics();
                        rx += s.BytesReceived;
                        tx += s.BytesSent;
                    }
                    catch { }
                }
            }
            catch { }
            return new long[] { rx, tx };
        }

        static bool IsProxyOn(out string detail)
        {
            bool sys = false, tun = false;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    if (k != null)
                    {
                        int en = 0;
                        try { en = Convert.ToInt32(k.GetValue("ProxyEnable", 0)); } catch { }
                        string srv = k.GetValue("ProxyServer") as string;
                        sys = en == 1 && !string.IsNullOrEmpty(srv);
                    }
                }
            }
            catch { }
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                    if (ni.Name == "Mihomo" && ni.OperationalStatus == OperationalStatus.Up) { tun = true; break; }
            }
            catch { }
            detail = tun ? "TUN" : (sys ? "系统代理" : "直连");
            return sys || tun;
        }

        void UpdateNode()
        {
            try
            {
                string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"io.github.clash-verge-rev.clash-verge-rev\profiles.yaml");
                if (!File.Exists(p)) { nodeName = ""; nodeRate = "1"; return; }
                string text = File.ReadAllText(p, Encoding.UTF8);
                var m = Regex.Match(text, @"name:\s*GLOBAL\s*\r?\n\s*now:\s*(.+)");
                if (!m.Success) return;
                nodeName = m.Groups[1].Value.Trim().Trim('\'', '"');
                var r = Regex.Match(nodeName, @"(\d+(?:\.\d+)?)\s*[xX]\s*$");
                nodeRate = r.Success ? r.Groups[1].Value : "1";
            }
            catch { }
        }

        void OnTick(object sender, EventArgs e)
        {
            var now = DateTime.Now;
            var cur = SampleTraffic();
            double secs = (now - lastTick).TotalSeconds;
            if (secs <= 0.2) secs = 1;
            long drx = cur[0] - lastRx;
            long dtx = cur[1] - lastTx;
            if (firstSample) { drx = 0; dtx = 0; firstSample = false; }
            if (drx < 0) drx = 0;
            if (dtx < 0) dtx = 0;
            lastRx = cur[0]; lastTx = cur[1]; lastTick = now;

            Rollover(now);
            todayRx += drx; todayTx += dtx;
            hourRx[now.Hour] += drx;
            if (proxyOn) { todayProxyRx += drx; todayProxyTx += dtx; }

            var day = Store.Today();
            day.Rx = todayRx; day.Tx = todayTx; day.PRx = todayProxyRx; day.PTx = todayProxyTx;

            curDown = drx / secs;
            curUp = dtx / secs;
            downQ.Add(curDown); upQ.Add(curUp);
            if (downQ.Count > 180) { downQ.RemoveAt(0); upQ.RemoveAt(0); }
            spark.Down = downQ;
            spark.Up = upQ;

            bool p = IsProxyOn(out proxyDetail);
            if (p != proxyOn)
            {
                proxyOn = p;
                AppendEvent(string.Format("{0} 代理切换为 {1}（{2}）", now.ToString("yyyy-MM-dd HH:mm:ss"), p ? "开" : "关", proxyDetail));
                SaveAll();
            }

            tick++;
            if (tick % 10 == 0) UpdateNode();
            if (tick % 5 == 0) SaveAll();
            if (tick % 2 == 0 && IsElevated) PerApp.Sample();
            if (tick % 300 == 0) AppendCsv();

            string tip = string.Format("NetMeter ↓{0}/s ↑{1}/s\n今日 {2}  本月 {3}",
                Speed(curDown), Speed(curUp), Fmt(todayRx + todayTx), Fmt(MonthTotal()));
            if (tip.Length > 63) tip = tip.Substring(0, 63);
            tray.Text = tip;

            speedCard.Invalidate();
            spark.Invalidate();
            todayCard.Invalidate();
            footCard.Invalidate();
        }

        void Rollover(DateTime now)
        {
            string d = now.ToString("yyyy-MM-dd");
            if (d != todayKey)
            {
                SaveAll();
                todayRx = todayTx = todayProxyRx = todayProxyTx = 0;
                for (int i = 0; i < 24; i++) hourRx[i] = 0;
                todayKey = d;
            }
        }

        double ParseRate()
        {
            double mult = 1;
            double.TryParse(nodeRate, NumberStyles.Float, CultureInfo.InvariantCulture, out mult);
            return mult <= 0 ? 1 : mult;
        }

        public long MonthTotal() { long t, p; Store.Sum(DateTime.Today.ToString("yyyy-MM"), out t, out p); return t; }
        public long YearTotal() { long t, p; Store.Sum(DateTime.Today.ToString("yyyy"), out t, out p); return t; }

        public static string Speed(double bps)
        {
            if (bps < 1024) return string.Format("{0:F0} B", bps);
            if (bps < 1024 * 1024) return string.Format("{0:F1} KB", bps / 1024.0);
            if (bps < 1024.0 * 1024 * 1024) return string.Format("{0:F1} MB", bps / 1024.0 / 1024.0);
            return string.Format("{0:F2} GB", bps / 1024.0 / 1024.0 / 1024.0);
        }

        public static string Fmt(long bytes)
        {
            if (bytes < 1024L) return bytes + " B";
            if (bytes < 1024L * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / 1024.0 / 1024.0).ToString("F2") + " MB";
            return (bytes / 1024.0 / 1024.0 / 1024.0).ToString("F2") + " GB";
        }

        // ---------- 持久化 ----------
        void LoadState()
        {
            try
            {
                if (!File.Exists(stateFile)) return;
                foreach (var line in File.ReadAllLines(stateFile, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string k = line.Substring(0, i).Trim();
                    string v = line.Substring(i + 1).Trim();
                    switch (k)
                    {
                        case "todayKey": todayKey = v; break;
                        case "todayRx": long.TryParse(v, out todayRx); break;
                        case "todayTx": long.TryParse(v, out todayTx); break;
                        case "todayProxyRx": long.TryParse(v, out todayProxyRx); break;
                        case "todayProxyTx": long.TryParse(v, out todayProxyTx); break;
                        case "hours":
                            var parts = v.Split(',');
                            for (int h = 0; h < parts.Length && h < 24; h++)
                            {
                                long hv;
                                if (long.TryParse(parts[h], out hv)) hourRx[h] = hv;
                            }
                            break;
                    }
                }
                if (todayKey != DateTime.Today.ToString("yyyy-MM-dd"))
                {
                    todayRx = todayTx = todayProxyRx = todayProxyTx = 0;
                    for (int i = 0; i < 24; i++) hourRx[i] = 0;
                    todayKey = DateTime.Today.ToString("yyyy-MM-dd");
                }
            }
            catch { }
        }

        void SaveAll()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("todayKey=" + todayKey);
                sb.AppendLine("todayRx=" + todayRx);
                sb.AppendLine("todayTx=" + todayTx);
                sb.AppendLine("todayProxyRx=" + todayProxyRx);
                sb.AppendLine("todayProxyTx=" + todayProxyTx);
                var hs = new string[24];
                for (int i = 0; i < 24; i++) hs[i] = hourRx[i].ToString();
                sb.AppendLine("hours=" + string.Join(",", hs));
                string tmp = stateFile + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
                if (File.Exists(stateFile)) File.Delete(stateFile);
                File.Move(tmp, stateFile);
            }
            catch { }
            Store.Save();
            PerApp.Save();
        }

        void AppendCsv()
        {
            try
            {
                string f = Path.Combine(dataDir, "traffic.csv");
                bool newFile = !File.Exists(f);
                using (var fs = new FileStream(f, FileMode.Append, FileAccess.Write, FileShare.Read))
                using (var sw = new StreamWriter(fs, Encoding.UTF8))
                {
                    if (newFile) sw.WriteLine("time,today_rx,today_tx,proxy_on,node,rate");
                    sw.WriteLine(string.Format("{0},{1},{2},{3},{4},{5}",
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), todayRx, todayTx, proxyOn ? 1 : 0,
                        (nodeName ?? "").Replace(",", " "), nodeRate));
                }
            }
            catch { }
        }

        void AppendEvent(string text)
        {
            try { File.AppendAllText(eventFile, text + Environment.NewLine, Encoding.UTF8); } catch { }
        }

        static bool AutoStartEnabled()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                    return k != null && k.GetValue(AppName) != null;
            }
            catch { return false; }
        }

        void SetAutoStart(bool on)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (on) k.SetValue(AppName, "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex) { MessageBox.Show("设置开机自启失败：" + ex.Message); }
        }

        void Quit()
        {
            SaveAll();
            tray.Visible = false;
            tray.Dispose();
            timer.Stop();
            Application.Exit();
        }

        public void QuitApp() { Quit(); }

        void TryElevateTasks()
        {
            try
            {
                string user = Environment.UserDomainName + "\\" + Environment.UserName;
                string exe = Application.ExecutablePath;
                string dir = Path.GetDirectoryName(exe);
                string head = "<?xml version=\"1.0\" encoding=\"UTF-16\"?>" +
                    "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">" +
                    "<Principals><Principal id=\"Author\"><UserId>" + user + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>" +
                    "<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>" +
                    "<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>" +
                    "<AllowHardTerminate>true</AllowHardTerminate><StartWhenAvailable>true</StartWhenAvailable>" +
                    "<RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable><AllowStartOnDemand>true</AllowStartOnDemand>" +
                    "<Enabled>true</Enabled><Hidden>false</Hidden><RunOnlyIfIdle>false</RunOnlyIfIdle>" +
                    "<ExecutionTimeLimit>PT0S</ExecutionTimeLimit></Settings>";
                string action = "<Actions Context=\"Author\"><Exec><Command>" + exe + "</Command><WorkingDirectory>" + dir + "</WorkingDirectory></Exec></Actions></Task>";

                string mainXml = head.Replace("</Settings>", "<RestartOnFailure><Interval>PT1M</Interval><Count>999</Count></RestartOnFailure></Settings>")
                    + "<Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + user + "</UserId></LogonTrigger></Triggers>" + action;

                string watchXml = head.Replace("</Settings>", "<RestartOnFailure><Interval>PT1M</Interval><Count>999</Count></RestartOnFailure></Settings>")
                    + "<Triggers><TimeTrigger><StartBoundary>2026-01-01T00:00:00</StartBoundary><Repetition><Interval>PT5M</Interval><StopAtDurationEnd>false</StopAtDurationEnd></Repetition><Enabled>true</Enabled></TimeTrigger></Triggers>" + action;

                string f1 = Path.Combine(Path.GetTempPath(), "nm_main.xml");
                string f2 = Path.Combine(Path.GetTempPath(), "nm_watch.xml");
                File.WriteAllText(f1, mainXml, Encoding.Unicode);
                File.WriteAllText(f2, watchXml, Encoding.Unicode);

                RunTask("schtasks", "/create /tn \"NetMeter\" /xml \"" + f1 + "\" /f");
                RunTask("schtasks", "/create /tn \"NetMeterWatch\" /xml \"" + f2 + "\" /f");
            }
            catch (Exception ex) { AppendEvent("提权任务创建失败：" + ex.Message); }
        }

        static void RunTask(string cmd, string args)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(cmd, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardError = true;
                psi.RedirectStandardOutput = true;
                var p = System.Diagnostics.Process.Start(psi);
                string err = p.StandardError.ReadToEnd();
                string outp = p.StandardOutput.ReadToEnd();
                p.WaitForExit(4000);
                if (p.ExitCode != 0) File.AppendAllText(Path.Combine(Path.GetTempPath(), "NetMeter-crash.log"),
                    DateTime.Now + " task create rc=" + p.ExitCode + " " + err + outp + Environment.NewLine);
            }
            catch { }
        }
    }

    // ---------- 统计窗口 ----------
    public class StatsForm : Form
    {
        const int WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 2;
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string sub, string id);

        readonly MainForm owner;
        Panel page;
        Table list;
        Table apps;
        Label lblAppStatus;
        Card appNote;
        Pill tabApps;
        Label lblSum;
        Card sumCard, chartCard, barCard;
        Spark line, bars;
        Pill tabHist, tabChart, tabInfo;
        System.Windows.Forms.Timer refresh;
        string tab = "hist";

        public StatsForm(MainForm o)
        {
            owner = o;
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(700, 574);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = T.Bg;
            DoubleBuffered = true;
            MouseDown += Drag;
            Text = "NetMeter 统计";

            var head = new Card();
            head.Radius = 0;
            head.Fill = T.Bg;
            head.BorderColor = T.Bg;
            head.SetBounds(0, 0, 700, 50);
            head.MouseDown += Drag;
            head.Paint += delegate(object s, PaintEventArgs e)
            {
                var g = e.Graphics;
                using (var b = new SolidBrush(T.Accent)) g.FillEllipse(b, 16, 18, 8, 8);
                D.Txt(g, "统计", T.FTitle, new Rectangle(32, 11, 200, 22), T.Text);
            };
            var close = new Pill();
            close.Text = "×";
            close.SetBounds(660, 13, 30, 24);
            close.Click += delegate { Close(); };

            tabHist = Tab("历史", 16);
            tabChart = Tab("曲线", 96);
            tabInfo = Tab("说明", 176);
            tabApps = Tab("程序", 256);

            page = new Panel();
            page.SetBounds(16, 100, 668, 456);
            page.BackColor = T.Bg;

            // 历史页
            sumCard = new Card();
            sumCard.SetBounds(0, 0, 668, 88);
            sumCard.Paint += PaintSum;

            list = new Table();
            list.SetBounds(0, 100, 668, 350);
            list.ColX = new int[] { 16, 210, 390, 545 };

            // 曲线页
            chartCard = new Card();
            chartCard.SetBounds(0, 0, 668, 264);
            line = new Spark();
            line.SetBounds(8, 8, 652, 248);
            chartCard.Controls.Add(line);

            barCard = new Card();
            barCard.SetBounds(0, 276, 668, 176);
            bars = new Spark();
            bars.ShowBars = true;
            bars.SetBounds(8, 34, 652, 132);
            barCard.Controls.Add(bars);
            barCard.Paint += delegate(object s, PaintEventArgs e)
            {
                TextRenderer.DrawText(e.Graphics, "今日每小时", T.FSmall, new Rectangle(16, 8, 200, 18), T.Muted);
            };

            // 说明页
            var info = new Card();
            info.SetBounds(0, 0, 668, 456);
            info.Paint += PaintInfo;

            page.Controls.Add(sumCard);
            page.Controls.Add(list);

            // 程序页
            apps = new Table();
            apps.SetBounds(0, 0, 668, 414);
            apps.Headers = new string[] { "程序", "下载", "上传", "合计" };
            apps.ColX = new int[] { 16, 260, 420, 560 };
            lblAppStatus = new Label();
            lblAppStatus.SetBounds(0, 420, 668, 24);
            lblAppStatus.BackColor = T.Bg;
            lblAppStatus.ForeColor = T.Muted;
            lblAppStatus.Font = T.FTiny;
            appNote = new Card();
            appNote.SetBounds(0, 0, 668, 456);
            appNote.Paint += PaintAppNote;
            var btnElevate = new Pill();
            btnElevate.Text = "以管理员身份重启";
            btnElevate.Accent = true;
            btnElevate.Backdrop = T.Card;
            btnElevate.SetBounds(246, 232, 176, 34);
            btnElevate.Click += delegate { RestartElevated(); };
            appNote.Controls.Add(btnElevate);

            Controls.AddRange(new Control[] { head, close, tabHist, tabChart, tabInfo, tabApps, page });
            close.BringToFront();

            refresh = new System.Windows.Forms.Timer();
            refresh.Interval = 1000;
            refresh.Tick += delegate { Reload(); };
            refresh.Start();
            SelectTab("hist");
            Reload();
        }

        Pill Tab(string text, int x)
        {
            var b = new Pill();
            b.Text = text;
            b.SetBounds(x, 58, 70, 26);
            b.Size = new Size(70, 26);
            b.Click += delegate { SelectTab(text == "历史" ? "hist" : text == "曲线" ? "chart" : text == "说明" ? "info" : "app"); };
            return b;
        }

        void SelectTab(string t)
        {
            tab = t;
            page.Controls.Clear();
            if (t == "hist") { page.Controls.Add(sumCard); page.Controls.Add(list); }
            else if (t == "chart") { page.Controls.Add(chartCard); page.Controls.Add(barCard); }
            else if (t == "app")
            {
                if (MainForm.IsElevated && PerApp.Error == "") { page.Controls.Add(apps); page.Controls.Add(lblAppStatus); }
                else page.Controls.Add(appNote);
            }
            else { page.Controls.Add(InfoCard()); }
            foreach (var b in new Pill[] { tabHist, tabChart, tabInfo, tabApps })
            {
                b.Accent = (b.Text == "历史" && t == "hist") || (b.Text == "曲线" && t == "chart")
                    || (b.Text == "说明" && t == "info") || (b.Text == "程序" && t == "app");
                b.Invalidate();
            }
            page.Invalidate(true);
        }

        void PaintAppNote(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            string title = MainForm.IsElevated ? "按程序统计暂时读不到" : "按程序统计需要管理员权限";
            string body;
            if (MainForm.IsElevated)
                body = "读取失败：" + PerApp.Error + "\r\n\r\n" +
                       "诊断日志：%APPDATA%\\NetMeter\\perapp.log\r\n" +
                       "把那个文件发我，我按返回码继续修。\r\n\r\n" +
                       "（如果系统不支持 TCP 扩展统计，这一页就只能用 设置 → 数据使用量 代替）";
            else
                body = "Windows 只允许管理员读取每个程序的连接字节数（TCP 扩展统计），\r\n" +
                       "普通权限下这一页是空的。\r\n\r\n" +
                       "点下面的按钮会弹一次 UAC，之后程序以管理员身份运行，\r\n" +
                       "这一页就会按用量排出每个程序。";
            TextRenderer.DrawText(g, title, new Font("Microsoft YaHei UI", 13f, FontStyle.Bold),
                new Rectangle(40, 40, 588, 30), T.Text);
            TextRenderer.DrawText(g, body, T.FSmall, new Rectangle(40, 90, 588, 140), T.Muted,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak);
        }

        void RestartElevated()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath);
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                System.Diagnostics.Process.Start(psi);
                owner.QuitApp();
            }
            catch (Exception ex)
            {
                MessageBox.Show("没有拿到管理员权限：" + ex.Message);
            }
        }

        Card infoCard;
        Card InfoCard()
        {
            if (infoCard == null)
            {
                infoCard = new Card();
                infoCard.SetBounds(0, 0, 668, 456);
                infoCard.Paint += PaintInfo;
                infoCard.MouseDown += Drag;
            }
            return infoCard;
        }

        void Drag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int p = 2; DwmSetWindowAttribute(Handle, 33, ref p, 4);
                int one = 1; DwmSetWindowAttribute(Handle, 20, ref one, 4);
            }
            catch { }
        }

        void PaintSum(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            long mt, mp, yt, yp;
            Store.Sum(DateTime.Today.ToString("yyyy-MM"), out mt, out mp);
            Store.Sum(DateTime.Today.ToString("yyyy"), out yt, out yp);
            D.Txt(g, "本月", T.FTiny, new Rectangle(20, 12, 60, 18), T.Muted);
            D.Txt(g, MainForm.Fmt(mt), T.FBig, new Rectangle(20, 30, 280, 34), T.Text);
            D.Txt(g, "代理 " + MainForm.Fmt(mp) + "  ·  直连 " + MainForm.Fmt(mt - mp),
                T.FTiny, new Rectangle(20, 66, 300, 18), T.Muted);

            D.Txt(g, "本年", T.FTiny, new Rectangle(364, 12, 60, 18), T.Muted);
            D.Txt(g, MainForm.Fmt(yt), T.FBig, new Rectangle(364, 30, 280, 34), T.Text);
            D.Txt(g, "代理 " + MainForm.Fmt(yp) + "  ·  直连 " + MainForm.Fmt(yt - yp),
                T.FTiny, new Rectangle(364, 66, 300, 18), T.Muted);
        }

        void PaintInfo(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var r = new Rectangle(24, 18, 620, 420);
            TextRenderer.DrawText(g,
                "数据与口径\r\n\r\n" +
                "· 每日合计存在 history.csv，关掉再开、重启电脑都会接着累计，不清零。\r\n" +
                "· 今日每小时分布存在 state.ini，跨天自动结转到 history。\r\n" +
                "· 代理切换记录在 events.log。\r\n\r\n" +
                "统计范围\r\n\r\n" +
                "· 只统计本机物理网卡（IPv4 + IPv6），已排除 TUN / Wi-Fi Direct / 蓝牙等虚拟网卡。\r\n" +
                "· 「代理开」= 系统代理或 TUN 开着时的流量；「直连」= 其余。\r\n" +
                "· 「计费估算」= 今日代理流量 × 当前节点倍率，换过节点会不准。\r\n\r\n" +
                "按程序统计\r\n\r\n" +
                "· 「程序」页会按程序列出下载/上传，但 Windows 只允许管理员读取每个程序的\r\n" +
                "  连接字节数（TCP 扩展统计），普通权限读不到。\r\n" +
                "· 点「程序」页的「以管理员身份重启」按钮，弹一次 UAC 点是即可。\r\n" +
                "  之后计划任务也会改成以最高权限启动，下次开机不再弹窗。\r\n" +
                "· 如果重启后还是空的，说明系统不支持 TCP 扩展统计，页面上会显示原因。",
                T.FSmall, r, T.Text, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak);
        }

        void Reload()
        {
            long mt, mp, yt, yp;
            Store.Sum(DateTime.Today.ToString("yyyy-MM"), out mt, out mp);
            Store.Sum(DateTime.Today.ToString("yyyy"), out yt, out yp);
            if (sumCard != null) sumCard.Invalidate();

            if (tab == "hist" && list != null)
            {
                var keys = new List<string>(Store.Days.Keys);
                keys.Sort();
                keys.Reverse();
                list.Rows.Clear();
                foreach (var k in keys)
                {
                    var r = Store.Days[k];
                    list.Rows.Add(new string[] { k, MainForm.Fmt(r.Total), MainForm.Fmt(r.Proxy), MainForm.Fmt(r.Direct) });
                    if (list.Rows.Count >= 400) break;
                }
                list.Invalidate();
            }
            if (tab == "chart" && line != null)
            {
                line.Down = owner.DownQ;
                line.Up = owner.UpQ;
                bars.Hours = owner.Hours;
                line.Invalidate();
                bars.Invalidate();
            }
            if (tab == "app" && apps != null && MainForm.IsElevated)
            {
                var tmp = new List<KeyValuePair<string, AppStat>>(PerApp.Procs);
                tmp.Sort(delegate(KeyValuePair<string, AppStat> a, KeyValuePair<string, AppStat> b) { return b.Value.Total.CompareTo(a.Value.Total); });
                apps.Rows.Clear();
                int shown = 0;
                foreach (var kv in tmp)
                {
                    apps.Rows.Add(new string[] { kv.Key, MainForm.Fmt(kv.Value.Rx), MainForm.Fmt(kv.Value.Tx), MainForm.Fmt(kv.Value.Total) });
                    if (++shown >= 200) break;
                }
                apps.Invalidate();
                if (lblAppStatus != null) lblAppStatus.Text = PerApp.Status;
            }
        }
    }
}
