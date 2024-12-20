using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Text.Json;
using System.Web;
using Tesseract;
using Rect = System.Windows.Rect;
using System.Windows.Threading;
using Widgets.Common;
using System.Reflection;
using System;

namespace SnapLate
{
    public partial class MainWindow : Window, IWidgetWindow
    {
        [GeneratedRegex(@"(\r?\n)+")]
        private static partial Regex ClearNewLineRegex();

        public readonly static string WidgetName = "SnapLate";
        public readonly static string SettingsFile = "settings.snaplate.json";
        public readonly static string ThisPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

        private static readonly string tessaract_data_path = Path.Combine(ThisPath, "tesseract_data");
        private static readonly string tessaract_data_github_base_url = "https://raw.githubusercontent.com/tesseract-ocr/tessdata/refs/heads/main/";
        private static readonly string translate_api_url = "https://translate.googleapis.com/translate_a/single";

        private readonly Config config = new(SettingsFile);
        private TranslateViewModel.SettingsStruct Settings = TranslateViewModel.Default;
        public TranslateViewModel ViewModel { get; set; }
        private System.Windows.Point startPoint;
        private System.Windows.Point endPoint;
        private Window? overlayWindow;
        private Canvas? overlayCanvas;
        private bool isSelecting;
        private Border? selectionRectangle;
        private byte[]? memoryImage;
        private DispatcherTimer _typingTimer;
        private bool _isTranslating;
        private ToolTip _copyTooltip = new ToolTip();

        public MainWindow()
        {
            LoadSettings();
            InitializeComponent();

            ViewModel = new TranslateViewModel();
            DataContext = ViewModel;

            Loaded += Window_Loaded;

            _typingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _typingTimer.Tick += TypingTimer_Tick;
        }

        // config file load
        private void LoadSettings()
        {
            try
            {
                Settings.ShortCut = PropertyParser.ToString(config.GetValue("short_cut"), Settings.ShortCut);
                Settings.SourceLang = PropertyParser.ToString(config.GetValue("source_lang"), Settings.SourceLang);
                Settings.TargetLang = PropertyParser.ToString(config.GetValue("target_lang"), Settings.TargetLang);
                Settings.FontSize = PropertyParser.ToFloat(config.GetValue("font_size"), Settings.FontSize);
                Settings.PrimaryColor = PropertyParser.ToColorBrush(config.GetValue("primary_color"), Settings.PrimaryColor.ToString());
                Settings.PrimaryDarkColor = PropertyParser.ToColorBrush(config.GetValue("primary_color_light"), Settings.PrimaryDarkColor.ToString());
                Settings.SecondaryColor = PropertyParser.ToColorBrush(config.GetValue("secondary_color"), Settings.SecondaryColor.ToString());
                Settings.SecondaryDarkColor = PropertyParser.ToColorBrush(config.GetValue("secondary_color_light"), Settings.SecondaryDarkColor.ToString());
                Settings.TextColor = PropertyParser.ToColorBrush(config.GetValue("text_color"), Settings.TextColor.ToString());
            }
            catch (Exception)
            {
                config.Add("short_cut", Settings.ShortCut);
                config.Add("source_lang", Settings.SourceLang);
                config.Add("target_lang", Settings.TargetLang);
                config.Add("font_size", Settings.FontSize);
                config.Add("primary_color", Settings.PrimaryColor);
                config.Add("primary_color_light", Settings.PrimaryDarkColor);
                config.Add("secondary_color", Settings.SecondaryColor);
                config.Add("secondary_color_light", Settings.SecondaryDarkColor);
                config.Add("text_color", Settings.TextColor);

                config.Save();
            }
        }

        // Window to WdigetWindow
        public WidgetWindow WidgetWindow()
        {
            return new WidgetWindow(this, WidgetDefaultStruct());
        }

        // WidgetWindow default settings
        public static WidgetDefaultStruct WidgetDefaultStruct()
        {
            return new()
            {
                Width = 400,
                MinHeight = 200,
                MaxHeight = 600,
                SizeToContent = SizeToContent.Height
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FromLang.SelectedValue = Settings.SourceLang;
            ToLang.SelectedValue = Settings.TargetLang;

            SSButton.ToolTip = new ToolTip
            {
                Content = Settings.ShortCut,
                Background = Settings.SecondaryDarkColor,
                Foreground = Settings.TextColor,
            };
            ToolTipService.SetInitialShowDelay(SSButton, 100);

            HotKey.Register(Settings.ShortCut, () => {
                ShowOverlay();
                StartMouseTracking();
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            Settings.SourceLang = (string)FromLang.SelectedValue;
            Settings.TargetLang = (string)ToLang.SelectedValue;
            config.Add("source_lang", Settings.SourceLang);
            config.Add("target_lang", Settings.TargetLang);
            config.Save();

            FromText.TextChanged -= FromText_Changed;
  
            HotKey.Unregister();
            base.OnClosed(e);
        }

        protected override void OnContentRendered(EventArgs e)
        {
            var mainWindowsResource = this.Resources.MergedDictionaries;
            var styleResource = mainWindowsResource.FirstOrDefault(d => d.Source.OriginalString == "Style.xaml");

            if (styleResource != null)
            {
                styleResource["PrimaryColor"] = Settings.PrimaryColor.Color;
                styleResource["PrimaryDarkColor"] = Settings.PrimaryDarkColor.Color;
                styleResource["SecondaryColor"] = Settings.SecondaryColor.Color;
                styleResource["SecondaryDarkColor"] = Settings.SecondaryDarkColor.Color;
                styleResource["TextColor"] = Settings.TextColor.Color;
            }
            base.OnContentRendered(e);
        }

        // Screenshot overlayWindow Open
        private void ScreenShot_Button_Click(object sender, RoutedEventArgs e)
        {
            ShowOverlay();
            StartMouseTracking();
        }

        // overlay window
        private void ShowOverlay()
        {
            HideOverlay();

            overlayWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                Background = new SolidColorBrush(Colors.Black) { Opacity = 0.1 },
                Topmost = true,
                AllowsTransparency = true,
                Width = SystemParameters.VirtualScreenWidth,
                Height = SystemParameters.VirtualScreenHeight,
                Left = 0,
                Top = 0,
                Cursor = Cursors.Cross,
            };

            overlayWindow.Deactivated += OverlayWindow_Deactivated;
            overlayWindow.KeyDown += (e, s) =>
            {
                if (s.Key == Key.Escape)
                {
                    HideOverlay();
                }
            };

            selectionRectangle = new Border
            {
                Width = 0,
                Height = 0,
                BorderBrush = new SolidColorBrush(Colors.LightYellow),
                BorderThickness = new Thickness(1)
            };

            overlayCanvas = new()
            {
                Background = new SolidColorBrush(Colors.Black) { Opacity = 0.4 },
                Width = SystemParameters.VirtualScreenWidth,
                Height = SystemParameters.VirtualScreenHeight
            };

            overlayCanvas.Children.Add(selectionRectangle);
            overlayWindow.Content = overlayCanvas;
            overlayWindow.Show();
            overlayWindow.Activate();
        }

        // Close overlayWindow
        private void HideOverlay()
        {
            if (overlayWindow != null && overlayWindow.IsVisible)
            {
                overlayWindow.Deactivated -= OverlayWindow_Deactivated;
                overlayWindow.Close();
                StopMouseTracking();
            }
        }

        private void OverlayWindow_Deactivated(object? sender, EventArgs e)
        {
            HideOverlay();
        }

        // Selection area
        private void RedrawSelectionArea()
        {
            if (overlayWindow == null || overlayCanvas == null || selectionRectangle == null)
            {
                return;
            }

            try
            {
                var width = Math.Abs(startPoint.X - endPoint.X);
                var height = Math.Abs(startPoint.Y - endPoint.Y);
                var x = Math.Min(startPoint.X, endPoint.X);
                var y = Math.Min(startPoint.Y, endPoint.Y);

                var outerRect = new RectangleGeometry(new Rect(0, 0, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight));
                var innerRect = new RectangleGeometry(new Rect(x, y, width, height));
                var combinedGeometry = new CombinedGeometry(GeometryCombineMode.Exclude, outerRect, innerRect);

                overlayCanvas.Clip = combinedGeometry;

                // for selection range border width
                selectionRectangle.Width = width + 2;
                selectionRectangle.Height = height + 2;

                Canvas.SetLeft(selectionRectangle, x - 1);
                Canvas.SetTop(selectionRectangle, y - 1);
            }
            catch (Exception ex)
            {
                Logger.Error("Selection Area Error: " + ex.Message);
            }
        }

        // Screenshot
        private void CaptureScreen(System.Windows.Point start, System.Windows.Point end)
        {
            try
            {
                int x = (int)Math.Min(start.X, end.X);
                int y = (int)Math.Min(start.Y, end.Y);
                int width = (int)Math.Abs(start.X - end.X);
                int height = (int)Math.Abs(start.Y - end.Y);

                if (width > 0 && height > 0)
                {
                    using var bmp = new Bitmap(width, height);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height));
                    }

                    using var memoryStream = new MemoryStream();
                    bmp.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                    memoryImage = memoryStream.ToArray();
                    //bmp.Save("screenshot.png", System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Screenshot Error: " + ex.Message);
            }
        }

        // Tessarct Screenshot image to text
        async Task<string> PerformOCR(byte[] image, string lang, string tessDataPath)
        {
            try
            {
                var multiLang = lang.Split('+').Distinct().ToArray();
                
                foreach (var singleLang in multiLang)
                {
                    var tessDataUrl = new Uri($"{tessaract_data_github_base_url}{singleLang}.traineddata");
                    var tessDataFile = Path.Combine(tessDataPath, $"{singleLang}.traineddata");

                    if (!File.Exists(tessDataFile))
                    {
                        Logger.Info($"Language file {singleLang} downloading..");
                        ToText.Text = $"Language file {singleLang} downloading..";

                        using HttpClient client = new();

                        using HttpResponseMessage response = await client.GetAsync(tessDataUrl);
                        response.EnsureSuccessStatusCode();

                        using Stream stream = await response.Content.ReadAsStreamAsync();

                        using FileStream fs = new(tessDataFile, FileMode.Create, FileAccess.Write);
                        await stream.CopyToAsync(fs);

                        Logger.Info($"Language file {singleLang} downloaded.");
                        ToText.Text = string.Empty;
                    }

                }

                using var engine = new TesseractEngine(tessDataPath, lang, EngineMode.Default);
                using var img = Pix.LoadFromMemory(image);
                using var page = engine.Process(img);
                return page.GetText();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Inner Exception: " + ex.InnerException?.Message);
                Debug.WriteLine("Stack Trace: " + ex.StackTrace);
                Logger.Error("OCR Error: " + ex.Message);
                return string.Empty;
            }
        }

        // Google translate api
        private async Task<string> TranslateApiRequest(string q, string sl = "auto", string tl = "auto")
        {
            try
            {
                if (string.IsNullOrEmpty(q))
                {
                    return string.Empty;
                }

                var translateApiUrl = new Uri($"{translate_api_url}?client=gtx&sl={sl}&tl={tl}&dt=t&dt=bd&dj=1&q={q}");

                using HttpClient client = new();

                using HttpResponseMessage response = await client.GetAsync(translateApiUrl);
                response.EnsureSuccessStatusCode();
                string stringResponse = await response.Content.ReadAsStringAsync();

                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(stringResponse);

                string translated = "";
                string original = "";

                foreach (var property in jsonResponse.GetProperty("sentences").EnumerateArray())
                {
                    translated += property.GetProperty("trans").GetString() ?? string.Empty;
                    original += property.GetProperty("orig").GetString() ?? string.Empty;
                }

                if (jsonResponse.TryGetProperty("dict", out var dict))
                {
                    dict = jsonResponse.GetProperty("dict")[0];
                    string? pos = dict.GetProperty("pos").GetString();
                    string? terms = string.Join(", ", dict.GetProperty("terms").EnumerateArray().Select(e => e.GetString()));

                    if (!string.IsNullOrEmpty(pos) && !string.IsNullOrEmpty(terms))
                    {
                        translated += $"\n\n{pos}: {terms}";
                    }
                }


                return translated;
            }
            catch (Exception ex)
            {
                Logger.Error("Translate Api Error" + ex.Message);
                return ex.Message;
            }
        }

        // Translate trigger method
        private async void Translate()
        {
            if (_isTranslating == true) return;
            _isTranslating = true;

            string sl = "auto";
            string tl = "auto";
            string q = HttpUtility.UrlEncode(FromText.Text.Trim());

            if (FromLang.SelectedValue != null)
            {
                sl = (string)FromLang.SelectedValue;
            }

            if (ToLang.SelectedValue != null)
            {
                tl = (string)ToLang.SelectedValue;
            }

            ToText.Text = await TranslateApiRequest(q, sl, tl);
            this.Activate();
            _isTranslating = false;
        }

        // Global Mouse Events
        private void StartMouseTracking()
        {
            if (overlayWindow == null) return;

            overlayWindow.MouseDown -= OverlayWindow_MouseDown;
            overlayWindow.MouseDown += OverlayWindow_MouseDown;

            overlayWindow.MouseMove -= OverlayWindow_MouseMove;
            overlayWindow.MouseMove += OverlayWindow_MouseMove;

            overlayWindow.MouseUp -= OverlayWindow_MouseUp;
            overlayWindow.MouseUp += OverlayWindow_MouseUp;
        }

        private void StopMouseTracking()
        {
            if (overlayWindow == null) return;

            overlayWindow.MouseDown -= OverlayWindow_MouseDown;
            overlayWindow.MouseMove -= OverlayWindow_MouseMove;
            overlayWindow.MouseUp -= OverlayWindow_MouseUp;
        }

        private void OverlayWindow_MouseDown(object? sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(overlayWindow);
                startPoint = new System.Windows.Point(pos.X, pos.Y);
                isSelecting = true;
            }
        }

        private void OverlayWindow_MouseMove(object? sender, MouseEventArgs e)
        {
            if (isSelecting)
            {
                var pos = e.GetPosition(overlayWindow);
                endPoint = new System.Windows.Point(pos.X, pos.Y);
                RedrawSelectionArea();
            }
        }

        private async void OverlayWindow_MouseUp(object? sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Released)
            {
                var pos = e.GetPosition(overlayWindow);
                endPoint = new System.Windows.Point(pos.X, pos.Y);
                HideOverlay();
                CaptureScreen(startPoint, endPoint);

                isSelecting = false;

                if (memoryImage != null)
                {
                    string lang = "auto";

                    if (FromLang.SelectedValue != null)
                    {
                        lang = (string)FromLang.SelectedValue;
                    }

                    var tesseractLang = LanguageMapping.LangToTessaract(lang);

                    if (ToLang.SelectedValue != null)
                    {
                        var lang2 = (string)ToLang.SelectedValue;
                        var tesseractLang2 = LanguageMapping.LangToTessaract(lang2);
                        tesseractLang = tesseractLang + "+" + tesseractLang2;
                    }

                    var text = await PerformOCR(memoryImage, tesseractLang, tessaract_data_path);
                    text = ClearNewLineRegex().Replace(text, "\n").Trim();
                    FromText.Text = text;
                }
            }
        }

        // Lazy Typing
        private void TypingTimer_Tick(object? sender, EventArgs e)
        {
            _typingTimer.Stop();
            Translate();
        }

        // Reset timer tick
        private void FromText_Changed(object? sender, EventArgs e)
        {
            _typingTimer.Stop();
            _typingTimer.Start();
        }

        // From lang and to lang are swapped
        private void SwapLanguage_Click(object? sender, EventArgs e)
        {
            var fromLangValue = (string)FromLang.SelectedValue;
            var toLangValue = (string)ToLang.SelectedValue;

            if (fromLangValue != null && toLangValue != null)
            {
                if (fromLangValue != "auto")
                {
                    ToLang.SelectedValue = fromLangValue;
                    FromLang.SelectedValue = toLangValue;
                }
            }
        }

        private void Language_Changed(object? sender, EventArgs e)
        {
            Translate();
        }

        private async void CopyFromText_Button_Click(object? sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(FromText.Text) && sender != null)
            {
                Clipboard.SetText(FromText.Text);

                _copyTooltip.Content = "Copied";
                _copyTooltip.PlacementTarget = (UIElement)sender;
                _copyTooltip.Placement = System.Windows.Controls.Primitives.PlacementMode.Left;
                _copyTooltip.Background = Settings.SecondaryDarkColor;
                _copyTooltip.Foreground = Settings.TextColor;
                _copyTooltip.FontSize = 10;
                _copyTooltip.Padding = new Thickness(2);
                _copyTooltip.IsOpen = true;

                await Task.Delay(1000);
                _copyTooltip.IsOpen = false;
            }
        }

        private async void CopyToText_Button_Click(object? sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(ToText.Text) && sender != null)
            {
                Clipboard.SetText(ToText.Text);

                _copyTooltip.Content = "Copied";
                _copyTooltip.PlacementTarget = (UIElement)sender;
                _copyTooltip.Placement = System.Windows.Controls.Primitives.PlacementMode.Left;
                _copyTooltip.Background = Settings.SecondaryDarkColor;
                _copyTooltip.Foreground = Settings.TextColor;
                _copyTooltip.FontSize = 10;
                _copyTooltip.Padding = new Thickness(2);
                _copyTooltip.IsOpen = true;

                await Task.Delay(1000);
                _copyTooltip.IsOpen = false;
            }
        }

        private void Clear_Button_Click(object? sender, EventArgs e)
        {
            FromText.Text = string.Empty;
            ToText.Text = string.Empty;
        }
    }
}
