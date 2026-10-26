using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    internal static class PopupRegressionTests
    {
        private static int checks;
        private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr devMode, uint flags, uint access, IntPtr security);
        [DllImport("user32.dll")]
        private static extern bool CloseDesktop(IntPtr desktop);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfo
        {
            internal int Size; internal string Reserved, Desktop, Title;
            internal int X, Y, Width, Height, CharsX, CharsY, Fill, Flags;
            internal short Show, ReservedSize; internal IntPtr ReservedBytes, Input, Output, Error;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInfo { internal IntPtr Process, Thread; internal int ProcessId, ThreadId; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateProcess(string application, System.Text.StringBuilder command, IntPtr processSecurity, IntPtr threadSecurity,
            bool inherit, uint flags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInfo process);
        [DllImport("kernel32.dll")] private static extern IntPtr GetStdHandle(int handle);
        [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(IntPtr handle, uint timeout);
        [DllImport("kernel32.dll")] private static extern bool GetExitCodeProcess(IntPtr process, out uint code);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        private sealed class OffscreenForm : Form
        {
            internal OffscreenForm()
            {
                ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
                Location = new Point(-10000, -10000); ClientSize = new Size(360, 300);
            }
            protected override bool ShowWithoutActivation { get { return true; } }
        }
        private static object Invoke(object target, string method, params object[] args)
        {
            try { return target.GetType().GetMethod(method, PrivateInstance).Invoke(target, args); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }
        private static void Check(bool result, string message)
        {
            checks++; if (!result) throw new InvalidOperationException(message);
        }
        private static ToolStripDropDown Prepare(CompactSelect select)
        {
            var dropdown = (ToolStripDropDown)Invoke(select, "GetPopup");
            return dropdown;
        }
        private static ListBox List(ToolStripDropDown dropdown) { return (ListBox)((ToolStripControlHost)dropdown.Items[0]).Control; }
        private static void Show(CompactSelect select, ToolStripDropDown dropdown)
        {
            dropdown.Show(select, new Point(0, select.Height + 3));
            Check(dropdown.Visible, "Test popup did not open.");
        }
        private static CompactSelect Select(Form form, int index)
        {
            var select = new CompactSelect { Location = new Point(12 + index % 2 * 170, 12 + index / 2 * 70), Size = new Size(145, 34) };
            int count = index == 2 ? 1440 : index == 3 ? 30 : 4;
            for (int i = 0; i < count; i++) select.Items.Add(index == 2 ? (i / 60).ToString("00") + ":" + (i % 60).ToString("00") : "Option " + i);
            select.SelectedIndex = index == 2 ? 485 : 0; form.Controls.Add(select); return select;
        }
        private static void Run()
        {
            CheckTimeInput();
            var owned = new List<ToolStripDropDown>();
            using (var form = new OffscreenForm())
            {
                var selects = new CompactSelect[4];
                for (int i = 0; i < selects.Length; i++) selects[i] = Select(form, i);
                form.Show();
                foreach (var select in selects)
                {
                    var dropdown = Prepare(select); owned.Add(dropdown);
                    IntPtr handle = dropdown.Handle;
                    Invoke(select, "Open");
                    Check(dropdown.Visible && List(dropdown).SelectedIndex == select.SelectedIndex, "Opening did not restore the selected value.");
                    Invoke(select, "Open");
                    Check(object.ReferenceEquals(dropdown, Prepare(select)), "Opening an already open field replaced its popup.");
                    dropdown.Close();
                    foreach (ToolStripDropDownCloseReason reason in Enum.GetValues(typeof(ToolStripDropDownCloseReason)))
                    for (int i = 0; i < 6; i++)
                    {
                        Show(select, dropdown); dropdown.Close(reason);
                        Check(!dropdown.IsDisposed && !List(dropdown).IsDisposed, "Closing a popup prematurely disposed its controls: " + reason);
                        Check(!dropdown.Visible && dropdown.Handle == handle, "Closed popup was not reusable: " + reason);
                        Check(object.ReferenceEquals(dropdown, Prepare(select)), "Reopening created another popup.");
                    }
                    int changes = 0; select.SelectedIndexChanged += delegate { changes++; };
                    Show(select, dropdown); List(dropdown).SelectedIndex = 1;
                    Invoke(List(dropdown), "OnClick", EventArgs.Empty);
                    Check(select.SelectedIndex == 1 && changes == 1 && !dropdown.Visible, "Click did not commit exactly once.");
                    Show(select, dropdown); List(dropdown).SelectedIndex = 2;
                    var escape = new KeyEventArgs(Keys.Escape); Invoke(List(dropdown), "OnKeyDown", escape);
                    Check(select.SelectedIndex == 1 && changes == 1 && escape.Handled && !dropdown.Visible, "Escape committed a pending selection.");
                    Show(select, dropdown); List(dropdown).SelectedIndex = 2;
                    var enter = new KeyEventArgs(Keys.Enter); Invoke(List(dropdown), "OnKeyDown", enter);
                    Check(select.SelectedIndex == 2 && changes == 2 && enter.Handled && !dropdown.Visible, "Enter did not commit exactly once.");
                    Show(select, dropdown); select.Enabled = false;
                    Check(!dropdown.Visible && !dropdown.IsDisposed, "Disabling a field left its popup open."); select.Enabled = true;
                    Show(select, dropdown); select.Visible = false;
                    Check(!dropdown.Visible && !dropdown.IsDisposed, "Hiding a field left its popup open."); select.Visible = true;
                }
                for (int i = 0; i < selects.Length; i++)
                {
                    Invoke(selects[i], "Open"); owned[i].Close(ToolStripDropDownCloseReason.AppClicked);
                    var next = selects[(i + 1) % selects.Length]; next.Focus(); Invoke(next, "Open");
                    Check(((ToolStripDropDown)Prepare(next)).Visible && !owned[i].Visible, "Switching fields reopened the previous popup.");
                    Prepare(next).Close();
                }
                Show(selects[3], owned[3]); form.Close();
            }
            foreach (var dropdown in owned) Check(dropdown.IsDisposed, "Settings window leaked a popup.");
            using (var form = new OffscreenForm())
            {
                var select = Select(form, 0); form.Show(); var dropdown = Prepare(select);
                select.SelectedIndexChanged += delegate { form.Close(); };
                Show(select, dropdown); List(dropdown).SelectedIndex = 1;
                Invoke(List(dropdown), "OnClick", EventArgs.Empty);
                Check(form.IsDisposed && dropdown.IsDisposed, "Closing the owner during selection leaked its popup.");
            }
        }
        private static void CheckTimeInput()
        {
            using (var input = new TimeInput())
            {
                var editor = (MaskedTextBox)input.Controls[0];
                int changes = 0; input.ValueChanged += delegate { changes++; };
                foreach (string valid in new[] { "00:00", "08:05", "12:30", "23:59" })
                {
                    input.TimeText = valid;
                    Check(input.TimeText == valid, "Time input changed a valid value.");
                }
                Check(changes > 0, "Editing the time did not notify settings.");
                foreach (string invalid in new[] { "24:00", "12:60", "99:99", "", "08:" })
                {
                    input.TimeText = invalid;
                    bool rejected = false;
                    try { string ignored = input.TimeText; } catch (ArgumentException) { rejected = true; }
                    Check(rejected, "Invalid time was accepted: " + invalid);
                }
                input.TimeText = "23:59"; editor.Select(3, 2);
                var up = new KeyEventArgs(Keys.Up); Invoke(editor, "OnKeyDown", up);
                Check(input.TimeText == "00:00" && up.SuppressKeyPress && editor.SelectionStart == 3, "Minute increment did not wrap at midnight.");
                Invoke(editor, "OnKeyDown", new KeyEventArgs(Keys.Down));
                Check(input.TimeText == "23:59", "Minute decrement did not wrap at midnight.");
                editor.Select(0, 2); Invoke(editor, "OnKeyDown", new KeyEventArgs(Keys.Up));
                Check(input.TimeText == "00:59" && editor.SelectionStart == 0, "Hour increment changed minutes.");
                Invoke(editor, "OnKeyDown", new KeyEventArgs(Keys.Down));
                Check(input.TimeText == "23:59", "Hour decrement did not wrap.");
                var selectAll = new KeyEventArgs(Keys.Control | Keys.A); Invoke(editor, "OnKeyDown", selectAll);
                Check(editor.SelectionLength == 5 && selectAll.SuppressKeyPress, "Select-all did not select the whole time.");
                input.Enabled = false;
                Check(!editor.Enabled, "Disabled time input stayed editable.");
                Check(input.Controls.Count == 1, "Time input created unnecessary child controls.");
            }
        }
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "--isolated-desktop") return RunOnDesktop();
            // Assign the child desktop before its STA/WinForms initialization creates hidden windows.
            // Never switch the user's input desktop; all real popup transitions stay on this private desktop.
            string name = "BingWallpaperPopupTests-" + Guid.NewGuid().ToString("N");
            IntPtr desktop = CreateDesktop(name, IntPtr.Zero, IntPtr.Zero, 0, 0x10000000, IntPtr.Zero);
            if (desktop == IntPtr.Zero) { Console.WriteLine("FAIL: could not create a test desktop: " + Marshal.GetLastWin32Error()); return 1; }
            try
            {
                var startup = new StartupInfo { Size = Marshal.SizeOf(typeof(StartupInfo)), Desktop = name, Flags = 0x100,
                    Input = GetStdHandle(-10), Output = GetStdHandle(-11), Error = GetStdHandle(-12) };
                ProcessInfo process;
                string executable = Assembly.GetExecutingAssembly().Location;
                if (!CreateProcess(executable, new System.Text.StringBuilder("\"" + executable + "\" --isolated-desktop"), IntPtr.Zero, IntPtr.Zero,
                    true, 0, IntPtr.Zero, null, ref startup, out process))
                { Console.WriteLine("FAIL: could not launch isolated tests: " + Marshal.GetLastWin32Error()); return 1; }
                try
                {
                    if (WaitForSingleObject(process.Process, 30000) != 0) { Console.WriteLine("FAIL: popup tests timed out."); return 1; }
                    uint code; return GetExitCodeProcess(process.Process, out code) ? (int)code : 1;
                }
                finally { CloseHandle(process.Thread); CloseHandle(process.Process); }
            }
            finally { CloseDesktop(desktop); }
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static int RunOnDesktop()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try { Run(); Console.WriteLine("PASS: " + checks + " dropdown close, reuse, selection and ownership checks."); return 0; }
            catch (Exception error) { Console.WriteLine("FAIL after " + checks + " checks: " + error); return 1; }
        }
    }
}
