// Spike: a Windows Forms app that reports when it is shown and where its button is, and animates on request.
using System.Diagnostics;
using System.Runtime.InteropServices;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Console.WriteLine($"STARTED {Environment.TickCount64} {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture} {RuntimeInformation.OSDescription}");
        if (args.Length == 3 && args[0] == "--resize")
        {
            var mode = new DevMode { dmSize = (ushort)Marshal.SizeOf<DevMode>() };
            var current = EnumDisplaySettingsW(null, -1, ref mode);
            mode.dmPelsWidth = uint.Parse(args[1]);
            mode.dmPelsHeight = uint.Parse(args[2]);
            mode.dmFields = 0x80000 | 0x100000;
            var result = ChangeDisplaySettingsExW(null, ref mode, IntPtr.Zero, 0, IntPtr.Zero);
            Console.WriteLine($"RESIZE enum={current} result={result}");
            return;
        }

        if (args.Contains("--hello"))
        {
            Console.WriteLine("HELLO from " + RuntimeInformation.FrameworkDescription);
            return;
        }

        ApplicationConfiguration.Initialize();
        var form = new Form { Text = "Spike Forms", ClientSize = new Size(560, 360), StartPosition = FormStartPosition.Manual, Location = new Point(16, 16) };
        var button = new Button { Text = "Click me", Location = new Point(32, 32), Size = new Size(180, 48) };
        var label = new Label { Text = "Not clicked yet", Location = new Point(32, 100), AutoSize = true, Font = new Font("Tahoma", 14) };
        var clicks = 0;
        button.Click += (_, _) =>
        {
            clicks++;
            label.Text = $"Clicked {clicks} times";
            button.BackColor = Color.LightGreen;
            Console.WriteLine($"CLICKED {clicks}");
        };
        var open = new Button { Text = "Open a window", Location = new Point(232, 32), Size = new Size(180, 48) };
        open.Click += (_, _) =>
        {
            var second = new Form { Text = "Second window", ClientSize = new Size(320, 160), StartPosition = FormStartPosition.Manual, Location = new Point(300, 300) };
            second.Controls.Add(new Label { Text = "A second top-level window", AutoSize = true, Location = new Point(16, 16) });
            second.Shown += (_, _) => Console.WriteLine("OPENED second");
            second.Show(form);
        };
        form.Controls.Add(button);
        form.Controls.Add(open);
        form.Controls.Add(label);
        if (args.Contains("--animate"))
        {
            var canvas = new DoubleBufferedPanel { Location = new Point(32, 160), Size = new Size(480, 160), BackColor = Color.White };
            var x = 0;
            canvas.Paint += (_, e) => e.Graphics.FillRectangle(Brushes.SteelBlue, x, 40, 80, 80);
            var timer = new System.Windows.Forms.Timer { Interval = 5 };
            timer.Tick += (_, _) => { x = (x + 7) % 400; canvas.Invalidate(); };
            form.Controls.Add(canvas);
            form.Shown += (_, _) => timer.Start();
        }

        form.Shown += (_, _) =>
        {
            var center = button.PointToScreen(new Point(button.Width / 2, button.Height / 2));
            var second = open.PointToScreen(new Point(open.Width / 2, open.Height / 2));
            Console.WriteLine($"SHOWN {Environment.TickCount64} BUTTON {center.X} {center.Y} OPEN {second.X} {second.Y} SCREEN {Screen.PrimaryScreen?.Bounds.Width}x{Screen.PrimaryScreen?.Bounds.Height}");
        };
        form.Resize += (_, _) => Console.WriteLine($"RESIZED {form.Width}x{form.Height}");
        var resizeProbe = new System.Windows.Forms.Timer { Interval = 500 };
        var lastScreen = string.Empty;
        resizeProbe.Tick += (_, _) =>
        {
            var screen = $"{Screen.PrimaryScreen?.Bounds.Width}x{Screen.PrimaryScreen?.Bounds.Height}";
            if (screen != lastScreen)
            {
                lastScreen = screen;
                Console.WriteLine($"SCREEN {screen}");
            }
        };
        resizeProbe.Start();
        Application.Run(form);
        Console.WriteLine("EXITED");
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettingsW(string? device, int mode, ref DevMode devMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettingsExW(string? device, ref DevMode devMode, IntPtr hwnd, uint flags, IntPtr param);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }

    private sealed class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel() => DoubleBuffered = true;
    }
}
