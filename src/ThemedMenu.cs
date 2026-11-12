using System;
using System.Drawing;
using System.Windows.Forms;

namespace Fleet
{
    // right-click and tray menus in the colours of the current theme
    public class ThemeColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Pal.Panel2; } }
        public override Color MenuBorder { get { return Pal.Line2; } }
        public override Color MenuItemBorder { get { return Pal.Line2; } }
        public override Color MenuItemSelected { get { return Pal.Line; } }
        public override Color MenuItemSelectedGradientBegin { get { return Pal.Line; } }
        public override Color MenuItemSelectedGradientEnd { get { return Pal.Line; } }
        public override Color MenuItemPressedGradientBegin { get { return Pal.Line; } }
        public override Color MenuItemPressedGradientEnd { get { return Pal.Line; } }
        public override Color ImageMarginGradientBegin { get { return Pal.Panel2; } }
        public override Color ImageMarginGradientMiddle { get { return Pal.Panel2; } }
        public override Color ImageMarginGradientEnd { get { return Pal.Panel2; } }
        public override Color SeparatorDark { get { return Pal.Line2; } }
        public override Color SeparatorLight { get { return Pal.Line2; } }
    }

    public static class Menus
    {
        public static void Style(ContextMenuStrip m)
        {
            m.Renderer = new ToolStripProfessionalRenderer(new ThemeColors()) { RoundedEdges = false };
            m.BackColor = Pal.Panel2;
            m.ForeColor = Pal.Text;
            m.ShowImageMargin = false;
            m.Font = new Font(Fonts.Body.FontFamily, 9.5f);
            m.Padding = new Padding(2);
            foreach (ToolStripItem i in m.Items) { i.ForeColor = Pal.Text; i.BackColor = Pal.Panel2; i.Padding = new Padding(8, 5, 8, 5); }
        }
    }
}
