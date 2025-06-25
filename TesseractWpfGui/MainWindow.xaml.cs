using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;


namespace TesseractWpfGui
{
    public partial class MainWindow : Window
    {
        // Hier speichern wir den gefundenen Pfad zur tesseract.exe für die aktuelle Sitzung
        private string _tesseractExePath = @"C:\Program Files\Tesseract-OCR\tesseract.exe";
        string tessdataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeineWpfAnwendung");

        public event PropertyChangedEventHandler? PropertyChanged;
        private string _outputFileBase = "";
        public string OutputFileBase
        {
            get { return _outputFileBase; }
            set
            {
                _outputFileBase = value;
                OnPropertyChanged(nameof(OutputFileBase));
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string SelectedOutputFormat
        {
            get { return OutputFormatComboBox.SelectedItem as string ?? "txt"; }
            set
            {
                OutputFormatComboBox.SelectedItem = value;
                // Notify the UI that the property has changed
                OnPropertyChanged(nameof(SelectedOutputFormat));
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            // Die Initialisierung wird nach dem Laden des Fensters gestartet
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            PopulatePsmComboBox();
            PopulateOutputFormatComboBox();
            await FindTesseractPathAsync();
        }

        /// <summary>
        /// Befüllt die ComboBox für die Page Segmentation Modes mit benutzerfreundlichen Beschreibungen.
        /// </summary>
        private void PopulatePsmComboBox()
        {
            var psmModes = new Dictionary<string, int>
            {
                { "3: Auto page segmentation (Default)", 3 },
                { "0: Orientation and script detection (OSD) only", 0 },
                { "1: Automatic page segmentation with OSD", 1 },
                { "4: Assume a single column of text", 4 },
                { "5: Assume a single uniform block of vertical text", 5 },
                { "6: Assume a single uniform block of text", 6 },
                { "7: Treat the image as a single text line", 7 },
                { "8: Treat the image as a single word", 8 },
                { "9: Treat the image as a single word in a circle", 9 },
                { "10: Treat the image as a single character", 10 },
                { "11: Sparse text", 11 },
                { "12: Sparse text with OSD", 12 },
                { "13: Raw line", 13 }
            };

            PsmComboBox.ItemsSource = psmModes;
            PsmComboBox.DisplayMemberPath = "Key";
            PsmComboBox.SelectedValuePath = "Value";
            PsmComboBox.SelectedValue = 3; // Standardwert setzen
        }

        /// <summary>
        /// Befüllt die ComboBox für die wählbaren Ausgabeformate.
        /// </summary>
        private void PopulateOutputFormatComboBox()
        {
            var outputFormats = new List<string> { "txt", "pdf", "hocr", "tsv" };
            OutputFormatComboBox.ItemsSource = outputFormats;
            SelectedOutputFormat = "txt"; // Set the initial value
        }

        /// <summary>
        /// Sucht nach tesseract.exe in der PATH-Variable und an bekannten Orten.
        /// Fordert den Benutzer zur manuellen Auswahl auf, wenn nichts gefunden wird.
        /// </summary>
        private async Task FindTesseractPathAsync()
        {
            StatusTextBlock.Text = "Suche nach Tesseract-OCR...";

            // 1. Suche in der PATH-Umgebungsvariable
            string? pathVar = Environment.GetEnvironmentVariable("PATH");
            if (pathVar != null)
            {
                var paths = pathVar.Split(Path.PathSeparator);
                foreach (var path in paths)
                {
                    string potentialPath = Path.Combine(path, "tesseract.exe");
                    if (File.Exists(potentialPath))
                    {
                        _tesseractExePath = potentialPath;
                        break;
                    }
                }
            }

            // 2. Wenn nicht im PATH, suche in Standard-Verzeichnissen
            if (string.IsNullOrEmpty(_tesseractExePath))
            {
                string[] commonPaths =
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", "tesseract.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tesseract-OCR", "tesseract.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tesseract-OCR", "tesseract.exe")
                };

                foreach (var path in commonPaths)
                {
                    if (File.Exists(path))
                    {
                        _tesseractExePath = path;
                        break;
                    }
                }
            }

            // 3. Wenn immer noch nicht gefunden, frage den Benutzer
            if (string.IsNullOrEmpty(_tesseractExePath))
            {
                StatusTextBlock.Text = "Tesseract-OCR konnte nicht automatisch gefunden werden. Bitte wählen Sie die 'tesseract.exe'-Datei manuell aus.";
                MessageBox.Show(StatusTextBlock.Text, "Tesseract nicht gefunden", MessageBoxButton.OK, MessageBoxImage.Warning);

                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "Tesseract Executable (tesseract.exe)|tesseract.exe",
                    Title = "Bitte tesseract.exe auswählen"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    _tesseractExePath = openFileDialog.FileName;
                }
            }

            // Finaler Check und UI-Status aktualisieren
            if (!string.IsNullOrEmpty(_tesseractExePath) && File.Exists(_tesseractExePath))
            {
                StatusTextBlock.Text = $"Bereit. Tesseract gefunden unter: {_tesseractExePath}";
                StartButton.IsEnabled = true;
            }
            else
            {
                StatusTextBlock.Text = "Fehler: Es wurde kein gültiger Pfad für tesseract.exe festgelegt. Der Start-Button ist deaktiviert.";
                StartButton.IsEnabled = false;
            }

            // Erstelle das tessdata-Verzeichnis, falls es nicht existiert
            if (!Directory.Exists(tessdataFolder))
            {
                Directory.CreateDirectory(tessdataFolder);
            }
        }

        private void InputFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            // Umfassender Filter für alle von Tesseract (via Leptonica) unterstützten Formate
            openFileDialog.Filter = "Unterstützte Bilder (*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.bmp;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.bmp;*.gif;*.webp|Alle Dateien (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                InputFileTextBox.Text = openFileDialog.FileName;
            }
        }

        private void OutputFileButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            string selectedFormat = OutputFormatComboBox.SelectedItem as string ?? "txt";

            saveFileDialog.Filter = $"{selectedFormat.ToUpper()}-Datei (*.{selectedFormat})|*.{selectedFormat}|Alle Dateien (*.*)|*.*";
            saveFileDialog.Title = "Speicherort für Output-Datei wählen (ohne Endung)";

            if (saveFileDialog.ShowDialog() == true)
            {
                // Entferne die Endung, Tesseract fügt sie selbst hinzu.
                OutputFileBase = Path.ChangeExtension(saveFileDialog.FileName, null);
            }
        }

        private void OutputFormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedOutputFormat = OutputFormatComboBox.SelectedItem as string ?? "txt";
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(InputFileTextBox.Text) || string.IsNullOrWhiteSpace(OutputFileTextBox.Text))
            {
                StatusTextBlock.Text = "Fehler: Bitte Input- und Output-Datei auswählen.";
                return;
            }

            // UI für den Start vorbereiten
            StatusTextBlock.Text = "OCR-Prozess wird ausgeführt...";
            StartButton.IsEnabled = false;

            string inputFile = $"\"{InputFileTextBox.Text}\"";
            string outputFileBase = $"\"{OutputFileTextBox.Text}\"";
            string language = $"-l {LanguageTextBox.Text}";
            string psm = $"--psm {PsmComboBox.SelectedValue}";
            string outputFormat = OutputFormatComboBox.SelectedItem as string ?? "txt";
            string languagePath = " --tessdata-dir " + $"\"{tessdataFolder}\"";


            // Das Ausgabeformat wird als "configfile" am Ende angehängt, außer bei txt (Standard)
            string arguments = $"{inputFile} {outputFileBase} {language} {psm} {languagePath}";
            if (outputFormat != "txt")
            {
                arguments += $" {outputFormat}";
            }

            // Prozess asynchron starten, damit die GUI nicht blockiert
            await Task.Run(() =>
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = _tesseractExePath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    process.WaitForExit();
                    string stderr = process.StandardError.ReadToEnd();

                    Dispatcher.Invoke(() =>
                    {
                        if (!string.IsNullOrWhiteSpace(stderr) && !stderr.Contains("Estimating resolution as"))
                        {
                            StatusTextBlock.Text = $"Prozess beendet mit Fehlern: {stderr}";
                        }
                        else
                        {
                            string finalOutputFile = $"{OutputFileTextBox.Text}.{outputFormat}";
                            StatusTextBlock.Text = $"Erfolgreich! Output wurde in '{finalOutputFile}' gespeichert.";
                        }
                    });
                }
            });

            // UI nach Beendigung wieder freigeben
            StartButton.IsEnabled = true;
        }

        private void ButtonLoadModels_Click(object sender, RoutedEventArgs e)
        {
            var langWindow = new LanguageManagerWindow(tessdataFolder);
            langWindow.Owner = this;
            langWindow.ShowDialog();
        }
    }

    public class OutputFileConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "";
            string fileName = value.ToString();
            string format = parameter as string;
            if (string.IsNullOrEmpty(format)) return fileName;
            return $"{fileName}.{format}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}