using Global_List_Editor.Model;
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
using System.Xml;
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
        private List<Model.GLOBALLIST> _selectedGlobalLists = new();
        private List<SelectableItem<string>> _exportedListItems;
        private string _globalListSearchText;
        private string _exportedListItemsSearchText;
        private string _downloadLocation;
        private List<string> _selectedExportedListItems = new();

        public List<string> SelectedExportedListItems
        {
            get => _selectedExportedListItems;
            set
            {
                _selectedExportedListItems = value;
                OnPropertyChanged(nameof(SelectedExportedListItems));
                OnPropertyChanged(nameof(ShowDeleteExportedListItemButton));
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get { return _isLoading; }
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        public Model.GLOBALLISTS GlobalLists
        {
            get => _globalLists;
            set
            {
                _globalLists = value;
                OnPropertyChanged(nameof(GlobalLists));

                // Re-subscribe to PropertyChanged for each GLOBALLIST
                if (_globalLists != null && _globalLists.GlobalList != null)
                {
                    foreach (var gl in _globalLists.GlobalList)
                    {
                        gl.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(GLOBALLIST.IsSelected))
                            {
                                SelectedGlobalLists = _globalLists.GlobalList.Where(x => x.IsSelected).ToList();
                            }
                        };
                    }
                }
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
                    FetchDownloadLocation();

                    IsLoading = true;
                    //call the long running method on a background thread...
                    Task.Run(() =>
                    {
                        Thread.Sleep(1000);

                        // Automatically list global lists when environment changes
                        if (_selectedEnvironment != null && !string.IsNullOrWhiteSpace(EnvironmentUrl))
                            ExecuteListAllGlobalListNames(null);
                    }).ContinueWith(task =>
                        {
                            //and set the IsLoading property back to false back on the UI thread once the task has finished
                            IsLoading = false;
                        }, System.Threading.CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
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
                    ExportedListItems = new List<SelectableItem<string>>();
                }
            }
        }

        public List<Model.GLOBALLIST> SelectedGlobalLists
        {
            get => _selectedGlobalLists;
            set
            {
                _selectedGlobalLists = value;
                OnPropertyChanged(nameof(SelectedGlobalLists));

                if(value.Count == 1)
                {
                    SelectedGlobalList = value.First();
                }
                else
                {
                    SelectedGlobalList = null;
                }
                OnPropertyChanged(nameof(SelectedGlobalList));
            }
        }

        public List<SelectableItem<string>> ExportedListItems
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

        public IEnumerable<SelectableItem<string>> FilteredExportedListItems =>
            string.IsNullOrWhiteSpace(ExportedListItemsSearchText)
                ? ExportedListItems
                : ExportedListItems?.Where(item => item.Value != null && item.Value.Contains(ExportedListItemsSearchText, StringComparison.OrdinalIgnoreCase));

        public bool ShowAddExportedListItemButton =>
            !string.IsNullOrWhiteSpace(ExportedListItemsSearchText) &&
            (ExportedListItems == null || !ExportedListItems.Any(item => item.Value.Equals(ExportedListItemsSearchText, StringComparison.OrdinalIgnoreCase)));

        public bool ShowDeleteExportedListItemButton =>
            (SelectedExportedListItems != null && SelectedExportedListItems.Any()) ||
            (!string.IsNullOrWhiteSpace(ExportedListItemsSearchText) &&
            ExportedListItems != null &&
            ExportedListItems.Any(item => item.Value.Equals(ExportedListItemsSearchText, StringComparison.OrdinalIgnoreCase)));

        public ICommand ApplyChangesToAzureServer { get; }

        public ICommand DownloadEntireGlobalList { get; }

        public ICommand AddExportedListItemCommand => new RelayCommand(param =>
        {
            var newItem = ExportedListItemsSearchText?.Trim();
            if (!string.IsNullOrEmpty(newItem) && SelectedGlobalList != null)
            {
                // Add to in-memory list
                var items = ExportedListItems?.ToList() ?? new List<SelectableItem<string>>();
                items.Add(new SelectableItem<string> { Value = newItem });
                ExportedListItems = items;

                AddOrDeleteListItem(newItem, true);

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
                var items = ExportedListItems?.ToList() ?? new List<SelectableItem<string>>();
                var removed = items.RemoveAll(i => i.Value.Equals(itemToDelete, StringComparison.OrdinalIgnoreCase));
                if (removed > 0)
                {
                    ExportedListItems = items;

                    AddOrDeleteListItem(itemToDelete, false);

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

            if (_globalLists != null && _globalLists.GlobalList != null)
            {
                foreach (var gl in _globalLists.GlobalList)
                {
                    gl.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(Model.GLOBALLIST.IsSelected))
                        {
                            SelectedGlobalLists = _globalLists.GlobalList.Where(x => x.IsSelected).ToList();
                        }
                    };
                }
            }
        }

        private bool CanDownloadEntireGlobalList(object obj)
        {
            if (string.IsNullOrWhiteSpace(SelectedEnvironment) || string.IsNullOrEmpty(EnvironmentUrl) || string.IsNullOrWhiteSpace(DownloadLocation))
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
            if (string.IsNullOrWhiteSpace(SelectedEnvironment) || string.IsNullOrEmpty(EnvironmentUrl) || string.IsNullOrEmpty(DownloadLocation))
            {
                //System.Windows.MessageBox.Show("Please select an environment first.", "Apply Changes");
                return false;
            }
            return true;
        }

        private void ExcecuteApplyChnagesToAzureServer(object obj)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "witadmin",
                        Arguments = $"/importgloballist /collection:{EnvironmentUrl} /f:\"{DownloadLocation}\"",
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
                    System.Windows.MessageBox.Show($"Global list imported successfully from:\n{DownloadLocation}", "Import Global List");
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
                DownloadLocation = Path.Combine(ConfigurationManager.AppSettings[key], "GlobalList.xml") ?? string.Empty;
            }
            else
            {
                DownloadLocation = string.Empty;
            }
        }

        private List<GLOBALLIST> GetGlobalListNames()
        {
            var globalListNames = new List<GLOBALLIST>();
            string xmlPath = DownloadLocation; // Replace with your actual path
            XmlDocument doc = new XmlDocument();
            doc.Load(xmlPath);

            XmlNamespaceManager nsMgr = new XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("gl", "http://schemas.microsoft.com/VisualStudio/2005/workitemtracking/globallists");

            XmlNodeList globalLists = doc.SelectNodes("//GLOBALLIST", nsMgr);
            foreach (XmlNode list in globalLists)
            {
                string listName = list.Attributes["name"]?.Value;
                globalListNames.Add(new Model.GLOBALLIST { Name = listName });
            }
            return globalListNames;
        }

        private List<string> GetListItemsForGlobalListName()
        {
            var globalListItemValues = new List<string>();
            string xmlPath = DownloadLocation; // Replace with your actual path
            XmlDocument doc = new XmlDocument();
            doc.Load(xmlPath);

            XmlNamespaceManager nsMgr = new XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("gl", "http://schemas.microsoft.com/VisualStudio/2005/workitemtracking/globallists");

            XmlNodeList globalLists = doc.SelectNodes("//GLOBALLIST", nsMgr);
            foreach (XmlNode list in globalLists)
            {
                string listName = list.Attributes["name"]?.Value;
                if (listName == SelectedGlobalList.Name)
                {
                    XmlNodeList items = list.SelectNodes("LISTITEM", nsMgr);
                    foreach (XmlNode item in items)
                    {
                        string value = item.Attributes["value"]?.Value;
                        globalListItemValues.Add(value);
                    }
                }
            }
            return globalListItemValues;
        }

        private void AddOrDeleteListItem(string value, bool add)
        {
            string xmlPath = DownloadLocation;
            XmlDocument doc = new XmlDocument();
            doc.Load(xmlPath);

            XmlNamespaceManager nsMgr = new XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("gl", "http://schemas.microsoft.com/VisualStudio/2005/workitemtracking/globallists");

            // 🔍 Target the specific global list
            XmlNode targetList = doc.SelectSingleNode($"//GLOBALLIST[@name='{SelectedGlobalList.Name}']", nsMgr);

            if (targetList != null)
            {
                if (add)
                {
                    // ✅ Add a new item
                    XmlNode newItem = doc.CreateElement("LISTITEM");
                    XmlAttribute attr = doc.CreateAttribute("value");
                    attr.Value = value;
                    newItem.Attributes.Append(attr);
                    targetList.AppendChild(newItem);
                }
                else
                {
                    // ❌ Remove an existing item
                    XmlNode itemToRemove = targetList.SelectSingleNode($"LISTITEM[@value='{value}']", nsMgr);
                    if (itemToRemove != null)
                    {
                        targetList.RemoveChild(itemToRemove);
                    }
                }
                // 💾 Save the updated document
                doc.Save(xmlPath);
            }
            else
            {
                Console.WriteLine("Target GLOBALLIST not found.");
            }

        }

        private void ExecuteListAllGlobalListNames(object parameter)
        {
            try
            {
                // Use the EnvironmentUrl as the collection argument if available
                string collectionArg = !string.IsNullOrWhiteSpace(EnvironmentUrl)
                    ? $"/collection:{EnvironmentUrl}"
                    : string.Empty;
                string fileArg = $"/f:\"{DownloadLocation}\"";

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "witadmin",
                        Arguments = $"exportgloballist {collectionArg} {fileArg}".Trim(),
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                var list = GetGlobalListNames();

                GlobalLists.GlobalList = list;
                OnPropertyChanged(nameof(FilteredGlobalLists));
                OnPropertyChanged(nameof(GlobalLists));

                if (_globalLists != null && _globalLists.GlobalList != null)
                {
                    foreach (var gl in _globalLists.GlobalList)
                    {
                        gl.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(Model.GLOBALLIST.IsSelected))
                            {
                                SelectedGlobalLists = _globalLists.GlobalList.Where(x => x.IsSelected).ToList();
                            }
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                var list = GetGlobalListNames();

                GlobalLists.GlobalList = list;
                OnPropertyChanged(nameof(FilteredGlobalLists));
                OnPropertyChanged(nameof(GlobalLists));

                if (_globalLists != null && _globalLists.GlobalList != null)
                {
                    foreach (var gl in _globalLists.GlobalList)
                    {
                        gl.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(Model.GLOBALLIST.IsSelected))
                            {
                                SelectedGlobalLists = _globalLists.GlobalList.Where(x => x.IsSelected).ToList();
                            }
                        };
                    }
                }

                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error");
            }
        }

        private void ExportAndLoadSelectedGlobalListValues()
        {
            try
            {
                var items = GetListItemsForGlobalListName();
                ExportedListItems = items.Select(item => new SelectableItem<string> { Value = item }).ToList();
                SyncSelectedExportedListItems();
            }
            catch
            {
                ExportedListItems = new List<SelectableItem<string>>();
            }
        }

        private void SyncSelectedExportedListItems()
        {
            foreach (var item in ExportedListItems)
            {
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SelectableItem<string>.IsSelected))
                    {
                        SelectedExportedListItems = ExportedListItems
                            .Where(x => x.IsSelected)
                            .Select(x => x.Value)
                            .ToList();
                    }
                };
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
