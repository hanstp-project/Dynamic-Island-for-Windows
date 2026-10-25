using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WindowsDynamicIsland.Services;

public enum RetroFilter
{
    Normal,
    Warm90s,
    Cyberpunk,
    Monochrome,
    Sepia,
    Vibrant
}

/// <summary>
/// Service providing Retro Instant Camera preview frames, real webcam capture (with MediaCapture),
/// authentic Polaroid/Instax photo rendering, and auto-saving to the user's Pictures library.
/// </summary>
public sealed class CameraService : IDisposable
{
    private readonly DispatcherTimer _renderTimer;
    private int _frameCounter;
    private readonly Random _random = new();

    private Windows.Media.Capture.MediaCapture? _mediaCapture;
    private Windows.Media.Capture.Frames.MediaFrameReader? _frameReader;
    private volatile bool _isWebcamInitialized;
    private volatile bool _isWebcamInitializing;
    private volatile bool _isStreaming;
    private volatile bool _isProcessingFrame;
    private BitmapSource? _latestWebcamFrame;

    public RetroFilter CurrentFilter { get; set; } = RetroFilter.Warm90s;
    public string CurrentDateStamp => DateTime.Now.ToString("yy MM dd", CultureInfo.InvariantCulture).Insert(0, "'");
    public bool HasRealWebcam => _isWebcamInitialized && _latestWebcamFrame != null;

    public bool IsRunning => _isStreaming;
    public event Action<BitmapSource>? FrameArrived;

    public CameraService()
    {
        _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(45) // ~22 FPS for retro feel & low CPU
        };
        _renderTimer.Tick += (s, e) => RenderNextFrame();

        // Attempt async webcam initialization in background
        _ = TryInitializeWebcamAsync();
    }

    private async Task TryInitializeWebcamAsync()
    {
        if (_isWebcamInitialized || _isWebcamInitializing) return;
        _isWebcamInitializing = true;

        try
        {
            var frameSourceGroups = await Windows.Media.Capture.Frames.MediaFrameSourceGroup.FindAllAsync();
            var selectedGroup = frameSourceGroups.FirstOrDefault(g => g.SourceInfos.Any(s =>
                s.MediaStreamType == Windows.Media.Capture.MediaStreamType.VideoPreview ||
                s.MediaStreamType == Windows.Media.Capture.MediaStreamType.VideoRecord));

            if (selectedGroup != null)
            {
                var frameSettings = new Windows.Media.Capture.MediaCaptureInitializationSettings
                {
                    SourceGroup = selectedGroup,
                    SharingMode = Windows.Media.Capture.MediaCaptureSharingMode.SharedReadOnly,
                    MemoryPreference = Windows.Media.Capture.MediaCaptureMemoryPreference.Cpu,
                    StreamingCaptureMode = Windows.Media.Capture.StreamingCaptureMode.Video
                };

                var capture = new Windows.Media.Capture.MediaCapture();
                await capture.InitializeAsync(frameSettings);

                var frameSource = capture.FrameSources.Values.FirstOrDefault(fs =>
                    fs.Info.MediaStreamType == Windows.Media.Capture.MediaStreamType.VideoPreview ||
                    fs.Info.MediaStreamType == Windows.Media.Capture.MediaStreamType.VideoRecord);

                if (frameSource != null)
                {
                    _frameReader = await capture.CreateFrameReaderAsync(frameSource);
                    _frameReader.FrameArrived += OnWebcamFrameArrived;
                    if (_isStreaming)
                    {
                        await _frameReader.StartAsync();
                    }
                }

                _mediaCapture = capture;
                _isWebcamInitialized = true;
            }
        }
        catch (Exception ex)
        {
            Log.Warn("CameraService webcam initialization failed: " + ex.Message, ex);
            _mediaCapture = null;
            _frameReader = null;
            _isWebcamInitialized = false;
        }
        finally
        {
            _isWebcamInitializing = false;
        }
    }

    public void Start()
    {
        _isStreaming = true;
        if (!_renderTimer.IsEnabled)
        {
            _renderTimer.Start();
        }

        if (_isWebcamInitialized && _frameReader != null)
        {
            _ = _frameReader.StartAsync();
        }
        else if (!_isWebcamInitialized && !_isWebcamInitializing)
        {
            _ = TryInitializeWebcamAsync();
        }
    }

    public void Stop()
    {
        _isStreaming = false;
        if (_renderTimer.IsEnabled)
        {
            _renderTimer.Stop();
        }

        if (_isWebcamInitialized && _frameReader != null)
        {
            _ = _frameReader.StopAsync();
        }
    }

    public void CycleFilter()
    {
        CurrentFilter = CurrentFilter switch
        {
            RetroFilter.Normal => RetroFilter.Warm90s,
            RetroFilter.Warm90s => RetroFilter.Cyberpunk,
            RetroFilter.Cyberpunk => RetroFilter.Monochrome,
            RetroFilter.Monochrome => RetroFilter.Sepia,
            RetroFilter.Sepia => RetroFilter.Vibrant,
            _ => RetroFilter.Normal
        };
    }

    public static string GetFilterDisplayName(RetroFilter filter) => filter switch
    {
        RetroFilter.Normal => "Original",
        RetroFilter.Warm90s => "Warm 90s",
        RetroFilter.Cyberpunk => "Cyberpunk",
        RetroFilter.Monochrome => "B&W Film",
        RetroFilter.Sepia => "Sepia '70",
        RetroFilter.Vibrant => "Vivid Color",
        _ => "Original"
    };

    public string GetFilterDisplayName() => GetFilterDisplayName(CurrentFilter);

    public static string GetFilterEmoji(RetroFilter filter) => filter switch
    {
        RetroFilter.Normal => "✨",
        RetroFilter.Warm90s => "🎞️",
        RetroFilter.Cyberpunk => "🌆",
        RetroFilter.Monochrome => "🖤",
        RetroFilter.Sepia => "📜",
        RetroFilter.Vibrant => "🌈",
        _ => "✨"
    };

    public string GetFilterEmoji() => GetFilterEmoji(CurrentFilter);

    private BitmapSource? _cachedThumbBase;
    private int _lastThumbBaseFrameCounter = -1;

    private void RenderNextFrame()
    {
        _frameCounter++;
        // If real webcam hasn't provided frames yet, run the animated fallback retro scene (368 x 168 Full Width with Focused 1:1 Center)
        if (_latestWebcamFrame == null)
        {
            var bmp = GenerateRetroViewfinderFrame(width: 368, height: 168, isPreview: true);
            FrameArrived?.Invoke(bmp);
        }
    }

    private async void OnWebcamFrameArrived(Windows.Media.Capture.Frames.MediaFrameReader sender, Windows.Media.Capture.Frames.MediaFrameArrivedEventArgs args)
    {
        if (!_isStreaming || _isProcessingFrame) return;
        _isProcessingFrame = true;

        try
        {
            using var frame = sender.TryAcquireLatestFrame();
            if (frame?.VideoMediaFrame?.SoftwareBitmap != null)
            {
                var sb = frame.VideoMediaFrame.SoftwareBitmap;
                if (sb.BitmapPixelFormat != Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8 ||
                    sb.BitmapAlphaMode != Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied)
                {
                    sb = Windows.Graphics.Imaging.SoftwareBitmap.Convert(
                        sb,
                        Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                        Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
                }

                using var encStream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                    Windows.Graphics.Imaging.BitmapEncoder.BmpEncoderId, encStream);
                encoder.SetSoftwareBitmap(sb);
                await encoder.FlushAsync();
                encStream.Seek(0);
                using var netStream = encStream.AsStreamForRead();

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = netStream;
                bmp.EndInit();
                bmp.Freeze();

                _latestWebcamFrame = bmp;
                _frameCounter++;

                // Compose real webcam frame with vintage filter, full-width with focused 1:1 center & blurred flanks
                var viewfinderBmp = ComposeRealCameraFrame(bmp, width: 368, height: 168, isPreview: true);
                FrameArrived?.Invoke(viewfinderBmp);
            }
        }
        catch
        {
            // Ignore transient frame acquisition exceptions
        }
        finally
        {
            _isProcessingFrame = false;
        }
    }

    /// <summary>
    /// Generates a live thumbnail for a specific filter preset (macOS Photo Booth style).
    /// Highly optimized: downsamples once per frame so UI thread stays 100% lag-free and silky smooth!
    /// </summary>
    public BitmapSource GenerateFilterThumbnail(RetroFilter filter, int width = 56, int height = 36)
    {
        if (_latestWebcamFrame != null)
        {
            if (_cachedThumbBase == null || _lastThumbBaseFrameCounter != _frameCounter)
            {
                _lastThumbBaseFrameCounter = _frameCounter;
                var dvThumb = new DrawingVisual();
                using (var dc = dvThumb.RenderOpen())
                {
                    dc.PushClip(new RectangleGeometry(new Rect(0, 0, width, height)));
                    dc.PushTransform(new ScaleTransform(-1.0, 1.0, width / 2.0, height / 2.0));
                    double rawW = _latestWebcamFrame.PixelWidth > 0 ? _latestWebcamFrame.PixelWidth : width;
                    double rawH = _latestWebcamFrame.PixelHeight > 0 ? _latestWebcamFrame.PixelHeight : height;
                    double scale = Math.Max((double)width / rawW, (double)height / rawH);
                    double drawW = rawW * scale;
                    double drawH = rawH * scale;
                    double drawX = (width - drawW) / 2.0;
                    double drawY = (height - drawH) / 2.0;
                    dc.DrawImage(_latestWebcamFrame, new Rect(drawX, drawY, drawW, drawH));
                    dc.Pop();
                    dc.Pop();
                }
                var rtbThumb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                rtbThumb.Render(dvThumb);
                rtbThumb.Freeze();
                _cachedThumbBase = rtbThumb;
            }

            return ComposeRealCameraFrame(_cachedThumbBase, width, height, isPreview: false, filterOverride: filter);
        }
        return GenerateRetroViewfinderFrame(width, height, isPreview: false, filterOverride: filter);
    }

    /// <summary>
    /// Composes a real camera frame with retro vintage filters, selfie mirroring,
    /// aspect-ratio-preserving center crop (UniformToFill, no horizontal stretching),
    /// vignette, grain, focus brackets, and orange date stamp.
    /// </summary>
    public BitmapSource ComposeRealCameraFrame(BitmapSource rawFrame, int width, int height, bool isPreview, RetroFilter? filterOverride = null)
    {
        var filter = filterOverride ?? CurrentFilter;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            // 1. Draw base webcam frame (mirror horizontally for natural selfie viewfinder)
            // Push clip so nothing bleeds outside bounds
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, width, height)));

            dc.PushTransform(new ScaleTransform(-1.0, 1.0, width / 2.0, height / 2.0));

            // Aspect-ratio-preserving center crop (UniformToFill): avoids stretched/squashed faces!
            double rawW = rawFrame.PixelWidth > 0 ? rawFrame.PixelWidth : width;
            double rawH = rawFrame.PixelHeight > 0 ? rawFrame.PixelHeight : height;
            double scale = Math.Max((double)width / rawW, (double)height / rawH);
            double drawW = rawW * scale;
            double drawH = rawH * scale;
            double drawX = (width - drawW) / 2.0;
            double drawY = (height - drawH) / 2.0;

            if (filter == RetroFilter.Monochrome || filter == RetroFilter.Sepia)
            {
                var gray = new FormatConvertedBitmap(rawFrame, PixelFormats.Gray8, null, 0);
                dc.DrawImage(gray, new Rect(drawX, drawY, drawW, drawH));
            }
            else
            {
                dc.DrawImage(rawFrame, new Rect(drawX, drawY, drawW, drawH));
            }

            dc.Pop(); // Restore mirror transform
            dc.Pop(); // Restore clip

            // 2. Vintage Vignette & Grain & Filter Overlay
            DrawRetroFilmOverlay(dc, width, height, filter);

            // 3. Vintage film frame is full width and clean; WPF composition handles soft bokeh blurring on unphotographed flanks seamlessly ("nyaru")

            // 4. Retro HUD Focus Brackets & Date Stamp
            if (isPreview)
            {
                DrawRetroHud(dc, width, height, isPreview: true);
            }
            else if (width > 200)
            {
                DrawRetroHud(dc, width, height, isPreview: false);
            }
        }

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    /// <summary>
    /// Generates an animated retro camera viewfinder scene with vintage color grading,
    /// authentic squircle aesthetic, and glowing orange date stamp.
    /// </summary>
    public BitmapSource GenerateRetroViewfinderFrame(int width, int height, bool isPreview, RetroFilter? filterOverride = null)
    {
        var filter = filterOverride ?? CurrentFilter;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            // 1. Background scene: Warm Retro Selfie / Studio Mirror Aesthetic
            DrawRetroSelfieScene(dc, width, height, filter);

            // 2. Vintage Vignette & Grain Filter
            DrawRetroFilmOverlay(dc, width, height, filter);

            // 3. Clean full-width retro composition; WPF composition handles soft bokeh blurring on flanks seamlessly ("nyaru")

            // 4. Retro Camera Viewfinder HUD
            if (isPreview)
            {
                DrawRetroHud(dc, width, height, isPreview: true);
            }
            else if (width > 200)
            {
                DrawRetroHud(dc, width, height, isPreview: false);
            }
        }

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    private void DrawRetroSelfieScene(DrawingContext dc, int w, int h, RetroFilter filter)
    {
        // Background gradient customized to filter vibe
        Color bgTop, bgBottom;
        switch (filter)
        {
            case RetroFilter.Normal:
                bgTop = Color.FromRgb(55, 60, 72);
                bgBottom = Color.FromRgb(20, 22, 28);
                break;
            case RetroFilter.Cyberpunk:
                bgTop = Color.FromRgb(35, 15, 65);
                bgBottom = Color.FromRgb(10, 28, 48);
                break;
            case RetroFilter.Monochrome:
                bgTop = Color.FromRgb(40, 40, 45);
                bgBottom = Color.FromRgb(15, 15, 18);
                break;
            case RetroFilter.Sepia:
                bgTop = Color.FromRgb(65, 48, 35);
                bgBottom = Color.FromRgb(28, 20, 15);
                break;
            case RetroFilter.Vibrant:
                bgTop = Color.FromRgb(220, 160, 140);
                bgBottom = Color.FromRgb(50, 25, 40);
                break;
            case RetroFilter.Warm90s:
            default:
                bgTop = Color.FromRgb(180, 145, 125);
                bgBottom = Color.FromRgb(45, 32, 28);
                break;
        }

        var bgBrush = new LinearGradientBrush(bgTop, bgBottom, 90.0);
        dc.DrawRectangle(bgBrush, null, new Rect(0, 0, w, h));

        // Soft ambient warm light or window glow in background
        var lightColor = filter == RetroFilter.Cyberpunk
            ? Color.FromArgb(70, 0, 240, 255)
            : Color.FromArgb(90, 255, 235, 200);
        var lightGlow = new RadialGradientBrush(
            lightColor,
            Color.FromArgb(0, 255, 235, 200));
        dc.DrawEllipse(lightGlow, null, new Point(w * 0.25, h * 0.35), w * 0.35, h * 0.45);

        // Cafe / room shelf silhouette background elements
        var shelfBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
        dc.DrawRectangle(shelfBrush, null, new Rect(0, h * 0.40, w * 0.35, 4));
        dc.DrawRectangle(shelfBrush, null, new Rect(0, h * 0.22, w * 0.25, 4));

        // Subtle animated breathing motion for the selfie subjects
        double breathe = Math.Sin(_frameCounter * 0.08) * 1.5;

        // Draw cute retro mirror selfie figures
        double cx1 = w * 0.38;
        double cy1 = h * 0.65 + breathe;
        double r1x = w * 0.15;
        double r1y = h * 0.34;
        var personBrush = new SolidColorBrush(filter == RetroFilter.Monochrome ? Color.FromRgb(60, 60, 65) : Color.FromRgb(75, 55, 48));
        dc.DrawEllipse(personBrush, null, new Point(cx1, cy1), r1x, r1y);

        // Right figure
        double cx2 = w * 0.62;
        double cy2 = h * 0.68 + breathe;
        double r2x = w * 0.14;
        double r2y = h * 0.32;
        var person2Brush = new SolidColorBrush(filter == RetroFilter.Monochrome ? Color.FromRgb(85, 85, 90) : Color.FromRgb(95, 70, 60));
        dc.DrawEllipse(person2Brush, null, new Point(cx2, cy2), r2x, r2y);

        // Headband on right figure
        if (filter != RetroFilter.Monochrome)
        {
            var bandBrush = new SolidColorBrush(filter == RetroFilter.Cyberpunk ? Color.FromRgb(255, 0, 128) : Color.FromRgb(244, 180, 190));
            dc.DrawRectangle(bandBrush, null, new Rect(cx2 - r2x * 0.6, cy2 - r2y * 0.75, r2x * 1.2, 7));
        }

        // Glasses on left figure
        var glassesColor = filter == RetroFilter.Monochrome
            ? Color.FromArgb(160, 200, 200, 200)
            : (filter == RetroFilter.Cyberpunk ? Color.FromArgb(220, 0, 240, 255) : Color.FromArgb(220, 245, 158, 11));
        var glassBrush = new SolidColorBrush(glassesColor);
        double gw = r1x * 0.45, gh = r1y * 0.28;
        dc.DrawRoundedRectangle(glassBrush, null, new Rect(cx1 - gw * 1.1, cy1 - gh * 0.4, gw, gh), 4, 4);
        dc.DrawRoundedRectangle(glassBrush, null, new Rect(cx1 + gw * 0.1, cy1 - gh * 0.4, gw, gh), 4, 4);
        dc.DrawLine(new Pen(glassBrush, 1.8), new Point(cx1 - gw * 0.1, cy1), new Point(cx1 + gw * 0.1, cy1));

        // Phone held up in front taking mirror selfie
        double phoneX = w * 0.50;
        double phoneY = h * 0.64 + breathe * 0.5;
        double pw = w * 0.14;
        double ph = h * 0.38;
        var phoneBody = new SolidColorBrush(Color.FromRgb(30, 32, 38));
        dc.DrawRoundedRectangle(phoneBody, null, new Rect(phoneX - pw / 2, phoneY - ph / 2, pw, ph), 7, 7);

        // Phone camera bump on the back
        var phoneCam = new SolidColorBrush(Color.FromRgb(85, 55, 85));
        dc.DrawRoundedRectangle(phoneCam, null, new Rect(phoneX - pw * 0.4, phoneY - ph * 0.42, pw * 0.8, ph * 0.22), 4, 4);
    }

    private void DrawRetroFilmOverlay(DrawingContext dc, int w, int h, RetroFilter filter)
    {
        switch (filter)
        {
            case RetroFilter.Normal:
                var cleanVignette = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.5),
                    GradientOrigin = new Point(0.5, 0.5),
                    RadiusX = 0.75,
                    RadiusY = 0.75
                };
                cleanVignette.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.65));
                cleanVignette.GradientStops.Add(new GradientStop(Color.FromArgb(50, 0, 0, 0), 1.0));
                dc.DrawRectangle(cleanVignette, null, new Rect(0, 0, w, h));
                break;

            case RetroFilter.Warm90s:
                var warmTint = new SolidColorBrush(Color.FromArgb(32, 255, 190, 70));
                dc.DrawRectangle(warmTint, null, new Rect(0, 0, w, h));

                var warmVignette = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.5),
                    GradientOrigin = new Point(0.5, 0.5),
                    RadiusX = 0.68,
                    RadiusY = 0.68
                };
                warmVignette.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.55));
                warmVignette.GradientStops.Add(new GradientStop(Color.FromArgb(120, 0, 0, 0), 1.0));
                dc.DrawRectangle(warmVignette, null, new Rect(0, 0, w, h));

                DrawFilmGrain(dc, w, h, 28);
                break;

            case RetroFilter.Cyberpunk:
                var cyberTint = new SolidColorBrush(Color.FromArgb(36, 0, 235, 255));
                dc.DrawRectangle(cyberTint, null, new Rect(0, 0, w, h));

                var neonGrad = new LinearGradientBrush(
                    Color.FromArgb(0, 255, 0, 128),
                    Color.FromArgb(55, 255, 0, 150),
                    90.0);
                dc.DrawRectangle(neonGrad, null, new Rect(0, h * 0.45, w, h * 0.55));

                var cyberVignette = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.5),
                    GradientOrigin = new Point(0.5, 0.5),
                    RadiusX = 0.7,
                    RadiusY = 0.7
                };
                cyberVignette.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.6));
                cyberVignette.GradientStops.Add(new GradientStop(Color.FromArgb(140, 10, 5, 25), 1.0));
                dc.DrawRectangle(cyberVignette, null, new Rect(0, 0, w, h));
                break;

            case RetroFilter.Monochrome:
                var monoVignette = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.5),
                    GradientOrigin = new Point(0.5, 0.5),
                    RadiusX = 0.65,
                    RadiusY = 0.65
                };
                monoVignette.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.5));
                monoVignette.GradientStops.Add(new GradientStop(Color.FromArgb(140, 0, 0, 0), 1.0));
                dc.DrawRectangle(monoVignette, null, new Rect(0, 0, w, h));

                DrawFilmGrain(dc, w, h, 35);
                break;

            case RetroFilter.Sepia:
                var sepiaTint = new SolidColorBrush(Color.FromArgb(55, 215, 145, 55));
                dc.DrawRectangle(sepiaTint, null, new Rect(0, 0, w, h));

                var sepiaVignette = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.5),
                    GradientOrigin = new Point(0.5, 0.5),
                    RadiusX = 0.65,
                    RadiusY = 0.65
                };
                sepiaVignette.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.5));
                sepiaVignette.GradientStops.Add(new GradientStop(Color.FromArgb(130, 35, 15, 5), 1.0));
                dc.DrawRectangle(sepiaVignette, null, new Rect(0, 0, w, h));

                DrawFilmGrain(dc, w, h, 30);
                break;

            case RetroFilter.Vibrant:
                var vividTint = new SolidColorBrush(Color.FromArgb(22, 255, 230, 200));
                dc.DrawRectangle(vividTint, null, new Rect(0, 0, w, h));

                var vividVignette = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.5),
                    GradientOrigin = new Point(0.5, 0.5),
                    RadiusX = 0.72,
                    RadiusY = 0.72
                };
                vividVignette.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.62));
                vividVignette.GradientStops.Add(new GradientStop(Color.FromArgb(90, 0, 0, 0), 1.0));
                dc.DrawRectangle(vividVignette, null, new Rect(0, 0, w, h));
                break;
        }
    }

    private void DrawFilmGrain(DrawingContext dc, int w, int h, int count)
    {
        for (int i = 0; i < count; i++)
        {
            double gx = (_frameCounter * 31 + i * 57) % w;
            double gy = (_frameCounter * 17 + i * 83) % h;
            byte alpha = (byte)(_random.Next(15, 45));
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 255)), null, new Point(gx, gy), 0.6, 0.6);
        }
    }

    public BitmapSource? LastCapturedPhotoOnly { get; private set; }

    private void DrawRetroHud(DrawingContext dc, int w, int h, bool isPreview)
    {
        // Viewfinder framing crosshair / focus brackets in center - ONLY in live preview, NEVER on the captured photo!
        if (isPreview)
        {
            var bracketPen = new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), 1.2);
            double cx = w / 2.0, cy = h / 2.0;
            double bs = Math.Min(w, h) * 0.12;
            dc.DrawLine(bracketPen, new Point(cx - bs, cy - bs), new Point(cx - bs + 6, cy - bs));
            dc.DrawLine(bracketPen, new Point(cx - bs, cy - bs), new Point(cx - bs, cy - bs + 6));
            dc.DrawLine(bracketPen, new Point(cx + bs, cy - bs), new Point(cx + bs - 6, cy - bs));
            dc.DrawLine(bracketPen, new Point(cx + bs, cy - bs), new Point(cx + bs, cy - bs + 6));
            dc.DrawLine(bracketPen, new Point(cx - bs, cy + bs), new Point(cx - bs + 6, cy + bs));
            dc.DrawLine(bracketPen, new Point(cx - bs, cy + bs), new Point(cx - bs, cy + bs - 6));
            dc.DrawLine(bracketPen, new Point(cx + bs, cy + bs), new Point(cx + bs - 6, cy + bs));
            dc.DrawLine(bracketPen, new Point(cx + bs, cy + bs), new Point(cx + bs, cy + bs - 6));
        }

        // When capturing full photo, stamp the iconic 90s amber digital date in bottom right!
        if (!isPreview)
        {
            var textFont = new Typeface(new FontFamily("Consolas, Courier New, Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            double fontSize = Math.Max(18.0, w * 0.026);
            string dateText = CurrentDateStamp;
            var amberGlow = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber retro LED
            var ftDate = new FormattedText(dateText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, textFont, fontSize, amberGlow, 1.0);
            var ftDateGlow = new FormattedText(dateText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, textFont, fontSize, new SolidColorBrush(Color.FromArgb(90, 245, 158, 11)), 1.0);
            dc.DrawText(ftDateGlow, new Point(w - ftDate.Width - 19, h - ftDate.Height - 15));
            dc.DrawText(ftDate, new Point(w - ftDate.Width - 20, h - ftDate.Height - 16));
        }
    }

    /// <summary>
    /// Captures a photo in authentic Polaroid / Instax Mini format:
    /// High-definition square photo enclosed in a crisp white instant film card
    /// with thicker bottom margin, date stamp, and "HTPPS / hanstp_projects" watermark.
    /// </summary>
    public async Task<BitmapSource> CapturePolaroidPhotoAsync()
    {
        // High Definition Polaroid dimensions: 1080 x 1380 (Full HD / 2K crisp Polaroid format)
        const int cardW = 1080;
        const int cardH = 1380;
        const int photoMarginSides = 54;
        const int photoMarginTop = 54;
        const int photoW = cardW - (photoMarginSides * 2); // 972 px
        const int photoH = photoW; // 972 px - True 1:1 Square matching 166x166 viewfinder exactly!

        BitmapSource photoContent;

        // 1. Capture real high-definition camera sensor frame via MediaCapture photo stream first!
        if (_isWebcamInitialized && _mediaCapture != null)
        {
            try
            {
                using var memStream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                await _mediaCapture.CapturePhotoToStreamAsync(
                    Windows.Media.MediaProperties.ImageEncodingProperties.CreatePng(),
                    memStream);
                memStream.Seek(0);
                using var netStream = memStream.AsStreamForRead();
                var decoder = BitmapDecoder.Create(netStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var capturedFrame = decoder.Frames[0];
                photoContent = ComposeRealCameraFrame(capturedFrame, photoW, photoH, isPreview: false);
            }
            catch (Exception ex)
            {
                Log.Warn("Hardware sensor capture failed, falling back to latest stream frame: " + ex.Message);
                if (_latestWebcamFrame != null)
                {
                    photoContent = ComposeRealCameraFrame(_latestWebcamFrame, photoW, photoH, isPreview: false);
                }
                else
                {
                    photoContent = GenerateRetroViewfinderFrame(photoW, photoH, isPreview: false);
                }
            }
        }
        else if (_latestWebcamFrame != null)
        {
            photoContent = ComposeRealCameraFrame(_latestWebcamFrame, photoW, photoH, isPreview: false);
        }
        else
        {
            photoContent = GenerateRetroViewfinderFrame(photoW, photoH, isPreview: false);
        }

        LastCapturedPhotoOnly = photoContent;

        // Render full physical Polaroid / Instax photo card with watermark
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            // 1. Polaroid White Instant Film Card
            var cardRect = new Rect(0, 0, cardW, cardH);
            var paperGradient = new LinearGradientBrush(
                Color.FromRgb(254, 254, 253),
                Color.FromRgb(246, 246, 243),
                90.0);
            dc.DrawRoundedRectangle(paperGradient, null, cardRect, 22, 22);

            // Subtle paper border outline
            var paperPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), 1.5);
            dc.DrawRoundedRectangle(null, paperPen, cardRect, 22, 22);

            // 2. Photo Content Area
            var photoRect = new Rect(photoMarginSides, photoMarginTop, photoW, photoH);

            // Inner dark inset border
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(15, 15, 18)), null, photoRect);
            dc.DrawImage(photoContent, photoRect);

            // Fine inner film frame border
            var innerPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)), 1.5);
            dc.DrawRectangle(null, innerPen, photoRect);

            // 3. Bottom Chin: Watermark & Stamped Polaroid Caption
            // Watermark: "HTPPS / hanstp_projects" (above date text, matching user requested design)
            var boldFont = new Typeface(new FontFamily("Segoe UI, Arial, sans-serif"), FontStyles.Normal, FontWeights.ExtraBold, FontStretches.Normal);
            var slashFont = new Typeface(new FontFamily("Segoe UI, Arial, sans-serif"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var regularFont = new Typeface(new FontFamily("Segoe UI, Arial, sans-serif"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

            double wmX = photoMarginSides + 6;
            double wmY = photoRect.Bottom + 54;

            // "HTPPS" (bold dark)
            var ftHtpps = new FormattedText("HTPPS", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, boldFont, 24, new SolidColorBrush(Color.FromRgb(17, 24, 39)), 1.0);
            dc.DrawText(ftHtpps, new Point(wmX, wmY));

            // " / " (vibrant orange slash)
            var ftSlash = new FormattedText(" / ", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, slashFont, 24, new SolidColorBrush(Color.FromRgb(255, 87, 34)), 1.0);
            dc.DrawText(ftSlash, new Point(wmX + ftHtpps.Width, wmY));

            // "hanstp_projects" (subdued sleek grey)
            var ftHandle = new FormattedText("hanstp_projects", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, regularFont, 22, new SolidColorBrush(Color.FromRgb(107, 114, 128)), 1.0);
            dc.DrawText(ftHandle, new Point(wmX + ftHtpps.Width + ftSlash.Width, wmY + 1.5));

            // Date & Retro Cam caption
            var captionFont = new Typeface(new FontFamily("Segoe UI, Arial"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            string bottomDate = DateTime.Now.ToString("dd MMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
            var ftBottomDate = new FormattedText($"★ {bottomDate} • RETRO CAM", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, captionFont, 18, new SolidColorBrush(Color.FromRgb(80, 80, 90)), 1.0);
            dc.DrawText(ftBottomDate, new Point(photoMarginSides + 6, wmY + 48));

            // Instant Film Brand Stamp
            var ftBrand = new FormattedText("INSTAX MINI FILM", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, captionFont, 14, new SolidColorBrush(Color.FromRgb(160, 160, 170)), 1.0);
            dc.DrawText(ftBrand, new Point(cardW - ftBrand.Width - photoMarginSides - 6, wmY + 52));
        }

        var rtb = new RenderTargetBitmap(cardW, cardH, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    /// <summary>
    /// Synchronous capture wrapper for unit testing or simple invocation.
    /// </summary>
    public BitmapSource CapturePolaroidPhoto()
    {
        return CapturePolaroidPhotoAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Automatically saves the captured Polaroid photo to Pictures\WindowsDynamicIsland\RetroCam_yyyyMMdd_HHmmss.png
    /// and returns the absolute saved file path.
    /// </summary>
    public string SavePhotoToDevice(BitmapSource photo)
    {
        string picturesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "WindowsDynamicIsland");

        if (!Directory.Exists(picturesDir))
        {
            Directory.CreateDirectory(picturesDir);
        }

        string fileName = $"RetroCam_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        string filePath = Path.Combine(picturesDir, fileName);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(photo));

        using (var stream = File.Create(filePath))
        {
            encoder.Save(stream);
        }

        return filePath;
    }

    /// <summary>
    /// Scans the saved pictures folder and returns up to 6 recent photos.
    /// </summary>
    public List<string> GetRecentPhotos(int maxCount = 6)
    {
        try
        {
            string picturesDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "WindowsDynamicIsland");

            if (!Directory.Exists(picturesDir)) return new List<string>();

            var dir = new DirectoryInfo(picturesDir);
            return dir.GetFiles("*.png")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(maxCount)
                .Select(f => f.FullName)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    public void Dispose()
    {
        _renderTimer.Stop();
        try
        {
            _frameReader?.StopAsync();
            _frameReader?.Dispose();
        }
        catch { }
        _frameReader = null;

        try
        {
            _mediaCapture?.Dispose();
        }
        catch { }
        _mediaCapture = null;
    }
}
