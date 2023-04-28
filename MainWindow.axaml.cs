using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using SchulDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace StS_GUI_Avalonia
{
    public partial class MainWindow : Window
    {
        private readonly SaveFileDialog globalSaveFileDialog = new();
        private readonly OpenFileDialog globalOpenFileDialog = new();
        private readonly OpenFolderDialog globalOpenFolderDialog = new();
        private readonly Timer leftInputTimer = new(350);
        private readonly Timer rightInputTimer = new(350);
        private Schuldatenbank myschool = new(":memory:");
        private readonly ContextMenu leftContext = new();
        private readonly Brush darkBackgroundColor = new SolidColorBrush(Color.FromRgb(80, 80, 80));
        private readonly Brush lightBackgroundColor = new SolidColorBrush(Color.FromRgb(242, 242, 242));
        private bool rightMutex;

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            var settings = myschool.GetSettings().Result;
            tbSettingMailplatzhalter.Text = settings[0].Split(';')[1];
            tbSettingKursersetzung.Text = settings[1].Split(';')[1];
            tbSettingKurssuffix.Text = settings[2].Split(';')[1];
            var kurzfach = myschool.GetFachersatz().Result.Select(t => t.Split(';')[0]);
            foreach (var fachk in kurzfach)
            {
                tbSettingFachkurz.Text += fachk + '\n';
            }

            var langfach = myschool.GetFachersatz().Result.Select(t => t.Split(';')[1]);
            foreach (var fachl in langfach)
            {
                tbSettingFachlang.Text += fachl + '\n';
            }

            leftInputTimer.Elapsed += OnLeftTimedEvent;
            rightInputTimer.Elapsed += OnRightTimedEvent;

            List<Control> leftContextItems = new();
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
                Header = "neues temp. Passwort geneieren"
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
                Name = "mnuItemMExport",
                Header = "markierte Elemente exportieren"
            };
            mnuItemMExport.Click += OnMnuExportClick;
            leftContextItems.Add(cbMAnfangsPassword);
            leftContextItems.Add(cbMEltern);
            leftContextItems.Add(cbMLLGIntern);
            leftContextItems.Add(mnuItemMSerienbrief);
            leftContextItems.Add(mnuItemMPasswordGenerieren);
            leftContextItems.Add(mnuItemMExport);
            leftContext.Items = leftContextItems;
            LeftListBox.ContextMenu = leftContext;
            rbL.IsChecked = true;
            LeftListBox.MaxHeight = ClientSize.Height * 1.1;
            RightListBox.MaxHeight = LeftListBox.MaxHeight;
        }

        private static void SetupSaveFileDialog(SaveFileDialog sfd, string dialogtitle, string[] extensions,
            string[] extensionsanames)
        {
            if (extensions.Length != extensionsanames.Length) return;
            sfd.DefaultExtension = extensions[0];
            sfd.Title = dialogtitle;
            List<FileDialogFilter> filters = new();
            for (var i = 0; i < extensions.Length; i++)
            {
                FileDialogFilter filter = new();
                List<string> extension = new() { extensions[i] };
                filter.Extensions = extension;
                filter.Name = extensionsanames[i];
                filters.Add(filter);
            }

            sfd.Filters = filters;
        }

        private static void SetupOpenFileDialog(OpenFileDialog ofd, string dialogtitle, string[] extensions,
            string[] extensionsanames)
        {
            if (extensions.Length != extensionsanames.Length) return;
            ofd.Title = dialogtitle;
            ofd.AllowMultiple = false;
            List<FileDialogFilter> filters = new();
            for (var i = 0; i < extensions.Length; i++)
            {
                FileDialogFilter filter = new();
                List<string> extension = new() { extensions[i] };
                filter.Extensions = extension;
                filter.Name = extensionsanames[i];
                filters.Add(filter);
            }

            ofd.Filters = filters;
        }

        private void SetupOpenFolderDialog(SystemDialog ofd, string dialogtitle)
        {
            ofd.Title = dialogtitle;
        }

        public async void OnMnuSchoolLoadClick(object? sender, RoutedEventArgs e)
        {
            SetupOpenFileDialog(globalOpenFileDialog, "Lade Datenbankdatei", new[] { "sqlite" },
                new[] { "Datenbankdatei" });
            var respath = await globalOpenFileDialog.ShowAsync(this);
            if (respath is { Length: > 0 })
            {
                myschool = new Schuldatenbank(respath[0]);
                Title = "SchildToSchule - " + await myschool.GetFilePath();
            }

            InitData();
        }


        public async void OnMnuschuleschließenClick(object? sender, RoutedEventArgs e)
        {
            if (myschool.GetFilePath().Result != ":memory:")
            {
                var leftlist = this.GetControl<ListBox>("LeftListBox");
                var rightlist = this.GetControl<ListBox>("RightListBox");
                leftlist.Items = new List<string>();
                rightlist.Items = new List<string>();
                Title = "SchildToSchule";
                myschool.CloseDB();
                myschool = new Schuldatenbank(":memory:");
                ClearTextFields();
                return;
            }

            SetupSaveFileDialog(globalSaveFileDialog, "Bitte einen Dateipfad angeben...", new[] { "sqlite" },
                new[] { "Datenbankdatei" });
            var saveDBFile = async () =>
            {
                var filepath = await globalSaveFileDialog.ShowAsync(this);
                if (filepath == null) return;
                var tempDB = new Schuldatenbank(filepath);
                var res = await tempDB.Import(myschool);
                if (res == 0)
                {
                    myschool = tempDB;
                    var saveDBInPath = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                        new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.Ok,
                            ContentTitle = "Erfolg",
                            ContentMessage =
                                "Datenbank erfolgreich gespeichert",
                            Icon = MessageBox.Avalonia.Enums.Icon.Success
                        });
                    await saveDBInPath.Show();
                }
                else
                {
                    var errorNoSystemDialog = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                        new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.Ok,
                            ContentTitle = "Fehler",
                            ContentMessage =
                                "Schließen fehlgeschlagen",
                            Icon = MessageBox.Avalonia.Enums.Icon.Error
                        });
                    await errorNoSystemDialog.Show();
                }
            };
            await Task.Run(saveDBFile);
        }

        public async void OnMnuschulespeichernunterClick(object? sender, RoutedEventArgs e)
        {
            SetupSaveFileDialog(globalSaveFileDialog, "Datenbankdatei speichern unter...", new[] { "sqlite" },
                new[] { "Datenbankdatei" });
            var saveDBFile = async () =>
            {
                var filepath = await globalSaveFileDialog.ShowAsync(this);
                if (filepath == null) return;
                var tempDB = new Schuldatenbank(filepath);
                var res = await tempDB.Import(myschool);
                if (res != 0)
                {
                    var errorNoSystemDialog = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                        new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.Ok,
                            ContentTitle = "Fehler",
                            ContentMessage =
                                "Speichern unter fehlgeschlagen",
                            Icon = MessageBox.Avalonia.Enums.Icon.Error
                        });
                    await errorNoSystemDialog.Show();
                }
                else
                {
                    myschool = tempDB;
                    var saveDBInPath = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                        new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.Ok,
                            ContentTitle = "Erfolg",
                            ContentMessage =
                                "Datenbank erfolgreich gespeichert",
                            Icon = MessageBox.Avalonia.Enums.Icon.Success
                        });
                    await saveDBInPath.Show();
                }
            };
            await Task.Run(saveDBFile);
        }

        public async void OnMnuschuleversspeichernClick(object? sender, RoutedEventArgs e)
        {
            SetupSaveFileDialog(globalSaveFileDialog, "Datenbankdatei verschlüsselt speichern unter...",
                new[] { "aes" },
                new[] { "verschlüsselte Datenbankdatei" });
            if (myschool.GetFilePath().Result == ":memory:") return;
            var getPasswordInput = async () =>
            {
                var pwiWindow = new PasswordInput();
                var test = await pwiWindow.ShowPWDDialog(this);
                return test;
            };
            var inputResult = await Dispatcher.UIThread.InvokeAsync(getPasswordInput, DispatcherPriority.Input);
            if (string.IsNullOrEmpty(inputResult)) return;
            var saveDBFile = async () =>
            {
                var filepath = await globalSaveFileDialog.ShowAsync(this);
                if (filepath == null) return;
                var db_path = await myschool.GetFilePath();
                myschool.CloseDB();
                LocalCryptoServive.FileEncrypt(db_path, filepath, inputResult);
                myschool = new Schuldatenbank(db_path);
            };
            await Task.Run(saveDBFile);
        }

        public async void OnMnuversschuleladenClick(object? sender, RoutedEventArgs e)
        {
            SetupOpenFileDialog(globalOpenFileDialog, "Lade verschlüsselte Datenbankdatei", new[] { "aes" },
                new[] { "verschlüsselte Datenbankdatei" });
            var respath = await globalOpenFileDialog.ShowAsync(this);
            if (respath is not { Length: > 0 }) return;
            SetupSaveFileDialog(globalSaveFileDialog, "Datenbankdatei speichern unter...", new[] { "sqlite" },
                new[] { "Datenbankdatei" });
            var getPasswordInput = async () =>
            {
                var pwiWindow = new PasswordInput();
                var test = await pwiWindow.ShowPWDDialog(this);
                return test;
            };
            var inputResult = await Dispatcher.UIThread.InvokeAsync(getPasswordInput, DispatcherPriority.Input);
            if (string.IsNullOrEmpty(inputResult)) return;
            var saveDBFile = async () =>
            {
                var filepath = await globalSaveFileDialog.ShowAsync(this);
                if (filepath == null) return;

                LocalCryptoServive.FileDecrypt(respath[0], filepath, inputResult);
                myschool = new Schuldatenbank(filepath);
            };
            await Task.Run(saveDBFile);
        }

        public void OnMnuexitClick(object? sender, RoutedEventArgs e)
        {
            myschool.Dispose();
            Close();
        }

        public async void OnMnuloadfolderClick(object? sender, RoutedEventArgs e)
        {
            var readFileTask = async () =>
            {
                SetupOpenFolderDialog(globalOpenFolderDialog, "Bitte den Ordner mit den Dateien auswählen");
                var folder = await globalOpenFolderDialog.ShowAsync(this);
                if (folder == null) return;
                if (File.Exists(folder + "/sus.csv") && File.Exists(folder + "/lul.csv") &&
                    File.Exists(folder + "/kurse.csv"))
                {
                    await myschool.SusEinlesen(folder + "/sus.csv");
                    await myschool.LulEinlesen(folder + "/lul.csv");
                    await myschool.KurseEinlesen(folder + "/kurse.csv");
                    var loadFolderMessageBox = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                        new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.Ok,
                            ContentTitle = "Information",
                            ContentMessage =
                                "Import erfolgreich",
                            Icon = MessageBox.Avalonia.Enums.Icon.Info
                        });
                    await loadFolderMessageBox.Show();
                }
            };
            await Task.Run(readFileTask);
        }

        public async void OnMnuloadsusfromfileClick(object? sender, RoutedEventArgs e)
        {
            SetupOpenFileDialog(globalOpenFileDialog, "Lade Schüler:innendaten", new[] { "csv", "*" },
                new[] { "CSV-Datei", "Alle-Dateien" });
            var respath = await globalOpenFileDialog.ShowAsync(this);
            if (respath is { Length: > 0 })
            {
                await myschool.SusEinlesen(respath[0]);
            }
        }

        public async void OnMnuloadlulfromfileClick(object? sender, RoutedEventArgs e)
        {
            SetupOpenFileDialog(globalOpenFileDialog, "Lade Lehrer:innendaten", new[] { "csv", "*" },
                new[] { "CSV-Datei", "Alle-Dateien" });
            var respath = await globalOpenFileDialog.ShowAsync(this);
            if (respath is { Length: > 0 })
            {
                await myschool.LulEinlesen(respath[0]);
            }
        }

        public async void OnMnuloadkursefromfileClick(object? sender, RoutedEventArgs e)
        {
            SetupOpenFileDialog(globalOpenFileDialog, "Lade Kursdaten", new[] { "csv", "*" },
                new[] { "CSV-Datei", "Alle-Dateien" });
            var respath = await globalOpenFileDialog.ShowAsync(this);
            if (respath is { Length: > 0 })
            {
                await myschool.KurseEinlesen(respath[0]);
            }
        }

        public async void OnMnuloadusernamesmailClick(object? sender, RoutedEventArgs e)
        {
            SetupOpenFileDialog(globalOpenFileDialog, "Lade Nutzernamen & Mailadressen", new[] { "csv", "*" },
                new[] { "CSV-Datei", "Alle-Dateien" });
            var respath = await globalOpenFileDialog.ShowAsync(this);
            if (respath is { Length: > 0 })
            {
                await myschool.IdsEinlesen(respath[0]);
            }
        }

        public async void OnMnuloadzweitaccountsClick(object? sender, RoutedEventArgs e)
        {
            SetupOpenFileDialog(globalOpenFileDialog, "Lade Zweitaccountdaten", new[] { "csv", "*" },
                new[] { "CSV-Datei", "Alle-Dateien" });
            var respath = await globalOpenFileDialog.ShowAsync(this);
            if (respath is { Length: > 0 })
            {
                await myschool.ZweitAccountsEinlesen(respath[0]);
            }
        }

        public async void OnMnuexporttocsvClick(object? sender, RoutedEventArgs e)
        {
            var readFileTask = async () =>
            {
                SetupOpenFolderDialog(globalOpenFolderDialog, "Bitte den Ordner mit den Dateien auswählen");
                var folder = await globalOpenFolderDialog.ShowAsync(this);
                if (folder == null) return;
                if (!File.Exists(folder + "/sus.csv") && !File.Exists(folder + "/lul.csv") &&
                    !File.Exists(folder + "/kurse.csv"))
                {
                    await myschool.DumpDataToCSVs(folder);
                }
            };
            await Task.Run(readFileTask);
        }

        public async void OnMnuaboutClick(object? sender, RoutedEventArgs e)
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version == null) return;
            var errorNoSystemDialog = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                new MessageBoxStandardParams
                {
                    ButtonDefinitions = ButtonEnum.Ok,
                    ContentTitle = "Über",
                    ContentMessage =
                        Application.Current?.Name + "\n" + version,
                    Icon = MessageBox.Avalonia.Enums.Icon.Info
                });
            await errorNoSystemDialog.Show();
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
            var suskurse = tbSuSKurse.Text.Split(',').ToList();
            if (susid == null || susvname == null || susnname is null || susklasse == null ||
                susnutzername == null || susaximail == null || suselternadresse == null || suszweitadresse == null ||
                susHatZweitaccount == null || susid == "" || susvname == "" || susnname is "" || susklasse == "" ||
                suselternadresse == "") return;
            var sid = Convert.ToInt32(susid);
            if (await myschool.GibtEsSchueler(sid))
            {
                await myschool.UpdateSchueler(sid, susvname, susnname, suselternadresse, susklasse, susnutzername,
                    susaximail, susHatZweitaccount == false ? 0 : 1, suszweitadresse);
                var alteKurse = myschool.GetKursVonSuS(sid).Result;
                foreach (var kurs in alteKurse.Where(kurs => !suskurse.Contains(kurs.Bezeichnung)))
                {
                    await myschool.RemoveSfromK(sid, kurs.Bezeichnung);
                }

                if (suskurse.Count <= 0) return;
                {
                    foreach (var kurs in suskurse)
                    {
                        await myschool.AddStoK(sid, kurs);
                    }
                }
            }
            else
            {
                await myschool.AddSchuelerIn(sid, susvname, susnname, suselternadresse, susklasse, susnutzername,
                    susaximail, susHatZweitaccount == false ? 0 : 1, suszweitadresse);
                if (suskurse.Count == 0) return;
                foreach (var kursbez in suskurse)
                {
                    await myschool.AddStoK(sid, kursbez);
                }
            }
        }

        public async void OnBtnsusdelClick(object? sender, RoutedEventArgs e)
        {
            var susid = tbSuSID.Text;
            var sid = Convert.ToInt32(susid);
            if (myschool.GetSchueler(sid).Result.ID == 0) return;
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
                await myschool.RemoveS(sid);
                return;
            }

            var templist = source.Items.Cast<string>().ToList();
            templist.Remove(tbSuSnachname.Text + "," + tbSuSVorname.Text + ";" + susid);
            await myschool.RemoveS(sid);
            source.Items = templist;
        }

        public async void OnbtnsuseinschreibenClick(object? sender, RoutedEventArgs e)
        {
            var susid = tbSuSID.Text;
            var sid = Convert.ToInt32(susid);
            var susklasse = tbSuSKlasse.Text;
            if (susklasse == "" || sid == 0) return;
            await myschool.AddStoKlassenKurse(await myschool.GetSchueler(sid), susklasse);
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
            var lulkurse = tbLuLKurse.Text.Split(',').ToList();
            if (lulid == null || lulvname == null || lulnname == null || lulkrz == null || lulfakultas == null ||
                lulmail == null || lulpwtemp == null || lulvname == "" || lulnname == "" ||
                lulkrz == "" || lulfakultas == "" ||
                lulmail == "") return;
            if (lulid == "")
            {
                lulid = myschool.GetLehrerIDListe().Result.Max() + 1 + "";
                lulpwtemp = Schuldatenbank.GeneratePasswort(8);
            }

            var lid = Convert.ToInt32(lulid);
            if (await myschool.GibtEsLehrkraft(lid))
            {
                await myschool.UpdateLehrkraft(lid, lulvname, lulnname, lulkrz, lulmail, lulfakultas, lulpwtemp);
                var alteKurse = myschool.GetKursVonLuL(lid).Result;
                foreach (var kurs in alteKurse.Where(kurs => !lulkurse.Contains(kurs.Bezeichnung)))
                {
                    await myschool.RemoveLfromK(lid, kurs.Bezeichnung);
                }

                if (lulkurse.Count <= 0) return;
                {
                    foreach (var kurs in lulkurse)
                    {
                        await myschool.AddLtoK(lid, kurs);
                    }
                }
            }
            else
            {
                await myschool.Addlehrkraft(lid, lulvname, lulnname, lulkrz, lulmail, lulfakultas);
                if (lulkurse.Count == 0) return;
                foreach (var kurs in lulkurse)
                {
                    await myschool.AddLtoK(lid, kurs);
                }
            }
        }

        public async void OnBtnluldelClick(object? sender, RoutedEventArgs e)
        {
            var lulid = tbLuLID.Text;
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
                await myschool.RemoveL(lid);
                return;
            }

            var templist = source.Items.Cast<string>().ToList();
            templist.Remove(tbLuLKuerzel + ";" + tbLuLNachname + "," + tbLuLVorname);
            await myschool.RemoveL(lid);
            source.Items = templist;
        }

        private void InitData()
        {
            if (CboxDataLeft == null || CboxDataRight == null) return;
            CboxDataLeft.SelectedIndex = 0;
            CboxDataRight.SelectedIndex = 1;
            var llist = myschool.GetSchuelerListe().Result.Select(s => (s.Nachname + "," + s.Vorname + ";" + s.ID))
                .ToList();
            llist.Sort(Comparer<string>.Default);
            LeftListBox.Items = llist;
        }

        private void CboxDataLeft_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            ClearTextFields();
            rightMutex = true;
            OnLeftDataChanged(true);
            rightMutex = false;
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
            rightMutex = true;
            OnRightDataChanged(true);
            rightMutex = false;
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
            if (rightMutex && !hasComboBoxChanged) return;
            if (hasComboBoxChanged)
            {
                RightListBox.Items = new List<string>();
            }
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
                        var slist = myschool.GetSchuelerListe().Result
                            .Select(s => (s.Nachname + "," + s.Vorname + ";" + s.ID)).Distinct().ToList();
                        slist.Sort(Comparer<string>.Default);
                        LeftListBox.Items = slist;
                        RightListBox.Items = new List<string>();
                    }
                    else
                    {
                        var sid = LeftListBox.SelectedItems[0]?.ToString()?.Split(';')[1];
                        var sus = myschool.GetSchueler(Convert.ToInt32(sid)).Result;
                        LoadSuSData(sus);
                        if (sus.ID == 0) return;
                        switch (CboxDataRight.SelectedIndex)
                        {
                            case 1:
                            {
                                var rlist = myschool.GetLuLvonSuS(sus.ID).Result
                                    .Select(l => (l.Kuerzel + ";" + l.Nachname + "," + l.Vorname)).Distinct().ToList();
                                rlist.Sort(Comparer<string>.Default);
                                RightListBox.Items = rlist;
                                break;
                            }
                            case 2:
                            {
                                var rlist = myschool.GetKursVonSuS(sus.ID).Result.Select(k => (k.Bezeichnung))
                                    .Distinct()
                                    .ToList();
                                RightListBox.Items = rlist;
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
                        var lullist = myschool.GetLehrerListe().Result
                            .Select(l => (l.Kuerzel + ";" + l.Nachname + "," + l.Vorname)).Distinct().ToList();
                        lullist.Sort(Comparer<string>.Default);
                        LeftListBox.Items = lullist;
                        RightListBox.Items = new List<string>();
                    }
                    else
                    {
                        var lulkrz = LeftListBox.SelectedItems[0]?.ToString()?.Split(';')[0];
                        if (lulkrz == "") return;
                        var lul = myschool.GetLehrkraft(lulkrz).Result;
                        LoadLuLData(lul);
                        switch (CboxDataRight.SelectedIndex)
                        {
                            case 0:
                            {
                                var rlist = myschool.GetSuSVonLuL(lul.ID).Result
                                    .Select(s => (s.Nachname + "," + s.Vorname + ";" + s.ID)).Distinct().ToList();
                                rlist.Sort(Comparer<string>.Default);
                                RightListBox.Items = rlist;

                                break;
                            }
                            case 2:
                            {
                                var rlist = myschool.GetKursVonLuL(lul.ID).Result.Select(k => (k.Bezeichnung))
                                    .Distinct()
                                    .ToList();
                                rlist.Sort(Comparer<string>.Default);
                                RightListBox.Items = rlist;
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
                        var klist = myschool.GetKursListe().Result.Select(k => (k.Bezeichnung)).Distinct().ToList();
                        klist.Sort(Comparer<string>.Default);
                        LeftListBox.Items = klist;
                        RightListBox.Items = new List<string>();
                    }
                    else
                    {
                        var kurzbez = LeftListBox.SelectedItems[0]?.ToString();
                        if (kurzbez == "") return;
                        var kurs = myschool.GetKurs(kurzbez).Result;
                        LoadKursData(kurs);
                        switch (CboxDataRight.SelectedIndex)
                        {
                            case 0:
                            {
                                var rlist = myschool.GetSuSAusKurs(kurs.Bezeichnung).Result
                                    .Select(s => (s.Nachname + "," + s.Vorname + ";" + s.ID)).Distinct().ToList();
                                rlist.Sort(Comparer<string>.Default);
                                RightListBox.Items = rlist;
                                break;
                            }
                            case 1:
                            {
                                var rlist = myschool.GetLuLAusKurs(kurs.Bezeichnung).Result
                                    .Select(l => (l.Kuerzel + ";" + l.Nachname + "," + l.Vorname)).Distinct().ToList();
                                rlist.Sort(Comparer<string>.Default);
                                RightListBox.Items = rlist;
                                break;
                            }
                        }
                    }

                    break;
            }
        }

        private void OnRightDataChanged(bool hasComboBoxChanged)
        {
            if (LeftListBox == null || RightListBox == null || CboxDataLeft == null || CboxDataRight == null) return;
            if (RightListBox.SelectedItems == null) return;
            if (rightMutex && !hasComboBoxChanged) return;
            if (hasComboBoxChanged)
            {
                RightListBox.Items = new List<string>();
                RightListBox.SelectedItems.Clear();
            }

            switch (CboxDataRight.SelectedIndex)
            {
                //s=0;l==1;k==2
                case 0:
                    if (CboxDataLeft.SelectedIndex == 0 || LeftListBox.SelectedItems.Count < 1 ||
                        LeftListBox.SelectedItems == null) return;
                    switch (CboxDataLeft.SelectedIndex)
                    {
                        case 1:
                        {
                            var lulkrz = LeftListBox.SelectedItems[0]?.ToString()?.Split(';')[0];
                            if (lulkrz == "") return;
                            var lul = myschool.GetLehrkraft(lulkrz).Result;
                            LoadLuLData(lul);
                            if (RightListBox.SelectedItems.Count > 0)
                            {
                                var sid = RightListBox.SelectedItems[0]?.ToString()?.Split(';')[1];
                                var sus = myschool.GetSchueler(Convert.ToInt32(sid)).Result;
                                if (sus.ID == 0) return;
                                LoadSuSData(sus);
                            }

                            if (!hasComboBoxChanged) return;
                            var rlist = myschool.GetSuSVonLuL(lul.ID).Result
                                .Select(s => (s.Nachname + "," + s.Vorname + ";" + s.ID)).Distinct().ToList();
                            rlist.Sort(Comparer<string>.Default);
                            RightListBox.Items = rlist;
                            break;
                        }
                        case 2:
                        {
                            var kurzbez = LeftListBox.SelectedItems[0]?.ToString();
                            if (kurzbez == "") return;
                            var kurs = myschool.GetKurs(kurzbez).Result;
                            LoadKursData(kurs);
                            if (RightListBox.SelectedItems.Count > 0)
                            {
                                var sid = RightListBox.SelectedItems[0]?.ToString()?.Split(';')[1];
                                var sus = myschool.GetSchueler(Convert.ToInt32(sid)).Result;
                                if (sus.ID == 0) return;
                                LoadSuSData(sus);
                            }

                            if (!hasComboBoxChanged) return;
                            var rlist = myschool.GetSuSAusKurs(kurs.Bezeichnung).Result
                                .Select(s => (s.Nachname + "," + s.Vorname + ";" + s.ID)).Distinct().ToList();
                            rlist.Sort(Comparer<string>.Default);
                            RightListBox.Items = rlist;
                            break;
                        }
                    }

                    break;
                case 1:
                    if (CboxDataLeft.SelectedIndex == 1 || LeftListBox.SelectedItems.Count < 1 ||
                        LeftListBox.SelectedItems == null) return;
                    switch (CboxDataLeft.SelectedIndex)
                    {
                        case 0:
                        {
                            var sid = LeftListBox.SelectedItems[0]?.ToString()?.Split(';')[1];
                            var sus = myschool.GetSchueler(Convert.ToInt32(sid)).Result;
                            if (sus.ID == 0) return;
                            LoadSuSData(sus);
                            if (RightListBox.SelectedItems.Count > 0)
                            {
                                var lulkrz = RightListBox.SelectedItems[0]?.ToString()?.Split(';')[0];
                                if (lulkrz == "") return;
                                var lul = myschool.GetLehrkraft(lulkrz).Result;
                                LoadLuLData(lul);
                            }

                            if (!hasComboBoxChanged) return;
                            var rlist = myschool.GetLuLvonSuS(sus.ID).Result
                                .Select(l => (l.Kuerzel + ";" + l.Nachname + "," + l.Vorname)).Distinct().ToList();
                            rlist.Sort(Comparer<string>.Default);
                            RightListBox.Items = rlist;
                            break;
                        }
                        case 2:
                        {
                            var kurzbez = LeftListBox.SelectedItems[0]?.ToString();
                            if (kurzbez == "") return;
                            var kurs = myschool.GetKurs(kurzbez).Result;
                            LoadKursData(kurs);
                            if (RightListBox.SelectedItems.Count > 0)
                            {
                                var lulkrz = RightListBox.SelectedItems[0]?.ToString()?.Split(';')[0];
                                if (lulkrz == "") return;
                                var lul = myschool.GetLehrkraft(lulkrz).Result;
                                LoadLuLData(lul);
                            }

                            if (!hasComboBoxChanged) return;
                            var rlist = myschool.GetLuLAusKurs(kurs.Bezeichnung).Result
                                .Select(l => (l.Kuerzel + ";" + l.Nachname + "," + l.Vorname)).Distinct().ToList();
                            rlist.Sort(Comparer<string>.Default);
                            RightListBox.Items = rlist;
                            break;
                        }
                    }

                    break;
                case 2:
                    if (CboxDataLeft.SelectedIndex == 2 || LeftListBox.SelectedItems.Count < 1 ||
                        LeftListBox.SelectedItems == null) return;
                    switch (CboxDataLeft.SelectedIndex)
                    {
                        case 0:
                        {
                            var sid = LeftListBox.SelectedItems[0]?.ToString()?.Split(';')[1];
                            var sus = myschool.GetSchueler(Convert.ToInt32(sid)).Result;
                            if (sus.ID == 0) return;
                            LoadSuSData(sus);
                            if (RightListBox.SelectedItems.Count > 0)
                            {
                                var kurzbez = RightListBox.SelectedItems[0]?.ToString();
                                if (kurzbez == "") return;
                                var kurs = myschool.GetKurs(kurzbez).Result;
                                LoadKursData(kurs);
                            }

                            if (!hasComboBoxChanged) return;
                            var rlist = myschool.GetKursVonSuS(sus.ID).Result.Select(k => (k.Bezeichnung))
                                .Distinct()
                                .ToList();
                            RightListBox.Items = rlist;
                            break;
                        }
                        case 1:
                        {
                            var lulkrz = LeftListBox.SelectedItems[0]?.ToString()?.Split(';')[0];
                            if (lulkrz == "") return;
                            var lul = myschool.GetLehrkraft(lulkrz).Result;
                            LoadLuLData(lul);
                            if (RightListBox.SelectedItems.Count > 0)
                            {
                                var kurzbez = RightListBox.SelectedItems[0]?.ToString();
                                if (kurzbez == "") return;
                                var kurs = myschool.GetKurs(kurzbez).Result;
                                LoadKursData(kurs);
                            }

                            if (!hasComboBoxChanged) return;
                            var rlist = myschool.GetKursVonLuL(lul.ID).Result.Select(k => (k.Bezeichnung))
                                .Distinct()
                                .ToList();
                            rlist.Sort(Comparer<string>.Default);
                            RightListBox.Items = rlist;
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
            tbSuSKurse.Text = myschool.GetKursVonSuS(s.ID).Result
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
            tbLuLKurse.Text = myschool.GetKursVonLuL(l.ID).Result
                .Aggregate("", (current, kurs) => current + (kurs.Bezeichnung + ",")).TrimEnd(',');
        }

        private void LoadKursData(Kurs k)
        {
            if (k.Bezeichnung == "") return;
            tbKursbezeichnung.Text = k.Bezeichnung;
            tbKursLuL.Text = myschool.GetLuLAusKurs(k.Bezeichnung).Result
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
                var errorNoSystemDialog = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                    new MessageBoxStandardParams
                    {
                        ButtonDefinitions = ButtonEnum.Ok,
                        ContentTitle = "Kein Zielsystem ausgewählt",
                        ContentMessage =
                            "Bitte wählen Sie entweder Moodle und/oder AIX als Zielsystem!",
                        Icon = MessageBox.Avalonia.Enums.Icon.Error
                    });
                await errorNoSystemDialog.Show();
                return;
            }

            var readFileTask = async () =>
            {
                SetupOpenFolderDialog(globalOpenFolderDialog, "Bitte den Ordner zum Speichern angeben");
                var folder = await globalOpenFolderDialog.ShowAsync(this);
                if (folder == null) return;
                var expandFiles = false;
                if (File.Exists(folder + "/aix_sus.csv") || File.Exists(folder + "/aix_lul.csv") ||
                    File.Exists(folder + "/mdl_einschreibungen.csv") || File.Exists(folder + "/mdl_kurse.csv") ||
                    File.Exists(folder + "/mdl_nutzer.csv"))
                {
                    var overwriteFilesDialog = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                        new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.YesNo,
                            ContentTitle = "Dateien gefunden",
                            ContentHeader = "Überschreiben?",
                            ContentMessage =
                                "Im Ordner existieren schon eine/mehrere Exportdateien.\nSollen diese überschrieben werden?",
                            Icon = MessageBox.Avalonia.Enums.Icon.Question
                        });
                    var dialogResult = await overwriteFilesDialog.ShowDialog(this);
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
                var nurMoodleSuffix = (cbNurMoodleSuffix.IsChecked != null);
                if (cbExportVorlagenkurse.IsChecked != null && cbExportVorlagenkurse.IsChecked.Value)
                {
                    kursvorlagen[0] = tbExportKl.Text;
                    kursvorlagen[1] = tbExportF.Text;
                }

                var res = await myschool.ExportCSV(folder, destsys, whattoexport,
                    cbExportwithPasswort.IsChecked != null && cbExportwithPasswort.IsChecked.Value,
                    expandFiles, nurMoodleSuffix, kursvorlagen,
                    await myschool.GetSchuelerIDListe(), await myschool.GetLehrerIDListe(),
                    await myschool.GetKursBezListe());
                await CheckSuccesfulExport(res);
            };

            await Dispatcher.UIThread.InvokeAsync(readFileTask);
        }

        private void BtnFehlerSuche_OnClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var ergebnisliste = new List<string>();
                if (cbFehlerLeereKurse.IsChecked != null && cbFehlerLeereKurse.IsChecked.Value)
                {
                    foreach (var k in myschool.GetKursListe().Result)
                    {
                        if (myschool.GetSuSAusKurs(k.Bezeichnung).Result.Count == 0)
                        {
                            ergebnisliste.Add(k.Bezeichnung + " ohne SuS");
                        }

                        if (myschool.GetLuLAusKurs(k.Bezeichnung).Result.Count == 0)
                        {
                            ergebnisliste.Add(k.Bezeichnung + " ohne LuL");
                        }
                    }
                }

                if (cbFehlerSuSoK.IsChecked != null && cbFehlerSuSoK.IsChecked.Value)
                {
                    ergebnisliste.AddRange(from sus in myschool.GetSchuelerListe().Result
                        where myschool.GetKursVonSuS(Convert.ToInt32(sus.ID)).Result.Count == 0
                        select sus.Nachname + ", " + sus.Vorname + ";" + sus.ID + ";ohne Kurs");
                }

                if (cbFehlerLuLoK.IsChecked != null && cbFehlerLuLoK.IsChecked.Value)
                {
                    ergebnisliste.AddRange(from lul in myschool.GetLehrerListe().Result
                        where myschool.GetKursVonLuL(Convert.ToInt32(lul.ID)).Result.Count == 0
                        select lul.Nachname + ", " + lul.Vorname + ";" + lul.ID + ";ohne Kurs");
                }

                if (cbFehlerLuL.IsChecked != null && cbFehlerLuL.IsChecked.Value)
                {
                    ergebnisliste.AddRange(from lul in myschool.GetLehrerListe().Result
                        where lul.Fakultas.Contains("NV")
                        select lul.Nachname + ", " + lul.Vorname + ";" + lul.ID + ";mit fehlerhafter Fakultas");
                }

                if (cbFehlerKurse.IsChecked != null && cbFehlerKurse.IsChecked.Value)
                {
                    ergebnisliste.AddRange(from kurs in myschool.GetKursListe().Result
                        where kurs.Fach.Length == 0 || kurs.Fach.Equals("---")
                        select kurs.Bezeichnung + " mit fehlerhaftem Fach");
                }

                if (cbFehlerSuS.IsChecked != null && cbFehlerSuS.IsChecked.Value)
                {
                    foreach (var sus in myschool.GetSchuelerListe().Result)
                    {
                        if (sus.Nutzername.Equals(""))
                        {
                            ergebnisliste.Add(sus.Nachname + ", " + sus.Vorname + ";Klasse " + sus.Klasse + ";" +
                                              sus.ID + ";ohne Nutzernamen");
                        }

                        if (sus.Aixmail.Contains(myschool.GetSettings().Result[0].Split(';')[1]))
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

                lbFehlerliste.Items = ergebnisliste;
            }
            catch (Exception ex)
            {
#if DEBUG
                _ = myschool.AddLogMessage("Debug", ex.StackTrace + ";" + ex.Message);
#endif
                _ = myschool.AddLogMessage("Fehler", "Fehler bei der Fehlersuche " + ex.Message);
            }
        }

        private async void BtnFehlerExport_OnClick(object? sender, RoutedEventArgs e)
        {
            SetupSaveFileDialog(globalSaveFileDialog, "Speichern unter...", new[] { "csv" }, new[] { "CSV-Datei" });
            var saveDBFile = async () =>
            {
                var filepath = await globalSaveFileDialog.ShowAsync(this);
                if (filepath == null) return;

                await File.WriteAllLinesAsync(filepath, lbFehlerliste.Items.Cast<string>(), Encoding.UTF8);
            };
            await Task.Run(saveDBFile);
        }

        private async void BtnExportStufenkurs_OnClick(object? sender, RoutedEventArgs e)
        {
            if (tbExportStufenkurse.Text == "") return;
            var readFileTask = async () =>
            {
                SetupOpenFolderDialog(globalOpenFolderDialog, "Bitte den Ordner für die Dateien auswählen");
                var folder = await globalOpenFolderDialog.ShowAsync(this);
                if (folder == null) return;
                int res;
                var nurMoodleSuffix = (cbNurMoodleSuffix.IsChecked != null);
                if (!tbExportStufenkurse.Text.Contains(';'))
                {
                    res = await myschool.ExportCSV(folder, "all", "s", false, false, nurMoodleSuffix,new[] { "", "" },
                        myschool.GetSusAusStufe(tbExportStufenkurse.Text).Result.Select(s => s.ID).ToList(),
                        new List<int>(), new List<string>());
                }
                else
                {
                    var suslist = new List<int>();
                    var stufen = tbExportStufenkurse.Text.Split(';');
                    foreach (var stufe in stufen)
                    {
                        suslist.AddRange(myschool.GetSusAusStufe(stufe).Result.Select(s => s.ID).ToList());
                    }

                    res = await myschool.ExportCSV(folder, "all", "s", false, false, nurMoodleSuffix, new[] { "", "" },
                        suslist,
                        new List<int>(), new List<string>());
                }
                await CheckSuccesfulExport(res);
            };
            await Task.Run(readFileTask);
        }

        private static async Task CheckSuccesfulExport(int res)
        {
            if (res == 1)
            {
                var successExportDialog = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                    new MessageBoxStandardParams
                    {
                        ButtonDefinitions = ButtonEnum.Ok,
                        ContentTitle = "Export erfolgreich",
                        ContentMessage =
                            "Der Expport war erfolgreich",
                        Icon = MessageBox.Avalonia.Enums.Icon.Info
                    });
                await successExportDialog.Show();
            }
            else
            {
                var failedExportDialog = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                    new MessageBoxStandardParams
                    {
                        ButtonDefinitions = ButtonEnum.Ok,
                        ContentTitle = "Export fehlgeschlagen",
                        ContentMessage =
                            "Export war nicht erfolgreiche. Bitte im Log nachschauen",
                        Icon = MessageBox.Avalonia.Enums.Icon.Error
                    });
                await failedExportDialog.Show();
            }
        }

        private async void BtnExport5InklPasswort_OnClick(object? sender, RoutedEventArgs e)
        {
            var readFileTask = async () =>
            {
                SetupOpenFolderDialog(globalOpenFolderDialog, "Bitte den Ordner für die Dateien auswählen");
                var folder = await globalOpenFolderDialog.ShowAsync(this);
                if (folder == null) return;
                var nurMoodleSuffix = (cbNurMoodleSuffix.IsChecked != null);
                var res = await myschool.ExportCSV(folder, "all", "s", false, false, nurMoodleSuffix,new[] { "", "" },
                    myschool.GetSusAusStufe("5").Result.Select(s => s.ID).ToList(),
                    new List<int>(), new List<string>());
                await CheckSuccesfulExport(res);
            };
            await Task.Run(readFileTask);
        }

        private async void BtnSettingSave_OnClick(object? sender, RoutedEventArgs e)
        {
            await myschool.SetSettings(tbSettingMailplatzhalter.Text, tbSettingKursersetzung.Text,
                tbSettingFachkurz.Text.Split('\n'), tbSettingFachlang.Text.Split('\n'), tbSettingKurssuffix.Text);
        }

        private async void BtnLogDelete_OnClick(object? sender, RoutedEventArgs e)
        {
            var reallyDeleteLog = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                new MessageBoxStandardParams
                {
                    ButtonDefinitions = ButtonEnum.YesNo,
                    ContentTitle = "Log löschen",
                    ContentHeader = "Sicherheitsabfrage",
                    ContentMessage =
                        "Möchten Sie das Log wirklich löschen?",
                    Icon = MessageBox.Avalonia.Enums.Icon.Question
                });
            var dialogResult = await reallyDeleteLog.ShowDialog(this);
            if (dialogResult == ButtonResult.No) return;
            lbLogDisplay.Items = new List<string>();
            await myschool.LoescheLog();
        }

        private async void BtnLogReload_OnClick(object? sender, RoutedEventArgs e)
        {
            var items = await myschool.GetLog();
            lbLogDisplay.Items = items.Select(message => message.Replace('\t', ' ').TrimEnd('\t')).ToList();
        }

        private async void BtnKurseAdd_OnClick(object? sender, RoutedEventArgs e)
        {
            var kursbez = tbKursbezeichnung.Text;
            var lehrkraefte = tbKursLuL.Text;
            var kursfach = tbKursFach.Text;
            var kurssuffix = tbKursSuffix.Text == "" ? tbSettingKurssuffix.Text : tbKursSuffix.Text;
            var kursklasse = tbKursKlasse.Text;
            var kursstufe = tbKursStufe.Text;
            var istKurs = cbKursIstKurs.IsChecked != null && cbKursIstKurs.IsChecked.Value;
            if (kursbez == null || lehrkraefte == null || kursfach == null || kursklasse == null || kursstufe == null ||
                kursbez == "" || lehrkraefte == "" || kursfach == "" || kursklasse == "" || kursstufe == "") return;
            if (await myschool.GibtEsKurs(kursbez))
            {
                await myschool.UpdateKurs(kursbez, kursfach, kursklasse, kursstufe, kurssuffix,
                    Convert.ToInt32(istKurs));
                List<LuL> tList = new();
                foreach (var lehrkraft in lehrkraefte.Split((';')))
                {
                    tList.Add(await myschool.GetLehrkraft(lehrkraft));
                }

                var tListAusKurs = await myschool.GetLuLAusKurs(kursbez);
                foreach (var lehrkraft in tListAusKurs.Where(lehrkraft => !tList.Contains(lehrkraft)))
                {
                    await myschool.RemoveLfromK(lehrkraft, await myschool.GetKurs(kursbez));
                }

                foreach (var lehrkraft in tList)
                {
                    await myschool.AddLtoK(lehrkraft, await myschool.GetKurs(kursbez));
                }
            }
            else
            {
                await myschool.AddKurs(kursbez, kursfach, kursklasse, kursstufe, kurssuffix, Convert.ToInt32(istKurs));
            }

            foreach (var lehrkraft in lehrkraefte.Split((';')))
            {
                await myschool.AddLtoK(await myschool.GetLehrkraft(lehrkraft), await myschool.GetKurs(kursbez));
            }

            if (cbKursSuSdKlasseEinschreiben.IsChecked != null && cbKursSuSdKlasseEinschreiben.IsChecked.Value)
            {
                foreach (var sus in await myschool.GetSuSAusKlasse(kursklasse))
                {
                    await myschool.AddStoK(sus, await myschool.GetKurs(kursbez));
                }
            }

            if (cbKursSuSdStufeEinschreiben.IsChecked != null && !cbKursSuSdStufeEinschreiben.IsChecked.Value) return;
            {
                foreach (var sus in await myschool.GetSusAusStufe(kursklasse))
                {
                    await myschool.AddStoK(sus, await myschool.GetKurs(kursbez));
                }
            }
        }

        private async void BtnKurseDel_OnClick(object? sender, RoutedEventArgs e)
        {
            var kursbez = tbKursbezeichnung.Text;
            await myschool.RemoveK(kursbez);
            OnLeftDataChanged(true);
        }

        private async void OnLeftTimedEvent(object? source, ElapsedEventArgs e)
        {
            var updateLeftList = () =>
            {
                LeftListBox.Items = LeftListBox.Items.Cast<string>()
                    .Where(listitem => listitem.ToLower().Contains(tbLeftSearch.Text.ToLower()));
            };
            await Dispatcher.UIThread.InvokeAsync(updateLeftList);
            leftInputTimer.Enabled = false;
        }

        private async void OnRightTimedEvent(object? source, ElapsedEventArgs e)
        {
            var updateRightList = () =>
            {
                RightListBox.Items = RightListBox.Items.Cast<string>()
                    .Where(listitem => listitem.ToLower().Contains(tbRightSearch.Text.ToLower()));
            };
            await Dispatcher.UIThread.InvokeAsync(updateRightList);
            rightInputTimer.Enabled = false;
        }

        private void TbLeftSearch_OnKeyUp(object? sender, KeyEventArgs e)
        {
            leftInputTimer.Enabled = true;
            leftInputTimer.Start();
        }

        private void TbRightSearch_OnKeyUp(object? sender, KeyEventArgs e)
        {
            rightInputTimer.Enabled = true;
            rightInputTimer.Start();
        }

        private async void OnMnuSerienbriefClick(object? sender, RoutedEventArgs e)
        {
            var readFileTask = async () =>
            {
                SetupSaveFileDialog(globalSaveFileDialog, "Serienbriefdatei...", new[] { "csv" },
                    new[] { "CSV-Datei" });
                var folder = await globalOpenFolderDialog.ShowAsync(this);
                if (folder == null) return;
                List<string> susausgabe = new() { "Vorname;Nachname;Anmeldename;Kennwort;E-Mail;Klasse" };
                switch (CboxDataLeft.SelectedIndex)
                {
                    case 0:
                        susausgabe.AddRange(LeftListBox.SelectedItems.Cast<string>().ToList()
                            .Select(sus => myschool.GetSchueler(Convert.ToInt32(sus.Split(';')[1])).Result).Select(s =>
                                s.Vorname + ";" + s.Nachname + ";" + s.Nutzername + ";" + "Klasse" + s.Klasse +
                                DateTime.Now.Year + "!;" + s.Aixmail + ";" + s.Klasse));
                        await File.WriteAllLinesAsync(folder, susausgabe.Distinct().ToList(), Encoding.UTF8);
                        break;
                    case 2:
                        foreach (string kursbez in LeftListBox.SelectedItems)
                        {
                            susausgabe.AddRange(myschool.GetSuSAusKurs(kursbez).Result.Distinct().Select(s =>
                                s.Vorname + ";" + s.Nachname + ";" + s.Nutzername + ";" + "Klasse" + s.Klasse +
                                DateTime.Now.Year + "!;" + s.Aixmail + ";" + s.Klasse));
                        }

                        await File.WriteAllLinesAsync(folder, susausgabe.Distinct().ToList(), Encoding.UTF8);
                        break;
                    default: return;
                }
            };
            await Task.Run(readFileTask);
        }

        private async void OnMnuPasswordGenClick(object? sender, RoutedEventArgs e)
        {
            if (CboxDataLeft.SelectedIndex != 1) return;
            await myschool.StartTransaction();
            foreach (string luleintrag in LeftListBox.SelectedItems)
            {
                var lul = await myschool.GetLehrkraft(luleintrag.Split(';')[0]);
                myschool.SetTPwd(lul.ID, Schuldatenbank.GeneratePasswort(8));
            }

            await myschool.StopTransaction();
        }

        private async void OnMnuExportClick(object? sender, RoutedEventArgs e)
        {
            var readFileTask = async () =>
            {
                SetupOpenFolderDialog(globalOpenFolderDialog, "Bitte den Ordner zum Speichern angeben");
                var folder = await globalOpenFolderDialog.ShowAsync(this);
                if (folder == null) return;
                var expandFiles = false;
                if (File.Exists(folder + "/aix_sus.csv") || File.Exists(folder + "/aix_lul.csv") ||
                    File.Exists(folder + "/mdl_einschreibungen.csv") || File.Exists(folder + "/mdl_kurse.csv") ||
                    File.Exists(folder + "/mdl_nutzer.csv"))
                {
                    var overwriteFilesDialog = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                        new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.YesNo,
                            ContentTitle = "Dateien gefunden",
                            ContentHeader = "Überschreiben?",
                            ContentMessage =
                                "Im Ordner existieren schon eine/mehrere Exportdateien.\nSollen diese überschrieben werden?",
                            Icon = MessageBox.Avalonia.Enums.Icon.Question
                        });
                    var dialogResult = await overwriteFilesDialog.ShowDialog(this);
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
                            suslist.Add(await myschool.GetSchueler(Convert.ToInt32(suseintrag.Split(';')[1])));
                        }

                        break;
                    case 1:
                        whattoexport += "l";
                        if (LeftListBox.ContextMenu != null)
                        {
                            var isllginternChecked = ((CheckBox)LeftListBox.ContextMenu.Items.Cast<Control>()
                                .Where(c => c.Name == "cbMnuLeftContextLLGIntern").ToList().First()).IsChecked;
                            if (isllginternChecked != null && isllginternChecked.Value) whattoexport += "i";
                        }

                        foreach (string luleintrag in LeftListBox.SelectedItems)
                        {
                            lullist.Add(await myschool.GetLehrkraft(luleintrag.Split(';')[0]));
                        }

                        break;
                    case 2:
                        whattoexport += "k";
                        foreach (string kurseintrag in LeftListBox.SelectedItems)
                        {
                            kurslist.Add(await myschool.GetKurs(kurseintrag));
                        }

                        break;
                    default:
                        return;
                }

                if (LeftListBox.ContextMenu != null)
                {
                    var isElternChecked = ((CheckBox)LeftListBox.ContextMenu.Items.Cast<Control>()
                        .Where(c => c.Name == "cbMnuLeftContextEltern").ToList().First()).IsChecked;
                    if (isElternChecked != null && isElternChecked.Value) whattoexport += "e";
                }

                var kursvorlagen = new[] { "", "" };
                if (cbExportVorlagenkurse.IsChecked != null && cbExportVorlagenkurse.IsChecked.Value)
                {
                    kursvorlagen[0] = tbExportKl.Text;
                    kursvorlagen[1] = tbExportF.Text;
                }

                if (LeftListBox.ContextMenu != null)
                {
                    var isAnfangsPasswortChecked = ((CheckBox)LeftListBox.ContextMenu.Items.Cast<Control>()
                        .Where(c => c.Name == "cbMnuLeftContextAnfangsPasswort").ToList().First()).IsChecked;
                    var nurMoodleSuffix = (cbNurMoodleSuffix.IsChecked != null);
                    var res = await myschool.ExportCSV(folder, destsys, whattoexport,
                        isAnfangsPasswortChecked != null && isAnfangsPasswortChecked.Value,
                        expandFiles, nurMoodleSuffix, kursvorlagen,
                        suslist.Select(s => s.ID).Distinct().ToList(), lullist.Select(l => l.ID).Distinct().ToList(),
                        kurslist.Select(k => k.Bezeichnung).Distinct().ToList());
                    await CheckSuccesfulExport(res);
                }
            };

            await Dispatcher.UIThread.InvokeAsync(readFileTask);
        }

        private async void MnuLoadElternMails_OnClick(object? sender, RoutedEventArgs e)
        {
            SetupOpenFileDialog(globalOpenFileDialog, "Lade Elternmailadressen", new[] { "csv", "*" },
                new[] { "CSV-Datei", "Alle-Dateien" });
            var respath = await globalOpenFileDialog.ShowAsync(this);
            if (respath is { Length: > 0 })
            {
                await myschool.ElternEinlesen(respath[0]);
            }
        }

        private async void MnuExportLKtoHP_OnClick(object? sender, RoutedEventArgs e)
        {
            SetupSaveFileDialog(globalSaveFileDialog, "Lehrkräfteexport für die Homepage", new[] { "csv" },
                new[] { "CSV-Datei" });
            var saveLKtoHP = async () =>
            {
                var filepath = await globalSaveFileDialog.ShowAsync(this);
                if (filepath == null) return;
                List<string> lulliste = new()
                {
                    "Kürzel;Nachname;Vorname;Fächer;Mailadresse"
                };
                lulliste.AddRange(myschool.GetLehrerListe().Result.Select(lehrer =>
                    lehrer.Kuerzel + ";" + lehrer.Nachname + ";" + lehrer.Vorname + ";" +
                    lehrer.Fakultas + ";" + lehrer.Mail).OrderBy(s => s.Split(';')[0]));
                await File.WriteAllLinesAsync(filepath, lulliste, Encoding.UTF8);
            };
            await Task.Run(saveLKtoHP);
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
                Background = darkBackgroundColor;
                LeftListBox.Background = darkBackgroundColor;
                RightListBox.Background = darkBackgroundColor;
                lbFehlerliste.Background = darkBackgroundColor;
            }
            else if (sender.Equals(rbL))
            {
                Background = lightBackgroundColor;
                LeftListBox.Background = lightBackgroundColor;
                RightListBox.Background = lightBackgroundColor;
                lbFehlerliste.Background = lightBackgroundColor;
            }
        }

        private async void BtnLuLNewTmpPasswort_OnClick(object? sender, RoutedEventArgs e)
        {
            if (tbLuLKuerzel == null || tbLuLID == null || tbLuLKuerzel.Text == "" || tbLuLID.Text == "") return;
            var lul = await myschool.GetLehrkraft(tbLuLKuerzel.Text);
            var pwd = Schuldatenbank.GeneratePasswort(8);
            myschool.SetTPwd(lul.ID, pwd);
            tbLuLtmpPwd.Text = pwd;
        }
    }
}