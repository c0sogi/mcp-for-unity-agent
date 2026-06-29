using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace McpForUnityAgent;

internal static class Program
{
	private const string MutexName = "Local\\McpForUnityAgent.WinForms";

	[STAThread]
	private static void Main(string[] args)
	{
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
		{
			AppPaths.WriteTrayError(e.Exception.ToString());
			MessageBox.Show("McpForUnity Agent failed.\r\n\r\n" + e.Exception.Message, "McpForUnity Agent", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		};
		AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
		{
			AppPaths.WriteTrayError((!(e.ExceptionObject is Exception ex)) ? Convert.ToString(e.ExceptionObject) : ex.ToString());
		};
		if (HasArg(args, "--check"))
		{
			return;
		}
		if (HasArg(args, "--dependency-check"))
		{
			NativeConsole.AttachParent();
			Environment.Exit(DependencySetup.RunDiagnostics(Console.Out, HasArg(args, "--simulate-missing-dependencies")));
			return;
		}
		if (HasArg(args, "--uninstall"))
		{
			using (UninstallForm mainForm2 = new UninstallForm())
			{
				Application.Run(mainForm2);
				return;
			}
		}
		if (HasArg(args, "--install") || IsSetupExecutable())
		{
			using (InstallForm mainForm = new InstallForm())
			{
				Application.Run(mainForm);
				return;
			}
		}
		bool createdNew;
		Mutex mutex = new Mutex(initiallyOwned: true, "Local\\McpForUnityAgent.WinForms", out createdNew);
		if (!createdNew)
		{
			MessageBox.Show("McpForUnity Agent is already running.\r\n\r\nUse the tray icon menu.", "McpForUnity Agent", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			mutex.Dispose();
			return;
		}
		try
		{
			Application.Run(new TrayAppContext());
		}
		finally
		{
			mutex.ReleaseMutex();
			mutex.Dispose();
		}
	}

	private static bool HasArg(string[] args, string expected)
	{
		foreach (string a in args)
		{
			if (string.Equals(a, expected, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsSetupExecutable()
	{
		string name = Path.GetFileNameWithoutExtension(Application.ExecutablePath);
		return name.EndsWith("Setup", StringComparison.OrdinalIgnoreCase)
			|| name.EndsWith("Installer", StringComparison.OrdinalIgnoreCase);
	}

	private static class NativeConsole
	{
		private const int AttachParentProcess = -1;

		[DllImport("kernel32.dll")]
		private static extern bool AttachConsole(int dwProcessId);

		public static void AttachParent()
		{
			try
			{
				AttachConsole(AttachParentProcess);
				Stream standardOutput = Console.OpenStandardOutput();
				Stream standardError = Console.OpenStandardError();
				Console.SetOut(new StreamWriter(standardOutput, Encoding.UTF8) { AutoFlush = true });
				Console.SetError(new StreamWriter(standardError, Encoding.UTF8) { AutoFlush = true });
			}
			catch
			{
			}
		}
	}
}

