using System.Windows.Forms.VisualStyles;

namespace MyKa.Spoofer.Identity;

/// <summary>The terminal's dark, monospaced palette on standard accessible Windows controls.</summary>
internal static class IdentityTheme
{
    public static Color Background => SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(12, 15, 20);
    public static Color Surface => SystemInformation.HighContrast ? SystemColors.Control : Color.FromArgb(23, 29, 38);
    public static Color Foreground => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(225, 231, 239);
    public static Color Dim => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(167, 179, 195);
    public static Color Accent => SystemInformation.HighContrast ? SystemColors.Highlight : Color.Cyan;
    public static Color SelectedText => SystemInformation.HighContrast ? SystemColors.HighlightText : Color.Black;
    public static Color Warning => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(255, 210, 95);

    public static void Apply(Control control)
    {
        control.BackColor = Background;
        control.ForeColor = Foreground;
        switch (control)
        {
            case Button button:
                button.FlatStyle = FlatStyle.Flat;
                button.UseVisualStyleBackColor = false;
                button.BackColor = Surface;
                button.FlatAppearance.BorderColor = Dim;
                button.FlatAppearance.MouseOverBackColor = SystemInformation.HighContrast ? SystemColors.Control : Color.FromArgb(38, 50, 64);
                break;
            case TextBox text:
                text.BackColor = Surface;
                text.BorderStyle = BorderStyle.FixedSingle;
                break;
            case CheckBox box:
                box.FlatStyle = FlatStyle.Standard;
                break;
            case DataGridView grid:
                grid.BackgroundColor = Background;
                grid.BorderStyle = BorderStyle.None;
                grid.EnableHeadersVisualStyles = false;
                grid.GridColor = Surface;
                grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Surface, ForeColor = Accent, SelectionBackColor = Surface,
                    SelectionForeColor = Accent, Padding = new Padding(6), WrapMode = DataGridViewTriState.False,
                };
                grid.DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Background, ForeColor = Foreground, SelectionBackColor = Accent,
                    SelectionForeColor = SelectedText, Padding = new Padding(4),
                };
                grid.AlternatingRowsDefaultCellStyle.BackColor = Surface;
                grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
                SizeGridText(grid);
                grid.FontChanged += (_, _) => SizeGridText(grid);
                break;
        }
        foreach (Control child in control.Controls) Apply(child);
    }

    private static void SizeGridText(DataGridView grid)
    {
        grid.RowTemplate.Height = grid.Font.Height + 14;
        foreach (DataGridViewRow row in grid.Rows) row.Height = grid.RowTemplate.Height;
        grid.ColumnHeadersHeight = grid.Font.Height + 20;
    }

    public static void DrawTab(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tabs || e.Index < 0 || e.Index >= tabs.TabCount) return;
        var selected = e.Index == tabs.SelectedIndex;
        using var background = new SolidBrush(selected ? Accent : Surface);
        e.Graphics.FillRectangle(background, e.Bounds);
        TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, e.Bounds,
            selected ? SelectedText : Foreground, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (selected && tabs.Focused)
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(e.Bounds, -3, -3), SelectedText, Accent);
    }
}

/// <summary>Keeps disabled labels readable against the dark surface while retaining native button behavior.</summary>
internal sealed class IdentityButton : Button
{
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Enabled || SystemInformation.HighContrast) { base.OnPaint(e); return; }
        e.Graphics.Clear(BackColor);
        ControlPaint.DrawBorder(e.Graphics, ClientRectangle, IdentityTheme.Dim, ButtonBorderStyle.Solid);
        var area = Rectangle.Inflate(ClientRectangle, -8, -4);
        TextRenderer.DrawText(e.Graphics, Text, Font, area, IdentityTheme.Dim,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak
            | (ShowKeyboardCues ? TextFormatFlags.Default : TextFormatFlags.HidePrefix));
    }
}

/// <summary>Uses a native disabled check glyph with a readable caption on the dark surface.</summary>
internal sealed class IdentityCheckBox : CheckBox
{
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Enabled || SystemInformation.HighContrast) { base.OnPaint(e); return; }
        e.Graphics.Clear(BackColor);
        var state = Checked ? CheckBoxState.CheckedDisabled : CheckBoxState.UncheckedDisabled;
        var glyph = CheckBoxRenderer.GetGlyphSize(e.Graphics, state);
        CheckBoxRenderer.DrawCheckBox(e.Graphics, new Point(0, Math.Max(0, (Height - glyph.Height) / 2)), state);
        var gap = (int)Math.Ceiling(3 * DeviceDpi / 96f);
        var area = new Rectangle(glyph.Width + gap, 0, Math.Max(0, Width - glyph.Width - gap), Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, area, IdentityTheme.Dim,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding
            | (ShowKeyboardCues ? TextFormatFlags.Default : TextFormatFlags.HidePrefix));
    }
}

/// <summary>Paints the unused tab strip too; the standard control still owns selection and accessibility.</summary>
internal sealed class IdentityTabs : TabControl
{
    public IdentityTabs()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        SizeMode = TabSizeMode.Fixed;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(IdentityTheme.Background);
        for (var index = 0; index < TabCount; index++)
            IdentityTheme.DrawTab(this, new DrawItemEventArgs(e.Graphics, Font, GetTabRect(index), index,
                index == SelectedIndex ? DrawItemState.Selected : DrawItemState.None));
        var border = Rectangle.Inflate(DisplayRectangle, 2, 2);
        ControlPaint.DrawBorder(e.Graphics, border, IdentityTheme.Dim, ButtonBorderStyle.Solid);
    }
}
