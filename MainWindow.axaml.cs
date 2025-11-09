using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using SchulDB;
using StS_GUI_Avalonia.services;
using Timer = System.Timers.Timer;

// ReSharper disable InconsistentNaming

namespace StS_GUI_Avalonia;

public partial class MainWindow : Window
{
    private readonly Timer _leftInputTimer = new(350);
    private readonly Timer _rightInputTimer = new(350);
    private Schuldatenbank _myschool;
    private readonly ContextMenu _leftListContext = new();
    private readonly Brush _darkBackgroundColor = new SolidColorBrush(Color.FromRgb(80, 80, 80));
    private readonly Brush _lightBackgroundColor = new SolidColorBrush(Color.FromRgb(242, 242, 242));
    private bool _rightMutex = false;
    private WindowIcon _msgBoxWindowIcon;
    private MenuItem _mnuItemCopySuSid;
    private MenuItem _mnuItemCopySuSMail;
    private MenuItem _mnuItemCopyKursBez;
    private MenuItem _mnuItemCopyLuLKrz;
    private MenuItem _mnuItemCopyLuLMails;
    private CheckBox _cbZeigeInaktiv;
    private CheckBox _cbZeigeNurBemerkungen;
    private readonly ContextMenu _logListContextMenu = new();
    private int leftLastComboIndex = -1;
    private int rightLastComboIndex = -1;

#pragma warning disable CS8618, CS9264
    //InitGUi() initialisiert die nicht initialisierten Variablen/Objekte/etc.
    public MainWindow()
    {
        InitializeComponent();
        InitGUI();
    }

    public MainWindow(IReadOnlyList<string> args)
#pragma warning restore CS8618, CS9264
    {
        InitializeComponent();
        InitGUI();
        if (args.Count != 1) return;
        var filepath = args[0];
        if (!File.Exists(filepath)) return;
        if (filepath.EndsWith(".sqlite"))
        {
            _myschool = new Schuldatenbank(filepath);
            Title = $"SchildToSchule - {_myschool.GetFilePath()}";
        }
        else if (filepath.EndsWith(".aes"))
        {
            Console.WriteLine("Bitte den Ausgabepfad angeben:");
            var outputFilePath = Console.ReadLine();
            if (string.IsNullOrEmpty(outputFilePath))
            {
                Console.WriteLine("Ungültiger Pfad.");
                return;
            }

            if (File.Exists(outputFilePath))
            {
                Console.WriteLine("Ausgabedatei existiert bereits. Soll sie überschrieben werden? (j/N)");
                var inputline = Console.ReadLine();
                if (inputline != "j") return;
            }

            Console.WriteLine("Bitte das Password eingeben:");
            var password = "";
            ConsoleKeyInfo keyInfo;
            do
            {
                keyInfo = Console.ReadKey(true);
                // Skip if Backspace or Enter is Pressed
                if (keyInfo.Key != ConsoleKey.Backspace && keyInfo.Key != ConsoleKey.Enter)
                {
                    password += keyInfo.KeyChar;
                    Console.Write("*");
                }
                else
                {
                    if (keyInfo.Key != ConsoleKey.Backspace || password.Length <= 0) continue;
                    // Remove last charcter if Backspace is Pressed
                    password = password[..^1];
                    Console.Write("\b \b");
                }
            } while (keyInfo.Key != ConsoleKey.Enter);

            LocalCryptoServive.FileDecrypt(filepath, outputFilePath, password);
            _myschool = new Schuldatenbank(outputFilePath);
        }
        else
        {
            Console.WriteLine("Es ist ein unbekannter Fehler aufgetreten, das Programm wird beendet.");
            Environment.Exit(-3);
        }

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
            tbSettingFachkurz.Text += $"{fachk}\n";
        }

        var langfach = _myschool.GetFachersatz().Result.Select(t => t.Split(';')[1]);
        foreach (var fachl in langfach)
        {
            tbSettingFachlang.Text += $"{fachl}\n";
        }

        _leftInputTimer.Elapsed += OnLeftTimedEvent;
        _rightInputTimer.Elapsed += OnRightTimedEvent;

        //ContextMenu für linkes ListBox
        List<Control> leftListContextItems = [];
        List<Control> copyContextItems = [];
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
        leftListContextItems.Add(mnuItemMPasswordGenerieren);
        leftListContextItems.Add(mnuItemMExport);
        _leftListContext.ItemsSource = leftListContextItems;
        leftListBox.ContextMenu = _leftListContext;

        var _mnuItemCopyLog = new MenuItem
        {
            Name = "mnuItemCopyLog",
            Header = "Kopieren"
        };
        _mnuItemCopyLog.Click += MnuItemCopyLogOnClick;

        _logListContextMenu.ItemsSource = new List<Control> { _mnuItemCopyLog };
        lbLogDisplay.ContextMenu = _logListContextMenu;

        //Kontextmenu fuer tbleftSearch
        List<Control> leftListButtonContextItems = [];
        var cbSucheVorname = new CheckBox
        {
            Name = "cbMnuSucheVorname",
            Content = "Vorname",
            IsChecked = true
        };
        var cbSucheNachname = new CheckBox
        {
            Name = "cbMnuSucheNachname",
            Content = "Nachname",
            IsChecked = true
        };
        var cbSucheMail = new CheckBox
        {
            Name = "cbMnuSucheMailadressen",
            Content = "Mailadressen",
            IsChecked = false
        };
        var cbSucheAnmeldename = new CheckBox
        {
            Name = "cbMnuSucheAnmeldename",
            Content = "Anmeldename/Kürzel",
            IsChecked = true
        };
        var cbSucheID = new CheckBox
        {
            Name = "cbMnuSucheID",
            Content = "ID",
            IsChecked = true
        };
        var cbSucheSeriennummer = new CheckBox
        {
            Name = "cbMnuSucheSeriennummer",
            Content = "Seriennummer",
            IsChecked = true
        };
        var cbSucheExact = new CheckBox
        {
            Name = "cbMnuSucheExact",
            Content = "Exakte Suche",
            IsChecked = false
        };
        _cbZeigeInaktiv = new CheckBox
        {
            Name = "cbMnuZeigeInaktiv",
            Content = "Zeige Inaktive",
            IsChecked = true
        };
        _cbZeigeNurBemerkungen = new CheckBox
        {
            Name = "cbMnuZeigeBemerkungen",
            Content = "Zeige nur SuS mit Bemerkungen",
            IsChecked = false
        };
        _cbZeigeInaktiv.Click += async (_, _) => { await CallLeftTimer(); };
        _cbZeigeNurBemerkungen.Click += async (_, _) => { await CallLeftTimer(); };
        leftListButtonContextItems.Add(cbSucheVorname);
        leftListButtonContextItems.Add(cbSucheNachname);
        leftListButtonContextItems.Add(cbSucheMail);
        leftListButtonContextItems.Add(cbSucheAnmeldename);
        leftListButtonContextItems.Add(cbSucheID);
        leftListButtonContextItems.Add(cbSucheSeriennummer);
        leftListButtonContextItems.Add(cbSucheExact);
        leftListButtonContextItems.Add(_cbZeigeInaktiv);
        leftListButtonContextItems.Add(_cbZeigeNurBemerkungen);
        tbLeftSearch.ContextMenu = new ContextMenu
        {
            ItemsSource = leftListButtonContextItems
        };

        //Rest
        rbD.IsChecked = true;
        Rb_OnClick(rbD, new RoutedEventArgs());
        leftListBox.MaxHeight = ClientSize.Height * 1.1;
        rightListBox.MaxHeight = leftListBox.MaxHeight;
        lbLogDisplay.MaxHeight = leftListBox.MaxHeight;
        lbLogDisplay.MaxWidth = ClientSize.Width * 1.1;
        _msgBoxWindowIcon =
            new WindowIcon(AssetLoader.Open(new Uri("avares://StS-GUI-Avalonia/Assets/gfx/school-building.png")));
        Resized += (_, _) =>
        {
            if (FrameSize == null) return;
            mainScroller.MaxHeight = FrameSize.Value.Height * 0.9;
            mainScroller.MaxWidth = FrameSize.Value.Width;
            leftListBox.MaxHeight = mainScroller.MaxHeight * 0.8;
            rightListBox.MaxHeight = leftListBox.MaxHeight;
            lbLogDisplay.MaxHeight = leftListBox.MaxHeight;
            lbLogDisplay.MaxWidth = FrameSize.Value.Width * 0.8;
            lbLogDisplay.Width = FrameSize.Value.Width * 0.75;
            exportScrollViewerFavo.MaxHeight = leftListBox.MaxHeight;
        };
    }

    private async Task<IStorageFile?> ShowSaveFileDialog(string dialogtitle,
        List<FilePickerFileType> extensions)
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
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = dialogtitle,
            FileTypeFilter = extensions,
            AllowMultiple = false
        });
        return files.Count > 0 ? files[0] : null;
    }

    private async Task<IStorageFolder?> ShowOpenFolderDialog(string dialogtitle)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return null;
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = dialogtitle,
            AllowMultiple = false
        });
        return folders.Count > 0 ? folders[0] : null;
    }

    private async Task ShowImportSuccessful()
    {
        await ShowCustomInfoMessage("Import abgeschlossen", "Information");
    }

    private async Task ShowCustomInfoMessage(string message, string title)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
                {
                    ButtonDefinitions = ButtonEnum.Ok,
                    ContentTitle = title,
                    ContentMessage = message,
                    Icon = MsBox.Avalonia.Enums.Icon.Info,
                    WindowIcon = _msgBoxWindowIcon
                })
                .ShowAsPopupAsync(this);
        });
    }

    private async Task ShowCustomErrorMessage(string message, string title)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
                {
                    ButtonDefinitions = ButtonEnum.Ok,
                    ContentTitle = title,
                    ContentMessage = message,
                    Icon = MsBox.Avalonia.Enums.Icon.Error,
                    WindowIcon = _msgBoxWindowIcon
                })
                .ShowAsPopupAsync(this);
        });
    }

    private async Task ShowCustomSuccessMessage(string message, string title)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
                {
                    ButtonDefinitions = ButtonEnum.Ok,
                    ContentTitle = title,
                    ContentMessage = message,
                    Icon = MsBox.Avalonia.Enums.Icon.Success,
                    WindowIcon = _msgBoxWindowIcon
                })
                .ShowAsPopupAsync(this);
        });
    }

    private async void OnMnuSchoolLoadClick(object? sender, RoutedEventArgs e)
    {
        var extx = new List<FilePickerFileType>
        {
            StSFileTypes.DataBaseFile
        };
        var files = await ShowOpenFileDialog("Bitte einen Dateipfad angeben...", extx);
        if (files == null) return;
        var filepath = files.Path.LocalPath;
        _myschool = new Schuldatenbank(filepath);
        Title = $"SchildToSchule - {_myschool.GetFilePath()}";
        LoadSettingsToGUI(_myschool.GetSettings().Result);
        InitData();
        SetStatusText();
    }

    private async Task LoadFavos()
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
                .Select(l => $"{l.Kuerzel};{l.Nachname},{l.Vorname}").ToList();
            if (cache.Count == 0) continue;
            exportFavoTabGrid.Children.Add(new TextBlock
            {
                Name = $"tbExportFavo{faecher[i]}",
                Text = faecher[i],
                [Grid.RowProperty] = i,
                [Grid.ColumnProperty] = 0
            });
            cache.Add("");
            cache.Sort();
            exportFavoTabGrid.Children.Add(new ComboBox
            {
                Name = $"cbExportFavo{faecher[i]}",
                ItemsSource = cache,
                [Grid.RowProperty] = i,
                [Grid.ColumnProperty] = 1
            });
            exportFavoTabGrid.Children.Add(new ComboBox
            {
                Name = $"cbExportSFavo{faecher[i]}",
                ItemsSource = cache,
                [Grid.RowProperty] = i,
                [Grid.ColumnProperty] = 2
            });
            // solved via https://github.com/AvaloniaUI/Avalonia/discussions/10144
        }

        var favos = await _myschool.GetFavos();

        foreach (var fach in faecher)
        {
            var validfach = exportFavoTabGrid.Children
                .Where(c => !string.IsNullOrEmpty(c.Name) && c.Name.Equals($"cbExportFavo{fach}"))
                .ToList();
            if (validfach.Count == 0) continue;
            var favocb = (ComboBox)exportFavoTabGrid.Children.Where(c =>
                    !string.IsNullOrEmpty(c.Name) && c.Name.Equals($"cbExportFavo{fach}"))
                .ToList()[0];
            var sfavocb = (ComboBox)exportFavoTabGrid.Children.Where(c =>
                    !string.IsNullOrEmpty(c.Name) && c.Name.Equals($"cbExportSFavo{fach}"))
                .ToList()[0];
            var favo = favos.Where(l => l.Favo.Split(',').Contains(fach)).ToList();
            if (favo.Count > 0)
            {
                favocb.SelectedItem = $"{favo[0].Kuerzel};{favo[0].Nachname},{favo[0].Vorname}";
            }

            var sfavo = favos.Where(l => l.SFavo.Split(',').Contains(fach)).ToList();
            if (sfavo.Count > 0)
            {
                sfavocb.SelectedItem = $"{sfavo[0].Kuerzel};{sfavo[0].Nachname},{sfavo[0].Vorname}";
            }
        }
    }

    private async void OnMnuschuleschließenClick(object? sender, RoutedEventArgs e)
    {
        if (_myschool.GetFilePath() != ":memory:")
        {
            var leftlist = this.GetControl<ListBox>("leftListBox");
            var rightlist = this.GetControl<ListBox>("rightListBox");
            ResetItemsSource(leftlist, []);
            ResetItemsSource(rightlist, []);
            Title = "SchildToSchule";
            _myschool = new Schuldatenbank(":memory:");
            ClearDisplayedData();
            InitData();
            SetStatusText();
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
                    ClearDisplayedData();
                    ResetItemsSource(leftListBox, []);
                    ResetItemsSource(rightListBox, []);
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
                    await ShowCustomSuccessMessage("Datenbank erfolgreich gespeichert", "Erfolg");
                }
                else
                {
                    await ShowCustomErrorMessage("Schließen fehlgeschlagen", "Fehler");
                }
            });
        }
    }

    private async void OnMnuschulespeichernunterClick(object? sender, RoutedEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(SaveDbFileAs);
        Title = $"SchildToSchule - {_myschool.GetFilePath()}";
        return;

        async Task SaveDbFileAs()
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
                    await ShowCustomErrorMessage("Speichern unter fehlgeschlagen", "Fehler");
                }
                else
                {
                    _myschool = tempDB;
                    await ShowCustomSuccessMessage("Datenbank erfolgreich gespeichert", "Erfolg");
                }
            });
        }
    }

    private async void OnMnuschuleversspeichernClick(object? sender, RoutedEventArgs e)
    {
        if (_myschool.GetFilePath() == ":memory:") return;

        var inputResult = await Dispatcher.UIThread.InvokeAsync(GetPasswordInput, DispatcherPriority.Input);
        if (string.IsNullOrEmpty(inputResult)) return;

        await Dispatcher.UIThread.InvokeAsync(SaveEncDbFile);
        return;

        async Task SaveEncDbFile()
        {
            var extx = new List<FilePickerFileType> { StSFileTypes.EncryptedFile };
            var files = await ShowSaveFileDialog("Bitte einen Dateipfad angeben...", extx);
            if (files == null) return;
            var filepath = files.Path.LocalPath;
            var dbPath = _myschool.GetFilePath();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                //workaround damit der FileHandle geschlossen ist, vor dem verschlüsseln
                File.Copy(dbPath, dbPath.Replace("sqlite", "tmp"), true);
                LocalCryptoServive.FileEncrypt(dbPath.Replace("sqlite", "tmp"), filepath, inputResult);
                File.Delete(dbPath.Replace("sqlite", "tmp"));
            }
            else
            {
                LocalCryptoServive.FileEncrypt(dbPath, filepath, inputResult);
            }

            await ShowCustomInfoMessage("Speichern erfolgreich.", "Erfolg");
        }

        async Task<string?> GetPasswordInput()
        {
            var pwiWindow = new PasswordInputEnc();
            var test = await pwiWindow.ShowPwdDialog(this);
            return test;
        }
    }

    private async void OnMnuversschuleladenClick(object? sender, RoutedEventArgs e)
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

        await Dispatcher.UIThread.InvokeAsync(LoadEncDbFile);
        Title = $"SchildToSchule - {_myschool.GetFilePath()}";
        InitData();
        SetStatusText();
        return;

        async Task<string?> GetPasswordInput()
        {
            var pwiWindow = new PasswordInputDec();
            var test = await pwiWindow.ShowPwdDialog(this);
            return test;
        }

        async Task LoadEncDbFile()
        {
            var encFileType = new List<FilePickerFileType> { StSFileTypes.DataBaseFile };
            var saveFile = await ShowSaveFileDialog("Bitte einen Dateipfad angeben...", encFileType);
            var outputFilePath = saveFile?.Path.LocalPath;
            if (outputFilePath == null) return;

            LocalCryptoServive.FileDecrypt(inputFilePath, outputFilePath, inputResult);
            _myschool = new Schuldatenbank(outputFilePath);
            await LoadFavos();
            await ShowCustomInfoMessage("Laden erfolgreich", "Information");
        }
    }

    private void OnMnuexitClick(object? sender, RoutedEventArgs e)
    {
        Environment.Exit(0);
    }

    private async void OnMnuloadfolderClick(object? sender, RoutedEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(ReadFileTask);
        SetStatusText();
        return;

        async Task ReadFileTask()
        {
            var folder = await ShowOpenFolderDialog("Bitte den Ordner mit den Dateien auswählen");
            if (folder == null) return;
            var folderpath = folder.Path.LocalPath;
            if (File.Exists($"{folderpath}/sus.csv") && File.Exists($"{folderpath}/lul.csv") &&
                File.Exists($"{folderpath}/kurse.csv"))
            {
                await _myschool.SusEinlesen($"{folderpath}/sus.csv");
                await _myschool.LulEinlesen($"{folderpath}/lul.csv");
                await _myschool.KurseEinlesen($"{folderpath}/kurse.csv");
                var aixcsvpath = "";
                var dvfile = "";
                var files = new DirectoryInfo(folderpath).GetFiles();
                foreach (var csvFile in files)
                {
                    if (csvFile.Name.StartsWith("AlleSchueler"))
                    {
                        aixcsvpath = csvFile.FullName;
                    }

                    if (csvFile.Name.StartsWith("Ohne DV"))
                    {
                        dvfile = csvFile.FullName;
                    }
                }

                if (aixcsvpath != "")
                {
                    await _myschool.AIXDatenEinlesen(aixcsvpath);
                }

                if (dvfile != "")
                {
                    foreach (var id_string in await File.ReadAllLinesAsync(dvfile))
                    {
                        if (!id_string.All(char.IsDigit)) continue;
                        var sus_id = Convert.ToInt32(id_string);
                        _myschool.SetM365(sus_id, 0);
                    }
                }

                await ShowImportSuccessful();
            }
        }
    }

    private async void OnMnuloadsusfromfileClick(object? sender, RoutedEventArgs e)
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
        await ShowImportSuccessful();
    }

    private async void OnMnuloadlulfromfileClick(object? sender, RoutedEventArgs e)
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
        await ShowImportSuccessful();
    }

    private async void OnMnuloadkursefromfileClick(object? sender, RoutedEventArgs e)
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
        SetStatusText();
        await ShowImportSuccessful();
    }

    private async void OnMnuloadusernamesmailClick(object? sender, RoutedEventArgs e)
    {
        var extx = new List<FilePickerFileType>
        {
            StSFileTypes.CSVFile,
            FilePickerFileTypes.All
        };
        var files = await ShowOpenFileDialog("Lade Nutzernamen & Mailadressen", extx);
        if (files == null) return;
        var filePath = files.Path.LocalPath;
        await _myschool.AIXDatenEinlesen(filePath);
        SetStatusText();
        await ShowImportSuccessful();
    }

    private async void OnMnuloadzweitaccountsClick(object? sender, RoutedEventArgs e)
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
        SetStatusText();
        await ShowImportSuccessful();
    }

    private async void OnMnuexporttocsvClick(object? sender, RoutedEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(ReadFileTask);
        return;

        async Task ReadFileTask()
        {
            var folder = await ShowOpenFolderDialog("Bitte den Ordner mit den Dateien auswählen");
            if (folder == null) return;
            var folderpath = folder.Path.LocalPath;
            int res;
            if (!File.Exists($"{folderpath}/sus.csv") && !File.Exists($"{folderpath}/lul.csv") &&
                !File.Exists($"{folderpath}/kurse.csv") &&
                !File.Exists($"{folderpath}/temp_accounts.csv"))
            {
                res = await _myschool.DumpDataToCSVs(folderpath);
            }
            else
            {
                var override_res = await ShowOverwriteDialog();
                if (override_res != ButtonResult.Yes) return;
                res = await _myschool.DumpDataToCSVs(folderpath);
            }

            if (res == 1)
            {
                await ShowCustomInfoMessage("Export abgeschlossen", "Information");
            }
            else
            {
                await ShowCustomErrorMessage("Fehler beim Export", "Fehler");
            }
        }
    }

    private async void OnMnuaboutClick(object? sender, RoutedEventArgs e)
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
                        $"SchildToSchule\n{version}",
                    Icon = MsBox.Avalonia.Enums.Icon.Database,
                    WindowIcon = _msgBoxWindowIcon
                }).ShowAsPopupAsync(this);
        });
    }

    private async void OnBtnsusaddClick(object? sender, RoutedEventArgs e)
    {
        var susid = tbSuSID.Text;
        var susvname = tbSuSVorname.Text;
        var susnname = tbSuSnachname.Text;
        var susklasse = tbSuSKlasse.Text;
        var susnutzername = tbSuSNutzername.Text;
        var susaixmail = tbSuSAIXMail.Text;
        var suselternadresse = tbSuSElternadresse.Text;
        var suszweitadresse = tbSuSZweitadresse.Text;
        var seriennummer = string.IsNullOrEmpty(tbSuSSeriennummer.Text) ? "" : tbSuSSeriennummer.Text;
        var susHatZweitaccount = cbSuSZweitaccount.IsChecked;
        var susM365 = cbSuSM365.IsChecked != null && cbSuSM365.IsChecked.Value;
        var susIstAktiv = cbSuSAktiv.IsChecked != null && cbSuSAktiv.IsChecked.Value;
        var susJAMFAllowed = cbSuSJAMF.IsChecked != null && cbSuSJAMF.IsChecked.Value;
        var susBemerkung = tbSuSBemerkung.Text ?? "";
        if (string.IsNullOrEmpty(susid) || string.IsNullOrEmpty(susvname) || string.IsNullOrEmpty(susnname) ||
            string.IsNullOrEmpty(susklasse) || susnutzername == null || string.IsNullOrEmpty(suselternadresse) ||
            susHatZweitaccount == null || tbSuSKurse == null || tbSuSKurse!.Text == null)
        {
            await ShowCustomErrorMessage(
                "Nicht alle erforderlichen Informationen angegeben!\nStellen Sie sicher, dass ID, Vorname, Nachname, Klasse\nund eine Elternadresse angegeben sind",
                "Fehler");
            SetStatusText();
            return;
        }

        var suskurse = tbSuSKurse.Text.Split(',').ToList();

        if (!susid.All(char.IsDigit))
        {
            await ShowCustomErrorMessage("Die SuS-ID enthält nicht nur Zahlen!", "Fehler");
            return;
        }

        var sid = Convert.ToInt32(susid);
        if (_myschool.GibtEsSchueler(sid))
        {
            if (suszweitadresse != null && susaixmail != null)
                if (!susM365)
                {
                    susaixmail = "";
                }
                await _myschool.UpdateSchueler(sid, susvname, susnname, suselternadresse, susklasse, susnutzername,
                    susaixmail, susHatZweitaccount == false ? 0 : 1, suszweitadresse,
                    susM365, susIstAktiv, seriennummer, susJAMFAllowed, susBemerkung);
            var alteKurse = _myschool.GetKurseVonSuS(sid).Result;
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
            if (suszweitadresse != null && susaixmail != null)
                await _myschool.AddSchuelerIn(sid, susvname, susnname, suselternadresse, susklasse, susnutzername,
                    susaixmail, susHatZweitaccount == false ? 0 : 1, suszweitadresse, seriennummer, susM365,
                    susJAMFAllowed, susIstAktiv, susBemerkung);
            if (suskurse is [""]) return;
            foreach (var kursbez in suskurse)
            {
                await _myschool.AddStoK(sid, kursbez);
            }
        }
    }

    private async void OnBtnsusdelClick(object? sender, RoutedEventArgs e)
    {
        var susid = tbSuSID.Text;
        if (string.IsNullOrEmpty(susid) || !susid.All(char.IsDigit)) return;
        var sid = Convert.ToInt32(susid);
        if (_myschool.GetSchueler(sid).Result.ID == 0) return;
        ListBox source;
        if (cboxDataLeft.SelectedIndex == 0)
        {
            source = leftListBox;
        }
        else if (cboxDataRight.SelectedIndex == 0)
        {
            source = rightListBox;
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
                if (string.IsNullOrEmpty(susstring)) return;
                sid = Convert.ToInt32(susstring.Split(';')[1]);
                await _myschool.RemoveS(sid);
            }
        }

        await _myschool.StopTransaction();
        OnLeftDataChanged(true);
        OnRightDataChanged(true);
        SetStatusText();
    }

    private async void OnbtnsuseinschreibenClick(object? sender, RoutedEventArgs e)
    {
        var susid = tbSuSID.Text;
        var sid = Convert.ToInt32(susid);
        var susklasse = string.IsNullOrEmpty(tbSuSKlasse.Text) ? "" : tbSuSKlasse.Text;
        if (susklasse == "" || sid == 0) return;
        if (susklasse.Contains(','))
        {
            foreach (var klasse in susklasse.Split(','))
            {
                await _myschool.AddStoKlassenKurse(await _myschool.GetSchueler(sid), klasse);
            }
        }
        else
        {
            await _myschool.AddStoKlassenKurse(await _myschool.GetSchueler(sid), susklasse);
        }

        OnLeftDataChanged(true);
        OnRightDataChanged(true);
        SetStatusText();
    }

    private async void OnBtnluladdClick(object? sender, RoutedEventArgs e)
    {
        var lulid = tbLuLID.Text;
        var lulvname = tbLuLVorname.Text;
        var lulnname = tbLuLNachname.Text;
        var lulkrz = tbLuLKuerzel.Text;
        var lulfakultas = tbLuLFach.Text;
        var lulmail = tbLuLMail.Text;
        var lulpwtemp = tbLuLtmpPwd.Text;
        var lulfavo = tbLuLFavo.Text;
        var lulsfavo = tbLuLSFavo.Text;
        var lulistAktiv = cbLuLAktiv.IsChecked;
        var lulBemerkung = tbLuLBemerkung.Text ?? "";
        var seriennummer = string.IsNullOrEmpty(tbLuLSeriennummer.Text) ? "" : tbLuLSeriennummer.Text;
        if (lulid == null || string.IsNullOrEmpty(lulvname) || string.IsNullOrEmpty(lulnname) ||
            string.IsNullOrEmpty(lulkrz) || string.IsNullOrEmpty(lulfakultas) ||
            string.IsNullOrEmpty(lulmail) || lulpwtemp == null || tbLuLKurse.Text == null || lulistAktiv == null)
        {
            await ShowCustomErrorMessage(
                "Nicht alle erforderlichen Informationen angegeben!\nStellen Sie sicher, dass ID, Vorname, Nachname, Kürzel\nund Fakultas ausgefüllt sind.",
                "Fehler");
            return;
        }

        if (string.IsNullOrEmpty(lulfavo))
        {
            lulfavo = "";
        }

        if (string.IsNullOrEmpty(lulsfavo))
        {
            lulsfavo = "";
        }

        var neue_kurse = tbLuLKurse.Text.Split(',').ToList();

        if (lulid == "" || !lulid.All(char.IsDigit))
        {
            lulid = $"{_myschool.GetLehrerIDListe().Result.Max() + 1}";
            lulpwtemp = Tooling.GeneratePasswort(8);
        }

        var lid = Convert.ToInt32(lulid);
        if (_myschool.GibtEsLehrkraft(lid))
        {
            var lehrkraft = await _myschool.GetLehrkraft(lid);
            var alteKurse = _myschool.GetKurseVonLuL(lid).Result;
            var schnittmenge = alteKurse.Where(kurs => !neue_kurse.Contains(kurs.Bezeichnung)).ToList();
            if (schnittmenge.Count != 0)
            {
                await ShowCustomInfoMessage(
                    $"Folgende Kurse wurden für {string.Join(" ", lehrkraft.Vorname, lehrkraft.Nachname)} entfernt: {string.Join(", ", schnittmenge.Select(k => k.Bezeichnung))}\nBitte stellen Sie sicher, dass es eine passende Vertretung gibt und tragen Sie sie ggf. ein.\n",
                    "Vorsicht");
            }

            await _myschool.UpdateLehrkraft(lid, lulvname, lulnname, lulkrz, lulmail, lulfakultas, lulpwtemp, lulfavo,
                lulsfavo, seriennummer, lulBemerkung);
            _myschool.SetzeAktivstatusLehrkraft(lid, cbLuLAktiv.IsChecked != null && lulistAktiv.Value);
            foreach (var kurs in schnittmenge.Where(kurs => _myschool.GibtEsKurs(kurs.Bezeichnung)))
            {
                await _myschool.RemoveLfromK(lid, kurs.Bezeichnung);
            }

            if (neue_kurse.Count <= 0) return;
            foreach (var kurs in neue_kurse.Where(kurs => _myschool.GibtEsKurs(kurs)))
            {
                await _myschool.AddLtoK(lid, kurs);
            }
        }
        else
        {
            await _myschool.Addlehrkraft(lid, lulvname, lulnname, lulkrz, lulmail, lulfakultas, lulfavo, lulsfavo,
                seriennummer, lulBemerkung);
            if (neue_kurse.Count == 0) return;
            foreach (var kurs in neue_kurse)
            {
                await _myschool.AddLtoK(lid, kurs);
            }
        }

        OnLeftDataChanged(true);
        OnRightDataChanged(true);
        SetStatusText();
    }

    private async void OnBtnluldelClick(object? sender, RoutedEventArgs e)
    {
        var lulid = tbLuLID.Text;
        if (string.IsNullOrEmpty(lulid) || !lulid.All(char.IsDigit)) return;
        var lid = Convert.ToInt32(lulid);
        ListBox source;
        if (cboxDataLeft.SelectedIndex == 1)
        {
            source = leftListBox;
        }
        else if (cboxDataRight.SelectedIndex == 1)
        {
            source = rightListBox;
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
        SetStatusText();
    }

    private void InitData()
    {
        if (cboxDataLeft == null || cboxDataRight == null) return;
        cboxDataLeft.SelectedIndex = 0;
        cboxDataRight.SelectedIndex = 1;
        var llist = _myschool.GetSchuelerListe().Result.Select(s => $"{s.Nachname},{s.Vorname};{s.ID}")
            .ToList();
        llist.Sort(Comparer<string>.Default);
        ResetItemsSource(leftListBox, llist);
        LoadSettingsToGUI(_myschool.GetSettings().Result);
        _ = LoadFavos();
    }

    private void LoadSettingsToGUI(Einstellungen settings)
    {
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
            tbSettingFachkurz.Text += $"{fachk}\n";
        }

        tbSettingFachlang.Text = "";
        foreach (var fachl in langfach)
        {
            tbSettingFachlang.Text += $"{fachl}\n";
        }

        tbSettingErprobungsstufenleitung.Text = settings.Erprobungstufenleitung;
        tbSettingMittelstufenleitung.Text = settings.Mittelstufenleitung;
        tbSettingEFstufenleitung.Text = settings.EFStufenleitung;
        tbSettingQ1Stufenleitung.Text = settings.Q1Stufenleitung;
        tbSettingQ2Stufenleitung.Text = settings.Q2Stufenleitung;
        tbSettingOberstufenkoordination.Text = settings.Oberstufenkoordination;
        tbSettingStuBos.Text = settings.StuBos;
        tbSettingErprobungsstufen.Text = string.Join(',', settings.Erprobungsstufe);
        tbSettingMittelstufen.Text = string.Join(',', settings.Mittelstufe);
        tbSettingOberstufe.Text = string.Join(',', settings.Oberstufe);
        tbSettingStuBoStufen.Text = string.Join(',', settings.StuboStufen);
        tbSettingJAMFStufen.Text = string.Join(',', settings.JAMFStufen);
    }

    private void CboxDataLeft_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ClearDisplayedData();
        _rightMutex = true;
        if (leftLastComboIndex != cboxDataLeft.SelectedIndex)
        {
            leftListBox.SelectedItems?.Clear();
            OnLeftDataChanged(true);
            leftLastComboIndex = cboxDataLeft.SelectedIndex;
        }

        _rightMutex = false;
    }

    private void ClearDisplayedData()
    {
        ClearSuSTextFields();
        ClearLuLTextFields();
        ClearKursTextFields();
        ResetItemsSource(lbLogDisplay, []);
        ResetItemsSource(lbFehlerliste, []);
        tbSonst1.Text = tbSonst2.Text = tbSonst3.Text = tbSonst11.Text = tbSuSNamen.Text = "";
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
        tbKursBemerkung.Text = "";
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
        cbLuLAktiv.IsChecked = false;
        tbLuLSeriennummer.Text = "";
        tbLuLBemerkung.Text = "";
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
        cbSuSM365.IsChecked = false;
        cbSuSAktiv.IsChecked = false;
        tbSuSSeriennummer.Text = "";
        cbSuSJAMF.IsChecked = false;
        tbSuSBemerkung.Text = "";
    }

    private void CboxDataRight_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _rightMutex = true;
        if (rightLastComboIndex != cboxDataRight.SelectedIndex)
        {
            rightListBox.SelectedItems?.Clear();
            OnRightDataChanged(true);
            rightLastComboIndex = cboxDataRight.SelectedIndex;
        }

        _rightMutex = false;
    }

    private void LeftListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ClearDisplayedData();
        OnLeftDataChanged(false);
    }

    private void RightListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        OnRightDataChanged(false);
    }

    private void OnLeftDataChanged(bool hasComboBoxChanged)
    {
        if (leftListBox == null || rightListBox == null || cboxDataLeft == null || cboxDataRight == null ||
            _cbZeigeInaktiv.IsChecked == null || leftListBox.SelectedItems == null) return;
        SetStatusText();
        if (_rightMutex && !hasComboBoxChanged) return;
        if (hasComboBoxChanged)
        {
            ResetItemsSource(rightListBox, []);
        }

        var zeigeInaktive = _cbZeigeInaktiv.IsChecked.Value;
        _mnuItemCopySuSid.IsVisible = _mnuItemCopySuSMail.IsVisible = cboxDataLeft.SelectedIndex == 0;
        _mnuItemCopyLuLKrz.IsVisible = _mnuItemCopyLuLMails.IsVisible = cboxDataLeft.SelectedIndex == 1;
        _mnuItemCopyKursBez.IsVisible = cboxDataLeft.SelectedIndex == 2;
        switch (cboxDataLeft.SelectedIndex)
        {
            //s=0;l==1;k==2
            case 0:
                if (cboxDataRight.SelectedIndex == 0)
                {
                    cboxDataRight.SelectedIndex = 1;
                }

                if (leftListBox.SelectedItems.Count < 1 || leftListBox.SelectedItems == null || hasComboBoxChanged)
                {
                    var slist = _myschool.GetSchuelerListe().Result.Where(s => s.IstAktiv || zeigeInaktive)
                        .Select(s => $"{s.Nachname},{s.Vorname};{s.ID}").Distinct().ToList();
                    slist.Sort(Comparer<string>.Default);
                    ResetItemsSource(leftListBox, slist);
                    ResetItemsSource(rightListBox, []);
                }
                else
                {
                    var sid = leftListBox.SelectedItems[0]?.ToString()?.Split(';')[1];
                    var sus = _myschool.GetSchueler(Convert.ToInt32(sid)).Result;
                    LoadSuSData(sus);
                    if (sus.ID == 0) return;
                    switch (cboxDataRight.SelectedIndex)
                    {
                        case 1:
                        {
                            var rlist = _myschool.GetLuLvonSuS(sus.ID).Result
                                .Select(l => $"{l.Kuerzel};{l.Nachname},{l.Vorname}").Distinct().ToList();
                            rlist.Sort(Comparer<string>.Default);
                            ResetItemsSource(rightListBox, rlist);
                            break;
                        }
                        case 2:
                        {
                            var rlist = _myschool.GetKurseVonSuS(sus.ID).Result.Select(k => k.Bezeichnung)
                                .Distinct()
                                .ToList();
                            ResetItemsSource(rightListBox, rlist);
                            break;
                        }
                    }
                }

                break;
            case 1:
                if (cboxDataRight.SelectedIndex == 1)
                {
                    cboxDataRight.SelectedIndex = 2;
                }

                if (leftListBox.SelectedItems.Count < 1 || leftListBox.SelectedItems == null || hasComboBoxChanged)
                {
                    var lullist = _myschool.GetLehrerListe().Result.Where(l => l.IstAktiv || zeigeInaktive)
                        .Select(l => $"{l.Kuerzel};{l.Nachname},{l.Vorname}").Distinct().ToList();
                    lullist.Sort(Comparer<string>.Default);
                    ResetItemsSource(leftListBox, lullist);
                    ResetItemsSource(rightListBox, []);
                }
                else
                {
                    var lulkrz = leftListBox.SelectedItems[0]?.ToString()?.Split(';')[0];
                    if (string.IsNullOrEmpty(lulkrz)) return;
                    var lul = _myschool.GetLehrkraft(lulkrz).Result;
                    LoadLuLData(lul);
                    switch (cboxDataRight.SelectedIndex)
                    {
                        case 0:
                        {
                            var rlist = _myschool.GetSuSVonLuL(lul.ID).Result
                                .Select(s => $"{s.Nachname},{s.Vorname};{s.ID}").Distinct().ToList();
                            rlist.Sort(Comparer<string>.Default);
                            ResetItemsSource(rightListBox, rlist);

                            break;
                        }
                        case 2:
                        {
                            var rlist = _myschool.GetKurseVonLuL(lul.ID).Result.Select(k => k.Bezeichnung)
                                .Distinct()
                                .ToList();
                            rlist.Sort(Comparer<string>.Default);
                            ResetItemsSource(rightListBox, rlist);
                            break;
                        }
                    }
                }

                break;
            case 2:
                if (cboxDataRight.SelectedIndex == 2)
                {
                    cboxDataRight.SelectedIndex = 0;
                }

                if (leftListBox.SelectedItems.Count < 1 || leftListBox.SelectedItems == null || hasComboBoxChanged)
                {
                    var klist = _myschool.GetKursListe().Result.Select(k => k.Bezeichnung).Distinct().ToList();
                    klist.Sort(Comparer<string>.Default);
                    ResetItemsSource(leftListBox, klist);
                    ResetItemsSource(rightListBox, []);
                }
                else
                {
                    var kurzbez = leftListBox.SelectedItems[0]?.ToString();
                    if (string.IsNullOrEmpty(kurzbez)) return;
                    var kurs = _myschool.GetKurs(kurzbez).Result;
                    LoadKursData(kurs);
                    switch (cboxDataRight.SelectedIndex)
                    {
                        case 0:
                        {
                            if (string.IsNullOrEmpty(kurs.Bezeichnung)) break;
                            var rlist = _myschool.GetSuSAusKurs(kurs.Bezeichnung).Result
                                .Select(s => $"{s.Nachname},{s.Vorname};{s.ID}").Distinct().ToList();
                            rlist.Sort(Comparer<string>.Default);
                            ResetItemsSource(rightListBox, rlist);
                            break;
                        }
                        case 1:
                        {
                            if (string.IsNullOrEmpty(kurs.Bezeichnung)) break;
                            var rlist = _myschool.GetLuLAusKurs(kurs.Bezeichnung).Result
                                .Select(l => $"{l.Kuerzel};{l.Nachname},{l.Vorname}").Distinct().ToList();
                            rlist.Sort(Comparer<string>.Default);
                            ResetItemsSource(rightListBox, rlist);
                            break;
                        }
                    }
                }

                break;
        }
    }

    private void SetStatusText()
    {
        if (leftListBox.SelectedItems == null || rightListBox.SelectedItems == null) return;
        var leftcounter = " | ";
        switch (cboxDataLeft.SelectedIndex)
        {
            case 0:
                leftcounter += $"{leftListBox.SelectedItems.Count} Schüler:Innen";
                break;
            case 1:
                leftcounter += $"{leftListBox.SelectedItems.Count} Lehrkräfte";
                break;
            case 2:
                leftcounter += $"{leftListBox.SelectedItems.Count} Kurse";
                break;
        }

        var rightcounter = " und ";
        switch (cboxDataRight.SelectedIndex)
        {
            case 0:
                rightcounter += $"{rightListBox.SelectedItems.Count} Schüler:Innen";
                break;
            case 1:
                rightcounter += $"{rightListBox.SelectedItems.Count} Lehrkräfte";
                break;
            case 2:
                rightcounter += $"{rightListBox.SelectedItems.Count} Kurse";
                break;
        }

        tbStatusBar.Text = $"{_myschool.GetStat().Result}{leftcounter}{rightcounter} markiert";
    }

    private void OnRightDataChanged(bool hasComboBoxChanged)
    {
        if (leftListBox == null || rightListBox == null || cboxDataLeft == null || cboxDataRight == null ||
            rightListBox.SelectedItems == null) return;
        SetStatusText();
        if (_rightMutex && !hasComboBoxChanged) return;
        switch (cboxDataRight.SelectedIndex)
        {
            //s=0;l==1;k==2
            case 0:
                if (cboxDataLeft.SelectedIndex == 0 || leftListBox.SelectedItems == null ||
                    leftListBox.SelectedItems.Count < 1 ||
                    leftListBox.SelectedItems == null) return;
                switch (cboxDataLeft.SelectedIndex)
                {
                    case 1:
                    {
                        var lulkrz = leftListBox.SelectedItems[0]?.ToString()?.Split(';')[0];
                        if (string.IsNullOrEmpty(lulkrz)) return;
                        var lul = _myschool.GetLehrkraft(lulkrz).Result;
                        LoadLuLData(lul);
                        if (rightListBox.SelectedItems.Count > 0)
                        {
                            var sid = rightListBox.SelectedItems[0]?.ToString()?.Split(';')[1];
                            var sus = _myschool.GetSchueler(Convert.ToInt32(sid)).Result;
                            if (sus.ID == 0) return;
                            LoadSuSData(sus);
                        }

                        if (!hasComboBoxChanged) return;
                        var rlist = _myschool.GetSuSVonLuL(lul.ID).Result
                            .Select(s => $"{s.Nachname},{s.Vorname};{s.ID}").Distinct().ToList();
                        rlist.Sort(Comparer<string>.Default);
                        ResetItemsSource(rightListBox, rlist);
                        break;
                    }
                    case 2:
                    {
                        var kurzbez = leftListBox.SelectedItems[0]?.ToString();
                        if (string.IsNullOrEmpty(kurzbez)) return;
                        var kurs = _myschool.GetKurs(kurzbez).Result;
                        LoadKursData(kurs);
                        if (rightListBox.SelectedItems.Count > 0)
                        {
                            var sid = rightListBox.SelectedItems[0]?.ToString()?.Split(';')[1];
                            var sus = _myschool.GetSchueler(Convert.ToInt32(sid)).Result;
                            if (sus.ID == 0) return;
                            LoadSuSData(sus);
                        }

                        if (!hasComboBoxChanged) return;
                        var rlist = _myschool.GetSuSAusKurs(kurs.Bezeichnung).Result
                            .Select(s => $"{s.Nachname},{s.Vorname};{s.ID}").Distinct().ToList();
                        rlist.Sort(Comparer<string>.Default);
                        ResetItemsSource(rightListBox, rlist);
                        break;
                    }
                }

                break;
            case 1:
                if (cboxDataLeft.SelectedIndex == 1 || leftListBox.SelectedItems == null ||
                    leftListBox.SelectedItems.Count < 1 ||
                    leftListBox.SelectedItems == null) return;
                switch (cboxDataLeft.SelectedIndex)
                {
                    case 0:
                    {
                        var sid = leftListBox.SelectedItems[0]?.ToString()?.Split(';')[1];
                        var sus = _myschool.GetSchueler(Convert.ToInt32(sid)).Result;
                        if (sus.ID == 0) return;
                        LoadSuSData(sus);
                        if (rightListBox.SelectedItems.Count > 0)
                        {
                            var lulkrz = rightListBox.SelectedItems[0]?.ToString()?.Split(';')[0];
                            if (string.IsNullOrEmpty(lulkrz)) return;
                            var lul = _myschool.GetLehrkraft(lulkrz).Result;
                            LoadLuLData(lul);
                        }

                        if (!hasComboBoxChanged) return;
                        var rlist = _myschool.GetLuLvonSuS(sus.ID).Result
                            .Select(l => $"{l.Kuerzel};{l.Nachname},{l.Vorname}").Distinct().ToList();
                        rlist.Sort(Comparer<string>.Default);
                        ResetItemsSource(rightListBox, rlist);
                        break;
                    }
                    case 2:
                    {
                        var kurzbez = leftListBox.SelectedItems[0]?.ToString();
                        if (string.IsNullOrEmpty(kurzbez)) return;
                        var kurs = _myschool.GetKurs(kurzbez).Result;
                        LoadKursData(kurs);
                        if (rightListBox.SelectedItems.Count > 0)
                        {
                            var lulkrz = rightListBox.SelectedItems[0]?.ToString()?.Split(';')[0];
                            if (string.IsNullOrEmpty(lulkrz)) return;
                            var lul = _myschool.GetLehrkraft(lulkrz).Result;
                            LoadLuLData(lul);
                        }

                        if (!hasComboBoxChanged) return;
                        if (string.IsNullOrEmpty(kurs.Bezeichnung)) break;
                        var rlist = _myschool.GetLuLAusKurs(kurs.Bezeichnung).Result
                            .Select(l => $"{l.Kuerzel};{l.Nachname},{l.Vorname}").Distinct().ToList();
                        rlist.Sort(Comparer<string>.Default);
                        ResetItemsSource(rightListBox, rlist);
                        break;
                    }
                }

                break;
            case 2:
                if (cboxDataLeft.SelectedIndex == 2 || leftListBox.SelectedItems == null ||
                    leftListBox.SelectedItems.Count < 1 ||
                    leftListBox.SelectedItems == null) return;
                switch (cboxDataLeft.SelectedIndex)
                {
                    case 0:
                    {
                        var sid = leftListBox.SelectedItems[0]?.ToString()?.Split(';')[1];
                        var sus = _myschool.GetSchueler(Convert.ToInt32(sid)).Result;
                        if (sus.ID == 0) return;
                        LoadSuSData(sus);
                        if (rightListBox.SelectedItems.Count > 0)
                        {
                            var kurzbez = rightListBox.SelectedItems[0]?.ToString();
                            if (string.IsNullOrEmpty(kurzbez)) return;
                            var kurs = _myschool.GetKurs(kurzbez).Result;
                            LoadKursData(kurs);
                        }

                        if (!hasComboBoxChanged) return;
                        var rlist = _myschool.GetKurseVonSuS(sus.ID).Result.Select(k => k.Bezeichnung)
                            .Distinct()
                            .ToList();
                        ResetItemsSource(rightListBox, rlist);
                        break;
                    }
                    case 1:
                    {
                        var lulkrz = leftListBox.SelectedItems[0]?.ToString()?.Split(';')[0];
                        if (string.IsNullOrEmpty(lulkrz)) return;
                        var lul = _myschool.GetLehrkraft(lulkrz).Result;
                        LoadLuLData(lul);
                        if (rightListBox.SelectedItems.Count > 0)
                        {
                            var kurzbez = rightListBox.SelectedItems[0]?.ToString();
                            if (string.IsNullOrEmpty(kurzbez)) return;
                            var kurs = _myschool.GetKurs(kurzbez).Result;
                            LoadKursData(kurs);
                        }

                        if (!hasComboBoxChanged) return;
                        var rlist = _myschool.GetKurseVonLuL(lul.ID).Result.Select(k => k.Bezeichnung)
                            .Distinct()
                            .ToList();
                        rlist.Sort(Comparer<string>.Default);
                        ResetItemsSource(rightListBox, rlist);
                        break;
                    }
                }

                break;
        }
    }

    private void LoadSuSData(SuS s)
    {
        if (s.ID is 0 or < 50000) return;
        tbSuSID.Text = $"{s.ID}";
        tbSuSVorname.Text = s.Vorname;
        tbSuSnachname.Text = s.Nachname;
        tbSuSKlasse.Text = s.Klasse;
        tbSuSNutzername.Text = s.Nutzername;
        tbSuSAIXMail.Text = s.Aixmail;
        tbSuSElternadresse.Text = s.Mail;
        tbSuSZweitadresse.Text = s.Zweitmail;
        tbSuSKurse.Text = _myschool.GetKurseVonSuS(s.ID).Result
            .Aggregate("", (current, kurs) => $"{current}{kurs.Bezeichnung},").TrimEnd(',');
        cbSuSZweitaccount.IsChecked = s.Zweitaccount;
        cbSuSM365.IsChecked = s.HasM365Account;
        cbSuSAktiv.IsChecked = s.IstAktiv;
        tbSuSSeriennummer.Text = s.Seriennummer;
        cbSuSJAMF.IsChecked = s.AllowJAMF;
        tbSuSBemerkung.Text = s.Bemerkung;
    }

    private void LoadLuLData(Lehrkraft l)
    {
        if (l.ID is 0 or > 1500) return;
        tbLuLID.Text = $"{l.ID}";
        tbLuLVorname.Text = l.Vorname;
        tbLuLNachname.Text = l.Nachname;
        tbLuLKuerzel.Text = l.Kuerzel;
        tbLuLFach.Text = l.Fakultas;
        tbLuLMail.Text = l.Mail;
        tbLuLtmpPwd.Text = l.Pwttemp;
        tbLuLKurse.Text = _myschool.GetKurseVonLuL(l.ID).Result
            .Aggregate("", (current, kurs) => $"{current}{kurs.Bezeichnung},").TrimEnd(',');
        tbLuLFavo.Text = l.Favo;
        tbLuLSFavo.Text = l.SFavo;
        cbLuLAktiv.IsChecked = l.IstAktiv;
        tbLuLSeriennummer.Text = l.Seriennummer;
        tbLuLBemerkung.Text = l.Bemerkung;
    }

    private void LoadKursData(Kurs k)
    {
        if (string.IsNullOrEmpty(k.Bezeichnung)) return;
        tbKursbezeichnung.Text = k.Bezeichnung;
        tbKursLuL.Text = _myschool.GetLuLAusKurs(k.Bezeichnung).Result
            .Aggregate("", (current, lul) => $"{current}{lul.Kuerzel},").TrimEnd(',');
        tbKursFach.Text = k.Fach;
        tbKursSuffix.Text = k.Suffix;
        tbKursKlasse.Text = k.Klasse;
        tbKursStufe.Text = k.Stufe;
        cbKursIstKurs.IsChecked = k.IstKurs;
        tbKursBemerkung.Text = k.Bemerkung;
    }

    private async void BtnExport_OnClick(object? sender, RoutedEventArgs e)
    {
        if (cbMoodle.IsChecked != null && !cbMoodle.IsChecked.Value && cbAIX.IsChecked != null &&
            !cbAIX.IsChecked.Value && cbAJAMF.IsChecked != null && !cbAJAMF.IsChecked.Value)
        {
            await ShowCustomErrorMessage("Bitte wählen Sie entweder Moodle und/oder AIX und/oder JAMF als Zielsystem!",
                "Kein Zielsystem ausgewählt");
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(ReadFileTask);
        return;

        async Task ReadFileTask()
        {
            var folder = await ShowOpenFolderDialog("Bitte den Ordner mit den Dateien auswählen");
            if (folder == null) return;
            var folderpath = folder.Path.LocalPath;
            var expandFiles = 1;
            if (File.Exists($"{folderpath}/aix_sus.csv") || File.Exists($"{folderpath}/aix_lul.csv") ||
                File.Exists($"{folderpath}/mdl_einschreibungen.csv") ||
                File.Exists($"{folderpath}/mdl_kurse.csv") || File.Exists($"{folderpath}/mdl_nutzer.csv") ||
                File.Exists($"{folderpath}/jamf_sus.csv") ||
                File.Exists($"{folderpath}/jamf_lul.csv") ||
                File.Exists($"{folderpath}/jamf_teacher_groups.csv"))
            {
                var overwriteFilesDialog = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
                {
                    ButtonDefinitions = ButtonEnum.YesNoAbort,
                    ContentTitle = "Dateien gefunden",
                    ContentHeader = "Überschreiben?",
                    ContentMessage =
                        "Im Ordner existieren schon eine/mehrere Exportdateien.\nSollen diese überschrieben werden?\nJa = überschreiben, Nein = erweitern, Abbrechen = nichts machen",
                    Icon = MsBox.Avalonia.Enums.Icon.Question,
                    WindowIcon = _msgBoxWindowIcon
                });
                var dialogResult = await overwriteFilesDialog.ShowAsPopupAsync(this);
                expandFiles = dialogResult switch
                {
                    ButtonResult.Yes => 1,
                    ButtonResult.No => 0,
                    ButtonResult.Abort => -1,
                    _ => 1
                };
            }

            if (expandFiles == -1) return;
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

            if (cbAJAMF.IsChecked != null && cbAJAMF.IsChecked.Value)
            {
                destsys += "j";
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

            var res = await _myschool.ExportToCSV(folderpath, destsys, whattoexport,
                cbExportwithPasswort.IsChecked != null && cbExportwithPasswort.IsChecked.Value, "", expandFiles == 0,
                nurMoodleSuffix, kursvorlagen, new ReadOnlyCollection<int>(_myschool.GetSchuelerIDListe().Result),
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
                //TODO: Fehlerforsetzen
                ergebnisliste.Add("######BEGIN Leere Kurse######");
                ergebnisliste.Add("Kursbezeichnung; Fehler");
                foreach (var k in kursliste)
                {
                    if (_myschool.GetSuSAusKurs(k.Bezeichnung).Result.Count == 0)
                    {
                        ergebnisliste.Add($"{k.Bezeichnung};ohne SuS");
                    }

                    if (_myschool.GetLuLAusKurs(k.Bezeichnung).Result.Count == 0)
                    {
                        ergebnisliste.Add($"{k.Bezeichnung} ohne LuL");
                    }
                }
            }

            if (cbFehlerSuSoK.IsChecked != null && cbFehlerSuSoK.IsChecked.Value)
            {
                ergebnisliste.Add("######BEGIN SUS OHNE KURSE######");
                ergebnisliste.Add("Nachname, Vorname; ID; Fehler");
                var susOhneKurse = from sus in _myschool.GetSchuelerListe().Result
                    where _myschool.GetKurseVonSuS(Convert.ToInt32(sus.ID)).Result.Count == 0
                    select $"{sus.Nachname}, {sus.Vorname};{sus.ID};ohne Kurs";
                var ohneKurse = susOhneKurse as string[] ?? susOhneKurse.ToArray();
                if (ohneKurse.Length != 0)
                {
                    ergebnisliste.AddRange(ohneKurse);
                }
            }

            if (cbFehlerLuLoK.IsChecked != null && cbFehlerLuLoK.IsChecked.Value)
            {
                ergebnisliste.Add("######BEGIN LUL OHNE KURSE######");
                ergebnisliste.Add("Nachname, Vorname; ID; Fehler");
                var lulOhneKurse = from lul in _myschool.GetLehrerListe().Result
                    where _myschool.GetKurseVonLuL(Convert.ToInt32(lul.ID)).Result.Count == 0
                    select $"{lul.Nachname}, {lul.Vorname};{lul.ID};ohne Kurs";
                var ohneKurse = lulOhneKurse as string[] ?? lulOhneKurse.ToArray();
                if (ohneKurse.Length != 0)
                {
                    ergebnisliste.AddRange(ohneKurse);
                }
            }

            if (cbFehlerLuL.IsChecked != null && cbFehlerLuL.IsChecked.Value)
            {
                ergebnisliste.Add("######BEGIN LUL-Fehler######");
                ergebnisliste.Add("Nachname, Vorname; ID; Fehler");
                ergebnisliste.AddRange(from lul in _myschool.GetLehrerListe().Result
                    where lul.Fakultas.Contains("NV")
                    select $"{lul.Nachname}, {lul.Vorname};{lul.ID};mit fehlerhafter Fakultas");
                ergebnisliste.AddRange(from lul in _myschool.GetLehrerListe().Result
                    where string.IsNullOrEmpty(lul.Seriennummer)
                    select $"{lul.Nachname}, {lul.Vorname};{lul.ID};mit fehlender Seriennummer");
            }

            if (cbFehlerKurse.IsChecked != null && cbFehlerKurse.IsChecked.Value)
            {
                ergebnisliste.Add("######BEGIN KURS-FEHLER######");
                ergebnisliste.Add("Kursbezeichnung;Fehler");
                var whitelist = new[]
                {
                    "Erprobungsstufe",
                    "Mittelstufe", "Einführungsphase",
                    "Qualifikationsphase 1", "Qualifikationsphase 2"
                };
                var kurscache = _myschool.GetKursListe().Result;
                ergebnisliste.AddRange(from kurs in kurscache
                    where kurs.Bezeichnung.Length < 3
                    select $"{kurs.Bezeichnung};Zu kurze Bezeichnung (Länge < 3)");
                ergebnisliste.AddRange(from kurs in kurscache
                    where !whitelist.Contains(kurs.Bezeichnung) && (kurs.Fach.Length == 0 || kurs.Fach.Equals("---"))
                    select $"{kurs.Bezeichnung};Fehlerhaftes Fach");
            }

            if (cbFehlerSuS.IsChecked != null && cbFehlerSuS.IsChecked.Value)
            {
                ergebnisliste.Add("######BEGIN SUS-FEHLER######");
                ergebnisliste.Add("Nachname, Vorname; Klasse; ID; Fehler;add1;add2");
                foreach (var sus in susliste.Where(s=>s.IstAktiv))
                {
                    if (sus.Nutzername.Equals(""))
                    {
                        ergebnisliste.Add(
                            $"{sus.Nachname}, {sus.Vorname};Klasse {sus.Klasse};{sus.ID};ohne Nutzernamen");
                    }

                    if (!sus.HasM365Account && sus.Aixmail != "")
                    {
                        ergebnisliste.Add(
                            $"{sus.Nachname}, {sus.Vorname};Klasse {sus.Klasse};{sus.ID};ohne M365 Zustimmung, aber mit M365-Adresse");
                    }
                    
                    var mailsuffixes = _myschool.GetSettings().Result.Mailsuffix;
                    if (string.IsNullOrEmpty(sus.Mail) || sus.Mail == sus.ID + mailsuffixes)
                    {
                        ergebnisliste.Add(
                            $"{sus.Nachname}, {sus.Vorname};Klasse {sus.Klasse};{sus.ID};ohne gültige Mailadresse");
                    }

                    if (sus.Zweitaccount && (sus.Zweitmail == "" || sus.Zweitmail == sus.Mail))
                    {
                        ergebnisliste.Add(
                            $"{sus.Nachname}, {sus.Vorname};Klasse {sus.Klasse};{sus.ID};ohne gültige Zweitmailadresse");
                    }

                    if (_myschool.Jamfstufen.Contains(sus.GetStufe()) && string.IsNullOrEmpty(sus.Seriennummer))
                    {
                        var mathekurse = _myschool.GetKurseVonSuS(sus.ID).Result.Where(k => k.Fach == "M").ToArray();
                        if (mathekurse.Length == 0)
                        {
                            _myschool.AddLogMessage(new LogEintrag
                            {
                                Eintragsdatum = DateTime.Now, Nachricht = $"SuS {sus.Vorname} {sus.Nachname}, {sus.ID} ohne Mathekurs",
                                Warnstufe = "Hinweis"
                            });
                            continue;
                        }

                        var mathekurs = mathekurse.Length == 1 ? mathekurse[0] : mathekurse[1];
                        var mathelehrkraft = _myschool.GetLuLAusKurs(mathekurs.Bezeichnung).Result
                            .Select(l => l.Kuerzel).ToArray();
                        ergebnisliste.Add(
                            $"{sus.Nachname}, {sus.Vorname};Klasse {sus.Klasse};{sus.ID};ohne Seriennummer in JAMF-Stufe {sus.GetStufe()} im Kurs;{mathekurs.Bezeichnung};" +
                            string.Join(",", mathelehrkraft));
                    }

                    ergebnisliste.AddRange(from kurs in _myschool.GetKurseVonSuS(sus.ID).Result
                        where !(kurs.Bezeichnung.Contains("stufe", StringComparison.CurrentCultureIgnoreCase) ||
                                kurs.Bezeichnung.Contains("stubo", StringComparison.CurrentCultureIgnoreCase)) &&
                              kurs.Stufe != sus.GetStufe()
                        select
                            $"{sus.Nachname}, {sus.Vorname};Klasse {sus.Klasse};{sus.ID};in Kurs {kurs.Bezeichnung} trotz Klasse {sus.Klasse}");
                }

                ergebnisliste.AddRange(_myschool.GetM365Blacklist().Result
                    .Select(susid => _myschool.GetSchueler(susid).Result).Select(sus =>
                        $"{sus.Nachname}, {sus.Vorname};Klasse {sus.Klasse};{sus.ID};ohne DV Zustimmung"));
            }
            
            if (cbSuSBemerkung.IsChecked != null && cbSuSBemerkung.IsChecked.Value)
            {
                ergebnisliste.Add("######BEGIN Bemerkungen######");
                ergebnisliste.Add("Nachname, Vorname; Klasse; ID; Fehler");
                var sus_bemerkungen = susliste.Where(s => s.Bemerkung != "");
                ergebnisliste.AddRange(from sus in sus_bemerkungen
                    select
                        $"{sus.Nachname}, {sus.Vorname};Klasse {sus.Klasse};{sus.ID};mit Bemerkung \"{sus.Bemerkung}\"");
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
            _myschool.AddLogMessage(new LogEintrag
                { Eintragsdatum = DateTime.Now, Nachricht = ex.Message, Warnstufe = "Debug" });
#endif
            _myschool.AddLogMessage(new LogEintrag
            {
                Eintragsdatum = DateTime.Now, Nachricht = $"Fehler bei der Fehlersuche {ex.Message}",
                Warnstufe = "Fehler"
            });
        }
    }

    private async void BtnFehlerExport_OnClick(object? sender, RoutedEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(SaveDbFile);
        return;

        async Task SaveDbFile()
        {
            var extx = new List<FilePickerFileType> { StSFileTypes.CSVFile };
            var files = await ShowSaveFileDialog("Bitte einen Dateipfad angeben...", extx);
            if (files == null) return;
            var filepath = files.Path.LocalPath;

            await File.WriteAllLinesAsync(filepath, lbFehlerliste.Items.Cast<string>(), Encoding.UTF8);
            await ShowCustomSuccessMessage("Speichern erfolgreich", "Erfolg");
        }
    }

    private async void BtnExportStufenkurs_OnClick(object? sender, RoutedEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(ReadFileTask);
        return;

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
                res = await _myschool.ExportToCSV(folderpath, "all", "s", false, "", false, nurMoodleSuffix,
                    ["", ""], new ReadOnlyCollection<int>([..susidlist]),
                    new ReadOnlyCollection<int>([]),
                    new ReadOnlyCollection<string>([]));
            }
            else
            {
                var stufen = tbExportStufenkurse.Text.Split(';');
                foreach (var stufe in stufen)
                {
                    susidlist.AddRange(_myschool.GetSusAusStufe(stufe).Result.Select(s => s.ID).ToList());
                }

                res = await _myschool.ExportToCSV(folderpath, "all", "s", false, "", false, nurMoodleSuffix,
                    ["", ""], new ReadOnlyCollection<int>([..susidlist]),
                    new ReadOnlyCollection<int>([]),
                    new ReadOnlyCollection<string>([]));
            }

            await CheckSuccesfulExport(res);
        }
    }

    private async void BtnExportJAMFStufenkurs_OnClick(object? sender, RoutedEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(ReadFileTask);
        return;

        async Task ReadFileTask()
        {
            if (string.IsNullOrEmpty(tbExportStufenkurse.Text)) return;
            var folder = await ShowOpenFolderDialog("Bitte den Ordner mit den Dateien auswählen");
            if (folder == null) return;
            var folderpath = folder.Path.LocalPath;
            List<string> ausgabeJamf = ["Username;Email;FirstName;LastName;SerialNumber;Groups;TeacherGroups"];
            if (!tbExportStufenkurse.Text.Contains(';'))
            {
                var stufe = tbExportStufenkurse.Text;
                ausgabeJamf.AddRange(from sus in _myschool.GetSusAusStufe(stufe).Result
                    where sus.AllowJAMF
                    where _myschool.Jamfstufen.Contains(sus.GetStufe())
                    let kbez_liste =
                        _myschool.GetKurseVonSuS(sus.ID).Result.Where(k => !k.Bezeichnung.EndsWith("KL")).ToList()
                            .Select(k => k.Bezeichnung).ToList()
                    select string.Join(";", sus.Nutzername, !string.IsNullOrEmpty(sus.Aixmail) ? sus.Aixmail : sus.Mail,
                        sus.Vorname, sus.Nachname, sus.Seriennummer, string.Join(',', kbez_liste), ""));
                ausgabeJamf.AddRange(from lul in _myschool.GetLuLAusStufe(stufe).Result
                    let kurse = _myschool.GetKurseVonLuL(lul.ID)
                        .Result.Where(x =>
                            !string.IsNullOrEmpty(x.Fach) && stufe == x.Stufe && !x.Bezeichnung.EndsWith("KL"))
                        .Select(x => x.Bezeichnung)
                    select string.Join(";", lul.Kuerzel, lul.Mail, lul.Vorname, lul.Nachname, lul.Seriennummer,
                        "Lehrer-605",
                        string.Join(',', kurse)));
            }
            else
            {
                var stufen = tbExportStufenkurse.Text.Split(';');
                foreach (var stufe in stufen)
                {
                    ausgabeJamf.AddRange(from sus in _myschool.GetSusAusStufe(stufe).Result
                        where sus.AllowJAMF
                        where _myschool.Jamfstufen.Contains(sus.GetStufe())
                        let kbez_liste =
                            _myschool.GetKurseVonSuS(sus.ID).Result.Where(k => !k.Bezeichnung.EndsWith("KL")).ToList()
                                .Select(k => k.Bezeichnung).ToList()
                        select string.Join(";", sus.Nutzername,
                            !string.IsNullOrEmpty(sus.Aixmail) ? sus.Aixmail : sus.Mail,
                            sus.Vorname, sus.Nachname, sus.Seriennummer, string.Join(',', kbez_liste), ""));
                    ausgabeJamf.AddRange(from lul in _myschool.GetLuLAusStufe(stufe).Result
                        let kurse = _myschool.GetKurseVonLuL(lul.ID)
                            .Result.Where(x =>
                                !string.IsNullOrEmpty(x.Fach) && stufe == x.Stufe &&
                                !x.Bezeichnung.EndsWith("KL"))
                            .Select(x => x.Bezeichnung)
                        select string.Join(";", lul.Kuerzel, lul.Mail, lul.Vorname, lul.Nachname, lul.Seriennummer,
                            "Lehrer-605",
                            string.Join(',', kurse)));
                }
            }

            await File.WriteAllLinesAsync(folderpath + "/jamf_import.csv", ausgabeJamf);
        }
    }

    private async Task CheckSuccesfulExport(int res)
    {
        if (res == 1)
        {
            await ShowCustomSuccessMessage("Der Export war erfolgreich", "Erfolg");
        }
        else
        {
            await ShowCustomErrorMessage("Export war nicht erfolgreiche. Bitte im Log nachschauen", "Fehler");
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
            var res = await _myschool.ExportToCSV(folderpath, "all", "s", true, "", false, nurMoodleSuffix,
                ["", ""],
                new ReadOnlyCollection<int>([.._myschool.GetSusAusStufe("5").Result.Select(s => s.ID).ToList()]),
                new ReadOnlyCollection<int>([]), new ReadOnlyCollection<string>([]));
            await CheckSuccesfulExport(res);
        }
    }

    private async void BtnSettingSave_OnClick(object? sender, RoutedEventArgs e)
    {
        Einstellungen settings = new()
        {
            Mailsuffix = string.IsNullOrEmpty(tbSettingMailplatzhalter.Text)
                ? "@local.domain"
                : tbSettingMailplatzhalter.Text,
            Fachersetzung = string.IsNullOrEmpty(tbSettingKursersetzung.Text)
                ? ""
                : tbSettingKursersetzung.Text,
            Kurzfaecher = string.IsNullOrEmpty(tbSettingFachkurz.Text)
                ? [""]
                : tbSettingFachkurz.Text.Split('\n'),
            Langfaecher = string.IsNullOrEmpty(tbSettingFachlang.Text)
                ? [""]
                : tbSettingFachlang.Text.Split('\n'),
            Kurssuffix = string.IsNullOrEmpty(tbSettingKurssuffix.Text)
                ? ""
                : tbSettingKurssuffix.Text,
            Erprobungstufenleitung = string.IsNullOrEmpty(tbSettingErprobungsstufenleitung.Text)
                ? ""
                : tbSettingErprobungsstufenleitung.Text.Replace(';', ',').TrimEnd(','),
            Mittelstufenleitung = string.IsNullOrEmpty(tbSettingMittelstufenleitung.Text)
                ? ""
                : tbSettingMittelstufenleitung.Text.Replace(';', ',').TrimEnd(','),
            EFStufenleitung = string.IsNullOrEmpty(tbSettingEFstufenleitung.Text)
                ? ""
                : tbSettingEFstufenleitung.Text.Replace(';', ',').TrimEnd(','),
            Q1Stufenleitung = string.IsNullOrEmpty(tbSettingQ1Stufenleitung.Text)
                ? ""
                : tbSettingQ1Stufenleitung.Text.Replace(';', ',').TrimEnd(','),
            Q2Stufenleitung = string.IsNullOrEmpty(tbSettingQ2Stufenleitung.Text)
                ? ""
                : tbSettingQ2Stufenleitung.Text.Replace(';', ',').TrimEnd(','),
            Oberstufenkoordination = string.IsNullOrEmpty(tbSettingOberstufenkoordination.Text)
                ? ""
                : tbSettingOberstufenkoordination.Text.Replace(';', ',').TrimEnd(','),
            StuBos = string.IsNullOrEmpty(tbSettingStuBos.Text)
                ? ""
                : tbSettingStuBos.Text.Replace(';', ',').TrimEnd(','),
            Erprobungsstufe = string.IsNullOrEmpty(tbSettingErprobungsstufen.Text)
                ? []
                : tbSettingErprobungsstufen.Text.Split(','),
            Mittelstufe = string.IsNullOrEmpty(tbSettingMittelstufen.Text) ? [] : tbSettingMittelstufen.Text.Split(','),
            Oberstufe = string.IsNullOrEmpty(tbSettingOberstufe.Text) ? [] : tbSettingOberstufe.Text.Split(','),
            StuboStufen = string.IsNullOrEmpty(tbSettingStuBoStufen.Text) ? [] : tbSettingStuBoStufen.Text.Split(','),
            JAMFStufen = string.IsNullOrEmpty(tbSettingJAMFStufen.Text) ? [] : tbSettingJAMFStufen.Text.Split(',')
        };

        await _myschool.SetSettings(settings);
        LoadSettingsToGUI(settings);
        await _myschool.StartTransaction();
        if (!_myschool.GibtEsKurs($"Erprobungsstufe{settings.Kurssuffix}"))
        {
            await _myschool.AddKurs("Erprobungsstufe", "", "", "", settings.Kurssuffix, 1, "");
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

        if (!_myschool.GibtEsKurs($"Mittelstufe{settings.Kurssuffix}"))
        {
            await _myschool.AddKurs("Mittelstufe", "", "", "", settings.Kurssuffix, 1, "");
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

        if (!_myschool.GibtEsKurs($"Einführungsphase{settings.Kurssuffix}"))
        {
            await _myschool.AddKurs("Einführungsphase", "", "EF", "EF", settings.Kurssuffix, 1, "");
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

        if (!_myschool.GibtEsKurs($"Qualifikationsphase 1{settings.Kurssuffix}"))
        {
            await _myschool.AddKurs("Qualifikationsphase 1", "", "Q1", "Q1", settings.Kurssuffix, 1, "");
            foreach (var s in await _myschool.GetSusAusStufe("Q1"))
            {
                await _myschool.AddStoK(s.ID, "Qualifikationsphase 1");
            }
        }

        if (!string.IsNullOrEmpty(tbSettingQ1Stufenleitung.Text))
        {
            if (tbSettingQ1Stufenleitung.Text.Contains(','))
            {
                foreach (var krz in tbSettingQ1Stufenleitung.Text.Split(','))
                {
                    await _myschool.AddLtoK(await _myschool.GetLehrkraft(krz),
                        await _myschool.GetKurs("Qualifikationsphase 1"));
                }
            }
            else
            {
                await _myschool.AddLtoK(await _myschool.GetLehrkraft(tbSettingQ1Stufenleitung.Text),
                    await _myschool.GetKurs("Qualifikationsphase 1"));
            }
        }

        if (!_myschool.GibtEsKurs($"Qualifikationsphase 2{settings.Kurssuffix}"))
        {
            await _myschool.AddKurs("Qualifikationsphase 2", "", "Q2", "Q2", settings.Kurssuffix, 1, "");
            foreach (var s in await _myschool.GetSusAusStufe("Q2"))
            {
                await _myschool.AddStoK(s.ID, "Qualifikationsphase 2");
            }
        }

        if (!string.IsNullOrEmpty(tbSettingQ2Stufenleitung.Text))
        {
            if (tbSettingQ2Stufenleitung.Text.Contains(','))
            {
                foreach (var krz in tbSettingQ2Stufenleitung.Text.Split(','))
                {
                    await _myschool.AddLtoK(await _myschool.GetLehrkraft(krz),
                        await _myschool.GetKurs("Qualifikationsphase 2"));
                }
            }
            else
            {
                await _myschool.AddLtoK(await _myschool.GetLehrkraft(tbSettingQ2Stufenleitung.Text),
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
                kurs.IstKurs ? 1 : 0, kurs.Bemerkung);
        }

        if (!string.IsNullOrEmpty(tbSettingStuBos.Text))
        {
            var stubo_krz = tbSettingStuBos.Text.Split(',').ToList();
            var kurscache = _myschool.GetKursBezListe().Result.Where(k => k.StartsWith("StuBo")).ToList();
            if (kurscache.Count > 0)
            {
                foreach (var kursbez in kurscache)
                {
                    var kurs = _myschool.GetKurs(kursbez).Result;
                    if (string.IsNullOrEmpty(kurs.Bezeichnung)) continue;
                    foreach (var krz in stubo_krz)
                    {
                        await _myschool.AddLtoK(_myschool.GetLehrkraft(krz).Result.ID, kursbez);
                    }

                    var aktuelle_lul_liste = await _myschool.GetLuLAusKurs(kurs.Bezeichnung);
                    foreach (var l in aktuelle_lul_liste)
                    {
                        if (!stubo_krz.Contains(l.Kuerzel))
                        {
                            await _myschool.RemoveLfromK(l.ID, kurs.Bezeichnung);
                        }
                    }

                    foreach (var sus in await _myschool.GetSusAusStufe(kurs.Stufe))
                    {
                        await _myschool.AddStoK(sus.ID, kurs.Bezeichnung);
                    }
                }
            }
            else
            {
                foreach (var stufe in _myschool.Stubostufen)
                {
                    await _myschool.AddKurs($"StuBo-{stufe}", "StuBo", stufe, stufe,
                        _myschool.GetSettings().Result.Kurssuffix, 1, "");
                    foreach (var sus in _myschool.GetSusAusStufe(stufe).Result)
                    {
                        await _myschool.AddStoK(sus.ID, $"StuBo-{stufe}");
                    }

                    foreach (var krz in stubo_krz)
                    {
                        await _myschool.AddLtoK(_myschool.GetLehrkraft(krz).Result.ID, $"StuBo-{stufe}");
                    }
                }
            }
        }

        var kursBez = "Jahrgangsstufenkonferenz EF";
        var settingsCache = await _myschool.GetSettings();
        if (!string.IsNullOrEmpty(settingsCache.EFStufenleitung))
        {
            if (!_myschool.GibtEsKurs(kursBez))
            {
                await _myschool.AddKurs(kursBez, "-", "EF", "EF", settingsCache.Kurssuffix, 1, "");
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
            if (!_myschool.GibtEsKurs(kursBez))
            {
                await _myschool.AddKurs(kursBez, "-", "Q1", "Q1", settingsCache.Kurssuffix, 1, "");
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
        if (!_myschool.GibtEsKurs(kursBez))
        {
            await _myschool.AddKurs(kursBez, "-", "Q2", "Q2", settingsCache.Kurssuffix, 1, "");
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
        await ShowCustomSuccessMessage("Einstellungen erfolgreich angewendet!", "Erfolg");
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
        ResetItemsSource(lbLogDisplay, []);
        await _myschool.LoescheLog();
    }

    private void BtnLogReload_OnClick(object? sender, RoutedEventArgs e)
    {
        if (lbLogLevel.SelectedItems == null) return;
        var items = _myschool.GetLog();
        var tlist = new List<string>();
        foreach (ListBoxItem item in lbLogLevel.SelectedItems)
        {
            if (item?.Content != null)
            {
                tlist.Add(item.Content.ToString() ?? throw new InvalidOperationException());
            }
        }

        var filtered_items = items.Result.Where(x => tlist.Contains(x.Warnstufe));
        ResetItemsSource(lbLogDisplay, filtered_items.Select(x => x.ToString()));
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
        var kursBemerkung = tbKursBemerkung.Text ?? "";
        if (string.IsNullOrEmpty(kursbez) || string.IsNullOrEmpty(lehrkraefte) || string.IsNullOrEmpty(kursfach) ||
            string.IsNullOrEmpty(kursklasse) || string.IsNullOrEmpty(kursstufe))
        {
            await ShowCustomErrorMessage(
                "Nicht alle erforderlichen Informationen angegeben!\nStellen Sie sicher, dass Kursbezeichnung, mind. ein Kürzel, das Fach, die Klasse und die Stufe ausgefüllt sind.",
                "Fehler");
            return;
        }

        if (_myschool.GibtEsKurs(kursbez))
        {
            await _myschool.UpdateKurs(kursbez, kursfach, kursklasse, kursstufe, kurssuffix,
                Convert.ToInt32(istKurs), kursBemerkung);
            List<Lehrkraft> tList = [];
            foreach (var lehrkraft in lehrkraefte.Split(','))
            {
                tList.Add(await _myschool.GetLehrkraft(lehrkraft.Trim()));
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
            await _myschool.AddKurs(kursbez, kursfach, kursklasse, kursstufe, kurssuffix, Convert.ToInt32(istKurs),
                kursBemerkung);
        }

        foreach (var lehrkraft in lehrkraefte.Split(','))
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
            foreach (var stufe in kursstufe.Split(','))
            {
                foreach (var sus in await _myschool.GetSusAusStufe(stufe.Trim()))
                {
                    await _myschool.AddStoK(sus, await _myschool.GetKurs(kursbez));
                }
            }
        }

        if (cbKursMarkierteSuSEinschreiben.IsChecked != null &&
            cbKursMarkierteSuSEinschreiben.IsChecked.Value && leftListBox.SelectedItems != null)
        {
            foreach (var susstring in leftListBox.SelectedItems.Cast<string>())
            {
                if (string.IsNullOrEmpty(susstring)) continue;
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
        if (cboxDataLeft.SelectedIndex == 2)
        {
            source = leftListBox;
        }
        else if (cboxDataRight.SelectedIndex == 2)
        {
            source = rightListBox;
        }
        else
        {
            await _myschool.RemoveK(kursbez);
            return;
        }

        if (source.SelectedItems == null) return;
        var tmp_list = source.SelectedItems;
        await _myschool.StartTransaction();
        foreach (var kurs in tmp_list.Cast<string>())
        {
            if (string.IsNullOrEmpty(kurs)) return;
            await _myschool.RemoveK(kurs);
        }

        await _myschool.StopTransaction();
        OnLeftDataChanged(true);
        OnRightDataChanged(true);
        SetStatusText();
    }

    private async void OnLeftTimedEvent(object? source, ElapsedEventArgs e)
    {
        await CallLeftTimer();
    }

    private async Task CallLeftTimer()
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

            if (_cbZeigeInaktiv.IsChecked == null || _cbZeigeNurBemerkungen.IsChecked == null) return;
            var zeigeInaktive = _cbZeigeInaktiv.IsChecked.Value;
            var zeigeNurBemerkungen = _cbZeigeNurBemerkungen.IsChecked.Value;
            tbLeftSearch.Text = tbLeftSearch.Text.TrimStart(' ').TrimEnd(' ');
            var eingabeliste = tbLeftSearch.Text.Split(";");
            if (tbLeftSearch.ContextMenu?.ItemsSource == null) return;
            var searchContextMenu = tbLeftSearch.ContextMenu.ItemsSource.Cast<CheckBox>().ToList();
            var searchFields = new[]
                { false, false, false, false, false, false, false, false, false }; //v,n,m,a/k,i,s,e,ia,b
            for (var i = 0; i < searchContextMenu.Count; ++i)
            {
                if (searchContextMenu[i].IsChecked == true)
                {
                    searchFields[i] = true;
                }
            }

            switch (cboxDataLeft.SelectedIndex)
            {
                case 0:
                    var sliste = new List<SuS>();
                    var scachelist = _myschool.GetSchuelerListe().Result
                        .Where(s => s.IstAktiv || zeigeInaktive)
                        .ToList();
                    if (zeigeNurBemerkungen)
                    {
                        scachelist = scachelist.Where(s => s.Bemerkung != "").ToList();
                    }

                    foreach (var eingabe in eingabeliste)
                    {
                        var lowereingabe = eingabe.ToLower();
                        sliste.AddRange(searchFields[6]
                            ? scachelist.Where(s =>
                                searchFields[5] && (s.ID + "").Equals(lowereingabe) ||
                                searchFields[0] && s.Vorname.ToLower().Equals(lowereingabe) ||
                                searchFields[1] && s.Nachname.ToLower().Equals(lowereingabe) ||
                                searchFields[2] &&
                                (s.Mail.Equals(lowereingabe) || s.Aixmail.Equals(lowereingabe) ||
                                 s.Zweitmail.Equals(lowereingabe)) ||
                                searchFields[3] && s.Nutzername.Equals(lowereingabe) ||
                                searchFields[4] &&
                                s.Seriennummer.Contains(eingabe, StringComparison.CurrentCultureIgnoreCase)).ToList()
                            : scachelist.Where(s =>
                                searchFields[5] && (s.ID + "").Contains(lowereingabe) ||
                                searchFields[0] && s.Vorname.Contains(lowereingabe,
                                    StringComparison.CurrentCultureIgnoreCase) ||
                                searchFields[1] && s.Nachname.Contains(lowereingabe,
                                    StringComparison.CurrentCultureIgnoreCase) ||
                                searchFields[2] && (s.Mail.Contains(lowereingabe) ||
                                                    s.Aixmail.Contains(lowereingabe) ||
                                                    s.Zweitmail.Contains(lowereingabe)) ||
                                searchFields[3] && s.Nutzername.Contains(lowereingabe) ||
                                searchFields[4] &&
                                s.Seriennummer.Contains(eingabe, StringComparison.CurrentCultureIgnoreCase)).ToList());
                    }

                    var seliste = sliste.Distinct()
                        .Select(s => $"{s.Nachname},{s.Vorname};{s.ID}")
                        .ToList();
                    seliste.Sort(Comparer<string>.Default);
                    ResetItemsSource(leftListBox, seliste);
                    break;
                case 1:
                    var lliste = new List<Lehrkraft>();
                    var cachlist = _myschool.GetLehrerListe().Result.Where(s => s.IstAktiv || zeigeInaktive).ToList();
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
                                searchFields[5] && (l.ID + "").Equals(lowereingabe)
                                ||
                                searchFields[4] &&
                                l.Seriennummer.Contains(eingabe, StringComparison.CurrentCultureIgnoreCase)).ToList()
                            : cachlist.Where(l =>
                                l.Kuerzel.Contains(lowereingabe, StringComparison.CurrentCultureIgnoreCase) ||
                                searchFields[0] && l.Vorname.Contains(lowereingabe,
                                    StringComparison.CurrentCultureIgnoreCase) ||
                                searchFields[1] && l.Nachname.Contains(lowereingabe,
                                    StringComparison.CurrentCultureIgnoreCase) ||
                                searchFields[2] && l.Mail.Contains(lowereingabe) ||
                                searchFields[3] && l.Kuerzel.Contains(lowereingabe) ||
                                searchFields[5] && (l.ID + "").Contains(lowereingabe) ||
                                searchFields[4] &&
                                l.Seriennummer.Contains(eingabe, StringComparison.CurrentCultureIgnoreCase)).ToList());
                    }

                    var leliste = lliste.Distinct()
                        .Select(l => $"{l.Kuerzel};{l.Nachname},{l.Vorname}")
                        .ToList();
                    leliste.Sort(Comparer<string>.Default);
                    ResetItemsSource(leftListBox, leliste);
                    break;
                case 2:
                    var kliste = new List<Kurs>();
                    var kcachelist = _myschool.GetKursListe().Result;
                    foreach (var eingabe in eingabeliste)
                    {
                        kliste.AddRange(kcachelist
                            .Where(s => s.Bezeichnung.Contains(eingabe, StringComparison.CurrentCultureIgnoreCase))
                            .ToList());
                    }

                    var keliste = kliste.Distinct()
                        .Select(k => k.Bezeichnung)
                        .ToList();
                    keliste.Sort(Comparer<string>.Default);
                    ResetItemsSource(leftListBox, keliste);
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

            tbRightSearch.Text = tbRightSearch.Text.TrimStart(' ').TrimEnd(' ');
            var eingabeliste = tbRightSearch.Text.Split(";");
            if (tbLeftSearch?.ContextMenu?.ItemsSource == null) return;
            var searchContextMenu = tbLeftSearch.ContextMenu.ItemsSource.Cast<CheckBox>().ToList();
            var searchFields = new[] { false, false, false, false, false, false, false }; //v,n,m,a/k,i,e,ia
            for (var i = 0; i < searchContextMenu.Count; ++i)
            {
                if (searchContextMenu[i].IsChecked == true)
                {
                    searchFields[i] = true;
                }
            }

            switch (cboxDataRight.SelectedIndex)
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
                                searchFields[0] && s.Vorname.Contains(lowereingabe,
                                    StringComparison.CurrentCultureIgnoreCase) ||
                                searchFields[1] && s.Nachname.Contains(lowereingabe,
                                    StringComparison.CurrentCultureIgnoreCase) ||
                                searchFields[2] && (s.Mail.Contains(lowereingabe) ||
                                                    s.Aixmail.Contains(lowereingabe) ||
                                                    s.Zweitmail.Contains(lowereingabe)) ||
                                searchFields[3] && s.Nutzername.Contains(lowereingabe)).ToList());
                    }

                    var seliste = sliste.Distinct()
                        .Select(s => $"{s.Nachname},{s.Vorname};{s.ID}")
                        .ToList();
                    seliste.Sort(Comparer<string>.Default);
                    ResetItemsSource(rightListBox, seliste);
                    break;
                case 1:
                    var lliste = new List<Lehrkraft>();
                    var lcachelist = _myschool.GetLehrerListe().Result;
                    foreach (var eingabe in eingabeliste)
                    {
                        var lowereingabe = eingabe.ToLower();
                        lliste.AddRange(searchFields[5]
                            ? lcachelist.Where(l =>
                                l.Kuerzel.ToLower().Equals(lowereingabe) ||
                                searchFields[0] && l.Vorname.ToLower().Equals(lowereingabe) ||
                                searchFields[1] && l.Nachname.ToLower().Equals(lowereingabe) ||
                                searchFields[2] && l.Mail.Equals(lowereingabe) ||
                                searchFields[3] && l.Kuerzel.Equals(lowereingabe) ||
                                searchFields[4] && (l.ID + "").Equals(lowereingabe)).ToList()
                            : lcachelist.Where(l =>
                                l.Kuerzel.Contains(lowereingabe, StringComparison.CurrentCultureIgnoreCase) ||
                                searchFields[0] && l.Vorname.Contains(lowereingabe,
                                    StringComparison.CurrentCultureIgnoreCase) ||
                                searchFields[1] && l.Nachname.Contains(lowereingabe,
                                    StringComparison.CurrentCultureIgnoreCase) ||
                                searchFields[2] && l.Mail.Contains(lowereingabe) ||
                                searchFields[3] && l.Kuerzel.Contains(lowereingabe) ||
                                searchFields[4] && (l.ID + "").Contains(lowereingabe)).ToList());
                    }

                    var leliste = lliste.Distinct()
                        .Select(l => $"{l.Kuerzel};{l.Nachname},{l.Vorname}")
                        .ToList();
                    leliste.Sort(Comparer<string>.Default);
                    ResetItemsSource(rightListBox, leliste);
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
                    ResetItemsSource(rightListBox, keliste);
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
            if (leftListBox.SelectedItems == null) return;
            var extx = new List<FilePickerFileType> { StSFileTypes.CSVFile };
            var files = await ShowSaveFileDialog("Bitte einen Dateipfad angeben...", extx);
            if (files == null) return;
            var file_path = files.Path.LocalPath;
            List<string> ausgabe;
            switch (cboxDataLeft.SelectedIndex)
            {
                case 0:
                    ausgabe = ["Vorname;Nachname;Anmeldename;Kennwort;E-Mail;Klasse"];
                    ausgabe.AddRange(leftListBox.SelectedItems.Cast<string>()
                        .ToList()
                        .Select(sus => _myschool.GetSchueler(Convert.ToInt32(sus.Split(';')[1])).Result)
                        .Select(s =>
                            $"{s.Vorname};{s.Nachname};{s.Nutzername};Klasse{s.Klasse}{DateTime.Now.Year}!;{s.Aixmail};{s.Klasse}"));
                    break;
                case 1:
                    var lul_liste = leftListBox.SelectedItems.Cast<string>()
                        .ToList()
                        .Select(lul => _myschool.GetLehrkraft(lul.Split(';')[0]).Result);
                    ausgabe =
                    [
                        "kuerzel;nachname;vorname;plz_ort_;adresse;tel_privat;tel_mobil;email_privat;email_dienst;gebdatum_;status_;mail_Adresse;fach1;fach2;fach3;fakult;funktion_;pw_temp;aktiv;gebdatum;plz;ort;titel;nachname;pop3_dienst;pop3_menge"
                    ];
                    foreach (var lt in lul_liste)
                    {
                        var fakultas = lt.Fakultas.Split(',');
                        var maildienst = lt.Mail.Split('@')[0];
                        var firstChar = maildienst[0];
                        var UpperCaseFirstCharacter = char.ToUpper(firstChar);
                        maildienst = UpperCaseFirstCharacter + maildienst[1..];
                        var fakult = fakultas.Aggregate("", (current, t) => $"{current}{t};");
                        switch (fakultas.Length)
                        {
                            case 2:
                                fakult += $";{lt.Fakultas}"; //; oder ,
                                break;
                            case 3:
                                fakult += lt.Fakultas;
                                break;
                        }

                        ausgabe.Add(
                            $"{lt.Kuerzel};{lt.Nachname};{lt.Vorname};;;;;;{maildienst};;;{lt.Mail};{fakult};;{_myschool.GetTempPasswort(lt.ID).Result};1;;;;;{lt.Nachname};{maildienst.ToLower()};1");
                    }

                    break;
                case 2:
                    ausgabe = ["Vorname;Nachname;Anmeldename;Kennwort;E-Mail;Klasse"];
                    foreach (string kursbez in leftListBox.SelectedItems)
                    {
                        ausgabe.AddRange(_myschool.GetSuSAusKurs(kursbez).Result.Distinct().Select(s =>
                            $"{s.Vorname};{s.Nachname};{s.Nutzername};Klasse{s.Klasse}{DateTime.Now.Year}!;{s.Aixmail};{s.Klasse}"));
                    }


                    break;
                default:
                    return;
            }

            await File.WriteAllLinesAsync(file_path, ausgabe.Distinct().ToList(), Encoding.UTF8);
        }
    }

    private async void OnMnuPasswordGenClick(object? sender, RoutedEventArgs e)
    {
        if (leftListBox.SelectedItems == null) return;
        if (cboxDataLeft.SelectedIndex != 1) return;
        await _myschool.StartTransaction();
        foreach (string luleintrag in leftListBox.SelectedItems)
        {
            var lul = await _myschool.GetLehrkraft(luleintrag.Split(';')[0]);
            _myschool.SetTPwd(lul.ID, Tooling.GeneratePasswort(8));
        }

        await _myschool.StopTransaction();
    }

    private async void OnMnuExportClick(object? sender, RoutedEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(ReadFileTask);
        return;

        async Task ReadFileTask()
        {
            if (leftListBox.SelectedItems == null) return;

            var folder = await ShowOpenFolderDialog("Bitte den Ordner zum Speichern angeben");
            if (folder == null) return;
            var folderpath = folder.Path.LocalPath;
            var expandFiles = 1;
            if (File.Exists($"{folderpath}/aix_sus.csv") || File.Exists($"{folderpath}/aix_lul.csv") ||
                File.Exists($"{folderpath}/mdl_einschreibungen.csv") ||
                File.Exists($"{folderpath}/mdl_kurse.csv") || File.Exists($"{folderpath}/mdl_nutzer.csv") ||
                File.Exists($"{folderpath}/jamf_sus.csv") ||
                File.Exists($"{folderpath}/jamf_lul.csv") ||
                File.Exists($"{folderpath}/jamf_teacher_groups.csv"))
            {
                var overwriteFilesDialog = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
                {
                    ButtonDefinitions = ButtonEnum.YesNoAbort,
                    ContentTitle = "Dateien gefunden",
                    ContentHeader = "Überschreiben?",
                    ContentMessage =
                        "Im Ordner existieren schon eine/mehrere Exportdateien.\nSollen diese überschrieben werden?\nJa = überschreiben, Nein = erweitern, Abbrechen = nichts machen",
                    Icon = MsBox.Avalonia.Enums.Icon.Question,
                    WindowIcon = _msgBoxWindowIcon
                });
                var dialogResult = await overwriteFilesDialog.ShowAsPopupAsync(this);
                expandFiles = dialogResult switch
                {
                    ButtonResult.Yes => 1,
                    ButtonResult.No => 0,
                    ButtonResult.Abort => -1,
                    _ => 1
                };
            }

            if (expandFiles == -1) return;
            List<SuS> suslist = [];
            List<Lehrkraft> lullist = [];
            List<Kurs> kurslist = [];
            var whattoexport = "";
            const string destsys = "amij";
            switch (cboxDataLeft.SelectedIndex)
            {
                case 0:
                    whattoexport += "s";
                    foreach (string suseintrag in leftListBox.SelectedItems)
                    {
                        suslist.Add(await _myschool.GetSchueler(Convert.ToInt32(suseintrag.Split(';')[1])));
                    }

                    break;
                case 1:
                    whattoexport += "l";
                    if (leftListBox.ContextMenu != null)
                    {
                        var isllginternChecked = ((CheckBox)leftListBox.ContextMenu.Items.Cast<Control>()
                            .Where(c => c.Name == "cbMnuLeftContextLLGIntern")
                            .ToList()
                            .First()).IsChecked;
                        if (isllginternChecked != null && isllginternChecked.Value) whattoexport += "i";
                    }

                    foreach (string luleintrag in leftListBox.SelectedItems)
                    {
                        lullist.Add(await _myschool.GetLehrkraft(luleintrag.Split(';')[0]));
                    }

                    break;
                case 2:
                    whattoexport += "ksl";
                    foreach (string kurseintrag in leftListBox.SelectedItems)
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

            if (leftListBox.ContextMenu != null)
            {
                var isElternChecked = ((CheckBox)leftListBox.ContextMenu.Items.Cast<Control>()
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

            if (leftListBox.ContextMenu != null)
            {
                var isAnfangsPasswortChecked = ((CheckBox)leftListBox.ContextMenu.Items.Cast<Control>()
                    .Where(c => c.Name == "cbMnuLeftContextAnfangsPasswort")
                    .ToList()
                    .First()).IsChecked;
                var nurMoodleSuffix = cbNurMoodleSuffix.IsChecked is not false;
                var res = await _myschool.ExportToCSV(folderpath, destsys, whattoexport,
                    isAnfangsPasswortChecked != null && isAnfangsPasswortChecked.Value, "", expandFiles == 0,
                    nurMoodleSuffix, kursvorlagen,
                    new ReadOnlyCollection<int>([..suslist.Select(s => s.ID).Distinct().ToList()]),
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
        if (file == null) return;
        var filepath = file.Path.LocalPath;
        await _myschool.ElternEinlesen(filepath);
    }

    private async void MnuExportLKtoHP1Spalte_OnClick(object? sender, RoutedEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(SaveLKtoHp);
        return;

        async Task SaveLKtoHp()
        {
            var extx = new List<FilePickerFileType> { StSFileTypes.CSVFile };
            var files = await ShowSaveFileDialog("Bitte einen Dateipfad angeben...", extx);
            if (files == null) return;
            var filepath = files.Path.LocalPath;
            List<string> lulliste = ["Kürzel;Nachname;Fächer;Mailadresse"];
            lulliste.AddRange(_myschool.GetLehrerListe().Result.Select(lehrer =>
                    $@"{lehrer.Kuerzel};{lehrer.Nachname};{lehrer.Fakultas};\underline{{\href{{mailto:{lehrer.Mail.ToLower()}}}{{{lehrer.Mail.ToLower()}}}}}")
                .OrderBy(s => s.Split(';')[0]));
            await File.WriteAllLinesAsync(filepath, lulliste, Encoding.UTF8);
        }
    }

    private async void MnuExportLKtoHP2Spalte_OnClick(object? sender, RoutedEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(SaveLKtoHp);
        return;

        async Task SaveLKtoHp()
        {
            var extx = new List<FilePickerFileType> { StSFileTypes.CSVFile };
            var files = await ShowSaveFileDialog("Bitte einen Dateipfad angeben...", extx);
            if (files == null) return;
            var filepath = files.Path.LocalPath;
            List<string> header = ["Kürzel;Nachname;Fächer;Mailadresse;Kürzel;Nachname;Fächer;Mailadresse"];
            List<string> lulliste = [];
            var llist = _myschool.GetLehrerListe().Result.OrderBy(lk => lk.Kuerzel).ToList();
            var half = llist.Count / 2;
            for (var i = 0; i < llist.Count / 2 + 1; ++i)
            {
                var lehrer = llist[i];
                var res = "";
                res +=
                    $@"{lehrer.Kuerzel};{lehrer.Nachname};{lehrer.Fakultas.TrimEnd(',')};\underline{{\href{{mailto:{lehrer.Mail.ToLower()}}}{{{lehrer.Mail.ToLower()}}}}}";
                lulliste.Add(res);
                var index = i + half + 1;
                if (index >= llist.Count) continue;
                lehrer = llist[index];
                lulliste[i] +=
                    $@";{lehrer.Kuerzel};{lehrer.Nachname};{lehrer.Fakultas.TrimEnd(',')};\underline{{\href{{mailto:{lehrer.Mail.ToLower()}}}{{{lehrer.Mail.ToLower()}}}}}";
            }

            if (llist.Count % 2 == 1)
            {
                lulliste[^1] += ";;;;";
            }

            header.AddRange(lulliste);
            await File.WriteAllLinesAsync(filepath, header, Encoding.UTF8);
        }
    }

    private void Rb_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender == null) return;
        if (sender.Equals(rbD))
        {
            Background = _darkBackgroundColor;
            leftListBox.Background = _darkBackgroundColor;
            rightListBox.Background = _darkBackgroundColor;
            lbFehlerliste.Background = _darkBackgroundColor;
        }
        else if (sender.Equals(rbL))
        {
            Background = _lightBackgroundColor;
            leftListBox.Background = _lightBackgroundColor;
            rightListBox.Background = _lightBackgroundColor;
            lbFehlerliste.Background = _lightBackgroundColor;
        }
    }

    private async void BtnLuLNewTmpPasswort_OnClick(object? sender, RoutedEventArgs e)
    {
        if (tbLuLKuerzel == null || tbLuLID == null || string.IsNullOrEmpty(tbLuLKuerzel.Text) ||
            string.IsNullOrEmpty(tbLuLID.Text)) return;
        var lul = await _myschool.GetLehrkraft(tbLuLKuerzel.Text);
        var pwd = Tooling.GeneratePasswort(8);
        _myschool.SetTPwd(lul.ID, pwd);
        tbLuLtmpPwd.Text = pwd;
    }

    private static void ResetItemsSource(ItemsControl sourceList, IEnumerable<string> dataList)
    {
        sourceList.ItemsSource = null;
        sourceList.Items.Clear();
        sourceList.ItemsSource = dataList;
    }

    /// <summary>
    /// zeigt die Abfrage, ob Dateien überschrieben werden sollen
    /// </summary>
    /// <returns>ausgewählter Button</returns>
    private async Task<ButtonResult> ShowOverwriteDialog()
    {
        var overwriteFilesDialog = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
        {
            ButtonDefinitions = ButtonEnum.YesNo,
            ContentTitle = "Dateien gefunden",
            ContentHeader = "Überschreiben?",
            ContentMessage = "Die Datei(en) ist/sind schon vorhanden.\nSollen diese überschrieben werden?",
            Icon = MsBox.Avalonia.Enums.Icon.Question,
            WindowIcon = _msgBoxWindowIcon
        });
        var dialogResult = await overwriteFilesDialog.ShowAsPopupAsync(this);
        return dialogResult;
    }

    private async void BtnModErstellung_OnClick(object? sender, RoutedEventArgs e)
    {
        if (cbSonst1 == null || cbSonst2 == null ||
            cbSonst3 == null || tbSonst1.Text == null || string.IsNullOrEmpty(tbSonst3.Text) ||
            tbSonst2.Text == null) return;
        var suscache = await _myschool.GetSchuelerListe();
        var lulcache = await _myschool.GetLehrerListe();
        var kurscache = await _myschool.GetKursListe();
        var susliste = new List<SuS>();
        var lulliste = new List<Lehrkraft>();
        var splitChar1 = tbSonst1.Text.Contains(';') ? ';' : tbSonst1.Text.Contains('\n') ? '\n' : ',';
        var splitChar2 = tbSonst2.Text.Contains(';') ? ';' : tbSonst2.Text.Contains('\n') ? '\n' : ',';
        var splitChar3 = tbSonst3.Text.Contains(';') ? ';' : tbSonst3.Text.Contains('\n') ? '\n' : ',';
        tbSonst1.Text = tbSonst1.Text.Replace(" ", "");
        tbSonst2.Text = tbSonst2.Text.Replace(" ", "");
        tbSonst3.Text = tbSonst3.Text.Replace(" ", "");
        switch (cbSonst1.SelectedIndex)
        {
            case 0 or 4:
                susliste = cbSonst2.SelectedIndex switch
                {
                    0 => suscache.Where(s => tbSonst2.Text.Split(splitChar2).Contains(s.GetStufe())).ToList(),
                    1 => suscache.Where(s => tbSonst2.Text.Split(splitChar2).Contains(s.Klasse)).ToList(),
                    _ => susliste
                };
                break;
            case 1:
                if (string.IsNullOrEmpty(tbSonst1.Text)) return;
                susliste = suscache.Where(s => tbSonst1.Text.Split(splitChar1).Contains(s.ID.ToString())).ToList();
                break;
            case 2:
                lulliste = [.. lulcache];
                break;
            case 3:
                if (string.IsNullOrEmpty(tbSonst1.Text)) return;
                lulliste = lulcache.Where(l => tbSonst1.Text.Split(splitChar1).Contains(l.Kuerzel)).ToList();
                break;
            case 5:
                susliste = suscache.Where(s =>
                    _myschool.Jamfstufen.Contains(s.GetStufe()) && string.IsNullOrEmpty(s.Seriennummer)).ToList();
                break;
            default:
                return;
        }

        if (rbEinschreibenDatenbank.IsChecked == true)
        {
            var kursliste = kurscache.Where(k => tbSonst3.Text.Split(splitChar3).Contains(k.Bezeichnung)).ToList();
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
        else if (rbEinschreibenDatei.IsChecked == true)
        {
            var filepath = await Dispatcher.UIThread.InvokeAsync(AskForFilepath);
            if (string.IsNullOrEmpty(filepath)) return;
            if (File.Exists(filepath))
            {
                var override_res = await ShowOverwriteDialog();
                if (override_res != ButtonResult.Yes) return;
            }

            var ListToFile = new List<string>();
            switch (cbSonst1.SelectedIndex)
            {
                case 0 or 1 when susliste.Count < 1:
                    return;
                case 0 or 1 or 4 or 5:
                {
                    foreach (var kurs in tbSonst3.Text.Split(';'))
                    {
                        ListToFile.AddRange(susliste.Select(sus => $"add,schueler,{sus.ID},{kurs}"));
                        if (cbSonst1.SelectedIndex == 4)
                        {
                            ListToFile.AddRange(susliste.Select(sus => $"add,eltern,E_{sus.ID},{kurs}"));
                        }
                    }

                    await _myschool.StopTransaction();
                    await File.WriteAllLinesAsync(filepath, ListToFile);
                    await ShowCustomSuccessMessage("Speichern und Einschreiben erfolgreich", "Erfolg");
                    return;
                }
            }
        }
        else if (rbEinschreibenDatei.IsChecked == true)
        {
            var filepath = await Dispatcher.UIThread.InvokeAsync(AskForFilepath);
            if (string.IsNullOrEmpty(filepath)) return;
            if (File.Exists(filepath))
            {
                var override_res = await ShowOverwriteDialog();
                if (override_res != ButtonResult.Yes) return;
            }

            var ListToFile = new List<string>();
            switch (cbSonst1.SelectedIndex)
            {
                case 0 or 1 when susliste.Count < 1:
                    return;
                case 0 or 1:
                {
                    foreach (var kurs in tbSonst3.Text.Split(';'))
                    {
                        ListToFile.AddRange(susliste.Select(sus => $"add,schueler,{sus.ID},{kurs}"));
                    }

                    break;
                }
                case 2 or 3 when lulliste.Count < 1:
                    break;
                case 2 or 3:
                {
                    foreach (var kurs in tbSonst3.Text.Split(';'))
                    {
                        ListToFile.AddRange(lulliste.Select(luL => $"add,teacher,{luL.ID},{kurs}"));
                    }

                    break;
                }
            }

            await File.WriteAllLinesAsync(filepath, ListToFile);
            await ShowCustomSuccessMessage("Speichern erfolgreich", "Erfolg");
        }

        return;

        async Task<string> AskForFilepath()
        {
            var extx = new List<FilePickerFileType>
            {
                StSFileTypes.CSVFile,
                FilePickerFileTypes.All
            };
            var file = await ShowSaveFileDialog("CSV speichern unter...", extx);
            return file == null ? "" : file.Path.LocalPath;
        }
    }

    private void CbSonst1_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        tbSonst1.IsVisible = cbSonst1.SelectedIndex % 2 == 1;
        tbSonst11.IsVisible = cbSonst1.SelectedIndex % 2 == 0;
        cbSonst2.IsVisible = cbSonst1.SelectedIndex % 2 == 0;
        tbSonst2.IsVisible = cbSonst1.SelectedIndex % 2 == 0;
        if (cbSonst1.SelectedIndex == 5)
        {
            tbSonst1.IsVisible = tbSonst11.IsVisible = cbSonst2.IsVisible = tbSonst2.IsVisible = false;
        }
    }

    private async void BtnSonstDVIDs_OnClick(object? sender, RoutedEventArgs e)
    {
        var extx = new List<FilePickerFileType>
        {
            StSFileTypes.CSVFile,
            FilePickerFileTypes.All
        };
        var file = await ShowOpenFileDialog("Einwilligungen alt", extx);
        if (file == null) return;
        var alterStatusFilePath = file.Path.LocalPath;
        var alterstatus = await File.ReadAllLinesAsync(alterStatusFilePath);
        file = await ShowOpenFileDialog("Einwilligungen neu", extx);
        if (file == null) return;
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
        var ids = diff.Aggregate("", (current, id) => $"{current};{id}").TrimStart(';');
        var clipboard = Clipboard;
        if (clipboard == null) return;
        await clipboard.SetTextAsync(ids);
    }

    private async void MnuItemCopySuSidOnClick(object? sender, RoutedEventArgs e)
    {
        if (leftListBox.SelectedItems == null) return;
        var ids = leftListBox.SelectedItems.Cast<string>()
            .Aggregate("", (current, item) => $"{current}{item.Split(';')[1]};");
        var clipboard = Clipboard;
        if (clipboard == null) return;
        await clipboard.SetTextAsync(ids.TrimEnd(';'));
    }

    private async void MnuItemCopySuSMailOnClick(object? sender, RoutedEventArgs e)
    {
        if (leftListBox.SelectedItems == null) return;
        var sus = leftListBox.SelectedItems.Cast<string>().Aggregate("",
            (current, item) =>
                $"{current}{_myschool.GetSchueler(Convert.ToInt32(item.Split(';')[1])).Result.Mail};");
        var clipboard = Clipboard;
        if (clipboard == null) return;
        await clipboard.SetTextAsync(sus.TrimEnd(';'));
    }

    private async void MnuItemCopyKursBezOnClick(object? sender, RoutedEventArgs e)
    {
        if (leftListBox.SelectedItems == null) return;
        var bezliste = leftListBox.SelectedItems.Cast<string>()
            .Aggregate("", (current, bez) => $"{current}{bez};");
        var clipboard = Clipboard;
        if (clipboard == null) return;
        await clipboard.SetTextAsync(bezliste.TrimEnd(';'));
    }

    private async void MnuItemCopyLuLMailsOnClick(object? sender, RoutedEventArgs e)
    {
        if (leftListBox.SelectedItems == null) return;
        var mails = leftListBox.SelectedItems.Cast<string>().Aggregate("",
            (current, line) => $"{current}{_myschool.GetLehrkraft(line.Split(';')[0]).Result.Mail};");
        var clipboard = Clipboard;
        if (clipboard == null) return;
        await clipboard.SetTextAsync(mails.TrimEnd(';'));
    }

    private async void MnuItemCopyLuLKrzOnClick(object? sender, RoutedEventArgs e)
    {
        if (leftListBox.SelectedItems == null) return;
        var krzs = leftListBox.SelectedItems.Cast<string>()
            .Aggregate("", (current, line) => $"{current}{line.Split(';')[0]};");
        var clipboard = Clipboard;
        if (clipboard == null) return;
        await clipboard.SetTextAsync(krzs.TrimEnd(';'));
    }

    private async void MnuItemCopyLogOnClick(object? sender, RoutedEventArgs e)
    {
        if (lbLogDisplay.SelectedItems == null) return;
        var logentries = lbLogDisplay.SelectedItems.Cast<string>()
            .Aggregate("", (current, line) => $"{current}{line.Split(';')[0].Trim()}\n");
        var clipboard = Clipboard;
        if (clipboard == null) return;
        await clipboard.SetTextAsync(logentries);
    }

    private async void TbLeftSearch_OnPastingFromClipboard(object? sender, RoutedEventArgs e)
    {
        var clipboard = Clipboard;
        if (clipboard == null) return;
        var text = await clipboard.GetTextAsync();
        if (text == null) return;
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
        return;

        async Task SaveFavosFile()
        {
            var files = await ShowOpenFolderDialog("Bitte einen Dateipfad angeben...");
            if (files == null) return;
            var filepath = $"{files.Path.LocalPath}/mdl_einschreibungen.csv";
            if (File.Exists(filepath))
            {
                var override_res = await ShowOverwriteDialog();
                if (override_res != ButtonResult.Yes) return;
            }

            var favos = await _myschool.GetFavos();
            var stringifiedFavos = favos.Select(lehrkraft => $"add,student,{lehrkraft.ID},EtatK").ToList();
            await File.WriteAllLinesAsync(filepath, stringifiedFavos, Encoding.UTF8);
            await ShowCustomInfoMessage("Speichern erfolgreich.", "Erfolg");
        }
    }

    private async void BtnFavoSave_OnClick(object? sender, RoutedEventArgs e)
    {
        await _myschool.StartTransaction();
        var favos = await _myschool.GetFavos();
        foreach (var l in favos)
        {
            await _myschool.UpdateLehrkraft(l.ID, l.Vorname, l.Nachname, l.Kuerzel, l.Mail, l.Fakultas, l.Pwttemp,
                "", "", l.Seriennummer, l.Bemerkung);
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
            var validfach = exportFavoTabGrid.Children
                .Where(c => !string.IsNullOrEmpty(c.Name) && c.Name.Equals($"cbExportFavo{fach}"))
                .ToList();
            if (validfach.Count == 0) continue;
            var favocb = (ComboBox)exportFavoTabGrid.Children.Where(c =>
                    !string.IsNullOrEmpty(c.Name) && c.Name.Equals($"cbExportFavo{fach}"))
                .ToList()[0];
            var sfavocb = (ComboBox)exportFavoTabGrid.Children.Where(c =>
                    !string.IsNullOrEmpty(c.Name) && c.Name.Equals($"cbExportSFavo{fach}"))
                .ToList()[0];
            var kuerzel = favocb.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(kuerzel))
            {
                var l = await _myschool.GetLehrkraft(kuerzel.Split(';')[0]);
                if (l.Favo != "")
                {
                    l.Favo += $",{fach}";
                }
                else
                {
                    l.Favo = fach;
                }

                _myschool.UpdateLehrkraft(l);
            }

            kuerzel = sfavocb.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(kuerzel)) continue;
            {
                var l = await _myschool.GetLehrkraft(kuerzel.Split(';')[0]);
                if (l.SFavo != "")
                {
                    l.SFavo += $",{fach}";
                }
                else
                {
                    l.SFavo = fach;
                }

                _myschool.UpdateLehrkraft(l);
            }
        }

        await _myschool.StopTransaction();
        await ShowCustomInfoMessage("Speichern erfolgreich.", "Erfolg");
    }

    private async void BtnSettingsToFile_OnClick(object? sender, RoutedEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(SaveSettingsToFile);
        return;

        async Task SaveSettingsToFile()
        {
            var extx = new List<FilePickerFileType> { StSFileTypes.JSONFile };
            var files = await ShowSaveFileDialog("Bitte einen Dateipfad angeben...", extx);
            if (files == null) return;

            var filepath = files.Path.LocalPath;
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    var json_options = new JsonSerializerOptions()
                    {
                        WriteIndented = true
                    };
                    var json_settings = JsonSerializer.Serialize(_myschool.GetSettings().Result, json_options);
                    await File.WriteAllTextAsync(filepath, json_settings);
                    await ShowCustomSuccessMessage("Einstellungen erfolgreich gespeichert", "Erfolg");
                }
                catch (Exception exception)
                {
                    _myschool.AddLogMessage(new LogEintrag
                        { Eintragsdatum = DateTime.Now, Nachricht = exception.Message, Warnstufe = "Fehler" });
                    await ShowCustomErrorMessage("Speichern der Einstellungen fehlgeschlagen", "Fehler");
                }
            });
        }
    }

    private async void BtnSettingsFromFile_OnClick(object? sender, RoutedEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(LoadSettingsFromFile);
        return;

        async Task LoadSettingsFromFile()
        {
            var extx = new List<FilePickerFileType> { StSFileTypes.JSONFile };
            var files = await ShowOpenFileDialog("Bitte einen Dateipfad angeben...", extx);
            if (files == null) return;
            var filepath = files.Path.LocalPath;
            try
            {
                var json_settings = JsonSerializer.Deserialize<Einstellungen>(File.ReadAllTextAsync(filepath).Result);
                await _myschool.SetSettings(json_settings);
                LoadSettingsToGUI(json_settings);
                await ShowCustomSuccessMessage("Einstellungen erfolgreich geladen", "Erfolg");
            }
            catch (Exception exception)
            {
                _myschool.AddLogMessage(new LogEintrag
                    { Eintragsdatum = DateTime.Now, Nachricht = exception.Message, Warnstufe = "Fehler" });
                await ShowCustomErrorMessage("Laden der Einstellungen fehlgeschlagen", "Fehler");
            }
        }
    }

    private async void MnuM365DVEinlesen_OnClick(object? sender, RoutedEventArgs e)
    {
        var extx = new List<FilePickerFileType>
        {
            StSFileTypes.CSVFile,
            FilePickerFileTypes.All
        };
        var file = await ShowOpenFileDialog("Aktuelle Accounts ohne DV", extx);
        if (file == null) return;
        var DVFilePath = file.Path.LocalPath;
        var DVFileText = await File.ReadAllLinesAsync(DVFilePath);
        var IDListe = (from line in DVFileText
            select line.Split(';')[0]
            into id
            where id.All(char.IsDigit)
            select Convert.ToInt32(id)).ToList();
        Parallel.ForEach(_myschool.GetSchuelerIDListe().Result, (id, _) =>
            //foreach (var id in _myschool.GetSchuelerIDListe().Result)
        {
            if (IDListe.Contains(id))
            {
                _myschool.SetM365(id, 0);
                var sus = _myschool.GetSchueler(id).Result;
                if (!string.IsNullOrEmpty(sus.Nutzername)) return;
                sus.Nutzername = sus.Vorname[..3] +
                                 sus.Nachname[..3] + Random.Shared.NextInt64(10, 100);
                _myschool.UpdateSchueler(sus);
            }
            else
            {
                _myschool.SetM365(id, 1);
            }
        });

        await ShowCustomInfoMessage("Import erfolgreich.", "Erfolg");
    }

    private async void MnuTempAccountsEinlesen_OnClick(object? sender, RoutedEventArgs e)
    {
        var extx = new List<FilePickerFileType>
        {
            StSFileTypes.CSVFile,
            FilePickerFileTypes.All
        };
        var file = await ShowOpenFileDialog("Aktuelle Accounts ohne DV", extx);
        if (file == null) return;
        var TAFilePath = file.Path.LocalPath;
        var TAFileText = File.ReadAllLinesAsync(TAFilePath).Result.ToList();
        if (TAFileText[0] != "id;accountname")
        {
            await ShowCustomErrorMessage("Fehlerhafte Datei, bitte den Header überprüfen", "Fehler");
            return;
        }

        TAFileText.RemoveAt(0);
        foreach (var line in TAFileText)
        {
            var string_id = line.Split(';')[0];
            if (string.IsNullOrEmpty(string_id))
            {
                await ShowCustomErrorMessage("Eine ID fehlt, bitte die angegebenen Daten überprüfen!", "Fehler");
                continue;
            }

            var id = Convert.ToInt32(line.Split(';')[0]);
            var name = line.Split(';')[1];
            var sus = await _myschool.GetSchueler(id);
            if (string.IsNullOrEmpty(name))
            {
                await ShowCustomErrorMessage($"Fehlerhafte Angaben bei Schüler:in mit der ID: {sus.ID}", "Fehler");
                continue;
            }

            sus.Nutzername = name;
            _myschool.UpdateSchueler(sus);
        }
    }

    private void BtnLuLShowPWD_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!tbLuLtmpPwd.RevealPassword)
        {
            panel_image_lulpwp_visible.Source =
                new Bitmap(AssetLoader.Open(new Uri("avares://StS-GUI-Avalonia/Assets/gfx/visible_off.png")));
            tbLuLtmpPwd.RevealPassword = true;
        }
        else
        {
            panel_image_lulpwp_visible.Source =
                new Bitmap(AssetLoader.Open(new Uri("avares://StS-GUI-Avalonia/Assets/gfx/visible_on.png")));
            tbLuLtmpPwd.RevealPassword = false;
        }
    }

    private void CbSuSM365_OnClick(object? sender, RoutedEventArgs e)
    {
        var susid_string = tbSuSID.Text;
        if (string.IsNullOrEmpty(susid_string) || !susid_string.All(char.IsDigit)) return;
        var sus = _myschool.GetSchueler(Convert.ToInt32(susid_string)).Result;
        _myschool.SetM365(sus.ID, cbSuSM365.IsChecked != null && cbSuSM365.IsChecked.Value ? 1 : 0);
    }

    private async void BtnLogExport_OnClick(object? sender, RoutedEventArgs e)
    {
        if (lbLogDisplay == null || lbLogDisplay.Items.Count == 0) return;
        await Dispatcher.UIThread.InvokeAsync(SaveLogToFile);
        return;

        async Task SaveLogToFile()
        {
            var extx = new List<FilePickerFileType> { StSFileTypes.CSVFile };
            var files = await ShowSaveFileDialog("Bitte einen Dateipfad angeben...", extx);
            if (files == null) return;

            var filepath = files.Path.LocalPath;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    List<LogEintrag> items = [];
                    if (lbLogDisplay.SelectedItems is { Count: > 0 })
                    {
                        items.AddRange(from entry in lbLogDisplay.SelectedItems.Cast<string>()
                            select entry.Split('\t')
                            into logentry
                            select new LogEintrag
                            {
                                Warnstufe = logentry[0], Eintragsdatum = DateTime.Parse(logentry[1]),
                                Nachricht = string.Join("\t", logentry[2..])
                            });
                    }
                    else
                    {
                        items = _myschool.GetLog().Result.ToList();
                    }

                    var tlist = new List<string>();
                    if (lbLogLevel.SelectedItems != null)
                    {
                        foreach (ListBoxItem item in lbLogLevel.SelectedItems)
                        {
                            if (item?.Content != null)
                            {
                                tlist.Add(item.Content.ToString() ?? throw new InvalidOperationException());
                            }
                        }
                    }

                    var filtered_items = items.Where(x => tlist.Contains(x.Warnstufe));
                    await File.WriteAllTextAsync(filepath,
                        string.Join(";",
                                filtered_items.Select(x =>
                                    $"{x.Warnstufe}\t{x.Datumsstring()}\t{x.Nachricht.Replace('\t', ' ').Replace("  ", " ").TrimEnd(' ')}\n"))
                            .Replace("\n;", "\n"));
                    await ShowCustomSuccessMessage("Log erfolgreich gespeichert", "Erfolg");
                }
                catch (Exception exception)
                {
                    _myschool.AddLogMessage(new LogEintrag
                        { Eintragsdatum = DateTime.Now, Nachricht = exception.Message, Warnstufe = "Fehler" });
                    await ShowCustomErrorMessage("Speichern des Logs fehlgeschlagen", "Fehler");
                }
            });
        }
    }

    private async void BtnExportInaktive_OnClick(object? sender, RoutedEventArgs e)
    {
        var inaktiveSuS = _myschool.GetSchuelerListe().Result.Where(s => !s.IstAktiv).ToList();
        var inaktiveLuL = _myschool.GetLehrerListe().Result.Where(l => !l.IstAktiv).ToList();
        List<string> exportMoodleListe = ["email;username;idnumber;lastname;firstname;suspended"];
        Parallel.ForEach(inaktiveSuS,
            (s, _) =>
            {
                exportMoodleListe.Add(string.Join(';', s.Mail, s.Nutzername, s.ID.ToString(), s.Nachname, s.Vorname,
                    1.ToString()));
            });
        Parallel.ForEach(inaktiveLuL,
            (l, _) =>
            {
                exportMoodleListe.Add(string.Join(';', l.Mail, l.Kuerzel, l.ID.ToString(), l.Nachname, l.Vorname,
                    1.ToString()));
            });

        var extx = new List<FilePickerFileType>
        {
            StSFileTypes.CSVFile,
            FilePickerFileTypes.All
        };
        var file = await ShowSaveFileDialog("Deaktivierte Accounts speichern", extx);
        if (file == null) return;
        var InaktiveFilePath = file.Path.LocalPath;
        await File.WriteAllLinesAsync(InaktiveFilePath, exportMoodleListe, Encoding.UTF8);

        await ShowCustomInfoMessage("Speichern erfolgreich.", "Erfolg");
    }

    private async void MnuExportFako_OnClick(object? sender, RoutedEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(ExportFaKo);
        return;

        async Task ExportFaKo()
        {
            var fakos = await _myschool.GetFaKos();
            List<string> favo_export = [];
            var favos = _myschool.GetFavos().Result.Distinct().ToList();
            favos.Sort();
            var favos_mail = favos.Aggregate("<a href=\"mailto:?bcc=", (current, l) => $"{current}{l.Mail},");
            favo_export.Add(
                $"{favos_mail.TrimEnd(',')}\">Alle Fachvorsitzenden</a><br><br>\n<strong>Hinweis</strong>: Der Klick auf das Fach erstellt eine Mail an alle Lehrkräfte, die das Fach unterrichten<br><br>\n");
            favo_export.AddRange(from fako in fakos
                let mailadressen = fako.Mitglieder.Aggregate("", (current, l) => $"{current}{l.Mail},").TrimEnd(',')
                let fako_string = new StringBuilder().Append("Fachschaft <a href=\"mailto:?bcc=")
                    .Append(mailadressen)
                    .Append("\">")
                    .Append(fako.Fach)
                    .Append("</a><br>Vorsitz: <a href=\"mailto:")
                    .Append(fako.Vorsitz.Mail.ToLower())
                    .Append("\">")
                    .Append(fako.Vorsitz.Mail.ToLower())
                    .Append("</a><br>Stellvertretung: <a href=\"mailto:")
                    .Append(fako.Stellvertretung.Mail.ToLower())
                    .Append("\">")
                    .Append(fako.Stellvertretung.Mail.ToLower())
                    .Append("</a><br>Mitglieder: ")
                    .ToString()
                select fako.Mitglieder.Aggregate(fako_string,
                    (current, lul) => $"{current}<a href=\"mailto:{lul.Mail.ToLower()}\">{lul.Mail.ToLower()}</a>, ")
                into fako_string
                select $"{fako_string.TrimEnd(' ').TrimEnd(',')}<br><br>");
#if DEBUG
            var alle_lul = _myschool.GetLehrerListe().Result.Where(l => l.IstAktiv).Select(l => l.Mail);
            favo_export.Add("<a href=\"mailto:" + string.Join(',', alle_lul) + "\">Alle Lehrkräfte</a>");
#endif
            var extx = new List<FilePickerFileType>
            {
                FilePickerFileTypes.All
            };
            var file = await ShowSaveFileDialog("FaKos exportieren", extx);
            if (file == null) return;
            var InaktiveFilePath = file.Path.LocalPath;
            File.Delete(InaktiveFilePath);
            await using (var outfs = File.AppendText(InaktiveFilePath))
            {
                await outfs.WriteAsync("<!DOCTYPE html><body>\n");
                foreach (var line in favo_export)
                    await outfs.WriteAsync(line);
                await outfs.WriteAsync("\n</body></html>");
            }

            await ShowCustomInfoMessage("Speichern erfolgreich.", "Erfolg");
        }
    }

    private async void MnuIPadSeriennummernEinlesen_OnClick(object? sender, RoutedEventArgs e)
    {
        var extx = new List<FilePickerFileType>
        {
            StSFileTypes.CSVFile,
            FilePickerFileTypes.All
        };
        var file = await ShowOpenFileDialog("Seriennummern einlesen", extx);
        if (file == null) return;
        var iPSFilePath = file.Path.LocalPath;
        var iPSFileText = File.ReadAllLinesAsync(iPSFilePath).Result.ToList();
        switch (iPSFileText[0])
        {
            case "Vorname;Nachname;Klasse;Seriennummer":
            {
                iPSFileText.RemoveAt(0);
                await _myschool.StartTransaction();
                var susliste = await _myschool.GetSchuelerListe();
                foreach (var line in iPSFileText)
                {
                    var split_line = line.Split(';');
                    var vorname = split_line[0];
                    var nachname = split_line[1];
                    var klasse = split_line[2];
                    var seriennummer = split_line[3];
                    if (string.Empty == vorname || string.Empty == nachname || /*string.Empty == klasse ||*/
                        string.Empty == seriennummer)
                    {
                        await ShowCustomErrorMessage(
                            $"Fehlerhafte Angaben bei Schüler:in {vorname} {nachname}:{klasse}", "Fehler");
                        continue;
                    }

                    var sus = susliste.Where(s => s.Vorname.StartsWith(vorname) && s.Nachname.StartsWith(nachname))
                        .ToList();
                    if (sus.Count == 0) continue;
                    foreach (var s in sus)
                    {
                        var tmp_sus = s;
                        tmp_sus.Seriennummer = seriennummer;
                        _myschool.UpdateSchueler(tmp_sus);
                    }
                }

                await _myschool.StopTransaction();
                break;
            }
            case "Kürzel;Seriennummer":
            {
                iPSFileText.RemoveAt(0);
                await _myschool.StartTransaction();
                var lulliste = await _myschool.GetLehrerListe();
                foreach (var line in iPSFileText)
                {
                    var split_line = line.Split(';');
                    var kuerzel = split_line[0];
                    var seriennummer = split_line[1];
                    if (string.IsNullOrEmpty(kuerzel) || string.IsNullOrEmpty(seriennummer))
                    {
                        await ShowCustomErrorMessage(
                            $"Fehlerhafte Angaben bei {line}", "Fehler");
                        continue;
                    }

                    var lul = lulliste.Where(l => l.Kuerzel.Equals(kuerzel, StringComparison.CurrentCultureIgnoreCase))
                        .ToList();
                    switch (lul.Count)
                    {
                        case 1:
                            var l = await _myschool.GetLehrkraft(kuerzel.ToUpper());
                            l.Seriennummer = seriennummer;
                            _myschool.UpdateLehrkraft(l);
                            break;
                        case > 1:
                            await ShowCustomErrorMessage($"Mehrere Lehrkräfte mit Kürzel {kuerzel} gefunden", "Fehler");
                            break;
                    }
                }

                await _myschool.StopTransaction();
                break;
            }
            case "Vorname;Nachname;Klasse;Seriennummer;ID":
            {
                iPSFileText.RemoveAt(0);
                await _myschool.StartTransaction();
                foreach (var line in iPSFileText)
                {
                    var split_line = line.Split(';');
                    var seriennummer = split_line[3];
                    var id = Convert.ToInt32(split_line[4]);
                    var sus = await _myschool.GetSchueler(id);
                    sus.Seriennummer = seriennummer;
                    _myschool.UpdateSchueler(sus);
                }

                await _myschool.StopTransaction();
                break;
            }
            case "Name,Seriennummer":
                iPSFileText.RemoveAt(0);
                await _myschool.StartTransaction();
                foreach (var line in iPSFileText)
                {
                    var split_line = line.Split(',');
                    var seriennummer = split_line[1];
                    if (seriennummer == "Nicht gefunden") continue;
                    var name = split_line[0];
                    var suslist = await _myschool.GetSchueler(name);
                    if (suslist.Count != 1)
                    {
                        await ShowCustomErrorMessage($"{name} ist uneindeutig", "Fehler");
                        continue;
                    }

                    var sus = suslist[0];
                    sus.Seriennummer = seriennummer;
                    _myschool.UpdateSchueler(sus);
                }

                await _myschool.StopTransaction();
                break;
            default:
            {
                await ShowCustomErrorMessage(
                    "Fehlerhafte Datei, bitte den Header überprüfen, für Schüler:innen Vorname;Nachname;Klasse;Seriennummer, für Lehrkräfte Kürzel;Seriennummer verwenden.",
                    "Fehler");
                await _myschool.StopTransaction();
                return;
            }
        }

        await ShowCustomSuccessMessage("Import der Seriennummern abgeschlossen", "Erfolg");
    }

    private async void BtnSonstigesNamenKlassen(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(tbSuSNamen.Text)) return;
        var namen = tbSuSNamen.Text;
        List<string> ergebnis = [];
        foreach (var line in namen.Split('\n'))
        {
            if (line == "") continue;
            var spliteingabe = line.Split('\t');
            if (spliteingabe.Length is < 1 or > 3) continue;
            var vorname = spliteingabe[0].Trim();
            var nachname = spliteingabe[1].Trim();
            var klasse = spliteingabe.Length == 3 ? spliteingabe[2].Trim() : "";
            var s = klasse == ""
                ? _myschool.GetSchuelerListe().Result
                    .Where(s => s.Vorname.StartsWith(vorname) && s.Nachname.Equals(nachname)).ToList()
                : _myschool.GetSchuelerListe().Result
                    .Where(s => s.Vorname.StartsWith(vorname) && s.Nachname.Equals(nachname) && s.Klasse.Equals(klasse))
                    .ToList();
            switch (s.Count)
            {
                case 1:
                    ergebnis.Add(string.Join(';', s[0].Vorname, s[0].Nachname, s[0].Klasse, s[0].ID));
                    break;
                case > 0:
                    await ShowCustomErrorMessage($"Mehrere Schüler mit dem Namen {vorname} {nachname} gefunden",
                        "Fehler");
                    break;
            }
        }

        tbSuSNamen.Text = string.Join('\n', ergebnis);
    }

    private async void BtnSonstNeuzugangAbgang_OnClick(object? sender, RoutedEventArgs e)
    {
        var extx = new List<FilePickerFileType>
        {
            StSFileTypes.DataBaseFile,
            FilePickerFileTypes.All
        };
        var file = await ShowOpenFileDialog("Alte DB angeben", extx);
        if (file == null) return;
        var oldschool = new Schuldatenbank(file.Path.LocalPath);
        var old_lul = oldschool.GetLehrerIDListe().Result.Except(_myschool.GetLehrerIDListe().Result).ToList();
        var old_sus = oldschool.GetSchuelerIDListe().Result.Except(_myschool.GetSchuelerIDListe().Result).ToList();
        var new_lul = _myschool.GetLehrerIDListe().Result.Except(oldschool.GetLehrerIDListe().Result).ToList();
        var new_sus = _myschool.GetSchuelerIDListe().Result.Except(oldschool.GetSchuelerIDListe().Result).ToList();
        var folder = await ShowOpenFolderDialog("Speichern unter...");
        if (folder == null) return;
        var path = folder.Path.AbsolutePath;
        var result = await _myschool.ExportToCSV(path, "all", "all", true, "def", false, true, ["", ""],
            new_sus.AsReadOnly(),
            new_lul.AsReadOnly(),
            new List<string>().AsReadOnly());
        if (result != 1)
        {
            await ShowCustomErrorMessage("Fehler beim Export der Neuzugänge", "Fehler");
        }

        var changes = new List<string>();
        foreach (var lul in old_lul)
        {
            var l = await oldschool.GetLehrkraft(lul);
            changes.Add($"Lehrkraft {l.Kuerzel} entfernt in neuer DB");
        }

        foreach (var sus in old_sus)
        {
            var s = await oldschool.GetSchueler(sus);
            changes.Add($"Schüler:in {s.Vorname} {s.Nachname} entfernt in neuer DB");
        }

        var export_lines = new List<string>();
        foreach (var sus in old_sus)
        {
            var s = await oldschool.GetSchueler(sus);
            export_lines.Add($"add,schueler,{s.ID},Abgang");
        }

        var alte_eltern = _myschool.GetSusAusStufe("EF").Result.ToList();
        alte_eltern.AddRange(_myschool.GetSusAusStufe("Q1").Result.ToList());
        alte_eltern.AddRange(_myschool.GetSusAusStufe("Q2").Result);
        foreach (var sus_eltern in alte_eltern)
        {
            export_lines.Add($"add,eltern,E_{sus_eltern.ID},Abgang");
            export_lines.Add($"add,eltern,E_{sus_eltern.ID}1,Abgang");
        }

        foreach (var lul in old_lul)
        {
            var l = await oldschool.GetLehrkraft(lul);
            export_lines.Add($"add,lehrer,{l.ID},Abgang");
        }

        if (File.Exists(path + "/mdl_einschreibungen.csv"))
        {
            await File.AppendAllLinesAsync(path + "/mdl_einschreibungen.csv", export_lines);
        }
        else
        {
            await File.WriteAllLinesAsync(path + "/mdl_einschreibungen.csv", export_lines);
        }

        await File.WriteAllLinesAsync(path + "/changes.log", changes);

        await ShowCustomSuccessMessage("Speichern erfolgreich", "Erfolg");
    }

    private async void BtnSonstSpielwiesen_OnClick(object? sender, RoutedEventArgs e)
    {
        var extx = new List<FilePickerFileType>
        {
            StSFileTypes.CSVFile,
            FilePickerFileTypes.All
        };
        var file = await ShowSaveFileDialog("CSV-Datei angegeben", extx);
        if (file == null) return;
        List<string> spielwiesen =
            ["shortname;fullname;idnumber;category_idnumber;format;enrolment_0;enrolment_0_role;enrolment_0_password"];
        for (var i = 1; i <= 15; ++i)
        {
            spielwiesen.Add($"SPW{i};Spielwiese {i};SPW{i};spw;tiles;self;editingteacher;SPW{i}");
        }

        await File.WriteAllLinesAsync(file.Path.LocalPath, spielwiesen, Encoding.UTF8);
    }

    private void CbSuSJAMF_OnClick(object? sender, RoutedEventArgs e)
    {
        var susid_string = tbSuSID.Text;
        if (string.IsNullOrEmpty(susid_string) || !susid_string.All(char.IsDigit)) return;
        var sus = _myschool.GetSchueler(Convert.ToInt32(susid_string)).Result;
        _myschool.SetJAMF(sus.ID, cbSuSM365.IsChecked != null && cbSuSM365.IsChecked.Value ? 1 : 0);
    }

    private async void MnuJAMFEinlesen_OnClick(object? sender, RoutedEventArgs e)
    {
        var extx = new List<FilePickerFileType>
        {
            StSFileTypes.CSVFile,
            FilePickerFileTypes.All
        };
        var file = await ShowOpenFileDialog("CSV-Datei angegeben", extx);
        if (file == null) return;
        var jamf_input = await File.ReadAllLinesAsync(file.Path.LocalPath);
        if (jamf_input.Length == 0 || !jamf_input[0].StartsWith("Vorname;Nachname;Klasse;JAMF (ja/nein/fehlt)"))
        {
            await ShowCustomErrorMessage("Fehler beim Einlesen der Datei", "Fehler");
            return;
        }

        foreach (var line in jamf_input[1..])
        {
            var split_line = line.Split(';');
            var vorname = split_line[0].Trim();
            var nachname = split_line[1].Trim();
            var klasse = split_line[2].Trim();
            var jamf = split_line[3].Trim().Equals("ja", StringComparison.CurrentCultureIgnoreCase);
            var sus_list = _myschool.GetSchueler(vorname, nachname).Result.Where(s => s.Klasse == klasse).ToList();
            switch (sus_list.Count)
            {
                case < 1:
                    continue;
                case > 1:
                    await ShowCustomErrorMessage(
                        $"Es gibt mehrere Schüler:innen mit dem Namen {vorname} {nachname} in der Klasse {klasse}, wird übersprungen! Sie müssen den Eintrag manuell in der Datenbank setzen.",
                        "Error");
                    break;
                default:
                {
                    var sus = sus_list[0];
                    sus.AllowJAMF = jamf;
                    _myschool.UpdateSchueler(sus);
                    break;
                }
            }
        }

        await ShowCustomSuccessMessage("Einlesen erfolgreich", "Erfolg");
    }
}