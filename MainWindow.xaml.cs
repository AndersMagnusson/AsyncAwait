using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using HtmlAgilityPack;

namespace AsyncAwait
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        #region Definitions
        string folderPath = @"c:\temp\asyncawait";
        
        public class DownloadedFile
        {
            public string Name { get; set; }
            public string ImageUri { get; set; }
        }

        public MainWindow()
        {
            Images = new ObservableCollection<DownloadedFile>();
            IsButtonsEnabled = true;
            SetUpFolders();
           
            InitializeComponent();
            wwwAddressTextBox.Text = "http://www.cybercom.com";
     
        }

        private void SetUpFolders()
        {
            if (Directory.Exists(folderPath))
                Directory.Delete(folderPath, true);
            Directory.CreateDirectory(folderPath);
            Directory.CreateDirectory(Path.Combine(folderPath, "Async"));
            Directory.CreateDirectory(Path.Combine(folderPath, "OldAsync"));
            Directory.CreateDirectory(Path.Combine(folderPath, "Sync"));
        }

        public ObservableCollection<DownloadedFile> Images { get; private set; }
        
        bool buttonsEnabled = false;
        public bool IsButtonsEnabled
        {
            get
            {
                return buttonsEnabled;
            }
            set
            {
                buttonsEnabled = value;
                OnPropertyChanged("IsButtonsEnabled");
            }
        }

        public int ImagesToDownload { get; set; }

        private void SetUp()
        {
            Images.Clear();
            IsButtonsEnabled = false;
        }
        #endregion

        #region Async with Await (new pattern)

        private async void AsyncButton_Click(object sender, RoutedEventArgs e)
        {
            SetUp();
            var pageContent = await GetPageContentAsync();

            var listOfImageSources = FindImages(pageContent);

            // Dowload and save images
            var images = await SaveFilesAsync(listOfImageSources.ToList());

            IsButtonsEnabled = true;
        }

        private void DisplayImage(string image)
        {
            Images.Add(new DownloadedFile() { Name = Path.GetFileName(image), ImageUri = image });
        }


        private Task<string> GetPageContentAsync()
        {
            WebClient client = new WebClient();
            return client.DownloadStringTaskAsync(new Uri(wwwAddressTextBox.Text.ToString()));
        }

        private async Task<List<string>> SaveFilesAsync(IList<string> imageSources)
        {
            var client = new WebClient();
            var images = new List<string>();
            foreach (var src in imageSources)
            {
                try
                {
                    string imageSrc = GetSrc(src);
                    string filePath = Path.Combine(folderPath, "Async", Path.GetFileName(imageSrc));

                    var pageContent = await client.DownloadDataTaskAsync(imageSrc);
                    await SaveFileAsync(pageContent, filePath);
                    
                    DisplayImage(filePath);
                    images.Add(filePath);
                }
                catch (Exception) { }
            }
            return images;
        }

        private async Task<bool> SaveFileAsync(byte[] file, string filePath)
        {
            if (File.Exists(filePath))
                return false;

            using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            {
                await fs.WriteAsync(file, 0, file.Length);
            }
            return true;

        }

        #endregion
        
        #region Old Async pattern

        private void OldAsyncButton_Click(object sender, RoutedEventArgs e)
        {
            SetUp();
            GetPageContentWithCallback();
        }

        private void GetPageContentWithCallback()
        {
            WebClient client = new WebClient();
            client.DownloadStringCompleted += client_DownloadStringCompleted;
            client.DownloadStringAsync(new Uri(wwwAddressTextBox.Text.ToString()));
        }

        void client_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            var listOfImageSources = FindImages(e.Result);
            ImagesToDownload = listOfImageSources.Count();

            foreach (var image in listOfImageSources)
            {
                var client = new WebClient();
                client.DownloadDataCompleted += client_DownloadDataCompleted;

                string src = GetSrc(image);
                client.DownloadDataAsync(new Uri(src), Path.GetFileName(src));
            }
        }

        void client_DownloadDataCompleted(object sender, DownloadDataCompletedEventArgs e)
        {
            try
            {
                string filePath = Path.Combine(folderPath, "OldAsync", e.UserState.ToString());
                SaveFile(e.Result, filePath);

                Images.Add(new DownloadedFile() { Name = e.UserState.ToString(), ImageUri = filePath });
                if (ImagesToDownload == Images.Count())
                {
                    IsButtonsEnabled = true;
                    //Dispatcher.Invoke(() => IsButtonsEnabled = true);
                }
            }
            catch (ArgumentException) { }
        }

        #endregion

        #region Synchronous

        private void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            SetUp();
            var pageContent = GetPageContent();

            var listOfImageSources = FindImages(pageContent);

            // Dowload and save images
            var images = SaveFiles(listOfImageSources);
            IsButtonsEnabled = true;
        }

        private string GetPageContent()
        {
            WebClient client = new WebClient();
            return client.DownloadString(new Uri(wwwAddressTextBox.Text.ToString()));
        }

        private List<string> SaveFiles(IList<string> imageSources)
        {
            var client = new WebClient();
            var images = new List<string>();
            foreach (var image in imageSources)
            {
                try
                {
                    string src = GetSrc(image);
                    string filePath = Path.Combine(folderPath, "Sync", Path.GetFileName(src));

                    var imageData = client.DownloadData(src);
                    SaveFile(imageData, filePath);
                    DisplayImage(filePath);
                    images.Add(filePath);
                }
                catch(Exception) { }
            }
            return images;
        }

        private void SaveFile(byte[] file, string filePath)
        {
            if (File.Exists(filePath))
                return;

            using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                fs.Write(file, 0, file.Length);
            }
        }
        #endregion

        #region Find Images

        private static List<string> FindImages(string pageContent)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(pageContent);
            var listOfImageSources = new List<string>();
            foreach (var node in doc.DocumentNode.SelectNodes("//img"))
            {
                var src = node.Attributes.FirstOrDefault(a => a.Name.ToLower() == "src");
                if (src != null && !string.IsNullOrEmpty(src.Value))
                {
                    listOfImageSources.Add(src.Value.ToLower());
                }
            }
            return listOfImageSources.Distinct().ToList();
        }

        private string GetSrc(string src)
        {
            string temp = src;
            if (!src.ToLower().StartsWith("http") || !src.ToLower().StartsWith("https"))
            {
                temp = string.Concat(wwwAddressTextBox.Text, "/", src);
            }
            return temp;
        }
        #endregion

        #region Notifications
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
       #endregion
}
}
