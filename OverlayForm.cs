using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace FastSnip;

enum Tool { Pencil, Line, Ellipse }

enum Outcome { Cancel, Copy, Save }

/// <summary>A drawn mark. Pencil strokes keep every point in <see cref="Path"/>; other tools use A/B.</summary>
record struct Shape(Tool Tool, Point A, Point B, Color Color, int Size, List<Point>? Path = null);

sealed class ToolButton
{
    public Rectangle Bounds;
    public string? Text;
    public Color? Swatch;
    public bool IsSlider;
    public Action Click = () => { };
    public Func<bool> IsActive = () => false;
}

/// <summary>
/// Full-virtual-screen window showing a frozen screenshot. Created once at startup and
/// only shown/hidden afterwards, so a capture never pays for window creation.
/// Flow: drag to select → annotate inside the selection → Enter/✓ copies, Ctrl+S saves.
/// </summary>
sealed class OverlayForm : Form
{
    const int WS_EX_TOOLWINDOW = 0x80;
    const int WM_DPICHANGED = 0x02E0;
    const int MinSelection = 3;
    const int ButtonHeight = 30;
    const int ToolbarGap = 6;
    const int MinSize = 1;
    const int MaxSize = 30;
    const int SliderWidth = 150;
    const int SliderLabelWidth = 42;

    static readonly Color[] Palette =
    {
        Color.FromArgb(235, 40, 40), Color.FromArgb(255, 200, 0),
        Color.FromArgb(0, 153, 255), Color.FromArgb(40, 190, 70),
    };

    static readonly Pen BorderPen = new(Color.FromArgb(0, 153, 255), 1);
    static readonly SolidBrush DimBrush = new(Color.FromArgb(110, 0, 0, 0));
    static readonly SolidBrush LabelBack = new(Color.FromArgb(200, 0, 0, 0));
    static readonly SolidBrush ToolbarBack = new(Color.FromArgb(245, 32, 32, 36));
    static readonly SolidBrush ActiveBack = new(Color.FromArgb(0, 120, 215));
    static readonly Font LabelFont = new("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
    static readonly Font ButtonFont = new("Microsoft JhengHei UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);

    readonly AppSettings settings;

    Bitmap? shot;
    Bitmap? dimmed;
    Point dragStart;
    Rectangle selection;
    bool selecting;
    bool editing;

    Tool tool = Tool.Line;
    Color color = Palette[0];
    // Each tool remembers its own stroke size; the slider edits the current tool's entry.
    readonly Dictionary<Tool, int> sizes = new() { [Tool.Pencil] = 1, [Tool.Line] = 3, [Tool.Ellipse] = 3 };
    bool draggingSlider;
    readonly List<Shape> shapes = new();
    Shape? drawing;
    readonly List<ToolButton> buttons = new();
    Rectangle toolbarRect;

    /// <summary>Raised with the file path after a PNG is written.</summary>
    public event Action<string>? Saved;

    /// <summary>Raised with the error message when writing a PNG fails.</summary>
    public event Action<string>? SaveFailed;

    public OverlayForm(AppSettings settings)
    {
        this.settings = settings;
        Text = $"{AppInfo.Name} overlay";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        Cursor = Cursors.Cross;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW; // keep out of Alt+Tab
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        // The overlay spans monitors and works in physical pixels; never let a DPI change resize it.
        if (m.Msg == WM_DPICHANGED) { m.Result = IntPtr.Zero; return; }
        base.WndProc(ref m);
    }

    /// <summary>Exercise the capture and paint paths once so the first real capture isn't JIT-bound.</summary>
    public void Warmup()
    {
        using var bmp = new Bitmap(16, 16, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(0, 0, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
        g.FillRectangle(DimBrush, 0, 0, 16, 16);
        g.DrawImage(bmp, new Rectangle(0, 0, 8, 8), new Rectangle(0, 0, 8, 8), GraphicsUnit.Pixel);
        TextRenderer.MeasureText("0 × 0", LabelFont);
        TextRenderer.MeasureText(L.ToolLine, ButtonFont);
    }

    public void StartCapture()
    {
        if (Visible) return;

        var virt = SystemInformation.VirtualScreen;
        shot = new Bitmap(virt.Width, virt.Height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(shot))
            g.CopyFromScreen(virt.Location, Point.Empty, virt.Size, CopyPixelOperation.SourceCopy);

        dimmed = new Bitmap(virt.Width, virt.Height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(dimmed))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImageUnscaled(shot, 0, 0);
            g.CompositingMode = CompositingMode.SourceOver;
            g.FillRectangle(DimBrush, 0, 0, virt.Width, virt.Height);
        }

        ResetState();
        Bounds = virt;
        Show();
        Activate();
    }

    void ResetState()
    {
        selection = Rectangle.Empty;
        selecting = editing = draggingSlider = false;
        shapes.Clear();
        drawing = null;
        buttons.Clear();
        toolbarRect = Rectangle.Empty;
        Cursor = Cursors.Cross;
    }

    void Finish(Outcome outcome)
    {
        var picked = selection;
        var drawn = shapes.ToArray();
        Hide();
        if (outcome != Outcome.Cancel && shot != null && picked.Width >= MinSelection && picked.Height >= MinSelection)
        {
            using var crop = RenderCrop(shot, picked, drawn);
            CopyToClipboard(crop);
            if (outcome == Outcome.Save || settings.AutoSave) SaveToFolder(crop);
        }

        shot?.Dispose();
        dimmed?.Dispose();
        shot = dimmed = null;
        ResetState();
        // Nothing from a finished capture is kept; reclaim it now (overlay is already hidden) so idle footprint stays flat.
        GC.Collect();
    }

    static Bitmap RenderCrop(Bitmap source, Rectangle area, Shape[] drawn)
    {
        // 24bpp so apps that read CF_DIB don't misinterpret an alpha channel.
        var crop = new Bitmap(area.Width, area.Height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(crop);
        g.DrawImage(source, new Rectangle(0, 0, area.Width, area.Height), area, GraphicsUnit.Pixel);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TranslateTransform(-area.X, -area.Y);
        foreach (var s in drawn) DrawShape(g, s);
        return crop;
    }

    static void CopyToClipboard(Bitmap image)
    {
        var data = new DataObject();
        data.SetImage(image);
        var png = new MemoryStream();
        image.Save(png, ImageFormat.Png);
        data.SetData("PNG", false, png);
        try
        {
            Clipboard.SetDataObject(data, true, 10, 50);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException or System.Threading.ThreadStateException)
        {
            // Clipboard held by another process; nothing sensible to do but drop this copy.
        }
    }

    void SaveToFolder(Bitmap image)
    {
        try
        {
            Directory.CreateDirectory(settings.SaveFolder);
            string path = Path.Combine(settings.SaveFolder, $"{AppInfo.Name}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
            image.Save(path, ImageFormat.Png);
            Saved?.Invoke(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                       or NotSupportedException or System.Runtime.InteropServices.ExternalException)
        {
            SaveFailed?.Invoke(ex.Message);
        }
    }

    static void DrawShape(Graphics g, Shape s)
    {
        if (s.Tool == Tool.Pencil && s.Path is { } path)
        {
            // Pixel-exact, no anti-aliasing: stamp a Size×Size square on every pixel of the stroke,
            // so at size 1 a click sets exactly one pixel.
            var smoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;
            using var brush = new SolidBrush(s.Color);
            g.FillRectangles(brush, PencilStamps(path, s.Size).ToArray());
            g.SmoothingMode = smoothing;
            return;
        }

        using var pen = new Pen(s.Color, s.Size) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        if (s.Tool == Tool.Line)
            g.DrawLine(pen, s.A, s.B);
        else
            g.DrawEllipse(pen, Rectangle.FromLTRB(
                Math.Min(s.A.X, s.B.X), Math.Min(s.A.Y, s.B.Y), Math.Max(s.A.X, s.B.X), Math.Max(s.A.Y, s.B.Y)));
    }

    /// <summary>Bresenham walk through the stroke's points, one square per visited pixel.</summary>
    static List<Rectangle> PencilStamps(List<Point> path, int size)
    {
        var stamps = new List<Rectangle>();
        int offset = (size - 1) / 2;
        void Stamp(int x, int y) => stamps.Add(new Rectangle(x - offset, y - offset, size, size));

        Stamp(path[0].X, path[0].Y);
        for (int i = 1; i < path.Count; i++)
        {
            int x0 = path[i - 1].X, y0 = path[i - 1].Y, x1 = path[i].X, y1 = path[i].Y;
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (x0 != x1 || y0 != y1)
            {
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
                Stamp(x0, y0);
            }
        }
        return stamps;
    }

    /// <summary>Shift constrains lines to 45° steps and ellipses to circles.</summary>
    static Point Constrain(Tool t, Point a, Point b)
    {
        if ((ModifierKeys & Keys.Shift) == 0) return b;
        int dx = b.X - a.X, dy = b.Y - a.Y;
        if (t == Tool.Ellipse)
        {
            int side = Math.Max(Math.Abs(dx), Math.Abs(dy));
            return new Point(a.X + Math.Sign(dx) * side, a.Y + Math.Sign(dy) * side);
        }
        double angle = Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4)) * (Math.PI / 4);
        double len = Math.Sqrt(dx * dx + dy * dy);
        return new Point(a.X + (int)Math.Round(len * Math.Cos(angle)), a.Y + (int)Math.Round(len * Math.Sin(angle)));
    }

    static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

    void EnterEditing()
    {
        editing = true;
        buttons.Clear();
        buttons.Add(new ToolButton { Text = L.ToolPencil, Click = () => tool = Tool.Pencil, IsActive = () => tool == Tool.Pencil });
        buttons.Add(new ToolButton { Text = L.ToolLine, Click = () => tool = Tool.Line, IsActive = () => tool == Tool.Line });
        buttons.Add(new ToolButton { Text = L.ToolEllipse, Click = () => tool = Tool.Ellipse, IsActive = () => tool == Tool.Ellipse });
        foreach (var c in Palette)
            buttons.Add(new ToolButton { Swatch = c, Click = () => color = c, IsActive = () => color == c });
        buttons.Add(new ToolButton { IsSlider = true });
        buttons.Add(new ToolButton { Text = L.ToolUndo, Click = Undo });
        buttons.Add(new ToolButton { Text = L.ToolSave, Click = () => Finish(Outcome.Save) });
        buttons.Add(new ToolButton { Text = "✕", Click = () => Finish(Outcome.Cancel) });
        buttons.Add(new ToolButton { Text = L.ToolCopy, Click = () => Finish(Outcome.Copy) });

        int x = 0;
        foreach (var b in buttons)
        {
            int w = b.IsSlider ? SliderWidth
                : b.Swatch != null ? ButtonHeight
                : Math.Max(ButtonHeight, TextRenderer.MeasureText(b.Text, ButtonFont).Width + 16);
            b.Bounds = new Rectangle(x, 0, w, ButtonHeight);
            x += w + 2;
        }
        var size = new Size(x + 2, ButtonHeight + 4);

        // Prefer below the selection, then above, then tucked inside its bottom edge.
        int top = selection.Bottom + ToolbarGap;
        if (top + size.Height > ClientSize.Height) top = selection.Top - ToolbarGap - size.Height;
        if (top < 0) top = selection.Bottom - size.Height - ToolbarGap;
        int left = Math.Max(0, Math.Min(selection.Right - size.Width, ClientSize.Width - size.Width));
        toolbarRect = new Rectangle(left, top, size.Width, size.Height);
        foreach (var b in buttons) b.Bounds.Offset(left + 2, top + 2);

        Invalidate(EditArea());
    }

    void Undo()
    {
        if (shapes.Count > 0) shapes.RemoveAt(shapes.Count - 1);
    }

    static Rectangle SliderTrack(Rectangle bounds) =>
        new(bounds.X + SliderLabelWidth, bounds.Y, bounds.Width - SliderLabelWidth - 8, bounds.Height);

    void SetSize(int value)
    {
        sizes[tool] = Clamp(value, MinSize, MaxSize);
        Invalidate(toolbarRect);
    }

    void SetSizeFromSlider(int mouseX)
    {
        var slider = buttons.Find(b => b.IsSlider);
        if (slider == null) return;
        var track = SliderTrack(slider.Bounds);
        double t = Math.Max(0, Math.Min(1, (mouseX - track.Left) / (double)track.Width));
        SetSize(MinSize + (int)Math.Round(t * (MaxSize - MinSize)));
    }

    Rectangle EditArea() => Rectangle.Union(Rectangle.Inflate(selection, 4, 4), toolbarRect);

    Rectangle LabelRect(Rectangle sel)
    {
        if (sel.IsEmpty) return Rectangle.Empty;
        var size = TextRenderer.MeasureText(LabelText(sel), LabelFont);
        size.Width += 8;
        size.Height += 4;
        int y = sel.Top - size.Height - 4;
        if (y < 0) y = sel.Top + 4;
        return new Rectangle(sel.Left, y, size.Width, size.Height);
    }

    static string LabelText(Rectangle sel) => $"{sel.Width} × {sel.Height}";

    Rectangle DirtyArea(Rectangle sel) =>
        sel.IsEmpty ? Rectangle.Empty : Rectangle.Union(Rectangle.Inflate(sel, 2, 2), LabelRect(sel));

    protected override void OnPaint(PaintEventArgs e)
    {
        if (dimmed == null || shot == null) return;
        var g = e.Graphics;
        var clip = e.ClipRectangle;
        g.CompositingMode = CompositingMode.SourceCopy;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImage(dimmed, clip, clip, GraphicsUnit.Pixel);

        if (selection.Width <= 0 || selection.Height <= 0) return;

        var lit = Rectangle.Intersect(selection, clip);
        if (!lit.IsEmpty) g.DrawImage(shot, lit, lit, GraphicsUnit.Pixel);

        g.CompositingMode = CompositingMode.SourceOver;
        if (shapes.Count > 0 || drawing != null)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.SetClip(selection);
            foreach (var s in shapes) DrawShape(g, s);
            if (drawing is Shape d) DrawShape(g, d);
            g.ResetClip();
            g.SmoothingMode = SmoothingMode.None;
        }
        g.DrawRectangle(BorderPen, selection.X, selection.Y, selection.Width - 1, selection.Height - 1);

        if (editing) PaintToolbar(g);
        else
        {
            var label = LabelRect(selection);
            g.FillRectangle(LabelBack, label);
            TextRenderer.DrawText(g, LabelText(selection), LabelFont, label, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    void PaintToolbar(Graphics g)
    {
        g.FillRectangle(ToolbarBack, toolbarRect);
        foreach (var b in buttons)
        {
            if (b.IsSlider)
            {
                PaintSlider(g, b.Bounds);
                continue;
            }
            if (b.IsActive()) g.FillRectangle(ActiveBack, b.Bounds);
            if (b.Swatch is Color c)
            {
                using var brush = new SolidBrush(c);
                g.FillRectangle(brush, Rectangle.Inflate(b.Bounds, -7, -7));
            }
            else
            {
                TextRenderer.DrawText(g, b.Text, ButtonFont, b.Bounds, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }

    void PaintSlider(Graphics g, Rectangle bounds)
    {
        int size = sizes[tool];
        var labelRect = new Rectangle(bounds.X, bounds.Y, SliderLabelWidth, bounds.Height);
        TextRenderer.DrawText(g, $"{size}px", ButtonFont, labelRect, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        var track = SliderTrack(bounds);
        int mid = track.Y + track.Height / 2;
        using (var trackBrush = new SolidBrush(Color.FromArgb(110, 110, 120)))
            g.FillRectangle(trackBrush, track.Left, mid - 2, track.Width, 4);
        int knobX = track.Left + (int)Math.Round((size - MinSize) / (double)(MaxSize - MinSize) * track.Width);
        g.FillRectangle(ActiveBack, track.Left, mid - 2, knobX - track.Left, 4);

        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillEllipse(Brushes.White, knobX - 7, mid - 7, 14, 14);
        g.SmoothingMode = smoothing;
    }

    ToolButton? HitButton(Point p)
    {
        foreach (var b in buttons)
            if (b.Bounds.Contains(p)) return b;
        return null;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right) { Finish(Outcome.Cancel); return; }
        if (e.Button != MouseButtons.Left) return;

        if (editing)
        {
            if (HitButton(e.Location) is ToolButton b)
            {
                if (b.IsSlider)
                {
                    draggingSlider = true;
                    Capture = true;
                    SetSizeFromSlider(e.X);
                    return;
                }
                b.Click();
                if (Visible) Invalidate(EditArea());
            }
            else if (selection.Contains(e.Location))
            {
                drawing = new Shape(tool, e.Location, e.Location, color, sizes[tool],
                    tool == Tool.Pencil ? new List<Point> { e.Location } : null);
                Invalidate(Rectangle.Inflate(selection, 4, 4));
                Capture = true;
            }
            return;
        }

        selecting = true;
        dragStart = e.Location;
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (editing)
        {
            if (draggingSlider)
            {
                SetSizeFromSlider(e.X);
            }
            else if (drawing is Shape d)
            {
                if (d.Path is { } path)
                {
                    if (path[path.Count - 1] != e.Location) path.Add(e.Location);
                }
                else drawing = d with { B = Constrain(d.Tool, d.A, e.Location) };
                Invalidate(Rectangle.Inflate(selection, 4, 4));
            }
            else
            {
                Cursor = toolbarRect.Contains(e.Location) ? Cursors.Hand : Cursors.Cross;
            }
            return;
        }

        if (!selecting) return;
        var next = Rectangle.FromLTRB(
            Math.Min(dragStart.X, e.X), Math.Min(dragStart.Y, e.Y),
            Math.Max(dragStart.X, e.X) + 1, Math.Max(dragStart.Y, e.Y) + 1);
        if (next == selection) return;
        var dirty = Rectangle.Union(DirtyArea(selection), DirtyArea(next));
        selection = next;
        Invalidate(dirty);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        Capture = false;

        if (editing)
        {
            draggingSlider = false;
            if (drawing is Shape d)
            {
                drawing = null;
                if (d.Tool == Tool.Pencil || Math.Abs(d.B.X - d.A.X) + Math.Abs(d.B.Y - d.A.Y) > 2) shapes.Add(d);
                Invalidate(Rectangle.Inflate(selection, 4, 4));
            }
            return;
        }

        if (!selecting) return;
        selecting = false;
        if (selection.Width < MinSelection || selection.Height < MinSelection)
        {
            Finish(Outcome.Cancel);
            return;
        }
        if (!settings.EditAfterSelect)
        {
            Finish(Outcome.Copy);
            return;
        }
        Invalidate(DirtyArea(selection));
        EnterEditing();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Escape:
                Finish(Outcome.Cancel);
                break;
            case Keys.Enter when editing:
            case Keys.C when editing && e.Control:
                Finish(Outcome.Copy);
                break;
            case Keys.S when editing && e.Control:
                Finish(Outcome.Save);
                break;
            case Keys.Z when editing && e.Control:
                Undo();
                Invalidate(EditArea());
                break;
            case Keys.OemOpenBrackets when editing:
                SetSize(sizes[tool] - 1);
                break;
            case Keys.OemCloseBrackets when editing:
                SetSize(sizes[tool] + 1);
                break;
            case Keys.P when editing && !e.Control:
                tool = Tool.Pencil;
                Invalidate(toolbarRect);
                break;
            case Keys.L when editing && !e.Control:
                tool = Tool.Line;
                Invalidate(toolbarRect);
                break;
            case Keys.O when editing && !e.Control:
                tool = Tool.Ellipse;
                Invalidate(toolbarRect);
                break;
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (editing && drawing == null) SetSize(sizes[tool] + Math.Sign(e.Delta));
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (Visible) Finish(Outcome.Cancel);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Only the tray's Exit should destroy the overlay; anything else (Alt+F4) just cancels.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Finish(Outcome.Cancel);
        }
        base.OnFormClosing(e);
    }
}
