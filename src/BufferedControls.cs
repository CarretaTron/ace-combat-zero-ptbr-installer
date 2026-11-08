using System.Windows.Forms;

namespace Ac0PtBrInstaller
{
    internal sealed class BufferedTableLayoutPanel : TableLayoutPanel
    {
        public BufferedTableLayoutPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            DoubleBuffered = true;
        }
    }

    internal sealed class BufferedLabel : Label
    {
        public BufferedLabel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            DoubleBuffered = true;
        }
    }
}
