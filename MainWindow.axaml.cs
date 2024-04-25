using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using SchulDB;
using StS_GUI_Avalonia.services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

// ReSharper disable InconsistentNaming

namespace StS_GUI_Avalonia
{
    public partial class MainWindow : Window
    {
        private readonly Timer _leftInputTimer = new(350);
        private readonly Timer _rightInputTimer = new(350);
        private Schuldatenbank _myschool;
        private readonly ContextMenu _leftListContext = new();
        private readonly Brush _darkBackgroundColor = new SolidColorBrush(Color.FromRgb(80, 80, 80));
        private readonly Brush _lightBackgroundColor = new SolidColorBrush(Color.FromRgb(242, 242, 242));
        private bool _rightMutex;
        private WindowIcon _msgBoxWindowIcon;
        private MenuItem _mnuItemCopySuSid;
        private MenuItem _mnuItemCopySuSMail;
        private MenuItem _mnuItemCopyKursBez;
        private MenuItem _mnuItemCopyLuLKrz;
        private MenuItem _mnuItemCopyLuLMails;
        private readonly ContextMenu _logListContextMenu = new();
        private int leftLastComboIndex = -1;
        private int rightLastComboIndex = -1;

        public MainWindow()
        {
#if DEBUG
            InitializeComponent(true, false);
#else
            InitializeComponent();
#endif
            InitGUI();
        }

        public MainWindow(IReadOnlyList<string> args)
        {
#if DEBUG
            InitializeComponent(true, false);
#else
            InitializeComponent();
#endif
            InitGUI();
            if (args.Count != 1) return;
            var filepath = args[0];
            if (!File.Exists(filepath) || !filepath.EndsWith(".sqlite")) return;
            _myschool = new Schuldatenbank(filepath);
            Title = "SchildToSchule - " + _myschool.GetFilePath().Result;
            InitData();
        }

        private void InitGUI()
        {
            _myschool = new Schuldatenbank(":memory:");
            var settings = _myschool.GetSettings().Result;
            tbSettingMailplatzhalter.Text = settings.Mailsuffix;
            tbSettingKursersetzung.Text = string.IsNullOrEmpty(settings.Fachersetzung)
                ? ""
                : settings.Fachersetzung.Split(';')[1];
            tbSettingKurssuffix.Text = settings.Kurssuffix;
            var kurzfach = _myschool.GetFachersatz().Result.Select(t => t.Split(';')[0]);
            foreach (var fachk in kurzfach)
            {
                tbSettingFachkurz.Text += fachk + '\n';
            }

            var langfach = _myschool.GetFachersatz().Result.Select(t => t.Split(';')[1]);
            foreach (var fachl in langfach)
            {
                tbSettingFachlang.Text += fachl + '\n';
            }

            _leftInputTimer.Elapsed += OnLeftTimedEvent;
            _rightInputTimer.Elapsed += OnRightTimedEvent;

            //ContextMenu für linkes ListBox
            List<Control> leftListContextItems = new();
            List<Control> copyContextItems = new();
            var cbMAnfangsPassword = new CheckBox
            {
                Name = "cbMnuLeftContextAnfangsPasswort",
                Content = "Mit Anfangspasswort"
            };
            var cbMEltern = new CheckBox
            {
                Name = "cbMnuLeftContextEltern",
                Content = "Eltern mitexportieren"
            };
            var cbMLLGIntern = new CheckBox
            {
                Name = "cbMnuLeftContextLLGIntern",
                Content = "LLG-Intern"
            };
            var mnuItemMPasswordGenerieren = new MenuItem
            {
                Name = "mnuItemMPasswordGenerieren",
                Header = "neues temp. Passwort generieren"
            };
            mnuItemMPasswordGenerieren.Click += OnMnuPasswordGenClick;
            var mnuItemMSerienbrief = new MenuItem
            {
                Name = "mnuItemMSerienbrief",
                Header = "Serienbrief-CSV exportieren"
            };
            mnuItemMSerienbrief.Click += OnMnuSerienbriefClick;
            var mnuItemMSerienbriefDV = new MenuItem
            {
                Name = "mnuItemMExportDV",
                Header = "Serienbrief-CSV exportieren (nur mit DV)"
            };
            mnuItemMSerienbriefDV.Click += OnMnuItemMSerienbriefDV;
            var mnuItemMExport = new MenuItem
            {
                Name = "mnuItemExport",
                Header = "markierte Elemente exportieren"
            };
            mnuItemMExport.Click += OnMnuExportClick;
            var mnuItemCopyMenu = new MenuItem
            {
                Name = "mnuItemCopyMenu",
                Header = "Kopiere..."
            };
            _mnuItemCopySuSid = new MenuItem
            {
                Name = "mnuItemCopyMenu",
                Header = "IDs"
            };
            _mnuItemCopySuSid.Click += MnuItemCopySuSidOnClick;
            _mnuItemCopySuSMail = new MenuItem
            {
                Name = "mnuItemCopyMail",
                Header = "Mail-Adressen"
            };
            _mnuItemCopySuSMail.Click += MnuItemCopySuSMailOnClick;
            _mnuItemCopyKursBez = new MenuItem
            {
                Name = "mnuItemCopyKursBez",
                Header = "Kursbezeichnungen"
            };
            _mnuItemCopyKursBez.Click += MnuItemCopyKursBezOnClick;
            _mnuItemCopyLuLKrz = new MenuItem
            {
                Name = "mnuItemCopyLuLKrz",
                Header = "Kürzel"
            };
            _mnuItemCopyLuLKrz.Click += MnuItemCopyLuLKrzOnClick;
            _mnuItemCopyLuLMails = new MenuItem
            {
                Name = "mnuItemCopyLuLMails",
                Header = "Mail-Adressen"
            };
            _mnuItemCopyLuLMails.Click += MnuItemCopyLuLMailsOnClick;
            copyContextItems.Add(_mnuItemCopySuSid);
            copyContextItems.Add(_mnuItemCopySuSMail);
            copyContextItems.Add(_mnuItemCopyKursBez);
            copyContextItems.Add(_mnuItemCopyLuLKrz);
            copyContextItems.Add(_mnuItemCopyLuLMails);
            mnuItemCopyMenu.ItemsSource = copyContextItems;
            leftListContextItems.Add(mnuItemCopyMenu);
            leftListContextItems.Add(cbMAnfangsPassword);
            leftListContextItems.Add(cbMEltern);
            leftListContextItems.Add(cbMLLGIntern);
            leftListContextItems.Add(mnuItemMSerienbrief);
            leftListContextItems.Add(mnuItemMSerienbriefDV);
            leftListContextItems.Add(mnuItemMPasswordGenerieren);
            leftListContextItems.Add(mnuItemMExport);
            _leftListContext.ItemsSource = leftListContextItems;
            LeftListBox.ContextMenu = _leftListContext;

            var _mnuItemCopyLog = new MenuItem
            {
                Name = "mnuItemCopyLog",
                Header = "Kopieren"
            };
            _mnuItemCopyLog.Click += MnuItemCopyLogOnClick;

            _logListContextMenu.ItemsSource = new List<Control> { _mnuItemCopyLog };
            lbLogDisplay.ContextMenu = _logListContextMenu;

            //Kontextmenu fuer tbleftSearch
            List<Control> leftListButtonContextItems = new();
            var cbSucheVorname = new CheckBox
            {
                Name = "cbMnuSucheVorname",
                Content = "Vorname",
                IsChecked = true,
            };
            var cbSucheNachname = new CheckBox
            {
                Name = "cbMnuSucheNachname",
                Content = "Nachname",
                IsChecked = true,
            };
            var cbSucheMail = new CheckBox
            {
                Name = "cbMnuSucheMailadressen",
                Content = "Mailadressen",
                IsChecked = false,
            };
            var cbSucheAnmeldename = new CheckBox
            {
                Name = "cbMnuSucheAnmeldename",
                Content = "Anmeldename/Kürzel",
                IsChecked = false,
            };
            var cbSucheID = new CheckBox
            {
                Name = "cbMnuSucheID",
                Content = "ID",
                IsChecked = false,
            };
            var cbSucheExact = new CheckBox
            {
                Name = "cbMnuSucheExact",
                Content = "Exakte Suche",
                IsChecked = false,
            };
            leftListButtonContextItems.Add(cbSucheVorname);
            leftListButtonContextItems.Add(cbSucheNachname);
            leftListButtonContextItems.Add(cbSucheMail);
            leftListButtonContextItems.Add(cbSucheAnmeldename);
            leftListButtonContextItems.Add(cbSucheID);
            leftListButtonContextItems.Add(cbSucheExact);
            tbLeftSearch.ContextMenu = new ContextMenu
            {
                ItemsSource = leftListButtonContextItems
            };

            //Rest
            rbD.IsChecked = true;
            Rb_OnClick(rbD, new RoutedEventArgs());
            LeftListBox.MaxHeight = ClientSize.Height * 1.1;
            RightListBox.MaxHeight = LeftListBox.MaxHeight;
            lbLogDisplay.MaxHeight = LeftListBox.MaxHeight;
            lbLogDisplay.MaxWidth = ClientSize.Width * 1.1;
            _msgBoxWindowIcon =
                new WindowIcon(AssetLoader.Open(new Uri("avares://StS-GUI-Avalonia/Assets/gfx/school-building.png")));
            Resized += (s, e) =>
            {
                if (FrameSize == null) return;
                mainScroller.MaxHeight = FrameSize.Value.Height *0.9;
                mainScroller.MaxWidth = FrameSize.Value.Width;
                LeftListBox.MaxHeight = mainScroller.MaxHeight * 0.8;
                RightListBox.MaxHeight = LeftListBox.MaxHeight;
                lbLogDisplay.MaxHeight = LeftListBox.MaxHeight;
                lbLogDisplay.MaxWidth = FrameSize.Value.Width * 0.8;
                exportScrollViewerFavo.MaxHeight = LeftListBox.MaxHeight;
            };
        }

        private async Task<IStorageFile?> ShowSaveFileDialog(string dialogtitle,
            IReadOnlyList<FilePickerFileType> extensions)
        {
            var topLevel = GetTopLevel(this);
            if (topLevel == null) return null;
            var files = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = dialogtitle,
                DefaultExtension = extensions[0].Name
            });
            return files;
        }

        private async Task<IStorageFile?> ShowOpenFileDialog(string dialogtitle,
            IReadOnlyList<FilePickerFileType> extensions)
        {
            var topLevel = GetTopLevel(this);
            if (topLevel == null) return null;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = dialogtitle,
                FileTypeFilter = extensions,
                AllowMultiple = false,
            });
            return files.Count > 0 ? files[0] : null;
        }

        private async Task<IStorageFolder?> ShowOpenFolderDialog(string dialogtitle)
        {
            var topLevel = GetTopLevel(this);
            if (topLevel == null) return null;
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = dialogtitle,
                AllowMultiple = false,
            });
            return folders.Count > 0 ? folders[0] : null;
        }

        public async void OnMnuSchoolLoadClick(object? sender, RoutedEventArgs e)
        {
            var extx = new List<FilePickerFileType>
            {
                StSFileTypes.DataBaseFile
            };
            var files = await ShowOpenFileDialog("Bitte einen Dateipfad angeben...", extx);
            if (files == null) return;
            var filepath = files.Path.LocalPath;
            _myschool = new Schuldatenbank(filepath);
            Title = "SchildToSchule - " + await _myschool.GetFilePath();
            InitData();
            await loadFavos();
        }

        private async Task loadFavos()
        {
            var faecherliste = _myschool.GetLehrerListe().Result.Select(l => l.Fakultas.Split(',')).Distinct().ToList();
            if (faecherliste.Count < 1) return;
            var faecher = new List<string>();
            foreach (var faecherarray in faecherliste)
            {
                faecher.AddRange(faecherarray);
            }

            faecher = faecher.Distinct().ToList();
            faecher.Sort();
            exportFavoTabGrid.ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto");
            var rowdefs = "";
            for (var i = 0; i < faecher.Count; i++)
            {
                rowdefs += "Auto,";
            }

            exportFavoTabGrid.RowDefinitions = new RowDefinitions(rowdefs.TrimEnd(','));
            for (var i = 0; i < faecher.Count; ++i)
            {
                var cache = _myschool.GetLehrerListe().Result.Where(l => l.Fakultas.Split(',').Contains(faecher[i]))
                    .ToList()
                    .Select(l => l.Kuerzel + ";" + l.Nachname + "," + l.Vorname).ToList();
                if (cache.Count == 0) continue;
                exportFavoTabGrid.Children.Add(new TextBlock
                {
                    Name = "tbExportFavo" + faecher[i],
                    Text = faecher[i],
                    [Grid.RowProperty] = i,
                    [Grid.ColumnProperty] = 0,
                });
                cache.Add("");
                cache.Sort();
                exportFavoTabGrid.Children.Add(new ComboBox()
                {
                    Name = "cbExportFavo" + faecher[i],
                    ItemsSource = cache,
                    [Grid.RowProperty] = i,
                    [Grid.ColumnProperty] = 1,
                });
                exportFavoTabGrid.Children.Add(new ComboBox
                {
                    Name = "cbExportSFavo" + faecher[i],
                    ItemsSource = cache,
                    [Grid.RowProperty] = i,
                    [Grid.ColumnProperty] = 2,
                });
                // solved via https://github.com/AvaloniaUI/Avalonia/discussions/10144
            }

            var favos = await _myschool.getFavos();

            foreach (var fach in faecher)
            {
                var validfach = exportFavoTabGrid.Children.Where(c => c.Name.Equals("cbExportFavo" + fach))
                    .ToList();
                if (validfach.Count == 0) continue;
                var favocb = (ComboBox)exportFavoTabGrid.Children.Where(c => c.Name.Equals("cbExportFavo" + fach))
                    .ToList()[0];
                var sfavocb = (ComboBox)exportFavoTabGrid.Children.Where(c => c.Name.Equals("cbExportSFavo" + fach))
                    .ToList()[0];
                var favo = favos.Where(l => l.Favo.Split(',').Contains(fach)).ToList();
                if (favo.Count > 0)
                {
                    favocb.SelectedItem = favo[0].Kuerzel + ";" + favo[0].Nachname + "," + favo[0].Vorname;
                }

                var sfavo = favos.Where(l => l.SFavo.Split(',').Contains(fach)).ToList();
                if (sfavo.Count > 0)
                {
                    sfavocb.SelectedItem = sfavo[0].Kuerzel + ";" + sfavo[0].Nachname + "," + sfavo[0].Vorname;
                }
            }
        }

        public async void OnMnuschuleschließenClick(object? sender, RoutedEventArgs e)
        {
            if (_myschool.GetFilePath().Result != ":memory:")
            {
                var leftlist = this.GetControl<ListBox>("LeftListBox");
                var rightlist = this.GetControl<ListBox>("RightListBox");
                ResetItemsSource(leftlist, new List<string>());
                ResetItemsSource(rightlist, new List<string>());
                Title = "SchildToSchule";
                _myschool.Dispose();
                _myschool = new Schuldatenbank(":memory:");
                ClearTextFields();
                InitData();
                exportFavoTabGrid.Children.Clear();
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(SaveDbFile);
            return;

            async Task SaveDbFile()
            {
                var extx = new List<FilePickerFileType> { StSFileTypes.DataBaseFile };
                var files = await ShowSaveFileDialog("Bitte einen Dateipfad angeben...", extx);
                if (files == null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        ClearTextFields();
                        ResetItemsSource(LeftListBox, new List<string>());
                        ResetItemsSource(RightListBox, new List<string>());
                    });
                    return;
                }

                var filepath = files.Path.LocalPath;
                var tempDB = new Schuldatenbank(filepath);
                var res = await tempDB.Import(_myschool);
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    if (res == 0)
                    {
                        _myschool = tempDB;
                        var saveDBInPath = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.Ok,
                            ContentTitle = "Erfolg",
                            ContentMessage = "Datenbank erfolgreich gespeichert",
                            Icon = MsBox.Avalonia.Enums.Icon.Success,
                            WindowIcon = _msgBoxWindowIcon
                        });
                        await saveDBInPath.ShowAsPopupAsync(this);
                    }
                    else
                    {
                        var errorNoSystemDialog = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.Ok,
                            ContentTitle = "Fehler",
                            ContentMessage = "Schließen fehlgeschlagen",
                            Icon = MsBox.Avalonia.Enums.Icon.Error,
                            WindowIcon = _msgBoxWindowIcon
                        });
                        await errorNoSystemDialog.ShowAsPopupAsync(this);
                    }
                });
            }
        }

        public async void OnMnuschulespeichernunterClick(object? sender, RoutedEventArgs e)
        {
            await Dispatcher.UIThread.InvokeAsync(SaveDbFile);
            return;

            async Task SaveDbFile()
            {
                var extx = new List<FilePickerFileType> { StSFileTypes.DataBaseFile };
                var files = await ShowSaveFileDialog("Bitte einen Dateipfad angeben...", extx);
                if (files == null) return;
                var filepath = files.Path.LocalPath;
                var tempDB = new Schuldatenbank(filepath);
                var res = await tempDB.Import(_myschool);
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    if (res != 0)
                    {
                        var errorNoSystemDialog = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.Ok,
                            ContentTitle = "Fehler",
                            ContentMessage = "Speichern unter fehlgeschlagen",
                            Icon = MsBox.Avalonia.Enums.Icon.Error,
                            WindowIcon = _msgBoxWindowIcon
                        });
                        await errorNoSystemDialog.ShowAsPopupAsync(this);
                    }
                    else
                    {
                        _myschool = tempDB;
                        var saveDBInPath = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.Ok,
                            ContentTitle = "Erfolg",
                            ContentMessage = "Datenbank erfolgreich gespeichert",
                            Icon = MsBox.Avalonia.Enums.Icon.Success,
                            WindowIcon = _msgBoxWindowIcon
                        });
                        await saveDBInPath.ShowAsPopupAsync(this);
                    }
                });
            }
        }

        public async void OnMnuschuleversspeichernClick(object? sender, RoutedEventArgs e)
        {
            if (_myschool.GetFilePath().Result == ":memory:") return;

            var inputResult = await Dispatcher.UIThread.InvokeAsync(GetPasswordInput, DispatcherPriority.Input);
            if (string.IsNullOrEmpty(inputResult)) return;

            await Dispatcher.UIThread.InvokeAsync(SaveDbFile);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                MessageBoxManager.GetMessageBoxStandard(
                    new MessageBoxStandardParams
                    {
                        ButtonDefinitions = ButtonEnum.Ok,
                        ContentTitle = "Information",
                        ContentMessage =
                            "Speichern erfolgreich",
                        Icon = MsBox.Avalonia.Enums.Icon.Info,
                        WindowIcon = _msgBoxWindowIcon
                    }).ShowAsPopupAsync(this);
            });
            return;

            async Task SaveDbFile()
            {
                var extx = new List<FilePickerFileType> { StSFileTypes.EncryptedFile };
                var files = await ShowSaveFileDialog("Bitte einen Dateipfad angeben...", extx);
                if (files == null) return;
                var filepath = files.Path.LocalPath;
                var dbPath = await _myschool.GetFilePath();
                _myschool.Dispose();
                LocalCryptoServive.FileEncrypt(dbPath, filepath, inputResult);
                _myschool = new Schuldatenbank(dbPath);
            }

            async Task<string?> GetPasswordInput()
            {
                var pwiWindow = new PasswordInput();
                var test = await pwiWindow.ShowPWDDialog(this);
                return test;
            }
        }

        public async void OnMnuversschuleladenClick(object? sender, RoutedEventArgs e)
        {
            var extx = new List<FilePickerFileType>
            {
                StSFileTypes.EncryptedFile
            };
            var files = await ShowOpenFileDialog("Bitte einen Dateipfad angeben...", extx);
            if (files == null) return;
            var inputFilePath = files.Path.LocalPath;

            var inputResult = await Dispatcher.UIThread.InvokeAsync(GetPasswordInput, DispatcherPriority.Input);
            if (string.IsNullOrEmpty(inputResult)) return;

            await Dispatcher.UIThread.InvokeAsync(SaveDbFile);
            return;

            async Task<string?> GetPasswordInput()
            {
                var pwiWindow = new PasswordInput();
                var test = await pwiWindow.ShowPWDDialog(this);
                return test;
            }

            async Task SaveDbFile()
            {
                var encFileType = new List<FilePickerFileType> { StSFileTypes.EncryptedFile };
                var saveFile = await ShowSaveFileDialog("Bitte einen Dateipfad angeben...", encFileType);
                var outputFilePath = saveFile?.Path.LocalPath;
                if (outputFilePath == null) return;

                LocalCryptoServive.FileDecrypt(inputFilePath, outputFilePath, inputResult);
                _myschool = new Schuldatenbank(outputFilePath);
                await loadFavos();
            }
        }

        public void OnMnuexitClick(object? sender, RoutedEventArgs e)
        {
            _myschool.Dispose();
            Environment.Exit(0);
        }

        public async void OnMnuloadfolderClick(object? sender, RoutedEventArgs e)
        {
            await Dispatcher.UIThread.InvokeAsync(ReadFileTask);
            return;

            async Task ReadFileTask()
            {
                var folder = await ShowOpenFolderDialog("Bitte den Ordner mit den Dateien auswählen");
                if (folder == null) return;
                var folderpath = folder.Path.LocalPath;
                if (File.Exists(folderpath + "/sus.csv") && File.Exists(folderpath + "/lul.csv") &&
                    File.Exists(folderpath + "/kurse.csv"))
                {
                    await _myschool.SusEinlesen(folderpath + "/sus.csv");
                    await _myschool.LulEinlesen(folderpath + "/lul.csv");
                    await _myschool.KurseEinlesen(folderpath + "/kurse.csv");
                    var aixcsvpath = "";
                    var d = new DirectoryInfo(folderpath);
                    var files = d.GetFiles();
                    foreach (var csvFile in files)
                    {
                        if (!csvFile.Name.StartsWith("AlleSchueler")) continue;
                        aixcsvpath = csvFile.FullName;
                        break;
                    }

                    if (aixcsvpath != "")
                    {
                        await _myschool.IdsEinlesen(aixcsvpath);
                    }

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        InitData();
                        MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
                            {
                                ButtonDefinitions = ButtonEnum.Ok,
                                ContentTitle = "Information",
                                ContentMessage = "Import erfolgreich",
                                Icon = MsBox.Avalonia.Enums.Icon.Info,
                                WindowIcon = _msgBoxWindowIcon
                            })
                            .ShowAsPopupAsync(this);
                    });
                }
            }
        }

        public async void OnMnuloadsusfromfileClick(object? sender, RoutedEventArgs e)
        {
            var extx = new List<FilePickerFileType>
            {
                StSFileTypes.CSVFile,
                FilePickerFileTypes.All
            };
            var files = await ShowOpenFileDialog("Lade Schüler:innendaten", extx);
            if (files == null) return;
            var filePath = files.Path.LocalPath;
            await _myschool.SusEinlesen(filePath);
        }

        public async void OnMnuloadlulfromfileClick(object? sender, RoutedEventArgs e)
        {
            var extx = new List<FilePickerFileType>
            {
                StSFileTypes.CSVFile,
                FilePickerFileTypes.All
            };
            var files = await ShowOpenFileDialog("Lade Lehrer:innendaten", extx);
            if (files == null) return;
            var filePath = files.Path.LocalPath;
            await _myschool.LulEinlesen(filePath);
        }

        public async void OnMnuloadkursefromfileClick(object? sender, RoutedEventArgs e)
        {
            var extx = new List<FilePickerFileType>
            {
                StSFileTypes.CSVFile,
                FilePickerFileTypes.All
            };
            var files = await ShowOpenFileDialog("Lade Kursdaten", extx);
            if (files == null) return;
            var filePath = files.Path.LocalPath;
            await _myschool.KurseEinlesen(filePath);
        }

        public async void OnMnuloadusernamesmailClick(object? sender, RoutedEventArgs e)
        {
            var extx = new List<FilePickerFileType>
            {
                StSFileTypes.CSVFile,
                FilePickerFileTypes.All
            };
            var files = await ShowOpenFileDialog("Lade Nutzernamen & Mailadressen", extx);
            if (files == null) return;
            var filePath = files.Path.LocalPath;
            await _myschool.IdsEinlesen(filePath);
        }

        public async void OnMnuloadzweitaccountsClick(object? sender, RoutedEventArgs e)
        {
            var extx = new List<FilePickerFileType>
            {
                StSFileTypes.CSVFile,
                FilePickerFileTypes.All
            };
            var files = await ShowOpenFileDialog("Lade Zweitaccountdaten", extx);
            if (files == null) return;
            var filePath = files.Path.LocalPath;
            await _myschool.ZweitAccountsEinlesen(filePath);
        }

        public async void OnMnuexporttocsvClick(object? sender, RoutedEventArgs e)
        {
            await Dispatcher.UIThread.InvokeAsync(ReadFileTask);
            return;

            async Task ReadFileTask()
            {
                var folder = await ShowOpenFolderDialog("Bitte den Ordner mit den Dateien auswählen");
                if (folder == null) return;
                var folderpath = folder.Path.LocalPath;
                if (!File.Exists(folderpath + "/sus.csv") && !File.Exists(folderpath + "/lul.csv") &&
                    !File.Exists(folderpath + "/kurse.csv"))
                {
                    await _myschool.DumpDataToCSVs(folderpath);
                }
            }
        }

        public async void OnMnuaboutClick(object? sender, RoutedEventArgs e)
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version == null) return;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                MessageBoxManager.GetMessageBoxStandard(
                    new MessageBoxStandardParams
                    {
                        ButtonDefinitions = ButtonEnum.Ok,
                        ContentTitle = "Über",
                        ContentMessage =
                            Application.Current?.Name + "\n" + version,
                        Icon = MsBox.Avalonia.Enums.Icon.Setting,
                        WindowIcon = _msgBoxWindowIcon
                    }).ShowAsPopupAsync(this);
            });
        }

        public async void OnBtnsusaddClick(object? sender, RoutedEventArgs e)
        {
            var susid = tbSuSID.Text;
            var susvname = tbSuSVorname.Text;
            var susnname = tbSuSnachname.Text;
            var susklasse = tbSuSKlasse.Text;
            var susnutzername = tbSuSNutzername.Text;
            var susaximail = tbSuSAIXMail.Text;
            var suselternadresse = tbSuSElternadresse.Text;
            var suszweitadresse = tbSuSZweitadresse.Text;
            var susHatZweitaccount = cbSuSZweitaccount.IsChecked;
            if (susid == null || susvname == null || susnname is null || susklasse == null ||
                susnutzername == null || susaximail == null || suselternadresse == null || suszweitadresse == null ||
                susHatZweitaccount == null || susid == "" || susvname == "" || susnname is "" || susklasse == "" ||
                suselternadresse == "" || tbSuSKurse.Text == null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MessageBoxManager.GetMessageBoxStandard(
                        new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.Ok,
                            ContentTitle = "Fehler",
                            ContentMessage =
                                "Nicht alle erforderlichen Informationen angegeben!\nStellen Sie sicher, dass ID, Vorname, Nachname, Klasse\nund eine Elternadresse angegeben sind",
                            Icon = MsBox.Avalonia.Enums.Icon.Error,
                            WindowIcon = _msgBoxWindowIcon
                        }).ShowAsPopupAsync(this);
                });
                return;
            }

            var suskurse = tbSuSKurse.Text.Split(',').ToList();

            if (!susid.All(char.IsDigit))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MessageBoxManager.GetMessageBoxStandard(
                        new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.Ok,
                            ContentTitle = "Fehler",
                            ContentMessage =
                                "Die SuS-ID enthält nicht nur Zahlen!",
                            Icon = MsBox.Avalonia.Enums.Icon.Error,
                            WindowIcon = _msgBoxWindowIcon
                        }).ShowAsPopupAsync(this);
                });
                return;
            }

            var sid = Convert.ToInt32(susid);
            if (await _myschool.GibtEsSchueler(sid))
            {
                await _myschool.UpdateSchueler(sid, susvname, susnname, suselternadresse, susklasse, susnutzername,
                    susaximail, susHatZweitaccount == false ? 0 : 1, suszweitadresse);
                var alteKurse = _myschool.GetKursVonSuS(sid).Result;
                foreach (var kurs in alteKurse.Where(kurs => !suskurse.Contains(kurs.Bezeichnung)))
                {
                    await _myschool.RemoveSfromK(sid, kurs.Bezeichnung);
                }

                if (suskurse.Count <= 0) return;
                {
                    foreach (var kurs in suskurse)
                    {
                        await _myschool.AddStoK(sid, kurs);
                    }
                }
            }
            else
            {
                await _myschool.AddSchuelerIn(sid, susvname, susnname, suselternadresse, susklasse, susnutzername,
                    susaximail, susHatZweitaccount == false ? 0 : 1, suszweitadresse);
                if (suskurse.Count == 1 && suskurse[0] == "") return;
                foreach (var kursbez in suskurse)
                {
                    await _myschool.AddStoK(sid, kursbez);
                }
            }
        }

        public async void OnBtnsusdelClick(object? sender, RoutedEventArgs e)
        {
            var susid = tbSuSID.Text;
            if (susid is null or "" || !susid.All(char.IsDigit)) return;
            var sid = Convert.ToInt32(susid);
            if (_myschool.GetSchueler(sid).Result.ID == 0) return;
            ListBox source;
            if (CboxDataLeft.SelectedIndex == 0)
            {
                source = LeftListBox;
            }
            else if (CboxDataRight.SelectedIndex == 0)
            {
                source = RightListBox;
            }
            else
            {
                await _myschool.RemoveS(sid);
                return;
            }

            await _myschool.StartTransaction();
            if (source.SelectedItems != null)
            {
                foreach (var susstring in source.SelectedItems.Cast<string>())
                {
                    if (susstring == null) return;
                    sid = Convert.ToInt32(susstring.Split(';')[1]);
                    await _myschool.RemoveS(sid);
                }
            }

            await _myschool.StopTransaction();
            OnLeftDataChanged(true);
            OnRightDataChanged(true);
        }

        public async void OnbtnsuseinschreibenClick(object? sender, RoutedEventArgs e)
        {
            var susid = tbSuSID.Text;
            var sid = Convert.ToInt32(susid);
            var susklasse = string.IsNullOrEmpty(tbSuSKlasse.Text) ? "" : tbSuSKlasse.Text;
            if (susklasse == "" || sid == 0) return;
            await _myschool.AddStoKlassenKurse(await _myschool.GetSchueler(sid), susklasse);
            OnLeftDataChanged(true);
            OnRightDataChanged(true);
        }

        public async void OnBtnluladdClick(object? sender, RoutedEventArgs e)
        {
            var lulid = tbLuLID.Text;
            var lulvname = tbLuLVorname.Text;
            var lulnname = tbLuLNachname.Text;
            var lulkrz = tbLuLKuerzel.Text;
            var lulfakultas = tbLuLFach.Text;
            var lulmail = tbLuLMail.Text;
            var lulpwtemp = tbLuLtmpPwd.Text;
            var favo = tbLuLFavo.Text;
            var sfavo = tbLuLSFavo.Text;
            if (lulid == null || lulvname == null || lulnname == null || lulkrz == null || lulfakultas == null ||
                lulmail == null || lulpwtemp == null || lulvname == "" || lulnname == "" ||
                lulkrz == "" || lulfakultas == "" || lulmail == "" || tbLuLKurse.Text == null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MessageBoxManager.GetMessageBoxStandard(
                        new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.Ok,
                            ContentTitle = "Fehler",
                            ContentMessage =
                                "Nicht alle erforderlichen Informationen angegeben!\nStellen Sie sicher, dass ID, Vorname, Nachname, Kürzel\nund Fakultas ausgefüllt sind.",
                            Icon = MsBox.Avalonia.Enums.Icon.Error,
                            WindowIcon = _msgBoxWindowIcon
                        }).ShowAsPopupAsync(this);
                });
                return;
            }

            if (string.IsNullOrEmpty(favo))
            {
                favo = "";
            }

            if (string.IsNullOrEmpty(sfavo))
            {
                sfavo = "";
            }

            var lulkurse = tbLuLKurse.Text.Split(',').ToList();

            if (lulid == "" || !lulid.All(char.IsDigit))
            {
                lulid = _myschool.GetLehrerIDListe().Result.Max() + 1 + "";
                lulpwtemp = Schuldatenbank.GeneratePasswort(8);
            }

            var lid = Convert.ToInt32(lulid);
            if (await _myschool.GibtEsLehrkraft(lid))
            {
                await _myschool.UpdateLehrkraft(lid, lulvname, lulnname, lulkrz, lulmail, lulfakultas, lulpwtemp, favo,
                    sfavo);
                var alteKurse = _myschool.GetKursVonLuL(lid).Result;
                foreach (var kurs in alteKurse.Where(kurs => !lulkurse.Contains(kurs.Bezeichnung)))
                {
                    await _myschool.RemoveLfromK(lid, kurs.Bezeichnung);
                }

                if (lulkurse.Count <= 0) return;
                {
                    foreach (var kurs in lulkurse)
                    {
                        await _myschool.AddLtoK(lid, kurs);
                    }
                }
            }
            else
            {
                await _myschool.Addlehrkraft(lid, lulvname, lulnname, lulkrz, lulmail, lulfakultas, favo, sfavo);
                if (lulkurse.Count == 0) return;
                foreach (var kurs in lulkurse)
                {
                    await _myschool.AddLtoK(lid, kurs);
                }
            }

            OnLeftDataChanged(true);
            OnRightDataChanged(true);
        }

        public async void OnBtnluldelClick(object? sender, RoutedEventArgs e)
        {
            var lulid = tbLuLID.Text;
            if (lulid is null or "" || !lulid.All(char.IsDigit)) return;
            var lid = Convert.ToInt32(lulid);
            ListBox source;
            if (CboxDataLeft.SelectedIndex == 1)
            {
                source = LeftListBox;
            }
            else if (CboxDataRight.SelectedIndex == 1)
            {
                source = RightListBox;
            }
            else
            {
                await _myschool.RemoveL(lid);
                return;
            }

            await _myschool.StartTransaction();
            if (source.SelectedItems != null)
            {
                foreach (var lulstring in source.SelectedItems.Cast<string>())
                {
                    var krz = lulstring.Split(';')[0];
                    await _myschool.RemoveL(krz);
                }
            }

            await _myschool.StopTransaction();
            OnLeftDataChanged(true);
            OnRightDataChanged(true);
        }

        private void InitData()
        {
            if (CboxDataLeft == null || CboxDataRight == null) return;
            CboxDataLeft.SelectedIndex = 0;
            CboxDataRight.SelectedIndex = 1;
            var llist = _myschool.GetSchuelerListe().Result.Select(s => (s.Nachname + "," + s.Vorname + ";" + s.ID))
                .ToList();
            llist.Sort(Comparer<string>.Default);
            ResetItemsSource(LeftListBox, llist);
            var settings = _myschool.GetSettings().Result;
            tbSettingMailplatzhalter.Text = settings.Mailsuffix;
            tbSettingKursersetzung.Text = string.IsNullOrEmpty(settings.Fachersetzung)
                ? ""
                : settings.Fachersetzung.Split(';')[1];
            tbSettingKurssuffix.Text = settings.Kurssuffix;
            var kurzfach = _myschool.GetFachersatz().Result.Select(t => t.Split(';')[0]);
            var langfach = _myschool.GetFachersatz().Result.Select(t => t.Split(';')[1]);
            tbSettingFachkurz.Text = "";
            foreach (var fachk in kurzfach)
            {
                tbSettingFachkurz.Text += fachk + '\n';
            }

            tbSettingFachlang.Text = "";
            foreach (var fachl in langfach)
            {
                tbSettingFachlang.Text += fachl + '\n';
            }

            tbSettingErprobungsstufenleitung.Text = settings.Erprobungstufenleitung;
            tbSettingMittelstufenleitung.Text = settings.Mittelstufenleitung;
            tbSettingEFstufenleitung.Text = settings.EFStufenleitung;
            tbSettingQ1stufenleitung.Text = settings.Q1Stufenleitung;
            tbSettingQ2stufenleitung.Text = settings.Q2Stufenleitung;
            tbSettingOberstufenkoordination.Text = settings.Oberstufenkoordination;
        }

        private void CboxDataLeft_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            ClearTextFields();
            _rightMutex = true;
            if (leftLastComboIndex != CboxDataLeft.SelectedIndex)
            {
                LeftListBox.SelectedItems?.Clear();
                OnLeftDataChanged(true);
                leftLastComboIndex = CboxDataLeft.SelectedIndex;
            }

            _rightMutex = false;
        }

        private void ClearTextFields()
        {
            ClearSuSTextFields();
            ClearLuLTextFields();
            ClearKursTextFields();
        }

        private void ClearKursTextFields()
        {
            tbKursbezeichnung.Text = "";
            tbKursLuL.Text = "";
            tbKursFach.Text = "";
            tbKursSuffix.Text = "";
            tbKursKlasse.Text = "";
            tbKursStufe.Text = "";
            cbKursIstKurs.IsChecked = false;
        }

        private void ClearLuLTextFields()
        {
            tbLuLID.Text = "";
            tbLuLVorname.Text = "";
            tbLuLNachname.Text = "";
            tbLuLKuerzel.Text = "";
            tbLuLFach.Text = "";
            tbLuLMail.Text = "";
            tbLuLtmpPwd.Text = "";
            tbLuLKurse.Text = "";
        }

        private void ClearSuSTextFields()
        {
            tbSuSID.Text = "";
            tbSuSVorname.Text = "";
            tbSuSnachname.Text = "";
            tbSuSKlasse.Text = "";
            tbSuSNutzername.Text = "";
            tbSuSAIXMail.Text = "";
            tbSuSElternadresse.Text = "";
            tbSuSZweitadresse.Text = "";
            tbSuSKurse.Text = "";
            cbSuSZweitaccount.IsChecked = false;
        }

        private void CboxDataRight_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            _rightMutex = true;
            if (rightLastComboIndex != CboxDataRight.SelectedIndex)
            {
                RightListBox.SelectedItems?.Clear();
                OnRightDataChanged(true);
                rightLastComboIndex = CboxDataRight.SelectedIndex;
            }

            _rightMutex = false;
        }

        private void LeftListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            ClearTextFields();
            OnLeftDataChanged(false);
        }

        private void RightListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            OnRightDataChanged(false);
        }

        private void OnLeftDataChanged(bool hasComboBoxChanged)
        {
            if (LeftListBox == null || RightListBox == null || CboxDataLeft == null || CboxDataRight == null) return;
            if (LeftListBox.SelectedItems == null) return;
            SetStatusText();
            if (_rightMutex && !hasComboBoxChanged) return;
            if (hasComboBoxChanged)
            {
                ResetItemsSource(RightListBox, new List<string>());
            }

            _mnuItemCopySuSid.IsVisible = _mnuItemCopySuSMail.IsVisible = CboxDataLeft.SelectedIndex == 0;
            _mnuItemCopyLuLKrz.IsVisible = _mnuItemCopyLuLMails.IsVisible = CboxDataLeft.SelectedIndex == 1;
            _mnuItemCopyKursBez.IsVisible = CboxDataLeft.SelectedIndex == 2;
            switch (CboxDataLeft.SelectedIndex)
            {
                //s=0;l==1;k==2
                case 0:
                    if (CboxDataRight.SelectedIndex == 0)
                    {
                        CboxDataRight.SelectedIndex = 1;
                    }

                    if (LeftListBox.SelectedItems.Count < 1 || LeftListBox.SelectedItems == null || hasComboBoxChanged)
                    {
                        var slist = _myschool.GetSchuelerListe().Result
                            .Select(s => (s.Nachname + "," + s.Vorname + ";" + s.ID)).Distinct().ToList();
                        slist.Sort(Comparer<string>.Default);
                        ResetItemsSource(LeftListBox, slist);
                        ResetItemsSource(RightListBox, new List<string>());
                    }
                    else
                    {
                        var sid = LeftListBox.SelectedItems[0]?.ToString()?.Split(';')[1];
                        var sus = _myschool.GetSchueler(Convert.ToInt32(sid)).Result;
                        LoadSuSData(sus);
                        if (sus.ID == 0) return;
                        switch (CboxDataRight.SelectedIndex)
                        {
                            case 1:
                            {
                                var rlist = _myschool.GetLuLvonSuS(sus.ID).Result
                                    .Select(l => (l.Kuerzel + ";" + l.Nachname + "," + l.Vorname)).Distinct().ToList();
                                rlist.Sort(Comparer<string>.Default);
                                ResetItemsSource(RightListBox, rlist);
                                break;
                            }
                            case 2:
                            {
                                var rlist = _myschool.GetKursVonSuS(sus.ID).Result.Select(k => (k.Bezeichnung))
                                    .Distinct()
                                    .ToList();
                                ResetItemsSource(RightListBox, rlist);
                                break;
                            }
                        }
                    }

                    break;
                case 1:
                    if (CboxDataRight.SelectedIndex == 1)
                    {
                        CboxDataRight.SelectedIndex = 2;
                    }

                    if (LeftListBox.SelectedItems.Count < 1 || LeftListBox.SelectedItems == null || hasComboBoxChanged)
                    {
                        var lullist = _myschool.GetLehrerListe().Result
                            .Select(l => (l.Kuerzel + ";" + l.Nachname + "," + l.Vorname)).Distinct().ToList();
                        lullist.Sort(Comparer<string>.Default);
                        ResetItemsSource(LeftListBox, lullist);
                        ResetItemsSource(RightListBox, new List<string>());
                    }
                    else
                    {
                        var lulkrz = LeftListBox.SelectedItems[0]?.ToString()?.Split(';')[0];
                        if (string.IsNullOrEmpty(lulkrz)) return;
                        var lul = _myschool.GetLehrkraft(lulkrz).Result;
                        LoadLuLData(lul);
                        switch (CboxDataRight.SelectedIndex)
                        {
                            case 0:
                            {
                                var rlist = _myschool.GetSuSVonLuL(lul.ID).Result
                                    .Select(s => (s.Nachname + "," + s.Vorname + ";" + s.ID)).Distinct().ToList();
                                rlist.Sort(Comparer<string>.Default);
                                ResetItemsSource(RightListBox, rlist);

                                break;
                            }
                            case 2:
                            {
                                var rlist = _myschool.GetKursVonLuL(lul.ID).Result.Select(k => (k.Bezeichnung))
                                    .Distinct()
                                    .ToList();
                                rlist.Sort(Comparer<string>.Default);
                                ResetItemsSource(RightListBox, rlist);
                                break;
                            }
                        }
                    }

                    break;
                case 2:
                    if (CboxDataRight.SelectedIndex == 2)
                    {
                        CboxDataRight.SelectedIndex = 0;
                    }

                    if (LeftListBox.SelectedItems.Count < 1 || LeftListBox.SelectedItems == null || hasComboBoxChanged)
                    {
                        var klist = _myschool.GetKursListe().Result.Select(k => (k.Bezeichnung)).Distinct().ToList();
                        klist.Sort(Comparer<string>.Default);
                        ResetItemsSource(LeftListBox, klist);
                        ResetItemsSource(RightListBox, new List<string>());
                    }
                    else
                    {
                        var kurzbez = LeftListBox.SelectedItems[0]?.ToString();
                        if (string.IsNullOrEmpty(kurzbez)) return;
                        var kurs = _myschool.GetKurs(kurzbez).Result;
                        LoadKursData(kurs);
                        switch (CboxDataRight.SelectedIndex)
                        {
                            case 0:
                            {
                                var rlist = _myschool.GetSuSAusKurs(kurs.Bezeichnung).Result
                                    .Select(s => (s.Nachname + "," + s.Vorname + ";" + s.ID)).Distinct().ToList();
                                rlist.Sort(Comparer<string>.Default);
                                ResetItemsSource(RightListBox, rlist);
                                break;
                            }
                            case 1:
                            {
                                var rlist = _myschool.GetLuLAusKurs(kurs.Bezeichnung).Result
                                    .Select(l => (l.Kuerzel + ";" + l.Nachname + "," + l.Vorname)).Distinct().ToList();
                                rlist.Sort(Comparer<string>.Default);
                                ResetItemsSource(RightListBox, rlist);
                                break;
                            }
                        }
                    }

                    break;
            }
        }

        private void SetStatusText()
        {
            if (LeftListBox.SelectedItems == null || RightListBox.SelectedItems == null) return;
            var leftcounter = " | ";
            switch (CboxDataLeft.SelectedIndex)
            {
                case 0:
                    leftcounter += LeftListBox.SelectedItems.Count + " Schüler:Innen";
                    break;
                case 1:
                    leftcounter += LeftListBox.SelectedItems.Count + " Lehrkräfte";
                    break;
                case 2:
                    leftcounter += LeftListBox.SelectedItems.Count + " Kurse";
                    break;
            }

            var rightcounter = " und ";
            switch (CboxDataRight.SelectedIndex)
            {
                case 0:
                    rightcounter += RightListBox.SelectedItems.Count + " Schüler:Innen";
                    break;
                case 1:
                    rightcounter += RightListBox.SelectedItems.Count + " Lehrkräfte";
                    break;
                case 2:
                    rightcounter += RightListBox.SelectedItems.Count + " Kurse";
                    break;
            }

            tbStatusBar.Text = _myschool.GetStat().Result + leftcounter + rightcounter + " markiert";
        }

        private void OnRightDataChanged(bool hasComboBoxChanged)
        {
            if (LeftListBox == null || RightListBox == null || CboxDataLeft == null || CboxDataRight == null) return;
            if (RightListBox.SelectedItems == null) return;
            SetStatusText();
            if (_rightMutex && !hasComboBoxChanged) return;
            /*if (hasComboBoxChanged)
            {
                ResetItemsSource(LeftListBox, new List<string>());
            }*/

            switch (CboxDataRight.SelectedIndex)
            {
                //s=0;l==1;k==2
                case 0:
                    if (CboxDataLeft.SelectedIndex == 0 || LeftListBox.SelectedItems == null ||
                        LeftListBox.SelectedItems.Count < 1 ||
                        LeftListBox.SelectedItems == null) return;
                    switch (CboxDataLeft.SelectedIndex)
                    {
                        case 1:
                        {
                            var lulkrz = LeftListBox.SelectedItems[0]?.ToString()?.Split(';')[0];
                            if (string.IsNullOrEmpty(lulkrz)) return;
                            var lul = _myschool.GetLehrkraft(lulkrz).Result;
                            LoadLuLData(lul);
                            if (RightListBox.SelectedItems.Count > 0)
                            {
                                var sid = RightListBox.SelectedItems[0]?.ToString()?.Split(';')[1];
                                var sus = _myschool.GetSchueler(Convert.ToInt32(sid)).Result;
                                if (sus.ID == 0) return;
                                LoadSuSData(sus);
                            }

                            if (!hasComboBoxChanged) return;
                            var rlist = _myschool.GetSuSVonLuL(lul.ID).Result
                                .Select(s => (s.Nachname + "," + s.Vorname + ";" + s.ID)).Distinct().ToList();
                            rlist.Sort(Comparer<string>.Default);
                            ResetItemsSource(RightListBox, rlist);
                            break;
                        }
                        case 2:
                        {
                            var kurzbez = LeftListBox.SelectedItems[0]?.ToString();
                            if (string.IsNullOrEmpty(kurzbez)) return;
                            var kurs = _myschool.GetKurs(kurzbez).Result;
                            LoadKursData(kurs);
                            if (RightListBox.SelectedItems.Count > 0)
                            {
                                var sid = RightListBox.SelectedItems[0]?.ToString()?.Split(';')[1];
                                var sus = _myschool.GetSchueler(Convert.ToInt32(sid)).Result;
                                if (sus.ID == 0) return;
                                LoadSuSData(sus);
                            }

                            if (!hasComboBoxChanged) return;
                            var rlist = _myschool.GetSuSAusKurs(kurs.Bezeichnung).Result
                                .Select(s => (s.Nachname + "," + s.Vorname + ";" + s.ID)).Distinct().ToList();
                            rlist.Sort(Comparer<string>.Default);
                            ResetItemsSource(RightListBox, rlist);
                            break;
                        }
                    }

                    break;
                case 1:
                    if (CboxDataLeft.SelectedIndex == 1 || LeftListBox.SelectedItems == null ||
                        LeftListBox.SelectedItems.Count < 1 ||
                        LeftListBox.SelectedItems == null) return;
                    switch (CboxDataLeft.SelectedIndex)
                    {
                        case 0:
                        {
                            var sid = LeftListBox.SelectedItems[0]?.ToString()?.Split(';')[1];
                            var sus = _myschool.GetSchueler(Convert.ToInt32(sid)).Result;
                            if (sus.ID == 0) return;
                            LoadSuSData(sus);
                            if (RightListBox.SelectedItems.Count > 0)
                            {
                                var lulkrz = RightListBox.SelectedItems[0]?.ToString()?.Split(';')[0];
                                if (string.IsNullOrEmpty(lulkrz)) return;
                                var lul = _myschool.GetLehrkraft(lulkrz).Result;
                                LoadLuLData(lul);
                            }

                            if (!hasComboBoxChanged) return;
                            var rlist = _myschool.GetLuLvonSuS(sus.ID).Result
                                .Select(l => (l.Kuerzel + ";" + l.Nachname + "," + l.Vorname)).Distinct().ToList();
                            rlist.Sort(Comparer<string>.Default);
                            ResetItemsSource(RightListBox, rlist);
                            break;
                        }
                        case 2:
                        {
                            var kurzbez = LeftListBox.SelectedItems[0]?.ToString();
                            if (string.IsNullOrEmpty(kurzbez)) return;
                            var kurs = _myschool.GetKurs(kurzbez).Result;
                            LoadKursData(kurs);
                            if (RightListBox.SelectedItems.Count > 0)
                            {
                                var lulkrz = RightListBox.SelectedItems[0]?.ToString()?.Split(';')[0];
                                if (string.IsNullOrEmpty(lulkrz)) return;
                                var lul = _myschool.GetLehrkraft(lulkrz).Result;
                                LoadLuLData(lul);
                            }

                            if (!hasComboBoxChanged) return;
                            var rlist = _myschool.GetLuLAusKurs(kurs.Bezeichnung).Result
                                .Select(l => (l.Kuerzel + ";" + l.Nachname + "," + l.Vorname)).Distinct().ToList();
                            rlist.Sort(Comparer<string>.Default);
                            ResetItemsSource(RightListBox, rlist);
                            break;
                        }
                    }

                    break;
                case 2:
                    if (CboxDataLeft.SelectedIndex == 2 || LeftListBox.SelectedItems == null ||
                        LeftListBox.SelectedItems.Count < 1 ||
                        LeftListBox.SelectedItems == null) return;
                    switch (CboxDataLeft.SelectedIndex)
                    {
                        case 0:
                        {
                            var sid = LeftListBox.SelectedItems[0]?.ToString()?.Split(';')[1];
                            var sus = _myschool.GetSchueler(Convert.ToInt32(sid)).Result;
                            if (sus.ID == 0) return;
                            LoadSuSData(sus);
                            if (RightListBox.SelectedItems.Count > 0)
                            {
                                var kurzbez = RightListBox.SelectedItems[0]?.ToString();
                                if (string.IsNullOrEmpty(kurzbez)) return;
                                var kurs = _myschool.GetKurs(kurzbez).Result;
                                LoadKursData(kurs);
                            }

                            if (!hasComboBoxChanged) return;
                            var rlist = _myschool.GetKursVonSuS(sus.ID).Result.Select(k => (k.Bezeichnung))
                                .Distinct()
                                .ToList();
                            ResetItemsSource(RightListBox, rlist);
                            break;
                        }
                        case 1:
                        {
                            var lulkrz = LeftListBox.SelectedItems[0]?.ToString()?.Split(';')[0];
                            if (string.IsNullOrEmpty(lulkrz)) return;
                            var lul = _myschool.GetLehrkraft(lulkrz).Result;
                            LoadLuLData(lul);
                            if (RightListBox.SelectedItems.Count > 0)
                            {
                                var kurzbez = RightListBox.SelectedItems[0]?.ToString();
                                if (string.IsNullOrEmpty(kurzbez)) return;
                                var kurs = _myschool.GetKurs(kurzbez).Result;
                                LoadKursData(kurs);
                            }

                            if (!hasComboBoxChanged) return;
                            var rlist = _myschool.GetKursVonLuL(lul.ID).Result.Select(k => (k.Bezeichnung))
                                .Distinct()
                                .ToList();
                            rlist.Sort(Comparer<string>.Default);
                            ResetItemsSource(RightListBox, rlist);
                            break;
                        }
                    }

                    break;
            }
        }

        private void LoadSuSData(SuS s)
        {
            if (s.ID is 0 or < 50000) return;
            tbSuSID.Text = s.ID + "";
            tbSuSVorname.Text = s.Vorname;
            tbSuSnachname.Text = s.Nachname;
            tbSuSKlasse.Text = s.Klasse;
            tbSuSNutzername.Text = s.Nutzername;
            tbSuSAIXMail.Text = s.Aixmail;
            tbSuSElternadresse.Text = s.Mail;
            tbSuSZweitadresse.Text = s.Zweitmail;
            tbSuSKurse.Text = _myschool.GetKursVonSuS(s.ID).Result
                .Aggregate("", (current, kurs) => current + (kurs.Bezeichnung + ",")).TrimEnd(',');
            cbSuSZweitaccount.IsChecked = s.Zweitaccount;
        }

        private void LoadLuLData(LuL l)
        {
            if (l.ID is 0 or > 1500) return;
            tbLuLID.Text = l.ID + "";
            tbLuLVorname.Text = l.Vorname;
            tbLuLNachname.Text = l.Nachname;
            tbLuLKuerzel.Text = l.Kuerzel;
            tbLuLFach.Text = l.Fakultas;
            tbLuLMail.Text = l.Mail;
            tbLuLtmpPwd.Text = l.Pwttemp;
            tbLuLKurse.Text = _myschool.GetKursVonLuL(l.ID).Result
                .Aggregate("", (current, kurs) => current + (kurs.Bezeichnung + ",")).TrimEnd(',');
            tbLuLFavo.Text = l.Favo;
            tbLuLSFavo.Text = l.SFavo;
        }

        private void LoadKursData(Kurs k)
        {
            if (string.IsNullOrEmpty(k.Bezeichnung)) return;
            tbKursbezeichnung.Text = k.Bezeichnung;
            tbKursLuL.Text = _myschool.GetLuLAusKurs(k.Bezeichnung).Result
                .Aggregate("", (current, lul) => current + (lul.Kuerzel + ";")).TrimEnd(';');
            tbKursFach.Text = k.Fach;
            tbKursSuffix.Text = k.Suffix;
            tbKursKlasse.Text = k.Klasse;
            tbKursStufe.Text = k.Stufe;
            cbKursIstKurs.IsChecked = k.Istkurs;
        }

        private async void BtnExport_OnClick(object? sender, RoutedEventArgs e)
        {
            if (cbMoodle.IsChecked != null && !cbMoodle.IsChecked.Value && cbAIX.IsChecked != null &&
                !cbAIX.IsChecked.Value)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        MessageBoxManager.GetMessageBoxStandard(
                            new MessageBoxStandardParams
                            {
                                ButtonDefinitions = ButtonEnum.Ok,
                                ContentTitle = "Kein Zielsystem ausgewählt",
                                ContentMessage =
                                    "Bitte wählen Sie entweder Moodle und/oder AIX als Zielsystem!",
                                Icon = MsBox.Avalonia.Enums.Icon.Error,
                                WindowIcon = _msgBoxWindowIcon
                            }).ShowAsPopupAsync(this);
                    }
                );
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(ReadFileTask);
            return;

            async Task ReadFileTask()
            {
                var folder = await ShowOpenFolderDialog("Bitte den Ordner mit den Dateien auswählen");
                if (folder == null) return;
                var folderpath = folder.Path.LocalPath;
                var expandFiles = false;
                if (File.Exists(folderpath + "/aix_sus.csv") || File.Exists(folderpath + "/aix_lul.csv") ||
                    File.Exists(folderpath + "/mdl_einschreibungen.csv") ||
                    File.Exists(folderpath + "/mdl_kurse.csv") || File.Exists(folderpath + "/mdl_nutzer.csv"))
                {
                    var overwriteFilesDialog = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
                    {
                        ButtonDefinitions = ButtonEnum.YesNo,
                        ContentTitle = "Dateien gefunden",
                        ContentHeader = "Überschreiben?",
                        ContentMessage =
                            "Im Ordner existieren schon eine/mehrere Exportdateien.\nSollen diese überschrieben werden?",
                        Icon = MsBox.Avalonia.Enums.Icon.Question,
                        WindowIcon = _msgBoxWindowIcon
                    });
                    var dialogResult = await overwriteFilesDialog.ShowAsPopupAsync(this);
                    expandFiles = dialogResult switch
                    {
                        ButtonResult.Yes => false,
                        ButtonResult.No => true,
                        _ => true
                    };
                }

                var whattoexport = "";
                var destsys = "";
                if (cbSuS.IsChecked != null && cbSuS.IsChecked.Value)
                {
                    whattoexport += "s";
                }

                if (cbLuL.IsChecked != null && cbLuL.IsChecked.Value)
                {
                    whattoexport += "l";
                }

                if (cbKurs.IsChecked != null && cbKurs.IsChecked.Value)
                {
                    whattoexport += "k";
                }

                if (cbEltern.IsChecked != null && cbEltern.IsChecked.Value)
                {
                    whattoexport += "e";
                }

                if (cbMoodle.IsChecked != null && cbMoodle.IsChecked.Value)
                {
                    destsys += "m";
                }

                if (cbAIX.IsChecked != null && cbAIX.IsChecked.Value)
                {
                    destsys += "a";
                }

                var kursvorlagen = new[] { "", "" };
                var nurMoodleSuffix = cbNurMoodleSuffix.IsChecked is not false;
                if (tbExportKl.Text != null && tbExportF.Text != null)
                {
                    if (cbExportVorlagenkurse.IsChecked != null && cbExportVorlagenkurse.IsChecked.Value)
                    {
                        kursvorlagen[0] = tbExportKl.Text;
                        kursvorlagen[1] = tbExportF.Text;
                    }
                }

                var res = await _myschool.ExportCSV(folderpath, destsys, whattoexport,
                    cbExportwithPasswort.IsChecked != null && cbExportwithPasswort.IsChecked.Value, "", expandFiles,
                    nurMoodleSuffix, kursvorlagen, await _myschool.GetSchuelerIDListe(),
                    await _myschool.GetLehrerIDListe(), await _myschool.GetKursBezListe());
                await CheckSuccesfulExport(res);
            }
        }

        private void BtnFehlerSuche_OnClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var kursliste = _myschool.GetKursListe().Result;
                var susliste = _myschool.GetSchuelerListe().Result;
                var ergebnisliste = new List<string>();
                if (cbFehlerLeereKurse.IsChecked != null && cbFehlerLeereKurse.IsChecked.Value)
                {
                    foreach (var k in kursliste)
                    {
                        if (_myschool.GetSuSAusKurs(k.Bezeichnung).Result.Count == 0)
                        {
                            ergebnisliste.Add(k.Bezeichnung + " ohne SuS");
                        }

                        if (_myschool.GetLuLAusKurs(k.Bezeichnung).Result.Count == 0)
                        {
                            ergebnisliste.Add(k.Bezeichnung + " ohne LuL");
                        }
                    }
                }

                if (cbFehlerSuSoK.IsChecked != null && cbFehlerSuSoK.IsChecked.Value)
                {
                    var susOhneKurse = from sus in _myschool.GetSchuelerListe().Result
                        where _myschool.GetKursVonSuS(Convert.ToInt32(sus.ID)).Result.Count == 0
                        select sus.Nachname + ", " + sus.Vorname + ";" + sus.ID + ";ohne Kurs";
                    var ohneKurse = susOhneKurse as string[] ?? susOhneKurse.ToArray();
                    if (ohneKurse.Any())
                    {
                        ergebnisliste.AddRange(ohneKurse);
                    }
                }

                if (cbFehlerLuLoK.IsChecked != null && cbFehlerLuLoK.IsChecked.Value)
                {
                    var lulOhneKurse = from lul in _myschool.GetLehrerListe().Result
                        where _myschool.GetKursVonLuL(Convert.ToInt32(lul.ID)).Result.Count == 0
                        select lul.Nachname + ", " + lul.Vorname + ";" + lul.ID + ";ohne Kurs";
                    var ohneKurse = lulOhneKurse as string[] ?? lulOhneKurse.ToArray();
                    if (ohneKurse.Any())
                    {
                        ergebnisliste.AddRange(ohneKurse);
                    }
                }

                if (cbFehlerLuL.IsChecked != null && cbFehlerLuL.IsChecked.Value)
                {
                    ergebnisliste.AddRange(from lul in _myschool.GetLehrerListe().Result
                        where lul.Fakultas.Contains("NV")
                        select lul.Nachname + ", " + lul.Vorname + ";" + lul.ID + ";mit fehlerhafter Fakultas");
                }

                if (cbFehlerKurse.IsChecked != null && cbFehlerKurse.IsChecked.Value)
                {
                    ergebnisliste.AddRange(from kurs in _myschool.GetKursListe().Result
                        where kurs.Fach.Length == 0 || kurs.Fach.Equals("---")
                        select kurs.Bezeichnung + " mit fehlerhaftem Fach");
                }

                if (cbFehlerSuS.IsChecked != null && cbFehlerSuS.IsChecked.Value)
                {
                    foreach (var sus in susliste)
                    {
                        if (sus.Nutzername.Equals(""))
                        {
                            ergebnisliste.Add(sus.Nachname + ", " + sus.Vorname + ";Klasse " + sus.Klasse + ";" +
                                              sus.ID + ";ohne Nutzernamen");
                        }

                        var mailsuffixes = _myschool.GetSettings().Result.Mailsuffix;
                        if (mailsuffixes.Contains(';') && sus.Aixmail.Contains(mailsuffixes.Split(';')[1]))
                        {
                            ergebnisliste.Add(sus.Nachname + ", " + sus.Vorname + ";Klasse " + sus.Klasse + ";" +
                                              sus.ID + ";ohne gültige Mailadresse");
                        }

                        if (sus is { Zweitaccount: true, Zweitmail: "" })
                        {
                            ergebnisliste.Add(sus.Nachname + ", " + sus.Vorname + ";Klasse " + sus.Klasse + ";" +
                                              sus.ID + ";ohne gültige Zweitmailadresse");
                        }
                    }
                }

                if (ergebnisliste.Count == 0)
                {
                    ergebnisliste.Add("Keine Fehler gefunden!");
                }

                ResetItemsSource(lbFehlerliste, ergebnisliste);
            }
            catch (Exception ex)
            {
#if DEBUG
                _ = _myschool.AddLogMessage("Debug", ex.StackTrace + ";" + ex.Message);
#endif
                _ = _myschool.AddLogMessage("Fehler", "Fehler bei der Fehlersuche " + ex.Message);
            }
        }

        private async void BtnFehlerExport_OnClick(object? sender, RoutedEventArgs e)
        {
            async Task SaveDbFile()
            {
                var extx = new List<FilePickerFileType> { StSFileTypes.CSVFile };
                var files = await ShowSaveFileDialog("Bitte einen Dateipfad angeben...", extx);
                if (files == null) return;
                var filepath = files.Path.LocalPath;

                await File.WriteAllLinesAsync(filepath, lbFehlerliste.Items.Cast<string>(), Encoding.UTF8);
            }

            await Dispatcher.UIThread.InvokeAsync(SaveDbFile);
        }

        private async void BtnExportStufenkurs_OnClick(object? sender, RoutedEventArgs e)
        {
            async Task ReadFileTask()
            {
                if (string.IsNullOrEmpty(tbExportStufenkurse.Text)) return;
                var folder = await ShowOpenFolderDialog("Bitte den Ordner mit den Dateien auswählen");
                if (folder == null) return;
                var folderpath = folder.Path.LocalPath;
                int res;
                var susidlist = new List<int>();
                var nurMoodleSuffix = cbNurMoodleSuffix.IsChecked is not false;
                if (!tbExportStufenkurse.Text.Contains(';'))
                {
                    var stufe = tbExportStufenkurse.Text;
                    susidlist.AddRange(_myschool.GetSusAusStufe(stufe).Result.Select(s => s.ID).ToList());
                    res = await _myschool.ExportCSV(folderpath, "all", "s", false, "", false, nurMoodleSuffix,
                        new[] { "", "" }, new ReadOnlyCollection<int>(susidlist),
                        new ReadOnlyCollection<int>(new List<int>()),
                        new ReadOnlyCollection<string>(new List<string>()));
                }
                else
                {
                    var stufen = tbExportStufenkurse.Text.Split(';');
                    foreach (var stufe in stufen)
                    {
                        susidlist.AddRange(_myschool.GetSusAusStufe(stufe).Result.Select(s => s.ID).ToList());
                    }

                    res = await _myschool.ExportCSV(folderpath, "all", "s", false, "", false, nurMoodleSuffix,
                        new[] { "", "" }, new ReadOnlyCollection<int>(susidlist),
                        new ReadOnlyCollection<int>(new List<int>()),
                        new ReadOnlyCollection<string>(new List<string>()));
                }

                await CheckSuccesfulExport(res);
            }

            await Dispatcher.UIThread.InvokeAsync(ReadFileTask);
        }

        private async Task CheckSuccesfulExport(int res)
        {
            if (res == 1)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MessageBoxManager.GetMessageBoxStandard(
                        new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.Ok,
                            ContentTitle = "Export erfolgreich",
                            ContentMessage =
                                "Der Export war erfolgreich",
                            Icon = MsBox.Avalonia.Enums.Icon.Info,
                            WindowIcon = _msgBoxWindowIcon
                        }).ShowAsPopupAsync(this);
                });
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MessageBoxManager.GetMessageBoxStandard(
                        new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.Ok,
                            ContentTitle = "Export fehlgeschlagen",
                            ContentMessage =
                                "Export war nicht erfolgreiche. Bitte im Log nachschauen",
                            Icon = MsBox.Avalonia.Enums.Icon.Error,
                            WindowIcon = _msgBoxWindowIcon
                        }).ShowAsPopupAsync(this);
                });
            }
        }

        private async void BtnExport5InklPasswort_OnClick(object? sender, RoutedEventArgs e)
        {
            await Dispatcher.UIThread.InvokeAsync(ReadFileTask);
            return;

            async Task ReadFileTask()
            {
                var folder = await ShowOpenFolderDialog("Bitte den Ordner mit den Dateien auswählen");
                if (folder == null) return;
                var folderpath = folder.Path.LocalPath;
                var nurMoodleSuffix = cbNurMoodleSuffix.IsChecked is not false;
                var res = await _myschool.ExportCSV(folderpath, "all", "s", false, "", false, nurMoodleSuffix,
                    new[] { "", "" },
                    new ReadOnlyCollection<int>(_myschool.GetSusAusStufe("5").Result.Select(s => s.ID).ToList()),
                    new ReadOnlyCollection<int>(new List<int>()), new ReadOnlyCollection<string>(new List<string>()));
                await CheckSuccesfulExport(res);
            }
        }

        private async void BtnSettingSave_OnClick(object? sender, RoutedEventArgs e)
        {
            Settings settings = new()
            {
                Mailsuffix = string.IsNullOrEmpty(tbSettingMailplatzhalter.Text)
                    ? "@local.domain"
                    : tbSettingMailplatzhalter.Text,
                Fachersetzung = string.IsNullOrEmpty(tbSettingKursersetzung.Text)
                    ? ""
                    : tbSettingKursersetzung.Text,
                Kurzfaecher = string.IsNullOrEmpty(tbSettingFachkurz.Text)
                    ? new[] { "" }
                    : tbSettingFachkurz.Text.Split('\n'),
                Langfaecher = string.IsNullOrEmpty(tbSettingFachlang.Text)
                    ? new[] { "" }
                    : tbSettingFachlang.Text.Split('\n'),
                Kurssuffix = string.IsNullOrEmpty(tbSettingKurssuffix.Text)
                    ? ""
                    : tbSettingKurssuffix.Text,
                Erprobungstufenleitung = string.IsNullOrEmpty(tbSettingErprobungsstufenleitung.Text)
                    ? ""
                    : tbSettingErprobungsstufenleitung.Text,
                Mittelstufenleitung = string.IsNullOrEmpty(tbSettingMittelstufenleitung.Text)
                    ? ""
                    : tbSettingMittelstufenleitung.Text.TrimEnd(','),
                EFStufenleitung = string.IsNullOrEmpty(tbSettingEFstufenleitung.Text)
                    ? ""
                    : tbSettingEFstufenleitung.Text.TrimEnd(','),
                Q1Stufenleitung = string.IsNullOrEmpty(tbSettingQ1stufenleitung.Text)
                    ? ""
                    : tbSettingQ1stufenleitung.Text.TrimEnd(','),
                Q2Stufenleitung = string.IsNullOrEmpty(tbSettingQ2stufenleitung.Text)
                    ? ""
                    : tbSettingQ2stufenleitung.Text.TrimEnd(','),
                Oberstufenkoordination = string.IsNullOrEmpty(tbSettingOberstufenkoordination.Text)
                    ? ""
                    : tbSettingOberstufenkoordination.Text.TrimEnd(','),
            };

            await _myschool.SetSettings(settings);
            await _myschool.StartTransaction();
            if (!await _myschool.GibtEsKurs("Erprobungsstufe" + settings.Kurssuffix))
            {
                await _myschool.AddKurs("Erprobungsstufe", "", "", "", settings.Kurssuffix, 1);
                foreach (var s in await _myschool.GetSusAusStufe("5"))
                {
                    await _myschool.AddStoK(s.ID, "Erprobungsstufe");
                }

                foreach (var s in await _myschool.GetSusAusStufe("6"))
                {
                    await _myschool.AddStoK(s.ID, "Erprobungsstufe");
                }
            }

            foreach (var l in await _myschool.GetLuLAusKurs("Erprobungsstufe"))
            {
                await _myschool.RemoveLfromK(l.ID, "Erprobungsstufe");
            }

            if (!string.IsNullOrEmpty(tbSettingErprobungsstufenleitung.Text))
            {
                if (tbSettingErprobungsstufenleitung.Text.Contains(','))
                {
                    foreach (var krz in tbSettingErprobungsstufenleitung.Text.Split(','))
                    {
                        await _myschool.AddLtoK(await _myschool.GetLehrkraft(krz),
                            await _myschool.GetKurs("Erprobungsstufe"));
                    }
                }
                else
                {
                    await _myschool.AddLtoK(await _myschool.GetLehrkraft(tbSettingErprobungsstufenleitung.Text),
                        await _myschool.GetKurs("Erprobungsstufe"));
                }
            }

            if (!_myschool.GibtEsKurs("Mittelstufe" + settings.Kurssuffix).Result)
            {
                await _myschool.AddKurs("Mittelstufe", "", "", "", settings.Kurssuffix, 1);
                foreach (var s in await _myschool.GetSusAusStufe("7"))
                {
                    await _myschool.AddStoK(s.ID, "Mittelstufe");
                }

                foreach (var s in await _myschool.GetSusAusStufe("8"))
                {
                    await _myschool.AddStoK(s.ID, "Mittelstufe");
                }

                foreach (var s in await _myschool.GetSusAusStufe("9"))
                {
                    await _myschool.AddStoK(s.ID, "Mittelstufe");
                }

                foreach (var s in await _myschool.GetSusAusStufe("10"))
                {
                    await _myschool.AddStoK(s.ID, "Mittelstufe");
                }
            }

            if (!string.IsNullOrEmpty(tbSettingMittelstufenleitung.Text))
            {
                if (tbSettingMittelstufenleitung.Text.Contains(','))
                {
                    foreach (var krz in tbSettingMittelstufenleitung.Text.Split(','))
                    {
                        await _myschool.AddLtoK(await _myschool.GetLehrkraft(krz),
                            await _myschool.GetKurs("Mittelstufe"));
                    }
                }
                else
                {
                    await _myschool.AddLtoK(await _myschool.GetLehrkraft(tbSettingMittelstufenleitung.Text),
                        await _myschool.GetKurs("Mittelstufe"));
                }
            }

            if (!_myschool.GibtEsKurs("Einführungsphase" + settings.Kurssuffix).Result)
            {
                await _myschool.AddKurs("Einführungsphase", "", "EF", "EF", settings.Kurssuffix, 1);
                foreach (var s in await _myschool.GetSusAusStufe("EF"))
                {
                    await _myschool.AddStoK(s.ID, "Einführungsphase");
                }
            }

            if (!string.IsNullOrEmpty(tbSettingEFstufenleitung.Text))
            {
                if (tbSettingEFstufenleitung.Text.Contains(','))
                {
                    foreach (var krz in tbSettingEFstufenleitung.Text.Split(','))
                    {
                        await _myschool.AddLtoK(await _myschool.GetLehrkraft(krz),
                            await _myschool.GetKurs("Einführungsphase"));
                    }
                }
                else
                {
                    await _myschool.AddLtoK(await _myschool.GetLehrkraft(tbSettingEFstufenleitung.Text),
                        await _myschool.GetKurs("Einführungsphase"));
                }
            }

            if (!_myschool.GibtEsKurs("Qualifikationsphase 1" + settings.Kurssuffix).Result)
            {
                await _myschool.AddKurs("Qualifikationsphase 1", "", "Q1", "Q1", settings.Kurssuffix, 1);
                foreach (var s in await _myschool.GetSusAusStufe("Q1"))
                {
                    await _myschool.AddStoK(s.ID, "Qualifikationsphase 1");
                }
            }

            if (!string.IsNullOrEmpty(tbSettingQ1stufenleitung.Text))
            {
                if (tbSettingQ1stufenleitung.Text.Contains(','))
                {
                    foreach (var krz in tbSettingQ1stufenleitung.Text.Split(','))
                    {
                        await _myschool.AddLtoK(await _myschool.GetLehrkraft(krz),
                            await _myschool.GetKurs("Qualifikationsphase 1"));
                    }
                }
                else
                {
                    await _myschool.AddLtoK(await _myschool.GetLehrkraft(tbSettingQ1stufenleitung.Text),
                        await _myschool.GetKurs("Qualifikationsphase 1"));
                }
            }

            if (!_myschool.GibtEsKurs("Qualifikationsphase 2" + settings.Kurssuffix).Result)
            {
                await _myschool.AddKurs("Qualifikationsphase 2", "", "Q2", "Q2", settings.Kurssuffix, 1);
                foreach (var s in await _myschool.GetSusAusStufe("Q2"))
                {
                    await _myschool.AddStoK(s.ID, "Qualifikationsphase 2");
                }
            }

            if (!string.IsNullOrEmpty(tbSettingQ2stufenleitung.Text))
            {
                if (tbSettingQ2stufenleitung.Text.Contains(','))
                {
                    foreach (var krz in tbSettingQ2stufenleitung.Text.Split(','))
                    {
                        await _myschool.AddLtoK(await _myschool.GetLehrkraft(krz),
                            await _myschool.GetKurs("Qualifikationsphase 2"));
                    }
                }
                else
                {
                    await _myschool.AddLtoK(await _myschool.GetLehrkraft(tbSettingQ2stufenleitung.Text),
                        await _myschool.GetKurs("Qualifikationsphase 2"));
                }
            }

            if (!string.IsNullOrEmpty(tbSettingOberstufenkoordination.Text))
            {
                if (tbSettingOberstufenkoordination.Text.Contains(';'))
                {
                    foreach (var krz in tbSettingOberstufenkoordination.Text.Split(','))
                    {
                        await _myschool.AddLtoK(await _myschool.GetLehrkraft(krz),
                            await _myschool.GetKurs("Einführungsphase"));
                        await _myschool.AddLtoK(await _myschool.GetLehrkraft(krz),
                            await _myschool.GetKurs("Qualifikationsphase 1"));
                        await _myschool.AddLtoK(await _myschool.GetLehrkraft(krz),
                            await _myschool.GetKurs("Qualifikationsphase 2"));
                    }
                }
                else
                {
                    await _myschool.AddLtoK(await _myschool.GetLehrkraft(tbSettingOberstufenkoordination.Text),
                        await _myschool.GetKurs("Einführungsphase"));
                    await _myschool.AddLtoK(await _myschool.GetLehrkraft(tbSettingOberstufenkoordination.Text),
                        await _myschool.GetKurs("Qualifikationsphase 1"));
                    await _myschool.AddLtoK(await _myschool.GetLehrkraft(tbSettingOberstufenkoordination.Text),
                        await _myschool.GetKurs("Qualifikationsphase 2"));
                }
            }

            foreach (var kurs in _myschool.GetKursListe().Result)
            {
                await _myschool.UpdateKurs(kurs.Bezeichnung, kurs.Fach, kurs.Klasse, kurs.Stufe, settings.Kurssuffix,
                    kurs.Istkurs ? 1 : 0);
            }

            await _myschool.StopTransaction();
        }

        private async void BtnLogDelete_OnClick(object? sender, RoutedEventArgs e)
        {
            var reallyDeleteLog = MessageBoxManager.GetMessageBoxStandard(
                new MessageBoxStandardParams
                {
                    ButtonDefinitions = ButtonEnum.YesNo,
                    ContentTitle = "Log löschen",
                    ContentHeader = "Sicherheitsabfrage",
                    ContentMessage =
                        "Möchten Sie das Log wirklich löschen?",
                    Icon = MsBox.Avalonia.Enums.Icon.Question,
                    WindowIcon = _msgBoxWindowIcon
                });
            var dialogResult = await reallyDeleteLog.ShowAsPopupAsync(this);
            if (dialogResult == ButtonResult.No) return;
            ResetItemsSource(lbLogDisplay, new List<string>());
            await _myschool.LoescheLog();
        }

        private async void BtnLogReload_OnClick(object? sender, RoutedEventArgs e)
        {
            var items = await _myschool.GetLog();
            ResetItemsSource(lbLogDisplay, items.Select(message => message.Replace('\t', ' ').TrimEnd('\t')));
        }

        private async void BtnKurseAdd_OnClick(object? sender, RoutedEventArgs e)
        {
            var kursbez = tbKursbezeichnung.Text;
            var lehrkraefte = tbKursLuL.Text;
            var kursfach = tbKursFach.Text;
            var kurssuffix = string.IsNullOrEmpty(tbKursSuffix.Text)
                ? _myschool.GetSettings().Result.Kurssuffix
                : tbKursSuffix.Text;
            var kursklasse = tbKursKlasse.Text;
            var kursstufe = tbKursStufe.Text;
            var istKurs = cbKursIstKurs.IsChecked != null && cbKursIstKurs.IsChecked.Value;
            if (kursbez == null || lehrkraefte == null || kursfach == null || kursklasse == null || kursstufe == null ||
                kursbez == "" || lehrkraefte == "" || kursfach == "" || kursklasse == "" || kursstufe == "")
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MessageBoxManager.GetMessageBoxStandard(
                        new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.Ok,
                            ContentTitle = "Fehler",
                            ContentMessage =
                                "Nicht alle erforderlichen Informationen angegeben!\nStellen Sie sicher, dass Kursbezeichnung, mind. ein Kürzel, das Fach, die Klasse und die Stufe ausgefüllt sind.",
                            Icon = MsBox.Avalonia.Enums.Icon.Error,
                            WindowIcon = _msgBoxWindowIcon
                        }).ShowAsPopupAsync(this);
                });
                return;
            }

            if (await _myschool.GibtEsKurs(kursbez))
            {
                await _myschool.UpdateKurs(kursbez, kursfach, kursklasse, kursstufe, kurssuffix,
                    Convert.ToInt32(istKurs));
                List<LuL> tList = new();
                foreach (var lehrkraft in lehrkraefte.Split((';')))
                {
                    tList.Add(await _myschool.GetLehrkraft(lehrkraft));
                }

                var tListAusKurs = await _myschool.GetLuLAusKurs(kursbez);
                foreach (var lehrkraft in tListAusKurs.Where(lehrkraft => !tList.Contains(lehrkraft)))
                {
                    await _myschool.RemoveLfromK(lehrkraft, await _myschool.GetKurs(kursbez));
                }

                foreach (var lehrkraft in tList)
                {
                    await _myschool.AddLtoK(lehrkraft, await _myschool.GetKurs(kursbez));
                }
            }
            else
            {
                await _myschool.AddKurs(kursbez, kursfach, kursklasse, kursstufe, kurssuffix, Convert.ToInt32(istKurs));
            }

            foreach (var lehrkraft in lehrkraefte.Split((';')))
            {
                await _myschool.AddLtoK(await _myschool.GetLehrkraft(lehrkraft), await _myschool.GetKurs(kursbez));
            }

            if (cbKursSuSdKlasseEinschreiben.IsChecked != null && cbKursSuSdKlasseEinschreiben.IsChecked.Value)
            {
                foreach (var sus in await _myschool.GetSuSAusKlasse(kursklasse))
                {
                    await _myschool.AddStoK(sus, await _myschool.GetKurs(kursbez));
                }
            }

            if (cbKursSuSdStufeEinschreiben.IsChecked != null && cbKursSuSdStufeEinschreiben.IsChecked.Value)
            {
                foreach (var stufe in kursstufe.Split(';'))
                {
                    foreach (var sus in await _myschool.GetSusAusStufe(stufe))
                    {
                        await _myschool.AddStoK(sus, await _myschool.GetKurs(kursbez));
                    }
                }
            }

            if (cbKursMarkierteSuSEinschreiben.IsChecked != null &&
                cbKursMarkierteSuSEinschreiben.IsChecked.Value && LeftListBox.SelectedItems != null)
            {
                foreach (var susstring in LeftListBox.SelectedItems.Cast<string>())
                {
                    if (susstring == null) continue;
                    var id = Convert.ToInt32(susstring.Split(';')[1]);
                    var sus = await _myschool.GetSchueler(id);
                    await _myschool.AddStoK(sus, await _myschool.GetKurs(kursbez));
                }
            }

            OnLeftDataChanged(true);
            OnRightDataChanged(true);
        }

        private async void BtnKurseDel_OnClick(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbKursbezeichnung.Text)) return;
            var kursbez = tbKursbezeichnung.Text;
            ListBox source;
            if (CboxDataLeft.SelectedIndex == 2)
            {
                source = LeftListBox;
            }
            else if (CboxDataRight.SelectedIndex == 2)
            {
                source = RightListBox;
            }
            else
            {
                await _myschool.RemoveK(kursbez);
                return;
            }

            if (source.SelectedItems == null) return;
            await _myschool.StartTransaction();
            foreach (var kurs in source.SelectedItems.Cast<string>())
            {
                if (kurs == null) return;
                await _myschool.RemoveK(kurs);
            }

            await _myschool.StopTransaction();
            OnLeftDataChanged(true);
            OnRightDataChanged(true);
        }

        private async void OnLeftTimedEvent(object? source, ElapsedEventArgs e)
        {
            await Dispatcher.UIThread.InvokeAsync(UpdateLeftList);
            _leftInputTimer.Enabled = false;
            return;

            void UpdateLeftList()
            {
                switch (tbLeftSearch.Text)
                {
                    case null:
                        return;
                    case "":
                        OnLeftDataChanged(true);
                        break;
                }

                var eingabeliste = tbLeftSearch.Text.Split(";");
                if (tbLeftSearch.ContextMenu == null || tbLeftSearch.ContextMenu.ItemsSource == null) return;
                var searchContextMenu = tbLeftSearch.ContextMenu.ItemsSource.Cast<CheckBox>().ToList();
                var searchFields = new[] { false, false, false, false, false, false }; //v,n,m,a/k,i,e
                for (var i = 0; i < searchContextMenu.Count; ++i)
                {
                    if (searchContextMenu[i].IsChecked == true)
                    {
                        searchFields[i] = true;
                    }
                }

                switch (CboxDataLeft.SelectedIndex)
                {
                    case 0:
                        var sliste = new List<SuS>();
                        var scachelist = _myschool.GetSchuelerListe().Result;
                        foreach (var eingabe in eingabeliste)
                        {
                            var lowereingabe = eingabe.ToLower();
                            sliste.AddRange(searchFields[5]
                                ? scachelist.Where(s =>
                                    searchFields[4] && (s.ID + "").Equals(lowereingabe) ||
                                    searchFields[0] && s.Vorname.ToLower().Equals(lowereingabe) ||
                                    searchFields[1] && s.Nachname.ToLower().Equals(lowereingabe) ||
                                    searchFields[2] &&
                                    (s.Mail.Equals(lowereingabe) || s.Aixmail.Equals(lowereingabe) ||
                                     s.Zweitmail.Equals(lowereingabe)) ||
                                    searchFields[3] && s.Nutzername.Equals(lowereingabe)).ToList()
                                : scachelist.Where(s =>
                                    searchFields[4] && (s.ID + "").Contains(lowereingabe) ||
                                    searchFields[0] && s.Vorname.ToLower().Contains(lowereingabe) ||
                                    searchFields[1] && s.Nachname.ToLower().Contains(lowereingabe) ||
                                    searchFields[2] && (s.Mail.Contains(lowereingabe) ||
                                                        s.Aixmail.Contains(lowereingabe) ||
                                                        s.Zweitmail.Contains(lowereingabe)) ||
                                    searchFields[3] && s.Nutzername.Contains(lowereingabe)).ToList());
                        }

                        var seliste = sliste.Distinct()
                            .Select(s => (s.Nachname + "," + s.Vorname + ";" + s.ID))
                            .ToList();
                        seliste.Sort(Comparer<string>.Default);
                        ResetItemsSource(LeftListBox, seliste);
                        break;
                    case 1:
                        var lliste = new List<LuL>();
                        var cachlist = _myschool.GetLehrerListe().Result;
                        foreach (var eingabe in eingabeliste)
                        {
                            var lowereingabe = eingabe.ToLower();
                            lliste.AddRange(searchFields[5]
                                ? cachlist.Where(l =>
                                    l.Kuerzel.ToLower().Equals(lowereingabe) ||
                                    searchFields[0] && l.Vorname.ToLower().Equals(lowereingabe) ||
                                    searchFields[1] && l.Nachname.ToLower().Equals(lowereingabe) ||
                                    searchFields[2] && l.Mail.Equals(lowereingabe) ||
                                    searchFields[3] && l.Kuerzel.Equals(lowereingabe) ||
                                    searchFields[4] && (l.ID + "").Equals(lowereingabe)).ToList()
                                : cachlist.Where(l =>
                                    l.Kuerzel.ToLower().Contains(lowereingabe) ||
                                    searchFields[0] && l.Vorname.ToLower().Contains(lowereingabe) ||
                                    searchFields[1] && l.Nachname.ToLower().Contains(lowereingabe) ||
                                    searchFields[2] && l.Mail.Contains(lowereingabe) ||
                                    searchFields[3] && l.Kuerzel.Contains(lowereingabe) ||
                                    searchFields[4] && (l.ID + "").Contains(lowereingabe)).ToList());
                        }

                        var leliste = lliste.Distinct()
                            .Select(l => (l.Kuerzel + ";" + l.Nachname + "," + l.Vorname))
                            .ToList();
                        leliste.Sort(Comparer<string>.Default);
                        ResetItemsSource(LeftListBox, leliste);
                        break;
                    case 2:
                        var kliste = new List<Kurs>();
                        var kcachelist = _myschool.GetKursListe().Result;
                        foreach (var eingabe in eingabeliste)
                        {
                            kliste.AddRange(kcachelist
                                .Where(s => s.Bezeichnung.ToLower().Contains(eingabe.ToLower()))
                                .ToList());
                        }

                        var keliste = kliste.Distinct()
                            .Select(k => k.Bezeichnung)
                            .ToList();
                        keliste.Sort(Comparer<string>.Default);
                        ResetItemsSource(LeftListBox, keliste);
                        break;
                }
            }
        }

        private async void OnRightTimedEvent(object? source, ElapsedEventArgs e)
        {
            await Dispatcher.UIThread.InvokeAsync(UpdateRightList);
            _rightInputTimer.Enabled = false;
            return;

            void UpdateRightList()
            {
                switch (tbRightSearch.Text)
                {
                    case null:
                        return;
                    case "":
                        OnRightDataChanged(true);
                        break;
                }

                var eingabeliste = tbRightSearch.Text.Split(";");
                if (tbLeftSearch?.ContextMenu?.ItemsSource == null) return;
                var searchContextMenu = tbLeftSearch.ContextMenu.ItemsSource.Cast<CheckBox>().ToList();
                var searchFields = new[] { false, false, false, false, false, false }; //v,n,m,a/k,i,e
                for (var i = 0; i < searchContextMenu.Count; ++i)
                {
                    if (searchContextMenu[i].IsChecked == true)
                    {
                        searchFields[i] = true;
                    }
                }

                switch (CboxDataRight.SelectedIndex)
                {
                    case 0:
                        var sliste = new List<SuS>();
                        var scachelist = _myschool.GetSchuelerListe().Result;
                        foreach (var eingabe in eingabeliste)
                        {
                            var lowereingabe = eingabe.ToLower();
                            sliste.AddRange(searchFields[5]
                                ? scachelist.Where(s =>
                                    searchFields[4] && (s.ID + "").Equals(lowereingabe) ||
                                    searchFields[0] && s.Vorname.ToLower().Equals(lowereingabe) ||
                                    searchFields[1] && s.Nachname.ToLower().Equals(lowereingabe) ||
                                    searchFields[2] && (s.Mail.Equals(lowereingabe) || s.Aixmail.Equals(lowereingabe) ||
                                                        s.Zweitmail.Equals(lowereingabe)) ||
                                    searchFields[3] && s.Nutzername.Equals(lowereingabe)).ToList()
                                : scachelist.Where(s =>
                                    searchFields[4] && (s.ID + "").Contains(lowereingabe) ||
                                    searchFields[0] && s.Vorname.ToLower().Contains(lowereingabe) ||
                                    searchFields[1] && s.Nachname.ToLower().Contains(lowereingabe) ||
                                    searchFields[2] && (s.Mail.Contains(lowereingabe) ||
                                                        s.Aixmail.Contains(lowereingabe) ||
                                                        s.Zweitmail.Contains(lowereingabe)) ||
                                    searchFields[3] && s.Nutzername.Contains(lowereingabe)).ToList());
                        }

                        var seliste = sliste.Distinct()
                            .Select(s => (s.Nachname + "," + s.Vorname + ";" + s.ID))
                            .ToList();
                        seliste.Sort(Comparer<string>.Default);
                        ResetItemsSource(RightListBox, seliste);
                        break;
                    case 1:
                        var lliste = new List<LuL>();
                        var cachlist = _myschool.GetLehrerListe().Result;
                        foreach (var eingabe in eingabeliste)
                        {
                            var lowereingabe = eingabe.ToLower();
                            lliste.AddRange(searchFields[5]
                                ? cachlist.Where(l =>
                                    l.Kuerzel.ToLower().Equals(lowereingabe) ||
                                    searchFields[0] && l.Vorname.ToLower().Equals(lowereingabe) ||
                                    searchFields[1] && l.Nachname.ToLower().Equals(lowereingabe) ||
                                    searchFields[2] && l.Mail.Equals(lowereingabe) ||
                                    searchFields[3] && l.Kuerzel.Equals(lowereingabe) ||
                                    searchFields[4] && (l.ID + "").Equals(lowereingabe)).ToList()
                                : cachlist.Where(l =>
                                    l.Kuerzel.ToLower().Contains(lowereingabe) ||
                                    searchFields[0] && l.Vorname.ToLower().Contains(lowereingabe) ||
                                    searchFields[1] && l.Nachname.ToLower().Contains(lowereingabe) ||
                                    searchFields[2] && l.Mail.Contains(lowereingabe) ||
                                    searchFields[3] && l.Kuerzel.Contains(lowereingabe) ||
                                    searchFields[4] && (l.ID + "").Contains(lowereingabe)).ToList());
                        }

                        var leliste = lliste.Distinct()
                            .Select(l => (l.Kuerzel + ";" + l.Nachname + "," + l.Vorname))
                            .ToList();
                        leliste.Sort(Comparer<string>.Default);
                        ResetItemsSource(RightListBox, leliste);
                        break;
                    case 2:
                        var kliste = new List<Kurs>();
                        var kcachelist = _myschool.GetKursListe().Result;
                        foreach (var eingabe in eingabeliste)
                        {
                            kliste.AddRange(kcachelist.Where(s => s.Bezeichnung.Contains(eingabe)).ToList());
                        }

                        var keliste = kliste.Distinct()
                            .Select(k => k.Bezeichnung)
                            .ToList();
                        keliste.Sort(Comparer<string>.Default);
                        ResetItemsSource(RightListBox, keliste);
                        break;
                }
            }
        }

        private void TbLeftSearch_OnKeyUp(object? sender, KeyEventArgs e)
        {
            _leftInputTimer.Enabled = true;
            _leftInputTimer.Start();
        }

        private void TbRightSearch_OnKeyUp(object? sender, KeyEventArgs e)
        {
            _rightInputTimer.Enabled = true;
            _rightInputTimer.Start();
        }

        private async void OnMnuSerienbriefClick(object? sender, RoutedEventArgs e)
        {
            await Dispatcher.UIThread.InvokeAsync(ReadFileTask);
            return;

            async Task ReadFileTask()
            {
                if (LeftListBox.SelectedItems == null) return;
                var extx = new List<FilePickerFileType> { StSFileTypes.CSVFile };
                var files = await ShowSaveFileDialog("Bitte einen Dateipfad angeben...", extx);
                if (files == null) return;
                var folder = files.Path.LocalPath;
                List<string> susausgabe = new() { "Vorname;Nachname;Anmeldename;Kennwort;E-Mail;Klasse" };
                switch (CboxDataLeft.SelectedIndex)
                {
                    case 0:
                        susausgabe.AddRange(LeftListBox.SelectedItems.Cast<string>()
                            .ToList()
                            .Select(sus => _myschool.GetSchueler(Convert.ToInt32(sus.Split(';')[1])).Result)
                            .Select(s =>
                                s.Vorname + ";" + s.Nachname + ";" + s.Nutzername + ";" + "Klasse" + s.Klasse +
                                DateTime.Now.Year + "!;" + s.Aixmail + ";" + s.Klasse));
                        await File.WriteAllLinesAsync(folder, susausgabe.Distinct().ToList(), Encoding.UTF8);
                        break;
                    case 2:
                        foreach (string kursbez in LeftListBox.SelectedItems)
                        {
                            susausgabe.AddRange(_myschool.GetSuSAusKurs(kursbez).Result.Distinct().Select(s =>
                                s.Vorname + ";" + s.Nachname + ";" + s.Nutzername + ";" + "Klasse" + s.Klasse +
                                DateTime.Now.Year + "!;" + s.Aixmail + ";" + s.Klasse));
                        }

                        await File.WriteAllLinesAsync(folder, susausgabe.Distinct().ToList(), Encoding.UTF8);
                        break;
                    default:
                        return;
                }
            }
        }

        private async void OnMnuItemMSerienbriefDV(object? sender, RoutedEventArgs e)
        {
            await Dispatcher.UIThread.InvokeAsync(ReadFileTask);
            return;

            async Task ReadFileTask()
            {
                if (LeftListBox.SelectedItems == null) return;
                switch (CboxDataLeft.SelectedIndex)
                {
                    case 2:
                        var extx = new List<FilePickerFileType> { StSFileTypes.CSVFile, FilePickerFileTypes.All };
                        var files = await ShowSaveFileDialog("Serienbriefdatei auswählen", extx);
                        if (files == null) return;
                        var folder = files.Path.LocalPath;
                        var file = await ShowOpenFileDialog("Nutzer ohne DV-Zustimmung", extx);
                        if (file is null) return;
                        var filepath = file.Path.LocalPath;
                        var fileentries = File.ReadAllLinesAsync(filepath).Result.ToList();
                        if (fileentries.Count < 1) return;
                        fileentries.RemoveAt(0);
                        var susToDel = fileentries
                            .Select(line => _myschool.GetSchueler(Convert.ToInt32(line.Split(';')[0])))
                            .ToList();
                        List<string> susausgabe = new() { "Vorname;Nachname;Anmeldename;Kennwort;E-Mail;Klasse" };
                        foreach (string kursbez in LeftListBox.SelectedItems)
                        {
                            var suslist = _myschool.GetSuSAusKurs(kursbez).Result.Distinct().ToList();
                            if (suslist.Count < 1) continue;
                            foreach (var s in susToDel)
                            {
                                suslist.Remove(s.Result);
                            }

                            susausgabe.AddRange(suslist.Select(s =>
                                s.Vorname + ";" + s.Nachname + ";" + s.Nutzername + ";" + "Klasse" + s.Klasse +
                                DateTime.Now.Year + "!;" + s.Aixmail + ";" + s.Klasse));
                        }

                        await File.WriteAllLinesAsync(folder, susausgabe.Distinct().ToList(), Encoding.UTF8);
                        break;
                    default:
                        return;
                }
            }
        }

        private async void OnMnuPasswordGenClick(object? sender, RoutedEventArgs e)
        {
            if (LeftListBox.SelectedItems == null) return;
            if (CboxDataLeft.SelectedIndex != 1) return;
            await _myschool.StartTransaction();
            foreach (string luleintrag in LeftListBox.SelectedItems)
            {
                var lul = await _myschool.GetLehrkraft(luleintrag.Split(';')[0]);
                _myschool.SetTPwd(lul.ID, Schuldatenbank.GeneratePasswort(8));
            }

            await _myschool.StopTransaction();
        }

        private async void OnMnuExportClick(object? sender, RoutedEventArgs e)
        {
            await Dispatcher.UIThread.InvokeAsync(ReadFileTask);
            return;

            async Task ReadFileTask()
            {
                if (LeftListBox.SelectedItems == null) return;

                var folder = await ShowOpenFolderDialog("Bitte den Ordner zum Speichern angeben");
                if (folder == null) return;
                var folderpath = folder.Path.LocalPath;
                var expandFiles = false;
                if (File.Exists(folderpath + "/aix_sus.csv") || File.Exists(folderpath + "/aix_lul.csv") ||
                    File.Exists(folderpath + "/mdl_einschreibungen.csv") ||
                    File.Exists(folderpath + "/mdl_kurse.csv") || File.Exists(folderpath + "/mdl_nutzer.csv"))
                {
                    var overwriteFilesDialog = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
                    {
                        ButtonDefinitions = ButtonEnum.YesNo,
                        ContentTitle = "Dateien gefunden",
                        ContentHeader = "Überschreiben?",
                        ContentMessage =
                            "Im Ordner existieren schon eine/mehrere Exportdateien.\nSollen diese überschrieben werden?",
                        Icon = MsBox.Avalonia.Enums.Icon.Question,
                        WindowIcon = _msgBoxWindowIcon
                    });
                    var dialogResult = await overwriteFilesDialog.ShowAsPopupAsync(this);
                    expandFiles = dialogResult switch
                    {
                        ButtonResult.Yes => false,
                        ButtonResult.No => true,
                        _ => true
                    };
                }

                List<SuS> suslist = new();
                List<LuL> lullist = new();
                List<Kurs> kurslist = new();
                var whattoexport = "";
                const string destsys = "ami";
                switch (CboxDataLeft.SelectedIndex)
                {
                    case 0:
                        whattoexport += "s";
                        foreach (string suseintrag in LeftListBox.SelectedItems)
                        {
                            suslist.Add(await _myschool.GetSchueler(Convert.ToInt32(suseintrag.Split(';')[1])));
                        }

                        break;
                    case 1:
                        whattoexport += "l";
                        if (LeftListBox.ContextMenu != null)
                        {
                            var isllginternChecked = ((CheckBox)LeftListBox.ContextMenu.Items.Cast<Control>()
                                .Where(c => c.Name == "cbMnuLeftContextLLGIntern")
                                .ToList()
                                .First()).IsChecked;
                            if (isllginternChecked != null && isllginternChecked.Value) whattoexport += "i";
                        }

                        foreach (string luleintrag in LeftListBox.SelectedItems)
                        {
                            lullist.Add(await _myschool.GetLehrkraft(luleintrag.Split(';')[0]));
                        }

                        break;
                    case 2:
                        whattoexport += "ksl";
                        foreach (string kurseintrag in LeftListBox.SelectedItems)
                        {
                            var kurs = await _myschool.GetKurs(kurseintrag);
                            kurslist.Add(kurs);
                            suslist.AddRange(_myschool.GetSuSAusKurs(kurs.Bezeichnung).Result);
                            lullist.AddRange(_myschool.GetLuLAusKurs(kurs.Bezeichnung).Result);
                        }

                        break;
                    default:
                        return;
                }

                if (LeftListBox.ContextMenu != null)
                {
                    var isElternChecked = ((CheckBox)LeftListBox.ContextMenu.Items.Cast<Control>()
                        .Where(c => c.Name == "cbMnuLeftContextEltern")
                        .ToList()
                        .First()).IsChecked;
                    if (isElternChecked != null && isElternChecked.Value) whattoexport += "e";
                }

                var kursvorlagen = new[] { "", "" };
                if (!(string.IsNullOrEmpty(tbExportKl.Text) || string.IsNullOrEmpty(tbExportF.Text)))
                {
                    if (cbExportVorlagenkurse.IsChecked != null && cbExportVorlagenkurse.IsChecked.Value)
                    {
                        kursvorlagen[0] = tbExportKl.Text;
                        kursvorlagen[1] = tbExportF.Text;
                    }
                }

                if (LeftListBox.ContextMenu != null)
                {
                    var isAnfangsPasswortChecked = ((CheckBox)LeftListBox.ContextMenu.Items.Cast<Control>()
                        .Where(c => c.Name == "cbMnuLeftContextAnfangsPasswort")
                        .ToList()
                        .First()).IsChecked;
                    var nurMoodleSuffix = cbNurMoodleSuffix.IsChecked is not false;
                    var res = await _myschool.ExportCSV(folderpath, destsys, whattoexport,
                        isAnfangsPasswortChecked != null && isAnfangsPasswortChecked.Value, "", expandFiles,
                        nurMoodleSuffix, kursvorlagen,
                        new ReadOnlyCollection<int>(suslist.Select(s => s.ID).Distinct().ToList()),
                        new ReadOnlyCollection<int>(lullist.Select(l => l.ID).Distinct().ToList()),
                        new ReadOnlyCollection<string>(kurslist.Select(k => k.Bezeichnung).Distinct().ToList()));
                    await CheckSuccesfulExport(res);
                }
            }
        }

        private async void MnuLoadElternMails_OnClick(object? sender, RoutedEventArgs e)
        {
            var extx = new List<FilePickerFileType>
            {
                StSFileTypes.CSVFile,
                FilePickerFileTypes.All
            };
            var file = await ShowOpenFileDialog("Lade Elternmailadressen", extx);
            if (file is null) return;
            var filepath = file.Path.LocalPath;
            await _myschool.ElternEinlesen(filepath);
        }

        private async void mnuExportLKtoHP1Spalte_OnClick(object? sender, RoutedEventArgs e)
        {
            await Dispatcher.UIThread.InvokeAsync(SaveLKtoHp);
            return;

            async Task SaveLKtoHp()
            {
                var extx = new List<FilePickerFileType> { StSFileTypes.CSVFile };
                var files = await ShowSaveFileDialog("Bitte einen Dateipfad angeben...", extx);
                if (files == null) return;
                var filepath = files.Path.LocalPath;
                List<string> lulliste = new() { "Kürzel;Nachname;Fächer;Mailadresse" };
                lulliste.AddRange(_myschool.GetLehrerListe().Result.Select(lehrer =>
                    lehrer.Kuerzel + ";" + lehrer.Nachname + ";" + lehrer.Fakultas + @";\underline{\href{mailto:" +
                    lehrer.Mail.ToLower() + "}{" + lehrer.Mail.ToLower() + "}}").OrderBy(s => s.Split(';')[0]));
                await File.WriteAllLinesAsync(filepath, lulliste, Encoding.UTF8);
            }
        }

        private async void mnuExportLKtoHP2Spalte_OnClick(object? sender, RoutedEventArgs e)
        {
            await Dispatcher.UIThread.InvokeAsync(SaveLKtoHp);
            return;

            async Task SaveLKtoHp()
            {
                var extx = new List<FilePickerFileType> { StSFileTypes.CSVFile };
                var files = await ShowSaveFileDialog("Bitte einen Dateipfad angeben...", extx);
                if (files == null) return;
                var filepath = files.Path.LocalPath;
                List<string> header = new() { "Kürzel;Nachname;Fächer;Mailadresse;Kürzel;Nachname;Fächer;Mailadresse" };
                List<string> lulliste = new();
                var llist = _myschool.GetLehrerListe().Result.OrderBy(lk => lk.Kuerzel).ToList();
                var half = llist.Count / 2;
                for (var i = 0; i < llist.Count / 2 + 1; ++i)
                {
                    var lehrer = llist[i];
                    var res = "";
                    res += lehrer.Kuerzel + ";" + lehrer.Nachname + ";" + lehrer.Fakultas.TrimEnd(',') +
                           @";\underline{\href{mailto:" +
                           lehrer.Mail.ToLower() + "}{" + lehrer.Mail.ToLower() + "}}";
                    lulliste.Add(res);
                    var index = i + half + 1;
                    if (index >= llist.Count) continue;
                    lehrer = llist[index];
                    lulliste[i] += ";" + lehrer.Kuerzel + ";" + lehrer.Nachname + ";" + lehrer.Fakultas.TrimEnd(',') +
                                   @";\underline{\href{mailto:" +
                                   lehrer.Mail.ToLower() + "}{" + lehrer.Mail.ToLower() + "}}";
                }

                if (llist.Count % 2 == 1)
                {
                    lulliste[^1] += ";;;;";
                }

                header.AddRange(lulliste);
                await File.WriteAllLinesAsync(filepath, header, Encoding.UTF8);
            }
        }

        private void TbLuLtmpPwd_OnPointerEnter(object? sender, PointerEventArgs e)
        {
            tbLuLtmpPwd.RevealPassword = true;
        }

        private void TbLuLtmpPwd_OnPointerLeave(object? sender, PointerEventArgs e)
        {
            tbLuLtmpPwd.RevealPassword = false;
        }

        private void Rb_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender == null) return;
            if (sender.Equals(rbD))
            {
                Background = _darkBackgroundColor;
                LeftListBox.Background = _darkBackgroundColor;
                RightListBox.Background = _darkBackgroundColor;
                lbFehlerliste.Background = _darkBackgroundColor;
            }
            else if (sender.Equals(rbL))
            {
                Background = _lightBackgroundColor;
                LeftListBox.Background = _lightBackgroundColor;
                RightListBox.Background = _lightBackgroundColor;
                lbFehlerliste.Background = _lightBackgroundColor;
            }
        }

        private async void BtnLuLNewTmpPasswort_OnClick(object? sender, RoutedEventArgs e)
        {
            if (tbLuLKuerzel == null || tbLuLID == null || string.IsNullOrEmpty(tbLuLKuerzel.Text) ||
                string.IsNullOrEmpty(tbLuLID.Text)) return;
            var lul = await _myschool.GetLehrkraft(tbLuLKuerzel.Text);
            var pwd = Schuldatenbank.GeneratePasswort(8);
            _myschool.SetTPwd(lul.ID, pwd);
            tbLuLtmpPwd.Text = pwd;
        }

        private static void ResetItemsSource(ItemsControl sourceList, IEnumerable<string> dataList)
        {
            sourceList.ItemsSource = null;
            sourceList.Items.Clear();
            sourceList.ItemsSource = dataList;
        }

        private async void BtnSonstOberstufenLuLKurse_OnClick(object? sender, RoutedEventArgs e)
        {
            await _myschool.StartTransaction();
            var kursBez = "Jahrgangsstufenkonferenz EF";
            var settingsCache = await _myschool.GetSettings();
            if (!string.IsNullOrEmpty(settingsCache.EFStufenleitung))
            {
                if (!_myschool.GibtEsKurs(kursBez).Result)
                {
                    await _myschool.AddKurs(kursBez, "-", "EF", "EF", settingsCache.Kurssuffix, 1);
                }

                foreach (var krz in settingsCache.EFStufenleitung.Split(','))
                {
                    await _myschool.AddLtoK(_myschool.GetLehrkraft(krz).Result.ID, kursBez);
                }

                foreach (var krz in settingsCache.Oberstufenkoordination.Split(','))
                {
                    await _myschool.AddLtoK(_myschool.GetLehrkraft(krz).Result.ID, kursBez);
                }

                foreach (var lid in _myschool.GetLuLAusStufe("EF").Result)
                {
                    await _myschool.AddLtoK(lid.ID, kursBez);
                }
            }

            kursBez = "Jahrgangsstufenkonferenz Q1";
            if (!string.IsNullOrEmpty(settingsCache.Q1Stufenleitung))
            {
                if (!_myschool.GibtEsKurs(kursBez).Result)
                {
                    await _myschool.AddKurs(kursBez, "-", "Q1", "Q1", settingsCache.Kurssuffix, 1);
                }

                foreach (var krz in settingsCache.Q1Stufenleitung.Split(','))
                {
                    await _myschool.AddLtoK(_myschool.GetLehrkraft(krz).Result.ID, kursBez);
                }

                foreach (var krz in settingsCache.Oberstufenkoordination.Split(','))
                {
                    await _myschool.AddLtoK(_myschool.GetLehrkraft(krz).Result.ID, kursBez);
                }

                foreach (var lid in _myschool.GetLuLAusStufe("Q1").Result)
                {
                    await _myschool.AddLtoK(lid.ID, kursBez);
                }
            }

            kursBez = "Jahrgangsstufenkonferenz Q2";
            if (string.IsNullOrEmpty(settingsCache.Q2Stufenleitung)) return;
            if (!_myschool.GibtEsKurs(kursBez).Result)
            {
                await _myschool.AddKurs(kursBez, "-", "Q2", "Q2", settingsCache.Kurssuffix, 1);
            }

            foreach (var krz in settingsCache.Q2Stufenleitung.Split(','))
            {
                await _myschool.AddLtoK(_myschool.GetLehrkraft(krz).Result.ID, kursBez);
            }

            foreach (var krz in settingsCache.Oberstufenkoordination.Split(','))
            {
                await _myschool.AddLtoK(_myschool.GetLehrkraft(krz).Result.ID, kursBez);
            }

            foreach (var lid in _myschool.GetLuLAusStufe("Q2").Result)
            {
                await _myschool.AddLtoK(lid.ID, kursBez);
            }

            await _myschool.StopTransaction();
        }

        private async void BtnModErstellung_OnClick(object? sender, RoutedEventArgs e)
        {
            if (cbSonst1 == null || cbSonst2 == null ||
                cbSonst3 == null || string.IsNullOrEmpty(tbSonst3.Text) || string.IsNullOrEmpty(tbSonst2.Text)) return;
            var suscache = await _myschool.GetSchuelerListe();
            var lulcache = await _myschool.GetLehrerListe();
            var kurscache = await _myschool.GetKursListe();
            var susliste = new List<SuS>();
            var lulliste = new List<LuL>();
            switch (cbSonst1.SelectedIndex)
            {
                case 0:
                    susliste = cbSonst2.SelectedIndex switch
                    {
                        0 => suscache.Where(s => tbSonst2.Text.Split(';').Contains(s.GetStufe())).ToList(),
                        1 => suscache.Where(s => tbSonst2.Text.Split(';').Contains(s.Klasse)).ToList(),
                        _ => susliste
                    };
                    break;
                case 1:
                    if (string.IsNullOrEmpty(tbSonst1.Text)) return;
                    susliste = suscache.Where(s => tbSonst1.Text.Split(';').Contains(s.ID.ToString())).ToList();
                    break;
                case 2:
                    lulliste = lulcache.ToList();
                    break;
                case 3:
                    if (string.IsNullOrEmpty(tbSonst1.Text)) return;
                    lulliste = lulcache.Where(l => tbSonst1.Text.Split(';').Contains(l.Kuerzel)).ToList();
                    break;
                default:
                    return;
            }

            var kursliste = kurscache.Where(k => tbSonst3.Text.Split(";").Contains(k.Bezeichnung)).ToList();
            if (kursliste.Count < 1) return;
            switch (cbSonst1.SelectedIndex)
            {
                case 0 or 1 when susliste.Count < 1:
                    return;
                case 0 or 1:
                {
                    await _myschool.StartTransaction();
                    foreach (var kurs in kursliste)
                    {
                        foreach (var sus in susliste)
                        {
                            await _myschool.AddStoK(sus, kurs);
                        }
                    }

                    await _myschool.StopTransaction();
                    return;
                }
                case 2 or 3 when lulliste.Count < 1:
                    return;
                case 2 or 3:
                {
                    await _myschool.StartTransaction();
                    foreach (var kurs in kursliste)
                    {
                        foreach (var lul in lulliste)
                        {
                            await _myschool.AddLtoK(lul, kurs);
                        }
                    }

                    await _myschool.StopTransaction();
                    return;
                }
            }
        }

        private void CbSonst1_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            tbSonst1.IsVisible = cbSonst1.SelectedIndex % 2 == 1;
            tbSonst1_1.IsVisible = cbSonst1.SelectedIndex % 2 == 0;
            cbSonst2.IsVisible = cbSonst1.SelectedIndex % 2 == 0;
            tbSonst2.IsVisible = cbSonst1.SelectedIndex % 2 == 0;
        }

        private async void BtnSonstDVIDs_OnClick(object? sender, RoutedEventArgs e)
        {
            var extx = new List<FilePickerFileType>
            {
                StSFileTypes.CSVFile,
                FilePickerFileTypes.All
            };
            var file = await ShowOpenFileDialog("Einwilligungen alt", extx);
            if (file is null) return;
            var alterStatusFilePath = file.Path.LocalPath;
            var alterstatus = await File.ReadAllLinesAsync(alterStatusFilePath);
            file = await ShowOpenFileDialog("Einwilligungen neu", extx);
            if (file is null) return;
            var neuerStatusFilePath = file.Path.LocalPath;
            var neuerStatus = await File.ReadAllLinesAsync(neuerStatusFilePath);

            var alteIDListe = (from line in alterstatus
                select line.Split(';')[0]
                into id
                where id.All(char.IsDigit)
                select Convert.ToInt32(id)).ToList();
            var neueIDListe = (from line in neuerStatus
                select line.Split(';')[0]
                into id
                where id.All(char.IsDigit)
                select Convert.ToInt32(id)).ToList();
            var diff = alteIDListe.Except(neueIDListe);
            var ids = diff.Aggregate("", (current, id) => current + ';' + id).TrimStart(';');
            var clipboard = Clipboard;
            if (clipboard == null) return;
            await clipboard.SetTextAsync(ids);
        }

        private async void MnuItemCopySuSidOnClick(object? sender, RoutedEventArgs e)
        {
            if (LeftListBox.SelectedItems == null) return;
            var ids = LeftListBox.SelectedItems.Cast<string>()
                .Aggregate("", (current, item) => current + item.Split(';')[1] + ";");
            var clipboard = Clipboard;
            if (clipboard == null) return;
            await clipboard.SetTextAsync(ids.TrimEnd(';'));
        }

        private async void MnuItemCopySuSMailOnClick(object? sender, RoutedEventArgs e)
        {
            if (LeftListBox.SelectedItems == null) return;
            var sus = LeftListBox.SelectedItems.Cast<string>().Aggregate("",
                (current, item) =>
                    current + _myschool.GetSchueler(Convert.ToInt32(item.Split(';')[1])).Result.Mail + ";");
            var clipboard = Clipboard;
            if (clipboard == null) return;
            await clipboard.SetTextAsync(sus.TrimEnd(';'));
        }

        private async void MnuItemCopyKursBezOnClick(object? sender, RoutedEventArgs e)
        {
            if (LeftListBox.SelectedItems == null) return;
            var bezliste = LeftListBox.SelectedItems.Cast<string>()
                .Aggregate("", (current, bez) => current + bez + ";");
            var clipboard = Clipboard;
            if (clipboard == null) return;
            await clipboard.SetTextAsync(bezliste.TrimEnd(';'));
        }

        private async void MnuItemCopyLuLMailsOnClick(object? sender, RoutedEventArgs e)
        {
            if (LeftListBox.SelectedItems == null) return;
            var mails = LeftListBox.SelectedItems.Cast<string>().Aggregate("",
                (current, line) => current + (_myschool.GetLehrkraft(line.Split(';')[0]).Result.Mail + ";"));
            var clipboard = Clipboard;
            if (clipboard == null) return;
            await clipboard.SetTextAsync(mails.TrimEnd(';'));
        }

        private async void MnuItemCopyLuLKrzOnClick(object? sender, RoutedEventArgs e)
        {
            if (LeftListBox.SelectedItems == null) return;
            var krzs = LeftListBox.SelectedItems.Cast<string>()
                .Aggregate("", (current, line) => current + (line.Split(';')[0] + ";"));
            var clipboard = Clipboard;
            if (clipboard == null) return;
            await clipboard.SetTextAsync(krzs.TrimEnd(';'));
        }

        private async void MnuItemCopyLogOnClick(object? sender, RoutedEventArgs e)
        {
            if (lbLogDisplay.SelectedItems == null) return;
            var logentries = lbLogDisplay.SelectedItems.Cast<string>()
                .Aggregate("", (current, line) => current + (line.Split(';')[0].Trim() + "\n"));
            var clipboard = Clipboard;
            if (clipboard == null) return;
            await clipboard.SetTextAsync(logentries);
        }

        private async void TbLeftSearch_OnPastingFromClipboard(object? sender, RoutedEventArgs e)
        {
            var clipboard = Clipboard;
            if (clipboard == null) return;
            var text = await clipboard.GetTextAsync();
            if (text is null) return;
            while (text.Contains('\r') || text.Contains('\n'))
            {
                text = text.Replace("\r", "").Replace("\n", ";");
            }

            while (text.Contains(";;"))
            {
                text = text.Replace(";;", ";");
            }

            await clipboard.SetTextAsync(text.TrimEnd(';'));
        }

        private void BtnSuSClearTextFields_OnClick(object? sender, RoutedEventArgs e)
        {
            ClearSuSTextFields();
        }

        private void BtnLuLClearTextFields_OnClick(object? sender, RoutedEventArgs e)
        {
            ClearLuLTextFields();
        }

        private void BtnKursClearTextFields_OnClick(object? sender, RoutedEventArgs e)
        {
            ClearKursTextFields();
        }

        private async void BtnExportFavos_OnClick(object? sender, RoutedEventArgs e)
        {
            await Dispatcher.UIThread.InvokeAsync(SaveFavosFile);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                MessageBoxManager.GetMessageBoxStandard(
                    new MessageBoxStandardParams
                    {
                        ButtonDefinitions = ButtonEnum.Ok,
                        ContentTitle = "Information",
                        ContentMessage =
                            "Speichern erfolgreich",
                        Icon = MsBox.Avalonia.Enums.Icon.Info,
                        WindowIcon = _msgBoxWindowIcon
                    }).ShowAsPopupAsync(this);
            });
            return;

            async Task SaveFavosFile()
            {
                var files = await ShowOpenFolderDialog("Bitte einen Dateipfad angeben...");
                if (files == null) return;
                var filepath = files.Path.LocalPath + "/mdl_einschreibungen.csv";
                var favos = await _myschool.getFavos();
                var stringifiedFavos = favos.Select(lehrkraft => "add,student," + lehrkraft.ID + ",EtatK").ToList();
                await File.WriteAllLinesAsync(filepath, stringifiedFavos, Encoding.UTF8);
            }
        }

        private async void BtnFavoSave_OnClick(object? sender, RoutedEventArgs e)
        {
            await _myschool.StartTransaction();
            var favos = await _myschool.getFavos();
            foreach (var l in favos)
            {
                await _myschool.UpdateLehrkraft(l.ID, l.Vorname, l.Nachname, l.Kuerzel, l.Mail, l.Fakultas, l.Pwttemp,
                    "", "");
            }

            var faecherliste = _myschool.GetLehrerListe().Result.Select(l => l.Fakultas.Split(',')).Distinct().ToList();
            if (faecherliste.Count < 1) return;
            var faecher = new List<string>();
            foreach (var faecherarray in faecherliste)
            {
                faecher.AddRange(faecherarray);
            }

            faecher = faecher.Distinct().ToList();
            foreach (var fach in faecher)
            {
                var validfach = exportFavoTabGrid.Children.Where(c => !string.IsNullOrEmpty(c.Name) && c.Name.Equals("cbExportFavo" + fach))
                    .ToList();
                if (validfach.Count == 0) continue;
                var favocb = (ComboBox)exportFavoTabGrid.Children.Where(c => !string.IsNullOrEmpty(c.Name) && c.Name.Equals("cbExportFavo" + fach))
                    .ToList()[0];
                var sfavocb = (ComboBox)exportFavoTabGrid.Children.Where(c => !string.IsNullOrEmpty(c.Name) && c.Name.Equals("cbExportSFavo" + fach))
                    .ToList()[0];
                var kuerzel = favocb.SelectedItem?.ToString();
                if (kuerzel != null)
                {
                    var l = await _myschool.GetLehrkraft(kuerzel.Split(';')[0]);
                    if (l.Favo != "")
                    {
                        l.Favo += "," + fach;
                    }
                    else
                    {
                        l.Favo = fach;
                    }

                    _myschool.UpdateLehrkraft(l);
                }

                kuerzel = sfavocb.SelectedItem?.ToString();
                if (kuerzel == null) continue;
                {
                    var l = await _myschool.GetLehrkraft(kuerzel.Split(';')[0]);
                    if (l.SFavo != "")
                    {
                        l.SFavo += "," + fach;
                    }
                    else
                    {
                        l.SFavo = fach;
                    }
                    _myschool.UpdateLehrkraft(l);
                }
            }

            await _myschool.StopTransaction();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                MessageBoxManager.GetMessageBoxStandard(
                    new MessageBoxStandardParams
                    {
                        ButtonDefinitions = ButtonEnum.Ok,
                        ContentTitle = "Information",
                        ContentMessage =
                            "Speichern erfolgreich",
                        Icon = MsBox.Avalonia.Enums.Icon.Info,
                        WindowIcon = _msgBoxWindowIcon
                    }).ShowAsPopupAsync(this);
            });
        }

        private void BtnSettingsToFile_OnClick(object? sender, RoutedEventArgs e)
        {
            //todo: implementieren das Speichern als System.Text.Json 
        }

        private void BtnSettingsFromFile_OnClick(object? sender, RoutedEventArgs e)
        {
            //todo: implementieren das Laden als System.Text.Json 
        }
    }
}