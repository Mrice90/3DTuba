// Self-extracting installer stub for the Infinite Conquest 3D playtest. build_installer.ps1 compiles this with
// PowerShell's Add-Type and appends payload.zip plus an 8-byte length trailer. At run time the stub extracts the
// zip to %LOCALAPPDATA%\Programs\Infinite Conquest 3D with a progress window, then runs postinstall.ps1 from the
// extracted files (shortcuts, Apps entry). Per-user, no admin rights. /S = silent.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

static class SetupStub {
    const string Title = "Infinite Conquest 3D Playtest Setup";

    [STAThread]
    static int Main(string[] args) {
        bool silent = Array.Exists(args, a => a.Equals("/S", StringComparison.OrdinalIgnoreCase));
        string dest = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Infinite Conquest 3D");
        if (silent) return Install(dest, null);

        Application.EnableVisualStyles();
        if (MessageBox.Show("Install Infinite Conquest 3D Playtest for this Windows user?\n\nLocation: " + dest, Title,
                MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return 1;
        var form = new Form { Text = Title, Width = 460, Height = 140, FormBorderStyle = FormBorderStyle.FixedDialog,
                              StartPosition = FormStartPosition.CenterScreen, MaximizeBox = false, MinimizeBox = false };
        var label = new Label { Text = "Installing…", Left = 16, Top = 14, Width = 410 };
        var bar = new ProgressBar { Left = 16, Top = 44, Width = 410, Height = 22, Minimum = 0, Maximum = 1000 };
        form.Controls.Add(label); form.Controls.Add(bar);
        int result = 1;
        form.Shown += (s, e) => new Thread(() => {
            result = Install(dest, f => form.BeginInvoke((Action)(() => { bar.Value = (int)(f * 1000); label.Text = "Installing… " + (int)(f * 100) + "%"; })));
            form.BeginInvoke((Action)form.Close);
        }) { IsBackground = true }.Start();
        Application.Run(form);
        if (result != 0) { MessageBox.Show("Setup failed: " + lastError, Title, MessageBoxButtons.OK, MessageBoxIcon.Error); return result; }
        if (MessageBox.Show("Infinite Conquest 3D Playtest is installed.\n\nPlay now?", Title, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            Process.Start(new ProcessStartInfo(Path.Combine(dest, "InfiniteConquestPlaytest.exe")) { WorkingDirectory = dest });
        return 0;
    }

    static string lastError = "";

    static int Install(string dest, Action<double> progress) {
        try {
            foreach (var p in Process.GetProcessesByName("InfiniteConquestPlaytest")) { try { p.Kill(); p.WaitForExit(3000); } catch { } }
            if (Directory.Exists(dest)) Directory.Delete(dest, true);
            Directory.CreateDirectory(dest);
            string self = Assembly.GetExecutingAssembly().Location;
            using (var fs = new FileStream(self, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                var trailer = new byte[8];
                fs.Seek(-8, SeekOrigin.End); fs.Read(trailer, 0, 8);
                long len = BitConverter.ToInt64(trailer, 0);
                long start = fs.Length - 8 - len;
                using (var slice = new SubStream(fs, start, len))
                using (var zip = new ZipArchive(slice, ZipArchiveMode.Read)) {
                    long total = 0, done = 0;
                    foreach (var e in zip.Entries) total += e.Length;
                    foreach (var e in zip.Entries) {
                        string target = Path.GetFullPath(Path.Combine(dest, e.FullName));
                        if (!target.StartsWith(Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase)) continue;
                        if (e.FullName.EndsWith("/") || e.FullName.EndsWith("\\")) { Directory.CreateDirectory(target); continue; }
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        e.ExtractToFile(target, true);
                        done += e.Length;
                        if (progress != null && total > 0) progress(Math.Min(.97, done / (double)total));
                    }
                }
            }
            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + Path.Combine(dest, "postinstall.ps1") + "\"") {
                UseShellExecute = false, CreateNoWindow = true };
            var post = Process.Start(psi); post.WaitForExit();
            if (post.ExitCode != 0) throw new Exception("shortcut/registration step failed (" + post.ExitCode + ")");
            if (progress != null) progress(1);
            return 0;
        } catch (Exception ex) { lastError = ex.Message; return 2; }
    }

    // Read-only window over [start, start+length) of the installer exe, so ZipArchive can seek within the payload.
    sealed class SubStream : Stream {
        readonly Stream inner; readonly long start, length; long pos;
        public SubStream(Stream inner, long start, long length) { this.inner = inner; this.start = start; this.length = length; }
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { return length; } }
        public override long Position { get { return pos; } set { pos = value; } }
        public override int Read(byte[] buffer, int offset, int count) {
            long left = length - pos; if (left <= 0) return 0;
            if (count > left) count = (int)left;
            inner.Seek(start + pos, SeekOrigin.Begin);
            int n = inner.Read(buffer, offset, count); pos += n; return n;
        }
        public override long Seek(long offset, SeekOrigin origin) {
            pos = origin == SeekOrigin.Begin ? offset : origin == SeekOrigin.Current ? pos + offset : length + offset; return pos;
        }
        public override void Flush() { }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
    }
}
