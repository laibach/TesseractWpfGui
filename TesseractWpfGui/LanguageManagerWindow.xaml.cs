using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using TesseractWpfGui;

namespace TesseractWpfGui
{
    public partial class LanguageManagerWindow : Window
    {
        public ObservableCollection<LanguageModelViewModel> LanguageModels { get; set; }
        private readonly string _tessdataFolder;
        private static readonly HttpClient httpClient = new HttpClient();

        public LanguageManagerWindow(string tessdataFolder)
        {
            InitializeComponent();
            _tessdataFolder = tessdataFolder;
            LanguageModels = new ObservableCollection<LanguageModelViewModel>();
            DataContext = this;

            // Notwendige Konverter für die UI-Bindings hinzufügen
            // Resources.Add("BooleanToVisibilityConverter", new BooleanToVisibilityConverter());
            // Resources.Add("InverseBooleanToVisibilityConverter", new InverseBooleanToVisibilityConverter());

            LoadLanguagesAsync();
        }

        private async void LoadLanguagesAsync()
        {
            try
            {
                // GitHub API aufrufen, um die Liste der "besten" Sprachmodelle zu erhalten
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/tesseract-ocr/tessdata_best/contents/");
                request.Headers.Add("User-Agent", "TesseractWpfGui"); // GitHub API erfordert einen User-Agent

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();
                var files = JsonSerializer.Deserialize<List<GitHubFile>>(jsonString);

                var installedFiles = Directory.GetFiles(_tessdataFolder, "*.traineddata")
                                             .Select(Path.GetFileName)
                                             .ToHashSet();

                if (files != null)
                {
                    foreach (var file in files.Where(f => f.name.EndsWith(".traineddata")).OrderBy(f => f.name))
                    {
                        var vm = new LanguageModelViewModel(file.name, file.download_url, _tessdataFolder, installedFiles.Contains(file.name));
                        LanguageModels.Add(vm);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden der Sprachliste: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Hilfsklasse für das Deserialisieren der GitHub API-Antwort
        private class GitHubFile
        {
            [JsonPropertyName("name")]
            public string name { get; set; } = null!;

            [JsonPropertyName("download_url")]
            public string download_url { get; set; } = null!;
        }
    }

    // Konverter, um einen booleschen Wert in Sichtbarkeit umzuwandeln (für den Fortschrittsbalken)
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => (bool)value ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }
}