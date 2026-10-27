using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

public sealed class TimerDisplay : Control
{
    readonly Timer animation = new Timer();
    readonly Stopwatch elapsed = new Stopwatch();
    string previous = "", current = "";
    bool flip;
    public bool Compact { get; set; }
    public bool Flip { get { return flip; } set { flip = value; animation.Stop(); elapsed.Reset(); previous = current; Invalidate(); } }
    public TimerDisplay() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.StandardDoubleClick, true);
        animation.Interval = 16;
        animation.Tick += delegate { if (elapsed.ElapsedMilliseconds >= 420) { animation.Stop(); previous = current; } Invalidate(); };
    }
    protected override void OnTextChanged(EventArgs e) {
        previous = current; current = Text ?? "";
        if (flip && previous.Length == current.Length && previous != current) { elapsed.Restart(); animation.Start(); }
        else { animation.Stop(); elapsed.Reset(); previous = current; }
        Invalidate(); base.OnTextChanged(e);
    }
    protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.Clear(BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        float scale = Math.Min(Width / 220f, Height / 100f);
        g.TranslateTransform((Width - 220 * scale) / 2, (Height - 100 * scale) / 2); g.ScaleTransform(scale, scale);
        if (Compact) using (GraphicsPath p = new GraphicsPath()) using (Pen pen = new Pen(Color.FromArgb(53,52,47))) {
            p.AddArc(.5f,.5f,18,18,180,90); p.AddArc(201.5f,.5f,18,18,270,90); p.AddArc(201.5f,81.5f,18,18,0,90); p.AddArc(.5f,81.5f,18,18,90,90); p.CloseFigure(); g.DrawPath(pen,p);
        }
        float x = 53; double t = Math.Min(1, elapsed.Elapsed.TotalMilliseconds / 420);
        for (int i=0;i<current.Length;i++) {
            char c=current[i]; int width=c==':'?10:24;
            bool moving=flip && animation.Enabled && i<previous.Length && previous[i]!=c && c!=':';
            if (!moving) DrawGlyph(g,c,new RectangleF(x,0,width,100));
            else using(Bitmap old=Glyph(previous[i],width)) using(Bitmap next=Glyph(c,width)) {
                // New upper half and old lower half stay behind the rotating leaf.
                g.DrawImage(next,new RectangleF(x,0,width,50),new RectangleF(0,0,width*3,150),GraphicsUnit.Pixel);
                g.DrawImage(old,new RectangleF(x,50,width,50),new RectangleF(0,150,width*3,150),GraphicsUnit.Pixel);
                if(t<.5) { float h=(float)(50*Math.Cos(t*Math.PI)); if(h>.1f) g.DrawImage(old,new RectangleF(x,50-h,width,h),new RectangleF(0,0,width*3,150),GraphicsUnit.Pixel); }
                else { float h=(float)(50*Math.Sin((t-.5)*Math.PI)); if(h>.1f) g.DrawImage(next,new RectangleF(x,50,width,h),new RectangleF(0,150,width*3,150),GraphicsUnit.Pixel); }
            }
            x+=width+2;
        }
    }
    Bitmap Glyph(char c,int width) { Bitmap b=new Bitmap(width*3,300); using(Graphics g=Graphics.FromImage(b)){g.Clear(BackColor);g.ScaleTransform(3,3);g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;DrawGlyph(g,c,new RectangleF(0,0,width,100));}return b; }
    void DrawGlyph(Graphics g,char c,RectangleF rect) {
        using(Font f=new Font("Bahnschrift SemiCondensed",c==':'?30:44,FontStyle.Regular,GraphicsUnit.Pixel))
        using(Brush b=new SolidBrush(ForeColor)) using(StringFormat fmt=new StringFormat(StringFormat.GenericTypographic)) {
            fmt.Alignment=StringAlignment.Center;fmt.LineAlignment=StringAlignment.Center;if(c!=':')rect.Y+=1;g.DrawString(c.ToString(),f,b,rect,fmt);
        }
    }
}
