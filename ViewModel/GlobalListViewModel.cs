using Microsoft.Win32; // For SaveFileDialog
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration; // Add this for ConfigurationManager
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Xml.Linq;

namespace Global_List_Editor.ViewModel
{
    public class GlobalListViewModel : INotifyPropertyChanged
    {
        private Model.GLOBALLISTS _globalLists;
        private List<string> _environments;
        private string _selectedEnvironment;
        private string _environmentUrl;
        private Model.GLOBALLIST _selectedGlobalList;
        private List<string> _exportedListItems;
        private string _globalListSearchText;
        private string _exportedListItemsSearchText;
        private string _downloadLocation; 

        public Model.GLOBALLISTS GlobalLists
        {
            get { return _globalLists; }
            set
            {
                _globalLists = value;
                OnPropertyChanged(nameof(GlobalLists));
            }
        }

        public List<string> Environments
        {
            get { return _environments; }
            set
            {
                _environments = value;
                OnPropertyChanged(nameof(Environments));
            }
        }

        public string SelectedEnvironment
        {
            get { return _selectedEnvironment; }
            set
            {
                var trimmed = value?.Trim();
                if (_selectedEnvironment != trimmed)
                {
                    _selectedEnvironment = trimmed;
                    OnPropertyChanged(nameof(SelectedEnvironment));
                    FetchEnvironmentUrl();

                    // Automatically list global lists when environment changes
                    if (_selectedEnvironment != null && !string.IsNullOrWhiteSpace(EnvironmentUrl))
                        ExecuteListAllGlobalListNames(null);
                }
            }
        }

        public string EnvironmentUrl
        {
            get { return _environmentUrl; }
            set
            {
                _environmentUrl = value;
                OnPropertyChanged(nameof(EnvironmentUrl));
            }
        }

        public string DownloadLocation
        {
            get { return _downloadLocation; }
            set
            {
                _downloadLocation = value;
                OnPropertyChanged(nameof(DownloadLocation));
            }
        }

        public Model.GLOBALLIST SelectedGlobalList
        {
            get => _selectedGlobalList;
            set
            {
                _selectedGlobalList = value;
                OnPropertyChanged(nameof(SelectedGlobalList));

                // Automatically export and show items when a global list is selected
                if (_selectedGlobalList != null && !string.IsNullOrWhiteSpace(EnvironmentUrl))
                {
                    ExportAndLoadSelectedGlobalListValues();
                }
                else
                {
                    ExportedListItems = new List<string>();
                }
            }
        }

        public List<string> ExportedListItems
        {
            get => _exportedListItems;
            set
            {
                _exportedListItems = value;
                OnPropertyChanged(nameof(ExportedListItems));
                OnPropertyChanged(nameof(FilteredExportedListItems));
            }
        }

        public string GlobalListSearchText
        {
            get => _globalListSearchText;
            set
            {
                if (_globalListSearchText != value)
                {
                    _globalListSearchText = value;
                    OnPropertyChanged(nameof(GlobalListSearchText));
                    OnPropertyChanged(nameof(FilteredGlobalLists));
                }
            }
        }

        public string ExportedListItemsSearchText
        {
            get => _exportedListItemsSearchText;
            set
            {
                if (_exportedListItemsSearchText != value)
                {
                    _exportedListItemsSearchText = value;
                    OnPropertyChanged(nameof(ExportedListItemsSearchText));
                    OnPropertyChanged(nameof(FilteredExportedListItems));
                    OnPropertyChanged(nameof(ShowAddExportedListItemButton));
                    OnPropertyChanged(nameof(ShowDeleteExportedListItemButton));
                }
            }
        }

        public IEnumerable<Model.GLOBALLIST> FilteredGlobalLists =>
            string.IsNullOrWhiteSpace(GlobalListSearchText)
                ? GlobalLists.GlobalList
                : GlobalLists.GlobalList?.Where(gl => gl.Name != null && gl.Name.Contains(GlobalListSearchText, StringComparison.OrdinalIgnoreCase));

        public IEnumerable<string> FilteredExportedListItems =>
            string.IsNullOrWhiteSpace(ExportedListItemsSearchText)
                ? ExportedListItems
                : ExportedListItems?.Where(item => item != null && item.Contains(ExportedListItemsSearchText, StringComparison.OrdinalIgnoreCase));

        public bool ShowAddExportedListItemButton =>
            !string.IsNullOrWhiteSpace(ExportedListItemsSearchText) &&
            (ExportedListItems == null || !ExportedListItems.Any(item => item.Equals(ExportedListItemsSearchText, StringComparison.OrdinalIgnoreCase)));

        public bool ShowDeleteExportedListItemButton =>
            !string.IsNullOrWhiteSpace(ExportedListItemsSearchText) &&
            ExportedListItems != null &&
            ExportedListItems.Any(item => item.Equals(ExportedListItemsSearchText, StringComparison.OrdinalIgnoreCase));

        //public ICommand ListAllGlobalListNamesCommand { get; }

        //public ICommand ExportSelectedGlobalListCommand { get; }

        public ICommand ApplyChangesToAzureServer { get; }

        public ICommand DownloadEntireGlobalList { get; }

        public ICommand AddExportedListItemCommand => new RelayCommand(param =>
        {
            var newItem = ExportedListItemsSearchText?.Trim();
            if (!string.IsNullOrEmpty(newItem) && SelectedGlobalList != null)
            {
                // Add to in-memory list
                var items = ExportedListItems?.ToList() ?? new List<string>();
                items.Add(newItem);
                ExportedListItems = items;

                // Save to file
                string fileName = $"{SelectedGlobalList.Name}.xml";
                string filePath = System.IO.Path.Combine(Environment.CurrentDirectory, "output", fileName);

                // Load or create XML
                XDocument doc;
                if (System.IO.File.Exists(filePath))
                {
                    doc = XDocument.Load(filePath);
                }
                else
                {
                    doc = new XDocument(new XElement("GLOBALLIST", new XAttribute("name", SelectedGlobalList.Name)));
                }

                // Add new item
                var root = doc.Root;
                root.Add(new XElement("LISTITEM", new XAttribute("value", newItem)));
                doc.Save(filePath);

                // Optionally, update the in-memory model as well
                if (SelectedGlobalList.ListItem == null)
                    SelectedGlobalList.ListItem = new List<Model.GLOBALLISTLISTITEM>();
                SelectedGlobalList.ListItem.Add(new Model.GLOBALLISTLISTITEM { Value = newItem });

                // Clear search box after add
                ExportedListItemsSearchText = string.Empty;
            }
        });

        public ICommand DeleteExportedListItemCommand => new RelayCommand(param =>
        {
            var itemToDelete = ExportedListItemsSearchText?.Trim();
            if (!string.IsNullOrEmpty(itemToDelete) && SelectedGlobalList != null)
            {
                // Remove from in-memory list
                var items = ExportedListItems?.ToList() ?? new List<string>();
                var removed = items.RemoveAll(i => i.Equals(itemToDelete, StringComparison.OrdinalIgnoreCase));
                if (removed > 0)
                {
                    ExportedListItems = items;

                    // Save to file
                    string fileName = $"{SelectedGlobalList.Name}.xml";
                    string filePath = System.IO.Path.Combine(Environment.CurrentDirectory, "output", fileName);

                    if (System.IO.File.Exists(filePath))
                    {
                        var doc = XDocument.Load(filePath);
                        var listItems = doc.Descendants("LISTITEM")
                            .Where(x => (string)x.Attribute("value") == itemToDelete)
                            .ToList();
                        foreach (var x in listItems)
                            x.Remove();
                        doc.Save(filePath);
                    }

                    // Optionally, update the in-memory model as well
                    if (SelectedGlobalList.ListItem != null)
                    {
                        SelectedGlobalList.ListItem.RemoveAll(li => li.Value == itemToDelete);
                    }
                }

                // Clear search box after delete
                ExportedListItemsSearchText = string.Empty;
            }
        });

        public GlobalListViewModel()
        {
            _globalLists = new Model.GLOBALLISTS(); // Initialize the global lists model
            //ListAllGlobalListNamesCommand = new RelayCommand(ExecuteListAllGlobalListNames);
            //ExportSelectedGlobalListCommand = new RelayCommand(ExecuteExportSelectedGlobalList, CanExportGlobalList);
            ApplyChangesToAzureServer = new RelayCommand(ExcecuteApplyChnagesToAzureServer, CanApplyChangesToAzureServer);
            DownloadEntireGlobalList = new RelayCommand(ExcecuteDownloadEntireGlobalList, CanDownloadEntireGlobalList);

            // Read and parse the environment list from App.config
            var envString = ConfigurationManager.AppSettings["EnvironmentList"];
            if (!string.IsNullOrWhiteSpace(envString))
            {
                Environments = envString.Split(',').Select(e => e.Trim()).ToList();
            }
            else
            {
                Environments = new List<string>();
            }
        }

        private bool CanDownloadEntireGlobalList(object obj)
        {
            if (string.IsNullOrWhiteSpace(SelectedEnvironment) || string.IsNullOrEmpty(EnvironmentUrl))
            {
                return false;
            }

            if(string.IsNullOrWhiteSpace(DownloadLocation))
            {
                return false;
            }

            return true;
        }

        private void ExcecuteDownloadEntireGlobalList(object obj)
        {
            string fileName = $"{SelectedEnvironment}_GlobalLists.xml";
            string filePath = System.IO.Path.Combine(DownloadLocation, fileName);

            if (!Directory.Exists(DownloadLocation))
                Directory.CreateDirectory(DownloadLocation);

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "witadmin",
                        Arguments = $"/exportgloballist /collection:{EnvironmentUrl} /f:\"{filePath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    System.Windows.MessageBox.Show($"Global lists exported successfully to:\n{filePath}", "Export Global Lists");
                }
                else
                {
                    System.Windows.MessageBox.Show($"Export failed: {error}", "Export Global Lists");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Export Global Lists");
            }
        }

        private bool CanApplyChangesToAzureServer(object obj)
        {
            if(string.IsNullOrWhiteSpace(SelectedEnvironment) || string.IsNullOrEmpty(EnvironmentUrl))
            {
                //System.Windows.MessageBox.Show("Please select an environment first.", "Apply Changes");
                return false;
            }
            if (Directory.Exists(System.IO.Path.Combine(Environment.CurrentDirectory, "output")))
            {
                if (Directory.GetFiles(System.IO.Path.Combine(Environment.CurrentDirectory, "output"), "*.xml").Length > 0)
                {
                    return true;
                }
                else
                {
                    //System.Windows.MessageBox.Show("No changes to apply. Please export a global list first.", "Apply Changes");
                    return false;
                }
            }
            else
            {
                //System.Windows.MessageBox.Show("Output directory does not exist. Please ensure the application has exported global lists.", "Apply Changes");
                return false;
            }
        }

        private void ExcecuteApplyChnagesToAzureServer(object obj)
        {
            foreach(var file in Directory.GetFiles(System.IO.Path.Combine(Environment.CurrentDirectory, "output"), "*.xml"))
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "witadmin",
                            Arguments = $"/importgloballist /collection:{EnvironmentUrl} /f:\"{file}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        System.Windows.MessageBox.Show($"Global list imported successfully from:\n{file}", "Import Global List");
                    }
                    else
                    {
                        System.Windows.MessageBox.Show($"Import failed: {error}", "Import Global List");
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error: {ex.Message}", "Import Global List");
                }
            }
        }

        private void FetchEnvironmentUrl()
        {
            if (!string.IsNullOrWhiteSpace(SelectedEnvironment))
            {
                string key = $"{SelectedEnvironment}Url";
                EnvironmentUrl = ConfigurationManager.AppSettings[key] ?? string.Empty;
            }
            else
            {
                EnvironmentUrl = string.Empty;
            }
        }

        private void FetchDownloadLocation()
        {
            if (!string.IsNullOrWhiteSpace(SelectedEnvironment))
            {
                string key = $"{SelectedEnvironment}DownloadLocation";
                DownloadLocation = ConfigurationManager.AppSettings[key] ?? string.Empty;
            }
            else
            {
                DownloadLocation = string.Empty;
            }
        }

        private bool CanExportGlobalList(object parameter)
        {
            return SelectedGlobalList != null && !string.IsNullOrWhiteSpace(EnvironmentUrl);
        }

        private void ExecuteExportSelectedGlobalList(object parameter)
        {
            if (SelectedGlobalList == null)
            {
                System.Windows.MessageBox.Show("Please select a global list to export.", "Export Global List");
                return;
            }

            string fileName = $"{SelectedGlobalList.Name}.xml";
            string filePath = System.IO.Path.Combine(Environment.CurrentDirectory, fileName);

            string collectionArg = $"/collection:{EnvironmentUrl}";
            string nameArg = $"/n:{SelectedGlobalList.Name}";
            string fileArg = $"/f:\"{filePath}\"";

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "witadmin",
                        Arguments = $"exportgloballist {collectionArg} {nameArg} {fileArg}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    // Parse the exported XML and update ExportedListItems
                    ExportedListItems = ParseExportedGlobalList(filePath);
                    System.Windows.MessageBox.Show($"Global list exported and loaded successfully from:\n{filePath}", "Export Global List");
                }
                else
                {
                    System.Windows.MessageBox.Show($"Export failed: {error}", "Export Global List");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Export Global List");
            }
        }

        private List<string> ParseExportedGlobalList(string filePath)
        {
            try
            {
                var doc = XDocument.Load(filePath);
                // Adjust the namespace if needed
                XNamespace gl = "http://schemas.microsoft.com/VisualStudio/2005/workitemtracking/globallist";
                var items = doc.Descendants("LISTITEM")
                    .Select(x => (string)x.Attribute("value"))
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .ToList();

                // If namespace is present, use:
                // var items = doc.Descendants(gl + "LISTITEM")
                //     .Select(x => (string)x.Attribute("value"))
                //     .Where(v => !string.IsNullOrWhiteSpace(v))
                //     .ToList();

                return items;
            }
            catch
            {
                return new List<string>();
            }
        }

        private void ExecuteListAllGlobalListNames(object parameter)
        {
            try
            {
                //// Use the EnvironmentUrl as the collection argument if available
                //string collectionArg = !string.IsNullOrWhiteSpace(EnvironmentUrl)
                //    ? $"/collection:{EnvironmentUrl}"
                //    : string.Empty;

                //var process = new Process
                //{
                //    StartInfo = new ProcessStartInfo
                //    {
                //        FileName = "witadmin",
                //        Arguments = $"listgloballist {collectionArg}".Trim(),
                //        RedirectStandardOutput = true,
                //        UseShellExecute = false,
                //        CreateNoWindow = true
                //    }
                //};

                //process.Start();
                //string output = process.StandardOutput.ReadToEnd();
                //process.WaitForExit();

                //var list = output
                //    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                //    .Select(name => new Model.GLOBALLIST { Name = name })
                //    .ToList();

                // Initialize _globalLists with dummy data
                _globalLists.GlobalList = new List<Model.GLOBALLIST>();
                _globalLists.GlobalList.Add(new Model.GLOBALLIST
                {
                    Name = "SampleList1",
                    ListItem = new List<Model.GLOBALLISTLISTITEM>
                        {
                            new Model.GLOBALLISTLISTITEM { Value = "Item 1.1" },
                            new Model.GLOBALLISTLISTITEM { Value = "Item 1.2" },
                            new Model.GLOBALLISTLISTITEM { Value = "Item 1.3" },
                        }
                });
                _globalLists.GlobalList.Add(new Model.GLOBALLIST
                {
                    Name = "SampleList2",
                    ListItem = new List<Model.GLOBALLISTLISTITEM>
                        {
                            new Model.GLOBALLISTLISTITEM { Value = "Item 2.1" },
                            new Model.GLOBALLISTLISTITEM { Value = "Item 2.2" },
                        }
                });
                var list = _globalLists.GlobalList; // Use the existing global list for now

                GlobalLists.GlobalList = list;
                OnPropertyChanged(nameof(FilteredGlobalLists));
                OnPropertyChanged(nameof(GlobalLists));
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error");
            }
        }

        private void ExportAndLoadSelectedGlobalListValues()
        {
            string fileName = $"{SelectedGlobalList.Name}.xml";
            string filePath = System.IO.Path.Combine(Environment.CurrentDirectory, fileName);

            string collectionArg = $"/collection:{EnvironmentUrl}";
            string nameArg = $"/n:{SelectedGlobalList.Name}";
            string fileArg = $"/f:\"{filePath}\"";

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "witadmin",
                        Arguments = $"exportgloballist {collectionArg} {nameArg} {fileArg}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    ExportedListItems = ParseExportedGlobalList(filePath);
                }
                else
                {
                    ExportedListItems = new List<string>();
                }
            }
            catch
            {
                ExportedListItems = ParseExportedGlobalList(filePath);// new List<string>();
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
