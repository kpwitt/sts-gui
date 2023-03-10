using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SDB;
using SchulStructs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace StS_GUI_Avalonia
{
    public partial class MainWindow : Window
    {
        private SaveFileDialog globalOpenSaveDialog = new();
        private OpenFileDialog globalOpenFileDialog = new();
        private OpenFolderDialog globalOpenFolderDialog = new();
        private SchulDB myschool = new(":memory:");

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        //quelle: https://ourcodeworld.com/articles/read/471/how-to-encrypt-and-decrypt-files-using-the-aes-encryption-algorithm-in-c-sharp
        /// <summary>
        /// Creates a random salt that will be used to encrypt your file. This method is required on FileEncrypt.
        /// </summary>
        /// <returns></returns>
        private static byte[] GenerateRandomSalt()
        {
            var data = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            for (var i = 0; i < 10; i++)
            {
                // Fill the buffer with the generated data
                rng.GetBytes(data);
            }
            return data;
        }

        /// <summary>
        /// Decrypts an encrypted file with the FileEncrypt method through its path and the plain password.
        /// </summary>
        /// <param name="inputFile"></param>
        /// <param name="outputFile"></param>
        /// <param name="password"></param>
        private static void FileDecrypt(string inputFile, string outputFile, string password)
        {
            var passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
            var salt = new byte[32];
            FileStream fsCrypt = new(inputFile, FileMode.Open);
            fsCrypt.Read(salt, 0, salt.Length);
            var AES = Aes.Create();
            AES.KeySize = 256;
            AES.BlockSize = 128;
            var key = new Rfc2898DeriveBytes(passwordBytes, salt, 50000);
            AES.Key = key.GetBytes(AES.KeySize / 8);
            AES.IV = key.GetBytes(AES.BlockSize / 8);
            AES.Padding = PaddingMode.PKCS7;
            AES.Mode = CipherMode.CFB;
            CryptoStream cs = new(fsCrypt, AES.CreateDecryptor(), CryptoStreamMode.Read);
            FileStream fsOut = new(outputFile, FileMode.Create);
            var buffer = new byte[1048576];
            try
            {
                int read;
                while ((read = cs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fsOut.Write(buffer, 0, read);
                }
            }
            catch (CryptographicException exCryptographicException)
            {
                Console.WriteLine("CryptographicException error: " + exCryptographicException.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }

            try
            {
                cs.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error by closing CryptoStream: " + ex.Message);
            }
            finally
            {
                fsOut.Close();
                fsCrypt.Close();
            }
        }

        /// <summary>
        /// Encrypts a file from its path and a plain password.
        /// </summary>
        /// <param name="inputFile"></param>
        /// <param name="password"></param>
        /// <param name="outputFile"></param>
        private static void FileEncrypt(string inputFile, string outputFile, string password)
        {
            //http://stackoverflow.com/questions/27645527/aes-encryption-on-large-files
            //generate random salt
            var salt = GenerateRandomSalt();
            //create output file name
            FileStream fsCrypt = new(outputFile, FileMode.Create);
            //convert password string to byte arrray
            var passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
            //Set Rijndael symmetric encryption algorithm
            var AES = Aes.Create();
            AES.KeySize = 256;
            AES.BlockSize = 128;
            AES.Padding = PaddingMode.PKCS7;
            //http://stackoverflow.com/questions/2659214/why-do-i-need-to-use-the-rfc2898derivebytes-class-in-net-instead-of-directly
            //"What it does is repeatedly hash the user password along with the salt." High iteration counts.
            var key = new Rfc2898DeriveBytes(passwordBytes, salt, 50000);
            AES.Key = key.GetBytes(AES.KeySize / 8);
            AES.IV = key.GetBytes(AES.BlockSize / 8);
            //Cipher modes: http://security.stackexchange.com/questions/52665/which-is-the-best-cipher-mode-and-padding-mode-for-aes-encryption
            AES.Mode = CipherMode.CFB;
            // write salt to the begining of the output file, so in this case can be random every time
            fsCrypt.Write(salt, 0, salt.Length);
            CryptoStream cs = new(fsCrypt, AES.CreateEncryptor(), CryptoStreamMode.Write);
            FileStream fsIn = new(inputFile, FileMode.Open);
            //create a buffer (1mb) so only this amount will allocate in the memory and not the whole file
            var buffer = new byte[1048576];
            try
            {
                int read;
                while ((read = fsIn.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cs.Write(buffer, 0, read);
                }

                // Close up
                fsIn.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            finally
            {
                cs.Close();
                fsCrypt.Close();
            }
        }

        private void SetupSaveDialog(SaveFileDialog sfd, string dialogtitle, string[] extensions,
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

        private void SetupOpenFileDialog(OpenFileDialog ofd, string dialogtitle, string[] extensions,
            string[] extensionsanames)
        {
            if (extensions.Length != extensionsanames.Length) return;
            ofd.Title = dialogtitle;
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

        private void SetupOpenFolderDialog(OpenFolderDialog ofd, string dialogtitle)
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
                myschool = new SchulDB(respath[0]);
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
                clearTextFields();
                return;
            }
            SetupOpenFileDialog(globalOpenFileDialog, "Bitte einen Dateipfad angeben...", new[] { "sqlite" },
                new[] { "Datenbankdatei" });
            var saveDBFile = async () =>
            {
                var filepath = await globalOpenSaveDialog.ShowAsync(this);
                if (filepath == null) return;
                var tempDB = new SchulDB(filepath);
                var res = await tempDB.Import(myschool);
                if (res != 0)
                {
                    Debug.WriteLine("Task failed");
                }
                else
                {
                    myschool = tempDB;
                    Debug.WriteLine("Saving done");
                }
            };
            await Task.Run(saveDBFile);
        }

        public async void OnMnuschulespeichernunterClick(object? sender, RoutedEventArgs e)
        {
            SetupSaveDialog(globalOpenSaveDialog, "Datenbankdatei speichern unter...", new[] { "sqlite" },
                new[] { "Datenbankdatei" });
            var saveDBFile = async () =>
            {
                var filepath = await globalOpenSaveDialog.ShowAsync(this);
                if (filepath == null) return;
                var tempDB = new SchulDB(filepath);
                var res = await tempDB.Import(myschool);
                if (res != 0)
                {
                    Debug.WriteLine("Task failed");
                }
                else
                {
                    myschool = tempDB;
                    Debug.WriteLine("Saving done");
                }
            };
            await Task.Run(saveDBFile);
        }

        public async void OnMnuschuleversspeichernClick(object? sender, RoutedEventArgs e)
        {
            SetupSaveDialog(globalOpenSaveDialog, "Datenbankdatei verschlüsselt speichern unter...", new[] { "aes" },
                new[] { "verschlüsselte Datenbankdatei" });
            if (myschool.GetFilePath().Result == ":memory:") return;
            var saveDBFile = async () =>
            {
                var filepath = await globalOpenSaveDialog.ShowAsync(this);
                if (filepath == null) return;
                FileEncrypt(myschool.GetFilePath().Result,filepath,"TODO!");
            };
            await Task.Run(saveDBFile);
        }

        public async void OnMnuversschuleladenClick(object? sender, RoutedEventArgs e)
        {
            SetupOpenFileDialog(globalOpenFileDialog, "Lade verschlüsselte Datenbankdatei", new[] { "aes" },
                new[] { "verschlüsselte Datenbankdatei" });
            var respath = await globalOpenFileDialog.ShowAsync(this);
            if (respath is not { Length: > 0 }) return;
            SetupSaveDialog(globalOpenSaveDialog, "Datenbankdatei speichern unter...", new[] { "sqlite" },
                new[] { "Datenbankdatei" });
            var saveDBFile = async () =>
            {
                var filepath = await globalOpenSaveDialog.ShowAsync(this);
                if (filepath == null) return; 
                FileDecrypt(respath[0],filepath, "TODO!");
                myschool = new SchulDB(filepath);                
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
                var path = await globalOpenFileDialog.ShowAsync(this);
                if (path == null) return;
                FileInfo t = new(path[0]);
                var folder = t.Directory;
                if (File.Exists(folder + "/sus.csv") && File.Exists(folder + "/lul.csv") &&
                    File.Exists(folder + "/kurse.csv"))
                {
                    await myschool.SusEinlesen(folder + "/sus.csv");
                    await myschool.LulEinlesen(folder + "/lul.csv");
                    await myschool.KurseEinlesen(folder + "/kurse.csv");
                    Debug.WriteLine("Done importing");
                }
            };
            await Task.Run(readFileTask);
        }

        public void OnMnuloadsusfromfileClick(object? sender, RoutedEventArgs e)
        {
        }

        public void OnMnuloadlulfromfileClick(object? sender, RoutedEventArgs e)
        {
        }

        public void OnMnuloadkursefromfileClick(object? sender, RoutedEventArgs e)
        {
        }

        public void OnMnuloadusernamesmailClick(object? sender, RoutedEventArgs e)
        {
        }

        public void OnMnuloadzweitaccountsClick(object? sender, RoutedEventArgs e)
        {
        }

        public void OnMnureaddiffsClick(object? sender, RoutedEventArgs e)
        {
        }

        public void OnMnuexporttocsvClick(object? sender, RoutedEventArgs e)
        {
        }

        public void OnMnuaboutClick(object? sender, RoutedEventArgs e)
        {
        }

        public void OnBtnsusaddClick(object? sender, RoutedEventArgs e)
        {
        }

        public void OnBtnsusdelClick(object? sender, RoutedEventArgs e)
        {
        }

        public void OnBtnluladdClick(object? sender, RoutedEventArgs e)
        {
        }

        public void OnBtnluldelClick(object? sender, RoutedEventArgs e)
        {
        }

        public void OnBtnkurseaddClick(object? sender, RoutedEventArgs e)
        {
        }

        public void OnBtnkursedelClick(object? sender, RoutedEventArgs e)
        {
        }

        public void OnBtnexportClick(object? sender, RoutedEventArgs e)
        {
        }

        public void OnBtnexportstufenkursClick(object? sender, RoutedEventArgs e)
        {
        }

        public void OnBtnfehlersucheClick(object? sender, RoutedEventArgs e)
        {
        }

        public void OnBtnfehlerexportClick(object? sender, RoutedEventArgs e)
        {
        }

        public void OnBtnsettingsaveClick(object? sender, RoutedEventArgs e)
        {
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
            RightListBox.Items = new List<string>();
            clearTextFields();
            OnLeftDataChanged(true);
        }

        private void clearTextFields()
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
            tbLuLID.Text = "";
            tbLuLVorname.Text = "";
            tbLuLnachname.Text = "";
            tbLuLKuerzel.Text = "";
            tbLuLFach.Text = "";
            tbLuLMail.Text = "";
            tbLuLtmpPwd.Text = "";
            tbLuLKurse.Text = "";
            tbKursbezeichnung.Text = "";
            tbKursLuL.Text = "";
            tbKursFach.Text = "";
            tbKursSuffix.Text = "";
            tbKursKlasse.Text = "";
            tbKursStufe.Text = "";
            cbKursIstKurs.IsChecked = false;
        }

        private void CboxDataRight_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            RightListBox.Items = new List<string>();
            OnRightDataChanged(true);
        }

        private void LeftListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            clearTextFields();
            OnLeftDataChanged(false);
        }

        private void RightListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            OnRightDataChanged(false);
        }

        private void OnLeftDataChanged(bool changedCB)
        {
            if (LeftListBox == null || RightListBox == null || CboxDataLeft == null || CboxDataRight == null) return;
            if (LeftListBox.SelectedItems == null) return;
            switch (CboxDataLeft.SelectedIndex)
            {
                //s=0;l==1;k==2
                case 0:
                    if (CboxDataRight.SelectedIndex == 0)
                    {
                        CboxDataRight.SelectedIndex = 1;
                    }

                    if (LeftListBox.SelectedItems.Count < 1 || LeftListBox.SelectedItems == null || changedCB)
                    {
                        var slist = myschool.GetSchuelerListe().Result
                            .Select(s => (s.Nachname + "," + s.Vorname + ";" + s.ID)).Distinct().ToList();
                        slist.Sort(Comparer<string>.Default);
                        LeftListBox.Items = slist;
                        RightListBox.Items = new List<string>();
                    }
                    else
                    {
                        var sid = LeftListBox.SelectedItems[0].ToString().Split(';')[1];
                        var sus = myschool.GetSchueler(Convert.ToInt32(sid)).Result;
                        loadSuSData(sus);
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

                    if (LeftListBox.SelectedItems.Count < 1 || LeftListBox.SelectedItems == null || changedCB)
                    {
                        var lullist = myschool.GetLehrerListe().Result
                            .Select(l => (l.Kuerzel + ";" + l.Nachname + "," + l.Vorname)).Distinct().ToList();
                        lullist.Sort(Comparer<string>.Default);
                        LeftListBox.Items = lullist;
                        RightListBox.Items = new List<string>();
                    }
                    else
                    {
                        var lulkrz = LeftListBox.SelectedItems[0].ToString().Split(';')[0];
                        if (lulkrz == "") return;
                        var lul = myschool.GetLehrer(lulkrz).Result;
                        loadLuLData(lul);
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

                    if (LeftListBox.SelectedItems.Count < 1 || LeftListBox.SelectedItems == null || changedCB)
                    {
                        var klist = myschool.GetKursListe().Result.Select(k => (k.Bezeichnung)).Distinct().ToList();
                        klist.Sort(Comparer<string>.Default);
                        LeftListBox.Items = klist;
                        RightListBox.Items = new List<string>();
                    }
                    else
                    {
                        var kurzbez = LeftListBox.SelectedItems[0].ToString();
                        if (kurzbez == "") return;
                        var kurs = myschool.GetKurs(kurzbez).Result;
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
                default:
                    break;
            }
        }

        private void OnRightDataChanged(bool changedCB)
        {
            if (LeftListBox == null || RightListBox == null || CboxDataLeft == null || CboxDataRight == null) return;
            if (RightListBox.SelectedItems == null) return;
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
                            var lulkrz = LeftListBox.SelectedItems[0].ToString().Split(';')[0];
                            if (lulkrz == "") return;
                            var lul = myschool.GetLehrer(lulkrz).Result;
                            loadLuLData(lul);
                            if (RightListBox.SelectedItems.Count > 0)
                            {
                                var sid = RightListBox.SelectedItems[0].ToString().Split(';')[1];
                                var sus = myschool.GetSchueler(Convert.ToInt32(sid)).Result;
                                if (sus.ID == 0) return;
                                loadSuSData(sus);
                            }
                            if (!changedCB) return;
                            var rlist = myschool.GetSuSVonLuL(lul.ID).Result
                                .Select(s => (s.Nachname + "," + s.Vorname + ";" + s.ID)).Distinct().ToList();
                            rlist.Sort(Comparer<string>.Default);
                            RightListBox.Items = rlist;
                            break;
                        }
                        case 2:
                        {
                            var kurzbez = LeftListBox.SelectedItems[0].ToString();
                            if (kurzbez == "") return;
                            var kurs = myschool.GetKurs(kurzbez).Result;
                            loadKursData(kurs);
                            if (RightListBox.SelectedItems.Count > 0)
                            {
                                var sid = RightListBox.SelectedItems[0].ToString().Split(';')[1];
                                var sus = myschool.GetSchueler(Convert.ToInt32(sid)).Result;
                                if (sus.ID == 0) return;
                                loadSuSData(sus);
                            }
                            if (!changedCB) return;
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
                            var sid = LeftListBox.SelectedItems[0].ToString().Split(';')[1];
                            var sus = myschool.GetSchueler(Convert.ToInt32(sid)).Result;
                            if (sus.ID == 0) return;
                            loadSuSData(sus);
                            if (RightListBox.SelectedItems.Count > 0)
                            {
                                var lulkrz = RightListBox.SelectedItems[0].ToString().Split(';')[0];
                                if (lulkrz == "") return;
                                var lul = myschool.GetLehrer(lulkrz).Result;
                                loadLuLData(lul);
                            }
                            if (!changedCB) return;
                            var rlist = myschool.GetLuLvonSuS(sus.ID).Result
                                .Select(l => (l.Kuerzel + ";" + l.Nachname + "," + l.Vorname)).Distinct().ToList();
                            rlist.Sort(Comparer<string>.Default);
                            RightListBox.Items = rlist;
                            break;
                        }
                        case 2:
                        {
                            var kurzbez = LeftListBox.SelectedItems[0].ToString();
                            if (kurzbez == "") return;
                            var kurs = myschool.GetKurs(kurzbez).Result;
                            loadKursData(kurs);
                            if (RightListBox.SelectedItems.Count > 0)
                            {
                                var lulkrz = RightListBox.SelectedItems[0].ToString().Split(';')[0];
                                if (lulkrz == "") return;
                                var lul = myschool.GetLehrer(lulkrz).Result;
                                loadLuLData(lul);
                            }
                            if (!changedCB) return;
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
                            var sid = LeftListBox.SelectedItems[0].ToString().Split(';')[1];
                            var sus = myschool.GetSchueler(Convert.ToInt32(sid)).Result;
                            if (sus.ID == 0) return;
                            loadSuSData(sus);
                            if (RightListBox.SelectedItems.Count > 0)
                            {
                                var kurzbez = RightListBox.SelectedItems[0].ToString();
                                if (kurzbez == "") return;
                                var kurs = myschool.GetKurs(kurzbez).Result;
                                loadKursData(kurs);
                            }
                            if (!changedCB) return;
                            var rlist = myschool.GetKursVonSuS(sus.ID).Result.Select(k => (k.Bezeichnung))
                                .Distinct()
                                .ToList();
                            RightListBox.Items = rlist;
                            break;
                        }
                        case 1:
                        {
                            var lulkrz = LeftListBox.SelectedItems[0].ToString().Split(';')[0];
                            if (lulkrz == "") return;
                            var lul = myschool.GetLehrer(lulkrz).Result;
                            loadLuLData(lul);
                            if (RightListBox.SelectedItems.Count > 0)
                            {
                                var kurzbez = RightListBox.SelectedItems[0].ToString();
                                if (kurzbez == "") return;
                                var kurs = myschool.GetKurs(kurzbez).Result;
                                loadKursData(kurs);
                            }
                            if (!changedCB) return;
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

        private void loadSuSData(SuS s)
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

        private void loadLuLData(LuL l)
        {
            if (l.ID is 0 or > 1500) return;
            tbLuLID.Text = l.ID + "";
            tbLuLVorname.Text = l.Vorname;
            tbLuLnachname.Text = l.Nachname;
            tbLuLKuerzel.Text = l.Kuerzel;
            tbLuLFach.Text = l.Fakultas;
            tbLuLMail.Text = l.Mail;
            tbLuLtmpPwd.Text = l.Pwttemp;
            tbLuLKurse.Text = myschool.GetKursVonLuL(l.ID).Result
                .Aggregate("", (current, kurs) => current + (kurs.Bezeichnung + ",")).TrimEnd(',');
        }

        private void loadKursData(Kurs k)
        {
            if (k.Bezeichnung == "") return;
            tbKursbezeichnung.Text = k.Bezeichnung;
            tbKursLuL.Text = myschool.GetLuLAusKurs(k.Bezeichnung).Result
                .Aggregate("",(current,lul)=>current+(lul.Kuerzel+";")).TrimEnd(';');
            tbKursFach.Text = k.Fach;
            tbKursSuffix.Text = k.Suffix;
            tbKursKlasse.Text = k.Klasse;
            tbKursStufe.Text = k.Stufe;
            cbKursIstKurs.IsChecked = k.Istkurs;
        }
    }
}