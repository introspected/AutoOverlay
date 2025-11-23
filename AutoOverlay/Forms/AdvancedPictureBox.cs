using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AutoOverlay.Forms;

public class AdvancedPictureBox : PictureBox
{
    private float _zoom = 100.0f;
    private float _minZoom = 10.0f;
    private float _maxZoom = 1000.0f;
    private float _zoomStep = 10.0f;
    private PointF _offset = new(0, 0);
    private const float DefaultZoom = 100.0f;

    private bool _isDragging = false;
    private Point _lastMousePosition;

    public InterpolationMode InterpolationMode { get; set; } = InterpolationMode.HighQualityBicubic;

    public float MinZoom
    {
        get => _minZoom;
        set => _minZoom = Math.Max(0.1f, value);
    }

    public float MaxZoom
    {
        get => _maxZoom;
        set => _maxZoom = Math.Max(value, _minZoom);
    }

    public float ZoomStep
    {
        get => _zoomStep;
        set => _zoomStep = Math.Max(0.1f, value);
    }

    public AdvancedPictureBox()
    {
        this.MouseWheel += AdvancedPictureBox_MouseWheel;
        this.MouseDown += AdvancedPictureBox_MouseDown;
        this.MouseMove += AdvancedPictureBox_MouseMove;
        this.MouseUp += AdvancedPictureBox_MouseUp;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (this.Image == null)
        {
            base.OnPaint(e);
            return;
        }

        if (this.SizeMode != PictureBoxSizeMode.Zoom)
        {
            base.OnPaint(e);
            return;
        }

        e.Graphics.InterpolationMode = _isDragging ? InterpolationMode.NearestNeighbor : InterpolationMode;

        float aspectRatio = (float)this.Image.Width / this.Image.Height;
        float controlAspectRatio = (float)this.Width / this.Height;
        float scaledWidth, scaledHeight;

        if (aspectRatio > controlAspectRatio)
        {
            scaledWidth = this.Width * _zoom / 100f;
            scaledHeight = scaledWidth / aspectRatio;
        }
        else
        {
            scaledHeight = this.Height * _zoom / 100f;
            scaledWidth = scaledHeight * aspectRatio;
        }

        float x = (this.Width - scaledWidth) / 2 + _offset.X;
        float y = (this.Height - scaledHeight) / 2 + _offset.Y;

        if (scaledWidth > this.Width)
        {
            x = Math.Min(x, 0);
            x = Math.Max(x, this.Width - scaledWidth);
        }
        else
        {
            x = (this.Width - scaledWidth) / 2;
            _offset.X = 0;
        }

        if (scaledHeight > this.Height)
        {
            y = Math.Min(y, 0);
            y = Math.Max(y, this.Height - scaledHeight);
        }
        else
        {
            y = (this.Height - scaledHeight) / 2;
            _offset.Y = 0;
        }

        e.Graphics.DrawImage(this.Image, x, y, scaledWidth, scaledHeight);
    }
    private void AdvancedPictureBox_MouseWheel(object sender, MouseEventArgs e)
    {
        if (this.SizeMode != PictureBoxSizeMode.Zoom || this.Image == null)
            return;

        float scaledWidth, scaledHeight;
        float aspectRatio = (float)this.Image.Width / this.Image.Height;
        float controlAspectRatio = (float)this.Width / this.Height;

        if (aspectRatio > controlAspectRatio)
        {
            scaledWidth = this.Width * _zoom / 100f;
            scaledHeight = scaledWidth / aspectRatio;
        }
        else
        {
            scaledHeight = this.Height * _zoom / 100f;
            scaledWidth = scaledHeight * aspectRatio;
        }

        float centerX = (scaledWidth / 2f) - _offset.X;
        float centerY = (scaledHeight / 2f) - _offset.Y;

        float oldZoom = _zoom;
        if (e.Delta > 0)
            _zoom *= (1 + _zoomStep / 100f);
        else
            _zoom /= (1 + _zoomStep / 100f);

        _zoom = Math.Max(_minZoom, Math.Min(_maxZoom, _zoom));

        if (oldZoom != _zoom)
        {
            if (aspectRatio > controlAspectRatio)
            {
                scaledWidth = this.Width * _zoom / 100f;
                scaledHeight = scaledWidth / aspectRatio;
            }
            else
            {
                scaledHeight = this.Height * _zoom / 100f;
                scaledWidth = scaledHeight * aspectRatio;
            }

            float scaleChange = _zoom / oldZoom;
            _offset.X = (scaledWidth / 2f) - (centerX * scaleChange);
            _offset.Y = (scaledHeight / 2f) - (centerY * scaleChange);

            this.Invalidate();
        }
    }

    private void AdvancedPictureBox_MouseDown(object sender, MouseEventArgs e)
    {
        if (this.SizeMode != PictureBoxSizeMode.Zoom || this.Image == null)
            return;

        if (e.Button == MouseButtons.Left)
        {
            float aspectRatio = (float)this.Image.Width / this.Image.Height;
            float controlAspectRatio = (float)this.Width / this.Height;
            float scaledWidth, scaledHeight;

            if (aspectRatio > controlAspectRatio)
            {
                scaledWidth = this.Width * _zoom / 100f;
                scaledHeight = scaledWidth / aspectRatio;
            }
            else
            {
                scaledHeight = this.Height * _zoom / 100f;
                scaledWidth = scaledHeight * aspectRatio;
            }

            if (scaledWidth > this.Width || scaledHeight > this.Height)
            {
                _isDragging = true;
                _lastMousePosition = e.Location;
                this.Cursor = Cursors.Hand;
                this.Invalidate();
            }
        }
        else if (e.Button == MouseButtons.Middle)
        {
            _zoom = DefaultZoom;
            _offset = new PointF(0, 0);
            this.Invalidate();
        }
    }

    private void AdvancedPictureBox_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            _offset.X += e.X - _lastMousePosition.X;
            _offset.Y += e.Y - _lastMousePosition.Y;
            _lastMousePosition = e.Location;
            this.Invalidate();
        }
    }
    private void AdvancedPictureBox_MouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isDragging = false;
            this.Cursor = Cursors.Default;
            this.Invalidate();
        }
    }
}