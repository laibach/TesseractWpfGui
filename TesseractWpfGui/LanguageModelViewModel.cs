using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace TesseractWpfGui
{
    public class LanguageModelViewModel : INotifyPropertyChanged
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private readonly string _tessdataFolder;
        private CancellationTokenSource _cts;

        public string Code { get; }
        public string Name { get; }
        public string DownloadUrl { get; }

        private bool _isInstalled;
        public bool IsInstalled
        {
            get => _isInstalled;
            set { _isInstalled = value; OnPropertyChanged(); UpdateStatusAndAction(); }
        }

        private bool _isDownloading;
        public bool IsDownloading
        {
            get => _isDownloading;
            set { _isDownloading = value; OnPropertyChanged(); UpdateStatusAndAction(); }
        }

        private double _downloadProgress;
        public double DownloadProgress
        {
            get => _downloadProgress;
            set { _downloadProgress = value; OnPropertyChanged(); }
        }

        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        private string _actionText;
        public string ActionText
        {
            get => _actionText;
            set { _actionText = value; OnPropertyChanged(); }
        }

        private bool _isActionEnabled = true;
        public bool IsActionEnabled
        {
            get => _isActionEnabled;
            set { _isActionEnabled = value; OnPropertyChanged(); }
        }

        public ICommand ActionCommand { get; }

        public LanguageModelViewModel(string fileName, string downloadUrl, string tessdataFolder, bool isInstalled)
        {
            Code = fileName.Replace(".traineddata", "");
            Name = Code.Length > 3 ? $"{Code.Substring(0, 1).ToUpper()}{Code.Substring(1)}" : Code.ToUpper(); // Einfache Namenskonvertierung
            DownloadUrl = downloadUrl;
            _tessdataFolder = tessdataFolder;
            _isInstalled = isInstalled;

            ActionCommand = new RelayCommand(ExecuteAction);
            UpdateStatusAndAction();
        }

        private void UpdateStatusAndAction()
        {
            if (IsDownloading)
            {
                StatusText = "Wird heruntergeladen...";
                ActionText = "Abbrechen";
            }
            else if (IsInstalled)
            {
                StatusText = "Installiert";
                ActionText = "Löschen";
            }
            else
            {
                StatusText = "Nicht installiert";
                ActionText = "Herunterladen";
            }
        }

        private async void ExecuteAction()
        {
            IsActionEnabled = false;
            if (IsDownloading)
            {
                _cts?.Cancel();
            }
            else if (IsInstalled)
            {
                DeleteFile();
            }
            else
            {
                await DownloadFileAsync();
            }
            IsActionEnabled = true;
        }

        private void DeleteFile()
        {
            try
            {
                File.Delete(Path.Combine(_tessdataFolder, $"{Code}.traineddata"));
                IsInstalled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Löschen der Datei: {ex.Message}");
            }
        }

        private async Task DownloadFileAsync()
        {
            IsDownloading = true;
            _cts = new CancellationTokenSource();
            var destinationPath = Path.Combine(_tessdataFolder, $"{Code}.traineddata");

            try
            {
                using var response = await httpClient.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var totalBytesRead = 0L;
                var buffer = new byte[256]; 
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, _cts.Token)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, _cts.Token);
                    totalBytesRead += bytesRead;
                    if (totalBytes.HasValue)
                    {
                        DownloadProgress = (double)totalBytesRead / totalBytes.Value * 100;
                    }
                }
                IsInstalled = true;
            }
            catch (OperationCanceledException)
            {
                StatusText = "Download abgebrochen.";
                if (File.Exists(destinationPath)) File.Delete(destinationPath);
            }
            catch (Exception ex)
            {
                StatusText = "Fehler!";
                MessageBox.Show($"Fehler beim Download: {ex.Message}");
                if (File.Exists(destinationPath)) File.Delete(destinationPath);
            }
            finally
            {
                IsDownloading = false;
                DownloadProgress = 0;
                _cts.Dispose();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    // Einfache ICommand-Implementierung
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) { _execute = execute; }
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute();
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
}