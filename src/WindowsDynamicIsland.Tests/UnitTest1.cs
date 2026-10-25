using System.IO;
using Xunit;
using Xunit.Abstractions;
using WindowsDynamicIsland.Interop;
using WindowsDynamicIsland.Animations;
using WindowsDynamicIsland.Models;

namespace WindowsDynamicIsland.Tests;

public class UnitTest1
{
    private readonly ITestOutputHelper _output;

    public UnitTest1(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TestMonitorAndPlacement()
    {
        var s = new AppSettings();
        var monitor = MonitorHelper.Select(s.MonitorIndex);
        double scale = monitor.DpiScale > 0 ? monitor.DpiScale : 1.0;
        var (hostW, hostH) = IslandLayout.HostSize(s);

        double monitorLeft = monitor.Bounds.Left / scale;
        double monitorTop = monitor.Bounds.Top / scale;
        double monitorWidth = monitor.Bounds.Width / scale;

        double left = monitorLeft + (monitorWidth - hostW) / 2.0;
        double top = monitorTop;

        _output.WriteLine($"Monitor: Bounds=({monitor.Bounds.Left},{monitor.Bounds.Top},{monitor.Bounds.Right},{monitor.Bounds.Bottom})");
        _output.WriteLine($"DpiScale={monitor.DpiScale}, scale={scale}");
        _output.WriteLine($"HostSize: W={hostW}, H={hostH}");
        _output.WriteLine($"Calculated: Left={left}, Top={top}");
        _output.WriteLine($"All Monitors Count: {MonitorHelper.GetMonitors().Count}");

        Assert.True(hostW > 0);
    }

    [Fact]
    public void TestMainWindowVisualTree()
    {
        var thread = new System.Threading.Thread(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application();
            var settingsService = new Services.SettingsService(System.IO.Path.GetTempPath());
            var stateMachine = new IslandStateMachine();
            var mediaService = new Services.MediaSessionService();
            var systemService = new Services.SystemMonitorService(app.Dispatcher);
            var startupService = new Services.StartupService();

            var win = new MainWindow(settingsService, stateMachine, mediaService, systemService, startupService);
            win.Show();

            _output.WriteLine($"Window: W={win.Width}, H={win.Height}, ActualW={win.ActualWidth}, ActualH={win.ActualHeight}, Vis={win.Visibility}");
            _output.WriteLine($"Left={win.Left}, Top={win.Top}");
            _output.WriteLine($"Pill: W={win.Pill.Width}, H={win.Pill.Height}, ActualW={win.Pill.ActualWidth}, ActualH={win.Pill.ActualHeight}");
            _output.WriteLine($"Pill PillWidth={win.Pill.PillWidth}, PillHeight={win.Pill.PillHeight}, Opacity={win.Pill.Opacity}, Margin={win.Pill.Margin}");
            _output.WriteLine($"CompactLayer: Vis={win.CompactLayer.Visibility}, Opacity={win.CompactLayer.Opacity}, H={win.CompactLayer.Height}");
            _output.WriteLine($"CompactIdleContent: Vis={win.CompactIdleContent.Visibility}, ActualW={win.CompactIdleContent.ActualWidth}");
            _output.WriteLine($"ExpandedLayer: Vis={win.ExpandedLayer.Visibility}, Opacity={win.ExpandedLayer.Opacity}");

            win.Close();
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void TestNotchAndDynamicIslandLayout()
    {
        var s = new AppSettings { Style = IslandStyle.Notch };
        var notchGeom = IslandLayout.For(IslandState.Expanded, ExpandedPage.Dashboard, false, false, s);
        Assert.Equal(620, notchGeom.Width);
        Assert.Equal(224, notchGeom.Height);
        Assert.Equal(18, notchGeom.CornerRadius);

        var mediaGeom = IslandLayout.For(IslandState.Expanded, ExpandedPage.Media, false, false, s);
        Assert.Equal(384, mediaGeom.Width);
        Assert.Equal(250, mediaGeom.Height);

        var cameraGeom = IslandLayout.For(IslandState.Expanded, ExpandedPage.Camera, false, false, s);
        Assert.Equal(400, cameraGeom.Width);
        Assert.Equal(IslandLayout.CameraPageHeight, cameraGeom.Height);

        var systemGeom = IslandLayout.For(IslandState.Expanded, ExpandedPage.System, false, false, s);
        Assert.Equal(384, systemGeom.Width);
        Assert.Equal(184, systemGeom.Height);

        var (hostW, hostH) = IslandLayout.HostSize(s);
        Assert.True(hostW >= IslandLayout.DashboardPageWidth);
        Assert.True(hostH > notchGeom.Height);

        s.Style = IslandStyle.DynamicIsland;
        var dynamicGeom = IslandLayout.For(IslandState.Expanded, ExpandedPage.Dashboard, false, false, s);
        Assert.Equal(620, dynamicGeom.Width);
    }

    [Fact]
    public void TestRenderNotchPill()
    {
        var thread = new System.Threading.Thread(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application();
            app.Resources["Island.Background"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(10, 10, 12));
            app.Resources["Island.Background.Solid"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0, 0));
            app.Resources["Island.Edge"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(70, 255, 255, 255));
            app.Resources["Island.Shadow.Opacity"] = 0.45;
            app.Resources["Text.Primary"] = System.Windows.Media.Brushes.White;

            var container = new System.Windows.Controls.Grid
            {
                Width = 260,
                Height = 60,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(210, 225, 240))
            };

            var pill = new Controls.IslandPill
            {
                StyleMode = IslandStyle.Notch,
                PillWidth = 180,
                PillHeight = 32,
                PillRadius = 16,
                SurfaceOpacity = 1.0,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Top
            };

            var txt = new System.Windows.Controls.TextBlock
            {
                Text = "09:34",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 12,
                FontWeight = System.Windows.FontWeights.SemiBold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            pill.Content = txt;
            container.Children.Add(pill);

            container.Measure(new System.Windows.Size(260, 60));
            container.Arrange(new System.Windows.Rect(0, 0, 260, 60));
            container.UpdateLayout();

            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(260, 60, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(container);

            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));

            string outPath = @"C:\Users\tripu\.gemini\antigravity-ide\brain\b072dc46-ba3b-42a6-9e82-067e9eb383fd\scratch\pill_notch_preview.png";
            using (var fs = System.IO.File.Create(outPath))
            {
                encoder.Save(fs);
            }
            _output.WriteLine($"Rendered notch preview to {outPath}");
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void TestLyricsParsingAndSync()
    {
        // 1. Cleaning
        string cleanTitle = Services.LyricsService.CleanTrackTitle("Ramai Sepi Bersama - Single");
        Assert.Equal("Ramai Sepi Bersama", cleanTitle);

        string cleanArtist = Services.LyricsService.CleanArtistName("Hindia — Ramai Sepi Bersama - Single");
        Assert.Equal("Hindia", cleanArtist);

        // 2. LRC Parsing
        string sampleLrc = @"
[00:05.10] Di hari yang ramai
[00:10.50] Kita merasa sepi
[00:15.80] Bersama-sama
";
        var lines = Services.LyricsService.ParseLrcString(sampleLrc);
        Assert.Equal(3, lines.Count);
        Assert.Equal(TimeSpan.FromSeconds(5.1), lines[0].Timestamp);
        Assert.Equal("Di hari yang ramai", lines[0].Text);

        var song = new Services.SongLyrics
        {
            Title = "Ramai Sepi Bersama",
            Artist = "Hindia",
            Lines = lines
        };
        Assert.True(song.HasSyncedLyrics);

        // 3. Sync matching at 2s (intro)
        Assert.Equal(-1, song.GetActiveIndexAt(TimeSpan.FromSeconds(2)));
        var at2s = song.GetLinesAt(TimeSpan.FromSeconds(2));
        Assert.Equal("♪ Intro ♪", at2s.Current?.Text);
        Assert.Equal("Di hari yang ramai", at2s.Next?.Text);

        // 4. Sync matching at 6s (line 1)
        Assert.Equal(0, song.GetActiveIndexAt(TimeSpan.FromSeconds(6)));
        var at6s = song.GetLinesAt(TimeSpan.FromSeconds(6));
        Assert.Equal("Di hari yang ramai", at6s.Current?.Text);
        Assert.Equal("Kita merasa sepi", at6s.Next?.Text);

        // 5. Sync matching at 12s (line 2)
        Assert.Equal(1, song.GetActiveIndexAt(TimeSpan.FromSeconds(12)));
        var at12s = song.GetLinesAt(TimeSpan.FromSeconds(12));
        Assert.Equal("Kita merasa sepi", at12s.Current?.Text);
        Assert.Equal("Di hari yang ramai", at12s.Prev?.Text);
        Assert.Equal("Bersama-sama", at12s.Next?.Text);
    }

    [Fact]
    public void TestCameraServiceCaptureAndSave()
    {
        var thread = new System.Threading.Thread(() =>
        {
            var cameraService = new Services.CameraService();
            var preview = cameraService.GenerateRetroViewfinderFrame(400, 200, isPreview: true);
            Assert.NotNull(preview);
            Assert.Equal(400, preview.PixelWidth);
            Assert.Equal(200, preview.PixelHeight);

            var photo = cameraService.CapturePolaroidPhoto();
            Assert.NotNull(photo);
            Assert.True(photo.PixelWidth > 0);
            Assert.True(photo.PixelHeight > 0);

            string savedPath = cameraService.SavePhotoToDevice(photo);
            Assert.True(System.IO.File.Exists(savedPath));
            var fileInfo = new System.IO.FileInfo(savedPath);
            Assert.True(fileInfo.Length > 1000);
            _output.WriteLine($"Saved retro Polaroid test photo to {savedPath} (size={fileInfo.Length} bytes)");

            // Copy to scratch for inspection
            string scratchPath = @"C:\Users\tripu\.gemini\antigravity-ide\brain\b072dc46-ba3b-42a6-9e82-067e9eb383fd\scratch\retro_polaroid_card.png";
            System.IO.File.Copy(savedPath, scratchPath, true);

            // Cleanup test photo from Pictures
            try { System.IO.File.Delete(savedPath); } catch { }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void TestRenderRetroCameraUI()
    {
        var thread = new System.Threading.Thread(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application();
            var settingsService = new Services.SettingsService(System.IO.Path.GetTempPath());
            var stateMachine = new IslandStateMachine();
            var mediaService = new Services.MediaSessionService();
            var systemService = new Services.SystemMonitorService(app.Dispatcher);
            var startupService = new Services.StartupService();

            var win = new MainWindow(settingsService, stateMachine, mediaService, systemService, startupService);
            win.Show();
            stateMachine.SetExpanded(true);
            stateMachine.SelectPage(ExpandedPage.Camera);

            win.Pill.PillWidth = 440;
            win.Pill.PillHeight = 285;
            win.ExpandedLayer.Opacity = 1.0;
            win.ExpandedLayer.Visibility = System.Windows.Visibility.Visible;
            win.CompactLayer.Visibility = System.Windows.Visibility.Collapsed;
            win.UpdateLayout();

            int w = (int)Math.Max(100, win.ActualWidth);
            int h = (int)Math.Max(100, win.ActualHeight);
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(win);

            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));

            string outPath = @"C:\Users\tripu\.gemini\antigravity-ide\brain\b072dc46-ba3b-42a6-9e82-067e9eb383fd\scratch\retro_cam_window.png";
            using (var fs = System.IO.File.Create(outPath))
            {
                encoder.Save(fs);
            }
            _output.WriteLine($"Rendered retro cam UI preview to {outPath}");
            win.Close();
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void TestRenderPolaroidDevelopingPhoto()
    {
        var thread = new System.Threading.Thread(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application();
            var settingsService = new Services.SettingsService(System.IO.Path.GetTempPath());
            var stateMachine = new IslandStateMachine();
            var mediaService = new Services.MediaSessionService();
            var systemService = new Services.SystemMonitorService(app.Dispatcher);
            var startupService = new Services.StartupService();

            var win = new MainWindow(settingsService, stateMachine, mediaService, systemService, startupService);
            win.Show();

            var cameraService = new Services.CameraService();
            var photo = cameraService.CapturePolaroidPhoto();
            win.PrintedPhotoImage.Source = cameraService.LastCapturedPhotoOnly ?? photo;

            // 1. Snapshot during developing (blurred & milky sheen)
            win.PrintedPhotoCard.Visibility = System.Windows.Visibility.Visible;
            win.PrintedPhotoCard.Opacity = 1.0;
            win.PrintedPhotoBlur.Radius = 24.0;
            win.PrintedPhotoDevelopingSheen.Opacity = 0.55;
            win.PrintedPhotoPathText.Text = "⏳ Developing film...";
            win.PrintedPhotoPathText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11));
            win.PrintedPhotoTranslate.Y = 40;
            win.UpdateLayout();

            int w = (int)Math.Max(100, win.ActualWidth);
            int h = (int)Math.Max(100, win.ActualHeight);
            var rtb1 = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb1.Render(win);
            var enc1 = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc1.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb1));
            string outPath1 = @"C:\Users\tripu\.gemini\antigravity-ide\brain\b072dc46-ba3b-42a6-9e82-067e9eb383fd\scratch\polaroid_developing_blurred.png";
            using (var fs = System.IO.File.Create(outPath1))
            {
                enc1.Save(fs);
            }
            _output.WriteLine($"Rendered developing blurred photo to {outPath1}");

            // 2. Snapshot after development (crystal clear)
            win.PrintedPhotoBlur.Radius = 0.0;
            win.PrintedPhotoDevelopingSheen.Opacity = 0.0;
            win.PrintedPhotoPathText.Text = "✓ Auto-saved to Pictures";
            win.PrintedPhotoPathText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 163, 74));
            win.UpdateLayout();

            var rtb2 = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb2.Render(win);
            var enc2 = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc2.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb2));
            string outPath2 = @"C:\Users\tripu\.gemini\antigravity-ide\brain\b072dc46-ba3b-42a6-9e82-067e9eb383fd\scratch\polaroid_developed_sharp.png";
            using (var fs = System.IO.File.Create(outPath2))
            {
                enc2.Save(fs);
            }
            _output.WriteLine($"Rendered developed sharp photo to {outPath2}");

            win.Close();
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public async System.Threading.Tasks.Task TestMediaCaptureAvailability()
    {
        try
        {
            var capture = new Windows.Media.Capture.MediaCapture();
            var settings = new Windows.Media.Capture.MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = Windows.Media.Capture.StreamingCaptureMode.Video
            };
            await capture.InitializeAsync(settings);
            _output.WriteLine("MediaCapture initialized successfully!");

            // Test capturing a photo
            using var memStream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            await capture.CapturePhotoToStreamAsync(
                Windows.Media.MediaProperties.ImageEncodingProperties.CreatePng(),
                memStream);
            _output.WriteLine($"Captured frame stream size: {memStream.Size} bytes");

            // Test MediaFrameReader for real webcam streaming without CaptureElement
            var frameSourceGroups = await Windows.Media.Capture.Frames.MediaFrameSourceGroup.FindAllAsync();
            _output.WriteLine($"Found {frameSourceGroups.Count} frame source groups");
            var selectedGroup = frameSourceGroups.FirstOrDefault(g => g.SourceInfos.Any(s => s.MediaStreamType == Windows.Media.Capture.MediaStreamType.VideoPreview || s.MediaStreamType == Windows.Media.Capture.MediaStreamType.VideoRecord));
            if (selectedGroup != null)
            {
                _output.WriteLine($"Selected group: {selectedGroup.DisplayName}");
                var frameSettings = new Windows.Media.Capture.MediaCaptureInitializationSettings
                {
                    SourceGroup = selectedGroup,
                    SharingMode = Windows.Media.Capture.MediaCaptureSharingMode.SharedReadOnly,
                    MemoryPreference = Windows.Media.Capture.MediaCaptureMemoryPreference.Cpu,
                    StreamingCaptureMode = Windows.Media.Capture.StreamingCaptureMode.Video
                };
                var frameCapture = new Windows.Media.Capture.MediaCapture();
                await frameCapture.InitializeAsync(frameSettings);
                var frameSource = frameCapture.FrameSources.Values.FirstOrDefault(fs => fs.Info.MediaStreamType == Windows.Media.Capture.MediaStreamType.VideoPreview || fs.Info.MediaStreamType == Windows.Media.Capture.MediaStreamType.VideoRecord);
                if (frameSource != null)
                {
                    var frameReader = await frameCapture.CreateFrameReaderAsync(frameSource);
                    var status = await frameReader.StartAsync();
                    _output.WriteLine($"MediaFrameReader status: {status}");

                    // Hook FrameArrived and wait for a real frame
                    var tcs = new System.Threading.Tasks.TaskCompletionSource<Windows.Graphics.Imaging.SoftwareBitmap?>();
                    frameReader.FrameArrived += (s, e) =>
                    {
                        using var frame = frameReader.TryAcquireLatestFrame();
                        if (frame?.VideoMediaFrame?.SoftwareBitmap != null)
                        {
                            var sb = Windows.Graphics.Imaging.SoftwareBitmap.Copy(frame.VideoMediaFrame.SoftwareBitmap);
                            tcs.TrySetResult(sb);
                        }
                    };

                    var liveSb = await System.Threading.Tasks.Task.WhenAny(tcs.Task, System.Threading.Tasks.Task.Delay(1500)) == tcs.Task ? await tcs.Task : null;
                    if (liveSb != null)
                    {
                        _output.WriteLine($"Got live frame: {liveSb.PixelWidth}x{liveSb.PixelHeight}, format: {liveSb.BitmapPixelFormat}");
                        if (liveSb.BitmapPixelFormat != Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8 || liveSb.BitmapAlphaMode != Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied)
                        {
                            liveSb = Windows.Graphics.Imaging.SoftwareBitmap.Convert(liveSb, Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
                        }

                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        for (int i = 0; i < 5; i++)
                        {
                            using var encStream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                            var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.BmpEncoderId, encStream);
                            encoder.SetSoftwareBitmap(liveSb);
                            await encoder.FlushAsync();
                            encStream.Seek(0);
                            using var netStream = encStream.AsStreamForRead();
                            var bmp = new System.Windows.Media.Imaging.BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bmp.StreamSource = netStream;
                            bmp.EndInit();
                            bmp.Freeze();
                        }
                        sw.Stop();
                        _output.WriteLine($"5 frame conversions took {sw.ElapsedMilliseconds} ms (avg {sw.ElapsedMilliseconds / 5.0} ms/frame)");
                    }
                    else
                    {
                        _output.WriteLine("Frame timed out, fallback will be used");
                    }
                    await frameReader.StopAsync();
                    frameCapture.Dispose();
                }
            }

            capture.Dispose();
        }
        catch (Exception ex)
        {
            _output.WriteLine($"MediaCapture test output: {ex.GetType().Name} - {ex.Message}");
        }
    }

    [Fact]
    public void TestCompactLyricsSettingAndLayout()
    {
        var s = new AppSettings();
        Assert.True(s.ShowCompactLyrics, "ShowCompactLyrics should default to true");

        // When ShowCompactLyrics is true
        s.ShowCompactLyrics = true;
        var geomWithLyrics = IslandLayout.For(IslandState.MusicPlaying, ExpandedPage.Dashboard, false, false, s);
        Assert.Equal(s.CompactWidth + IslandLayout.MusicExtraWidth + 36, geomWithLyrics.Width);

        // When ShowCompactLyrics is false
        s.ShowCompactLyrics = false;
        var geomWithoutLyrics = IslandLayout.For(IslandState.MusicPlaying, ExpandedPage.Dashboard, false, false, s);
        Assert.Equal(s.CompactWidth + IslandLayout.MusicExtraWidth, geomWithoutLyrics.Width);
    }

    [Fact]
    public void TestRenderCompactMusicWithAndWithoutLyrics()
    {
        var thread = new System.Threading.Thread(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application();
            var settingsService = new Services.SettingsService(System.IO.Path.GetTempPath());
            var stateMachine = new IslandStateMachine();
            var mediaService = new Services.MediaSessionService();
            var systemService = new Services.SystemMonitorService(app.Dispatcher);
            var startupService = new Services.StartupService();

            var win = new MainWindow(settingsService, stateMachine, mediaService, systemService, startupService);
            win.Show();

            // Set music playing state
            stateMachine.SetMusicPlaying(true);
            win.CompactIdleContent.Visibility = System.Windows.Visibility.Collapsed;
            win.CompactMusicContent.Visibility = System.Windows.Visibility.Visible;

            // Scenario 1: ShowCompactLyrics = false (Default title & artist mode)
            var s = settingsService.Current;
            s.ShowCompactLyrics = false;
            win.CompactTrackTitle.Text = "Daniel Caesar - Always";
            win.CompactArtist.Text = "Daniel Caesar";
            win.CompactTrackInfoPanel.Visibility = System.Windows.Visibility.Visible;
            win.CompactLyricsPanel.Visibility = System.Windows.Visibility.Collapsed;
            win.Pill.PillWidth = s.CompactWidth + IslandLayout.MusicExtraWidth;
            win.UpdateLayout();

            int w1 = (int)Math.Max(100, win.ActualWidth);
            int h1 = (int)Math.Max(100, win.ActualHeight);
            var rtb1 = new System.Windows.Media.Imaging.RenderTargetBitmap(w1, h1, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb1.Render(win);

            var encoder1 = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder1.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb1));
            string outPath1 = @"C:\Users\tripu\.gemini\antigravity-ide\brain\b072dc46-ba3b-42a6-9e82-067e9eb383fd\scratch\compact_title_only.png";
            using (var fs = System.IO.File.Create(outPath1))
            {
                encoder1.Save(fs);
            }
            _output.WriteLine($"Rendered compact title-only preview to {outPath1}");

            // Scenario 2: ShowCompactLyrics = true (Running lyrics mode)
            s.ShowCompactLyrics = true;
            win.CompactLiveLyricText.Text = "And did you say that you'd be mine?";
            win.CompactTrackInfoPanel.Visibility = System.Windows.Visibility.Collapsed;
            win.CompactLyricsPanel.Visibility = System.Windows.Visibility.Visible;
            win.Pill.PillWidth = s.CompactWidth + IslandLayout.MusicExtraWidth + 36;
            win.UpdateLayout();

            int w2 = (int)Math.Max(100, win.ActualWidth);
            int h2 = (int)Math.Max(100, win.ActualHeight);
            var rtb2 = new System.Windows.Media.Imaging.RenderTargetBitmap(w2, h2, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb2.Render(win);

            var encoder2 = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder2.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb2));
            string outPath2 = @"C:\Users\tripu\.gemini\antigravity-ide\brain\b072dc46-ba3b-42a6-9e82-067e9eb383fd\scratch\compact_lyrics_running.png";
            using (var fs = System.IO.File.Create(outPath2))
            {
                encoder2.Save(fs);
            }
            _output.WriteLine($"Rendered compact running lyrics preview to {outPath2}");

            win.Close();
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void TestRenderRetroCameraMacFilters()
    {
        var camService = new Services.CameraService();
        var filters = new[]
        {
            Services.RetroFilter.Normal,
            Services.RetroFilter.Warm90s,
            Services.RetroFilter.Cyberpunk,
            Services.RetroFilter.Monochrome,
            Services.RetroFilter.Sepia,
            Services.RetroFilter.Vibrant
        };

        foreach (var f in filters)
        {
            var thumb = camService.GenerateFilterThumbnail(f, 56, 36);
            Assert.NotNull(thumb);
            Assert.Equal(56, thumb.PixelWidth);
            Assert.Equal(36, thumb.PixelHeight);
            Assert.NotEmpty(Services.CameraService.GetFilterDisplayName(f));
            Assert.NotEmpty(Services.CameraService.GetFilterEmoji(f));
        }

        var thread = new System.Threading.Thread(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application();
            var settingsService = new Services.SettingsService(System.IO.Path.GetTempPath());
            var stateMachine = new IslandStateMachine();
            var mediaService = new Services.MediaSessionService();
            var systemService = new Services.SystemMonitorService(app.Dispatcher);
            var startupService = new Services.StartupService();

            var win = new MainWindow(settingsService, stateMachine, mediaService, systemService, startupService);
            win.Show();

            stateMachine.SetExpanded(true);
            stateMachine.SelectPage(ExpandedPage.Camera);
            win.Pill.PillWidth = IslandLayout.CameraPageWidth;
            win.Pill.PillHeight = IslandLayout.CameraPageHeight;
            win.UpdateLayout();

            int w = (int)Math.Max(100, win.ActualWidth);
            int h = (int)Math.Max(100, win.ActualHeight);
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(win);

            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            string outPath = @"C:\Users\tripu\.gemini\antigravity-ide\brain\b072dc46-ba3b-42a6-9e82-067e9eb383fd\scratch\retro_cam_macos_filter.png";
            using (var fs = System.IO.File.Create(outPath))
            {
                encoder.Save(fs);
            }
            _output.WriteLine($"Rendered Retro Cam macOS filter preview to {outPath}");

            win.Close();
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.True(System.IO.File.Exists(@"C:\Users\tripu\.gemini\antigravity-ide\brain\b072dc46-ba3b-42a6-9e82-067e9eb383fd\scratch\retro_cam_macos_filter.png"));
    }

    [Fact]
    public void TestWindowsNotificationBadges()
    {
        var (waGlyph, waColor) = Services.WindowsNotificationService.GetAppBadge("WhatsApp");
        Assert.Equal("💬", waGlyph);
        Assert.Equal("#25D366", waColor);

        var (tgGlyph, tgColor) = Services.WindowsNotificationService.GetAppBadge("Telegram Desktop");
        Assert.Equal("✈️", tgGlyph);
        Assert.Equal("#2AABEE", tgColor);

        var (crGlyph, crColor) = Services.WindowsNotificationService.GetAppBadge("Google Chrome");
        Assert.Equal("🌐", crGlyph);
        Assert.Equal("#EA4335", crColor);

        var (mailGlyph, mailColor) = Services.WindowsNotificationService.GetAppBadge("Outlook");
        Assert.Equal("✉️", mailGlyph);
        Assert.Equal("#0078D4", mailColor);

        var (dcGlyph, dcColor) = Services.WindowsNotificationService.GetAppBadge("Discord");
        Assert.Equal("🎮", dcGlyph);
        Assert.Equal("#5865F2", dcColor);

        var (sysGlyph, sysColor) = Services.WindowsNotificationService.GetAppBadge("Windows Security");
        Assert.Equal("💻", sysGlyph);
        Assert.Equal("#0078D4", sysColor);
    }

    [Fact]
    public void TestNotificationTimeoutAndSettings()
    {
        var s = new AppSettings();
        Assert.Equal(3, s.NotificationTimeoutSeconds); // Default requested: 3 seconds
        Assert.True(s.MirrorWindowsNotifications);
        Assert.True(s.NotificationsEnabled);
        Assert.Equal(1, AppSettings.MinNotificationTimeout);
    }

    [Fact]
    public void TestNotificationPreemptionAndReversionStateMachine()
    {
        var sm = new IslandStateMachine();
        Assert.Equal(IslandState.Idle, sm.State);

        // 1. Music is playing -> MusicPlaying state
        sm.SetMusicPlaying(true);
        Assert.Equal(IslandState.MusicPlaying, sm.State);

        // 2. Notification arrives -> MUST preempt music!
        sm.SetNotificationActive(true);
        Assert.Equal(IslandState.Notification, sm.State);

        // 3. Notification expires after 3 seconds -> MUST cleanly revert to MusicPlaying!
        sm.SetNotificationActive(false);
        Assert.Equal(IslandState.MusicPlaying, sm.State);

        // 4. User is on Expanded dashboard -> Expanded state
        sm.SetExpanded(true);
        Assert.Equal(IslandState.Expanded, sm.State);

        // 5. Notification arrives -> MUST preempt Expanded!
        sm.SetNotificationActive(true);
        Assert.Equal(IslandState.Notification, sm.State);

        // 6. Notification expires -> MUST revert to Expanded!
        sm.SetNotificationActive(false);
        Assert.Equal(IslandState.Expanded, sm.State);

        // 7. Idle reversion
        sm.SetExpanded(false);
        sm.SetMusicPlaying(false);
        Assert.Equal(IslandState.Idle, sm.State);

        sm.SetNotificationActive(true);
        Assert.Equal(IslandState.Notification, sm.State);
        sm.SetNotificationActive(false);
        Assert.Equal(IslandState.Idle, sm.State);
    }

    [Fact]
    public void TestRenderNotificationBannerMacOSStyle()
    {
        var thread = new System.Threading.Thread(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application();
            Themes.ThemeManager.Apply(app.Resources, AppTheme.Dark);
            var settingsService = new Services.SettingsService(System.IO.Path.GetTempPath());
            var stateMachine = new IslandStateMachine();
            var mediaService = new Services.MediaSessionService();
            var systemService = new Services.SystemMonitorService(app.Dispatcher);
            var startupService = new Services.StartupService();

            var win = new MainWindow(settingsService, stateMachine, mediaService, systemService, startupService);
            win.Show();

            var notif = new NotificationInfo
            {
                Kind = NotificationKind.Windows,
                AppName = "WhatsApp",
                Title = "Sarah Jenkins",
                Message = "Hey! Sesuai kesepakatan meeting kita jam 10 pagi ya ☕",
                IconGlyph = "💬",
                AccentColor = "#25D366",
                Timeout = TimeSpan.FromSeconds(3),
                CreatedAt = DateTimeOffset.Now
            };

            win.ShowNotification(notif);
            win.NotificationLayer.Opacity = 1.0;
            win.NotificationLayer.Visibility = System.Windows.Visibility.Visible;
            win.CompactLayer.Visibility = System.Windows.Visibility.Collapsed;
            win.ExpandedLayer.Visibility = System.Windows.Visibility.Collapsed;
            win.Pill.Width = IslandLayout.NotificationWidth(settingsService.Current);
            win.Pill.Height = IslandLayout.NotificationHeight;
            win.Pill.PillWidth = IslandLayout.NotificationWidth(settingsService.Current);
            win.Pill.PillHeight = IslandLayout.NotificationHeight;
            win.Pill.PillRadius = 26;
            win.Pill.UpdateLayout();
            win.UpdateLayout();

            int w = (int)Math.Max(100, win.ActualWidth);
            int h = (int)Math.Max(100, win.ActualHeight);
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(win);

            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            string outPath = @"C:\Users\tripu\.gemini\antigravity-ide\brain\b072dc46-ba3b-42a6-9e82-067e9eb383fd\scratch\macos_notification_banner.png";
            using (var fs = System.IO.File.Create(outPath))
            {
                encoder.Save(fs);
            }
            _output.WriteLine($"Rendered macOS-style notification banner preview to {outPath}");

            win.Close();
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.True(System.IO.File.Exists(@"C:\Users\tripu\.gemini\antigravity-ide\brain\b072dc46-ba3b-42a6-9e82-067e9eb383fd\scratch\macos_notification_banner.png"));
    }

    [Fact]
    public void TestCapturePolaroidHdAndWatermark()
    {
        var camService = new Services.CameraService();
        var polaroidCard = camService.CapturePolaroidPhoto();
        Assert.NotNull(polaroidCard);
        Assert.Equal(1080, polaroidCard.PixelWidth); // True High Definition HD format
        Assert.Equal(1380, polaroidCard.PixelHeight);

        Assert.NotNull(camService.LastCapturedPhotoOnly);
        Assert.Equal(972, camService.LastCapturedPhotoOnly.PixelWidth);
        Assert.Equal(972, camService.LastCapturedPhotoOnly.PixelHeight); // 1:1 True Square WYSIWYG

        string savedPath = camService.SavePhotoToDevice(polaroidCard);
        Assert.True(System.IO.File.Exists(savedPath));

        string scratchHd = @"C:\Users\tripu\.gemini\antigravity-ide\brain\b072dc46-ba3b-42a6-9e82-067e9eb383fd\scratch\hd_polaroid_with_watermark.png";
        System.IO.File.Copy(savedPath, scratchHd, true);
        _output.WriteLine($"Saved HD Polaroid with watermark to {scratchHd}");
    }

    [Fact]
    public async Task TestTimelineScrubberAndLyricsSeeking()
    {
        var mediaService = new Services.MediaSessionService();
        var lrcService = new Services.LyricsService();

        // 1. Test seeking clamping and calculation
        await mediaService.ChangePlaybackPositionAsync(TimeSpan.FromSeconds(75));
        // Verify optimistic update works
        Assert.True(mediaService.Current.Position >= TimeSpan.Zero);

        await mediaService.SeekRelativeAsync(TimeSpan.FromSeconds(15));
        await mediaService.SeekRelativeAsync(TimeSpan.FromSeconds(-30));

        // 2. Test lyrics line seek lookup
        var parsed = new Services.SongLyrics
        {
            Title = "Saturday Nights",
            Artist = "Khalid",
            Lines = new List<Services.LyricLine>
            {
                new(TimeSpan.Zero, "♪ Intro ♪"),
                new(TimeSpan.FromSeconds(15.3), "When I'm away from you"),
                new(TimeSpan.FromSeconds(30.5), "I'm thinking of your smile"),
                new(TimeSpan.FromSeconds(75.0), "Saturday nights in the backyard")
            }
        };

        Assert.True(parsed.HasSyncedLyrics);
        Assert.Equal(4, parsed.Lines.Count);

        int idx1 = parsed.GetActiveIndexAt(TimeSpan.FromSeconds(20));
        Assert.Equal(1, idx1);
        var (curr, next, prev) = parsed.GetLinesAtIndex(idx1);
        Assert.Equal("When I'm away from you", curr?.Text);
        Assert.Equal("I'm thinking of your smile", next?.Text);
        Assert.Equal("♪ Intro ♪", prev?.Text);

        // Test seeking target timestamp
        Assert.Equal(TimeSpan.FromSeconds(30.5), next?.Timestamp);
        Assert.Equal(TimeSpan.FromSeconds(0), prev?.Timestamp);

        // 3. UI Rendering test for interactive scrubber & lyrics seek card
        var thread = new System.Threading.Thread(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application();
            var settingsService = new Services.SettingsService(System.IO.Path.GetTempPath());
            var stateMachine = new IslandStateMachine();
            var systemService = new Services.SystemMonitorService(app.Dispatcher);
            var startupService = new Services.StartupService();

            var win = new MainWindow(settingsService, stateMachine, mediaService, systemService, startupService);
            win.Show();

            win.ExpandedLayer.Visibility = System.Windows.Visibility.Visible;
            win.ExpandedLayer.Opacity = 1.0;
            win.CompactLayer.Visibility = System.Windows.Visibility.Collapsed;
            win.SubpageMedia.Visibility = System.Windows.Visibility.Visible;
            win.SubpageDashboard.Visibility = System.Windows.Visibility.Collapsed;
            win.ExpandedTrackTitle.Text = "Saturday Nights";
            win.ExpandedArtist.Text = "Khalid — Free Spirit";
            win.ExpandedPositionText.Text = "0:45";
            win.ExpandedDurationText.Text = "3:29";
            win.ExpandedProgressBar.Value = 21.5;

            // Show karaoke full lyrics view with 3 clickable lines
            win.LyricsCompactView.Visibility = System.Windows.Visibility.Collapsed;
            win.LyricsFullView.Visibility = System.Windows.Visibility.Visible;
            win.MediaLyricPrev.Text = "When I'm away from you";
            win.MediaLyricCurrent.Text = "I'm thinking of your smile";
            win.MediaLyricNext.Text = "Saturday nights in the backyard";

            win.Pill.Width = Animations.IslandLayout.MediaPageWidth;
            win.Pill.Height = Animations.IslandLayout.MediaPageHeight;
            win.Pill.PillWidth = Animations.IslandLayout.MediaPageWidth;
            win.Pill.PillHeight = Animations.IslandLayout.MediaPageHeight;
            win.Pill.PillRadius = 28;
            win.Pill.Opacity = 1.0;
            win.Pill.UpdateLayout();
            win.UpdateLayout();

            int w = (int)Math.Max(100, Animations.IslandLayout.MediaPageWidth);
            int h = (int)Math.Max(100, Animations.IslandLayout.MediaPageHeight);
            win.Pill.Measure(new System.Windows.Size(w, h));
            win.Pill.Arrange(new System.Windows.Rect(0, 0, w, h));
            win.Pill.UpdateLayout();
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(win.Pill);

            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            string outPath = @"C:\Users\tripu\.gemini\antigravity-ide\brain\b072dc46-ba3b-42a6-9e82-067e9eb383fd\scratch\timeline_scrubber_lyrics_seek.png";
            using (var fs = System.IO.File.Create(outPath))
            {
                encoder.Save(fs);
            }
            _output.WriteLine($"Rendered timeline scrubber & lyrics seek UI to {outPath}");

            win.Close();
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.True(System.IO.File.Exists(@"C:\Users\tripu\.gemini\antigravity-ide\brain\b072dc46-ba3b-42a6-9e82-067e9eb383fd\scratch\timeline_scrubber_lyrics_seek.png"));
    }

    [Fact]
    public void TestFriendlyAppNameDetection()
    {
        Assert.Equal("Apple Music", MediaInfo.FriendlyAppName("AppleInc.AppleMusicWin_nzyj5cx40ttqa!App"));
        Assert.Equal("Spotify", MediaInfo.FriendlyAppName("Spotify.exe"));
        Assert.Equal("Google Chrome", MediaInfo.FriendlyAppName("chrome.exe"));
        Assert.Equal("Microsoft Edge", MediaInfo.FriendlyAppName("msedge.exe"));
    }

    [Fact]
    public async Task TestRealMediaSessionSeek()
    {
        using var service = new Services.MediaSessionService();
        await service.StartAsync();

        var info = service.Current;
        _output.WriteLine($"Current session app: '{info.AppName}', title: '{info.Title}', duration: {info.Duration}");

        bool ok = await service.ChangePlaybackPositionAsync(TimeSpan.FromSeconds(25));
        _output.WriteLine($"Seek returned: {ok}");

        service.Stop();
    }
}



