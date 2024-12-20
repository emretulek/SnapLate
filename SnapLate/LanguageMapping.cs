using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace SnapLate
{
    public class LanguageMapping
    {
        private static readonly string language_path = Path.Combine(MainWindow.ThisPath, "language_mapping");
        public static readonly Dictionary<string, string> LanguageNames = GetLanguageNames();
        private static readonly Dictionary<string, string> TessearctLang = GetLanguageTesseract();
        private static readonly Dictionary<string, string> TessaractAlternate = GetTesseractAlternate();

        public static string LangToTessaract(string key)
        {
            string tessaractShort = string.Empty;

            foreach (var item in TessearctLang)
            {
                if (item.Value == key)
                {
                    tessaractShort = item.Key;
                }
            }

            if (string.IsNullOrEmpty(tessaractShort))
            {
                if(TessaractAlternate.TryGetValue(key, out var alternate))
                {
                    foreach (var item in TessearctLang)
                    {
                        if (item.Value == alternate)
                        {
                            tessaractShort = item.Key;
                        }
                    }
                }
            }

            if(string.IsNullOrEmpty(tessaractShort))
            {
                tessaractShort = "eng";
            }

            return tessaractShort;
        }

        public static Dictionary<string, string> GetLanguageNames()
        {
            Dictionary<string, string> dictionary = [];
            string jsonFilePath = Path.Combine(language_path,"language_names.json");

            try
            {   
                string jsonString = File.ReadAllText(jsonFilePath);
                dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString) ?? [];
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return dictionary;
        }

        public static Dictionary<string, string> GetLanguageTesseract()
        {
            Dictionary<string, string> dictionary = [];
            string jsonFilePath = Path.Combine(language_path, "language_tesseract.json");

            try
            {
                string jsonString = File.ReadAllText(jsonFilePath);
                dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString) ?? [];
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return dictionary;
        }

        public static Dictionary<string, string> GetTesseractAlternate()
        {
            Dictionary<string, string> dictionary = [];
            string jsonFilePath = Path.Combine(language_path, "tesseract_alternate.json");

            try
            {
                string jsonString = File.ReadAllText(jsonFilePath);
                dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString) ?? [];
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return dictionary;
        }

    }
}
