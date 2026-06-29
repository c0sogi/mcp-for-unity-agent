param(
    [string]$Configuration = "Release",
    [string]$ExePath = "",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $ExePath = Join-Path $root "bin\$Configuration\net40\McpForUnityAgent.exe"
}
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $root "artifacts\docs-build\assets\quickstart-ko"
}

if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "McpForUnityAgent.exe not found: $ExePath. Build first or pass -ExePath."
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$source = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

public static class McpForUnityDocScreenshots
{
    private static Assembly appAssembly;
    private static string outputDir;

    public static void Run(string exePath, string targetDir)
    {
        Exception error = null;
        Thread thread = new Thread(delegate()
        {
            try
            {
                RunSta(exePath, targetDir);
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null)
        {
            throw new ApplicationException(error.ToString());
        }
    }

    private static void RunSta(string exePath, string targetDir)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        appAssembly = Assembly.LoadFrom(exePath);
        outputDir = targetDir;

        CaptureTrayMenu();
        CaptureInstall();
        CaptureDependencies();
        CaptureConfigServer();
        CaptureConfigClient();
        CaptureUnityPlugin();
    }

    private static void CaptureTrayMenu()
    {
        using (Form host = new Form())
        using (ContextMenuStrip menu = new ContextMenuStrip())
        {
            host.StartPosition = FormStartPosition.Manual;
            host.Location = new Point(-3000, -3000);
            host.ShowInTaskbar = false;
            host.Size = new Size(300, 300);

            ToolStripMenuItem status = new ToolStripMenuItem("Status: running");
            status.Enabled = false;
            menu.Items.Add(status);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Start"));
            menu.Items.Add(new ToolStripMenuItem("Stop"));
            menu.Items.Add(new ToolStripMenuItem("Restart"));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Console"));
            menu.Items.Add(new ToolStripMenuItem("Port Killer"));
            menu.Items.Add(new ToolStripMenuItem("Config"));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Exit"));

            ApplyTheme(menu);
            host.Show();
            menu.Show(host, new Point(20, 20));
            Pump(80);
            menu.Size = menu.GetPreferredSize(Size.Empty);

            Bitmap bitmap = new Bitmap(menu.Width, menu.Height, PixelFormat.Format32bppArgb);
            menu.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            SaveAnnotated(bitmap, "01-tray-menu.png", new Mark[]
            {
                new Mark(ItemBounds(menu, "Start", "Restart"), "1"),
                new Mark(ItemBounds(menu, "Console", "Config"), "2")
            });
        }
    }

    private static void CaptureInstall()
    {
        Form form = (Form)Activator.CreateInstance(GetType("McpForUnityAgent.InstallForm"), true);
        try
        {
            Prepare(form);
            Control root = Root(form);
            List<TextBox> textBoxes = UserTextBoxes(form);
            SanitizeTextBoxes(form);

            Bitmap bitmap = Snapshot(root);
            SaveAnnotated(bitmap, "02-install.png", new Mark[]
            {
                new Mark(Union(ControlRect(root, textBoxes[0]), ControlRect(root, textBoxes[1])), "1"),
                new Mark(ControlRect(root, FindButton(form, "Setup Dependencies")), "2"),
                new Mark(ControlRect(root, FindButton(form, "Install")), "3")
            });
        }
        finally
        {
            form.Close();
            form.Dispose();
        }
    }

    private static void CaptureDependencies()
    {
        Type formType = GetType("McpForUnityAgent.DependencySetupForm");
        Form form = (Form)Activator.CreateInstance(formType, true);
        try
        {
            SetField(form, "checking", true);
            Prepare(form);
            SetField(form, "checking", false);

            object snapshot = Activator.CreateInstance(GetType("McpForUnityAgent.DependencySetup+DependencySnapshot"), true);
            SetField(snapshot, "UvInstalled", true);
            SetField(snapshot, "GitInstalled", false);
            SetField(snapshot, "UvInstallerAvailable", true);
            SetField(snapshot, "GitInstallerAvailable", true);
            SetField(snapshot, "UvInstalledDetail", "uv 0.7.x");
            SetField(snapshot, "GitInstalledDetail", "");
            SetField(snapshot, "UvInstallerDetail", "uv latest release found");
            SetField(snapshot, "GitInstallerDetail", "Git-2.x-64-bit.exe");
            formType.GetMethod("UpdateView", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(form, new object[] { snapshot, "Ready" });
            Pump(60);

            Control root = Root(form);
            Button installButton = null;
            foreach (Button button in FindAll<Button>(form))
            {
                if (button.Text == "Reinstall All" || button.Text == "Install Missing")
                {
                    installButton = button;
                    break;
                }
            }

            Bitmap bitmap = Snapshot(root);
            SaveAnnotated(bitmap, "03-dependencies.png", new Mark[]
            {
                new Mark(new Rectangle(88, 30, root.Width - 210, 108), "1"),
                new Mark(ControlRect(root, installButton), "2"),
                new Mark(ControlRect(root, FindButton(form, "Refresh")), "3")
            });
        }
        finally
        {
            form.Close();
            form.Dispose();
        }
    }

    private static void CaptureConfigServer()
    {
        Form form = (Form)Activator.CreateInstance(
            GetType("McpForUnityAgent.ConfigForm"),
            new object[] { CreateSampleConfig() });
        try
        {
            Prepare(form);
            Control root = Root(form);
            SanitizeTextBoxes(form);
            List<TextBox> textBoxes = UserTextBoxes(form);

            Bitmap bitmap = Snapshot(root);
            SaveAnnotated(bitmap, "04-config-server.png", new Mark[]
            {
                new Mark(Union(ControlRect(root, textBoxes[0]), ControlRect(root, textBoxes[1])), "1"),
                new Mark(Union(
                    ControlRect(root, FindCheckBox(form, "Kill port before start/restart")),
                    ControlRect(root, FindCheckBox(form, "Unity MCP session"))), "2"),
                new Mark(Union(
                    ControlRect(root, FindButton(form, "Dependencies...")),
                    ControlRect(root, FindButton(form, "Install Unity Plugin..."))), "3")
            });
        }
        finally
        {
            form.Close();
            form.Dispose();
        }
    }

    private static void CaptureConfigClient()
    {
        Form form = (Form)Activator.CreateInstance(
            GetType("McpForUnityAgent.ConfigForm"),
            new object[] { CreateSampleConfig() });
        try
        {
            Prepare(form);
            Control root = Root(form);

            Button clientTab = FindButton(form, "Client");
            clientTab.PerformClick();

            Control clientControl = null;
            foreach (Control control in AllControls(form))
            {
                if (control.GetType().FullName == "McpForUnityAgent.ClientConfigurationControl")
                {
                    clientControl = control;
                    break;
                }
            }
            clientControl.GetType().GetMethod("RefreshClients", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(clientControl, null);
            Pump(80);

            ListView listView = FindFirst<ListView>(form);
            SanitizeListPaths(listView);
            TextBox snippet = null;
            foreach (TextBox textBox in FindAll<TextBox>(form))
            {
                if (textBox.Multiline && textBox.ReadOnly)
                {
                    snippet = textBox;
                    break;
                }
            }

            Bitmap bitmap = Snapshot(root);
            SaveAnnotated(bitmap, "05-config-client.png", new Mark[]
            {
                new Mark(ControlRect(root, clientTab), "1"),
                new Mark(ControlRect(root, FindButton(form, "Configure All Detected Clients")), "2"),
                new Mark(ControlRect(root, listView), "3"),
                new Mark(ControlRect(root, snippet), "4")
            });
        }
        finally
        {
            form.Close();
            form.Dispose();
        }
    }

    private static void CaptureUnityPlugin()
    {
        Form form = (Form)Activator.CreateInstance(
            GetType("McpForUnityAgent.UnityProjectFolderForm"),
            new object[] { @"C:\Projects\MyUnityProject" });
        try
        {
            Prepare(form);
            Control root = Root(form);

            Bitmap bitmap = Snapshot(root);
            SaveAnnotated(bitmap, "06-unity-plugin.png", new Mark[]
            {
                new Mark(ControlRect(root, FindFirst<TextBox>(form)), "1"),
                new Mark(ControlRect(root, FindButton(form, "Browse...")), "2"),
                new Mark(ControlRect(root, FindButton(form, "Install")), "3")
            });
        }
        finally
        {
            form.Close();
            form.Dispose();
        }
    }

    private static object CreateSampleConfig()
    {
        object config = Activator.CreateInstance(GetType("McpForUnityAgent.AppConfig"), true);
        SetField(config, "CommandPath", @"%USERPROFILE%\.local\bin\uvx.exe");
        SetField(config, "CommandArgs", "--from \"mcpforunityserver==9.7.3\" mcp-for-unity --transport http --http-url http://127.0.0.1:8080 --project-scoped-tools");
        SetField(config, "AutoRestart", true);
        SetField(config, "RestartDelaySeconds", 5);
        SetField(config, "KillPortOnAutoRestart", true);
        SetField(config, "AutoRestartKillPort", 8080);
        SetField(config, "AutoConnectUnitySession", true);
        return config;
    }

    private static void Prepare(Form form)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-3000, -3000);
        form.ShowInTaskbar = false;
        form.Show();
        ApplyTheme(form);
        form.PerformLayout();
        foreach (Control control in AllControls(form))
        {
            control.PerformLayout();
        }
        Pump(120);
    }

    private static Control Root(Form form)
    {
        return form.Controls.Count == 0 ? form : form.Controls[0];
    }

    private static Bitmap Snapshot(Control root)
    {
        root.PerformLayout();
        Pump(40);
        Bitmap bitmap = new Bitmap(root.Width, root.Height, PixelFormat.Format32bppArgb);
        root.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        return bitmap;
    }

    private static void SaveAnnotated(Bitmap bitmap, string fileName, Mark[] marks)
    {
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (Mark mark in marks)
            {
                if (mark == null || mark.Rect.Width <= 0 || mark.Rect.Height <= 0)
                {
                    continue;
                }
                Rectangle rect = mark.Rect;
                rect.Inflate(5, 5);
                using (Pen outer = new Pen(Color.FromArgb(245, 255, 210, 56), 5))
                using (Pen inner = new Pen(Color.FromArgb(210, 20, 24, 28), 1))
                {
                    graphics.DrawRectangle(outer, rect);
                    graphics.DrawRectangle(inner, rect);
                }
                DrawBubble(graphics, mark.Label, new Point(Math.Max(6, rect.Left - 2), Math.Max(6, rect.Top - 18)));
            }
        }

        bitmap.Save(Path.Combine(outputDir, fileName), ImageFormat.Png);
        bitmap.Dispose();
    }

    private static void DrawBubble(Graphics graphics, string label, Point point)
    {
        Rectangle rect = new Rectangle(point.X, point.Y, 28, 28);
        using (Brush fill = new SolidBrush(Color.FromArgb(255, 255, 210, 56)))
        using (Pen line = new Pen(Color.FromArgb(255, 20, 24, 28), 2))
        using (Font font = new Font("Segoe UI", 11, FontStyle.Bold))
        using (Brush text = new SolidBrush(Color.FromArgb(255, 20, 24, 28)))
        {
            graphics.FillEllipse(fill, rect);
            graphics.DrawEllipse(line, rect);
            StringFormat format = new StringFormat();
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            graphics.DrawString(label, font, text, rect, format);
        }
    }

    private static Rectangle ItemBounds(ContextMenuStrip menu, string firstText, string lastText)
    {
        Rectangle result = Rectangle.Empty;
        bool active = false;
        foreach (ToolStripItem item in menu.Items)
        {
            if (item.Text == firstText)
            {
                active = true;
            }
            if (active && !(item is ToolStripSeparator))
            {
                result = result.IsEmpty ? item.Bounds : Rectangle.Union(result, item.Bounds);
            }
            if (item.Text == lastText)
            {
                break;
            }
        }
        return result;
    }

    private static Rectangle ControlRect(Control root, Control control)
    {
        if (control == null)
        {
            return Rectangle.Empty;
        }
        Point point = root.PointToClient(control.PointToScreen(Point.Empty));
        return new Rectangle(point, control.Size);
    }

    private static Rectangle Union(Rectangle left, Rectangle right)
    {
        if (left.IsEmpty)
        {
            return right;
        }
        if (right.IsEmpty)
        {
            return left;
        }
        return Rectangle.Union(left, right);
    }

    private static List<TextBox> UserTextBoxes(Control root)
    {
        List<TextBox> result = new List<TextBox>();
        foreach (TextBox textBox in FindAll<TextBox>(root))
        {
            if (!(textBox.Parent is NumericUpDown))
            {
                result.Add(textBox);
            }
        }
        return result;
    }

    private static void SanitizeTextBoxes(Control root)
    {
        foreach (TextBox textBox in FindAll<TextBox>(root))
        {
            textBox.Text = SanitizePath(textBox.Text);
        }
    }

    private static void SanitizeListPaths(ListView listView)
    {
        foreach (ListViewItem item in listView.Items)
        {
            for (int index = 0; index < item.SubItems.Count; index++)
            {
                item.SubItems[index].Text = SanitizePath(item.SubItems[index].Text);
            }
        }
    }

    private static string SanitizePath(string text)
    {
        string sanitized = text ?? string.Empty;
        sanitized = ReplaceIgnoreCase(sanitized, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%");
        sanitized = ReplaceIgnoreCase(sanitized, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "%APPDATA%");
        sanitized = ReplaceIgnoreCase(sanitized, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%");
        return sanitized;
    }

    private static string ReplaceIgnoreCase(string text, string oldValue, string newValue)
    {
        if (String.IsNullOrEmpty(text) || String.IsNullOrEmpty(oldValue))
        {
            return text;
        }
        int index = text.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return text;
        }
        return text.Substring(0, index) + newValue + text.Substring(index + oldValue.Length);
    }

    private static Button FindButton(Control root, string text)
    {
        foreach (Button button in FindAll<Button>(root))
        {
            if (button.Text == text)
            {
                return button;
            }
        }
        throw new InvalidOperationException("Button not found: " + text);
    }

    private static CheckBox FindCheckBox(Control root, string text)
    {
        foreach (CheckBox checkBox in FindAll<CheckBox>(root))
        {
            if (checkBox.Text == text)
            {
                return checkBox;
            }
        }
        throw new InvalidOperationException("CheckBox not found: " + text);
    }

    private static T FindFirst<T>(Control root) where T : Control
    {
        List<T> items = FindAll<T>(root);
        if (items.Count == 0)
        {
            throw new InvalidOperationException("Control not found: " + typeof(T).Name);
        }
        return items[0];
    }

    private static List<T> FindAll<T>(Control root) where T : Control
    {
        List<T> result = new List<T>();
        foreach (Control control in AllControls(root))
        {
            if (control is T)
            {
                result.Add((T)control);
            }
        }
        return result;
    }

    private static IEnumerable<Control> AllControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control grandChild in AllControls(child))
            {
                yield return grandChild;
            }
        }
    }

    private static void Pump(int milliseconds)
    {
        DateTime end = DateTime.Now.AddMilliseconds(milliseconds);
        do
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        while (DateTime.Now < end);
    }

    private static Type GetType(string name)
    {
        return appAssembly.GetType(name, true);
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    private static void ApplyTheme(Control control)
    {
        GetType("McpForUnityAgent.AppWindow")
            .GetMethod("ApplyTheme", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(Control) }, null)
            .Invoke(null, new object[] { control });
    }

    private static void ApplyTheme(ContextMenuStrip menu)
    {
        GetType("McpForUnityAgent.AppWindow")
            .GetMethod("ApplyTheme", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(ContextMenuStrip) }, null)
            .Invoke(null, new object[] { menu });
    }

    private sealed class Mark
    {
        public readonly Rectangle Rect;
        public readonly string Label;

        public Mark(Rectangle rect, string label)
        {
            Rect = rect;
            Label = label;
        }
    }
}
'@

Add-Type -TypeDefinition $source -ReferencedAssemblies System.Windows.Forms,System.Drawing
[McpForUnityDocScreenshots]::Run((Resolve-Path -LiteralPath $ExePath).Path, (Resolve-Path -LiteralPath $OutputDir).Path)

Get-ChildItem -LiteralPath $OutputDir -Filter *.png | Sort-Object Name | Select-Object Name, Length
