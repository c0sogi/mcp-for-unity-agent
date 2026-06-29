using System;
using System.Drawing;
using System.Windows.Forms;

namespace McpForUnityAgent;

internal static class AppWindow
{
	public static readonly Color BackColor = Color.FromArgb(30, 32, 36);

	public static readonly Color PanelBackColor = Color.FromArgb(38, 41, 46);

	public static readonly Color InputBackColor = Color.FromArgb(23, 25, 28);

	public static readonly Color TextColor = Color.FromArgb(232, 235, 239);

	public static readonly Color MutedTextColor = Color.FromArgb(154, 160, 170);

	public static readonly Color BorderColor = Color.FromArgb(78, 84, 94);

	public static readonly Color AccentColor = Color.FromArgb(88, 166, 255);

	public static readonly Color SuccessColor = Color.FromArgb(87, 196, 127);

	public static readonly Color ErrorColor = Color.FromArgb(255, 123, 114);

	public static void ApplyIcon(Form form)
	{
		if (form == null)
		{
			return;
		}
		try
		{
			Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
			if (icon != null)
			{
				form.Icon = icon;
			}
		}
		catch
		{
		}
		form.Shown += delegate
		{
			ApplyTheme(form);
		};
	}

	public static void ApplyTheme(Control root)
	{
		if (root == null)
		{
			return;
		}
		ApplyControlTheme(root);
		foreach (Control child in root.Controls)
		{
			ApplyTheme(child);
		}
	}

	public static void ApplyTheme(ContextMenuStrip menu)
	{
		if (menu == null)
		{
			return;
		}
		menu.BackColor = PanelBackColor;
		menu.ForeColor = TextColor;
		menu.RenderMode = ToolStripRenderMode.Professional;
		menu.Renderer = new DarkMenuRenderer();
		foreach (ToolStripItem item in menu.Items)
		{
			ApplyToolStripItemTheme(item);
		}
	}

	private static void ApplyControlTheme(Control control)
	{
		if (control is WebBrowser)
		{
			return;
		}
		if (control is Button button)
		{
			button.BackColor = PanelBackColor;
			button.ForeColor = TextColor;
			button.FlatStyle = FlatStyle.Flat;
			button.FlatAppearance.BorderColor = BorderColor;
			button.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 55, 63);
			button.FlatAppearance.MouseDownBackColor = Color.FromArgb(60, 66, 76);
			button.UseVisualStyleBackColor = false;
			return;
		}
		if (control is TextBoxBase || control is NumericUpDown || control is ComboBox)
		{
			control.BackColor = InputBackColor;
			control.ForeColor = TextColor;
			return;
		}
		if (control is ListView listView)
		{
			listView.BackColor = InputBackColor;
			listView.ForeColor = TextColor;
			listView.BorderStyle = BorderStyle.FixedSingle;
			return;
		}
		if (control is Form || control is UserControl || control is Panel || control is TableLayoutPanel || control is FlowLayoutPanel)
		{
			control.BackColor = BackColor;
			control.ForeColor = TextColor;
			return;
		}
		control.BackColor = BackColor;
		control.ForeColor = TextColor;
	}

	private static void ApplyToolStripItemTheme(ToolStripItem item)
	{
		item.BackColor = PanelBackColor;
		item.ForeColor = TextColor;
		if (item is ToolStripMenuItem menuItem)
		{
			foreach (ToolStripItem dropDownItem in menuItem.DropDownItems)
			{
				ApplyToolStripItemTheme(dropDownItem);
			}
		}
	}

	private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
	{
		public DarkMenuRenderer()
			: base(new DarkColorTable())
		{
		}
	}

	private sealed class DarkColorTable : ProfessionalColorTable
	{
		public override Color ToolStripDropDownBackground => PanelBackColor;

		public override Color ImageMarginGradientBegin => PanelBackColor;

		public override Color ImageMarginGradientMiddle => PanelBackColor;

		public override Color ImageMarginGradientEnd => PanelBackColor;

		public override Color MenuBorder => BorderColor;

		public override Color MenuItemBorder => AccentColor;

		public override Color MenuItemSelected => Color.FromArgb(52, 58, 67);

		public override Color MenuItemSelectedGradientBegin => Color.FromArgb(52, 58, 67);

		public override Color MenuItemSelectedGradientEnd => Color.FromArgb(52, 58, 67);

		public override Color MenuItemPressedGradientBegin => Color.FromArgb(45, 50, 58);

		public override Color MenuItemPressedGradientMiddle => Color.FromArgb(45, 50, 58);

		public override Color MenuItemPressedGradientEnd => Color.FromArgb(45, 50, 58);

		public override Color SeparatorDark => BorderColor;

		public override Color SeparatorLight => Color.FromArgb(62, 68, 78);
	}
}
