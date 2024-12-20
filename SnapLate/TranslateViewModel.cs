using System.ComponentModel;
using System.Windows.Media;

namespace SnapLate
{
    public class TranslateViewModel:INotifyPropertyChanged
    {
        public Dictionary<string, string> SrcLang { get; } = new Dictionary<string, string>(LanguageMapping.LanguageNames);
        public Dictionary<string, string> TargetLang { get; } = new Dictionary<string, string>(LanguageMapping.LanguageNames);

        public struct SettingsStruct
        {
            public string ShortCut {  get; set; }
            public string SourceLang { get; set; }
            public string TargetLang { get; set; }
            public float FontSize { get; set; }
            public SolidColorBrush PrimaryColor { get; set; }
            public SolidColorBrush PrimaryDarkColor { get; set; }
            public SolidColorBrush SecondaryColor { get; set; }
            public SolidColorBrush SecondaryDarkColor { get; set; }
            public SolidColorBrush TextColor { get; set; }
        }

        public static SettingsStruct Default => new()
        {
            ShortCut = "ALT+S",
            SourceLang = "auto",
            TargetLang = "en",
            FontSize = 14,
            PrimaryColor = new SolidColorBrush(Colors.Brown),
            PrimaryDarkColor = new SolidColorBrush(Colors.RosyBrown),
            SecondaryColor = new SolidColorBrush(Color.FromRgb(153, 153, 153)),
            SecondaryDarkColor = new SolidColorBrush(Color.FromRgb(33, 33, 33)),
            TextColor = new SolidColorBrush(Color.FromRgb(187, 187, 187)),
        };

        private SettingsStruct _settings = Default;
        public SettingsStruct Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                OnPropertyChanged(nameof(Settings));
            }
        }

        public TranslateViewModel() {
            TargetLang.Remove("auto");
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
