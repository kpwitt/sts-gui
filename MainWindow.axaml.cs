using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using SDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace StS_GUI_Avalonia
{
    public partial class MainWindow : Window
    {
        private SaveFileDialog gsfd = new();
        private OpenFileDialog gofd = new();
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
        public static byte[] GenerateRandomSalt() {
            byte[] data = new byte[32];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) {
                for (int i = 0; i < 10; i++) {
                    // Fill the buffer with the generated data
                    rng.GetBytes(data);
                }
            }

            return data;
        }

        /// <summary>
        /// Decrypts an encrypted file with the FileEncrypt method through its path and the plain password.
        /// </summary>
        /// <param name="inputFile"></param>
        /// <param name="outputFile"></param>
        /// <param name="password"></param>
        private static void FileDecrypt(string inputFile, string outputFile, string password) {
            byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
            byte[] salt = new byte[32];

            FileStream fsCrypt = new(inputFile, FileMode.Open);
            fsCrypt.Read(salt, 0, salt.Length);

            Aes AES = Aes.Create();
            AES.KeySize = 256;
            AES.BlockSize = 128;
            var key = new Rfc2898DeriveBytes(passwordBytes, salt, 50000);
            AES.Key = key.GetBytes(AES.KeySize / 8);
            AES.IV = key.GetBytes(AES.BlockSize / 8);
            AES.Padding = PaddingMode.PKCS7;
            AES.Mode = CipherMode.CFB;

            CryptoStream cs = new(fsCrypt, AES.CreateDecryptor(), CryptoStreamMode.Read);

            FileStream fsOut = new(outputFile, FileMode.Create);

            int read;
            byte[] buffer = new byte[1048576];

            try {
                while ((read = cs.Read(buffer, 0, buffer.Length)) > 0) {
                    fsOut.Write(buffer, 0, read);
                }
            }
            catch (CryptographicException ex_CryptographicException) {
                Console.WriteLine("CryptographicException error: " + ex_CryptographicException.Message);
            }
            catch (Exception ex) {
                Console.WriteLine("Error: " + ex.Message);
            }

            try {
                cs.Close();
            }
            catch (Exception ex) {
                Console.WriteLine("Error by closing CryptoStream: " + ex.Message);
            }
            finally {
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
        private static void FileEncrypt(string inputFile, string outputFile, string password) {
            //http://stackoverflow.com/questions/27645527/aes-encryption-on-large-files

            //generate random salt
            byte[] salt = GenerateRandomSalt();

            //create output file name
            FileStream fsCrypt = new(outputFile, FileMode.Create);

            //convert password string to byte arrray
            byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);

            //Set Rijndael symmetric encryption algorithm
            Aes AES = Aes.Create();
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
            byte[] buffer = new byte[1048576];
            int read;

            try {
                while ((read = fsIn.Read(buffer, 0, buffer.Length)) > 0) {
                    cs.Write(buffer, 0, read);
                }

                // Close up
                fsIn.Close();
            }
            catch (Exception ex) {
                Console.WriteLine("Error: " + ex.Message);
            }
            finally {
                cs.Close();
                fsCrypt.Close();
            }
        }
       private void SetupSaveDialog(SaveFileDialog sfd, string dialogtitle, string[] extensions, string[] extensionsanames)
        {
            if (extensions.Length == extensionsanames.Length)
            {
                sfd.DefaultExtension = extensions[0];
                sfd.Title = dialogtitle;
                List<FileDialogFilter> Filters = new();
                for (int i = 0; i < extensions.Length; i++)
                {
                    FileDialogFilter filter = new();
                    List<string> extension = new();
                    extension.Add(extensions[i]);
                    filter.Extensions = extension;
                    filter.Name = extensionsanames[i];
                    Filters.Add(filter);
                }
                sfd.Filters = Filters;
            }
        }

        private void SetupOpenDialog(OpenFileDialog ofd, string dialogtitle, string[] extensions, string[] extensionsanames)
        {
            if (extensions.Length == extensionsanames.Length)
            {
                ofd.Title = dialogtitle;
                List<FileDialogFilter> Filters = new();
                for (int i = 0; i < extensions.Length; i++)
                {
                    FileDialogFilter filter = new();
                    List<string> extension = new();
                    extension.Add(extensions[i]);
                    filter.Extensions = extension;
                    filter.Name = extensionsanames[i];
                    Filters.Add(filter);
                }
                ofd.Filters = Filters;
            }
        }


        private void OnLeftlistboxSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender != null && sender.GetType().Equals(this.FindControl<ListBox>("LeftListBox")))
            {
                var leftbox = (ListBox)sender;
                Debug.WriteLine(leftbox.SelectedItems.Count);

            }
        }

        private void OnRigthlistboxSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender != null && sender.GetType().Equals(this.FindControl<ListBox>("RightListBox")))
            {
                var rightbox = (ListBox)sender;
                Debug.WriteLine(rightbox.SelectedItems.Count);
            }
        }

        public async void OnMnuSchoolLoadClick(object? sender, RoutedEventArgs e)
        {
            SetupOpenDialog(gofd, "Lade Datenbankdatei", new String[] { "sqlite" }, new String[] { "Datenbankdatei" });

            
        }
       

        public void OnMnuschulespeichernClick(object? sender, RoutedEventArgs e)
        {
        }
        public async void OnMnuschulespeichernunterClick(object? sender, RoutedEventArgs e)
        {
            SetupSaveDialog(gsfd, "Lade Datenbankdatei", new String[] { "sqlite" }, new String[] { "Datenbankdatei" });

        }
        public void OnMnuschuleversspeichernClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnMnuversschuleladenClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnMnuexitClick(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }
        public void OnMnuloadfolderClick(object? sender, RoutedEventArgs e)
        {
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
        public void OnCbleftClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbleftsearchClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCboxdataleftClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCbrightClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbrightsearchClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OncboxDataRightClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbsusidClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbsusvornameClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbsusnachnameClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbsuselternadresseClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbsusaixmailClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbsusnutzernameClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbsuskurseClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnBtnsusaddClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnBtnsusdelClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTblulidClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTblulvornameClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTblulnachnameClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTblulkuerzelClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTblulfachClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTblulmailClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTblultmppwdClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTblulkurseClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnBtnluladdClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnBtnluldelClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbkursbezeichnungClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbkurslulClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbkursfachClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbkurssuffixClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbkursklasseClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbkursstufeClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCbkursistkursClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCbkursmarkiertesuseinschreibenClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCbkurssusdklasseeinschreibenClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCbkurssusdstufeeinschreibenClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnBtnkurseaddClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnBtnkursedelClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCbsusClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCblulClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCbkursClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCbelternClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCbmoodleClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCbaixClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCblogClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCbexportwithpasswortClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnBtnexportClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbexportklClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbexportfClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCbexportvorlagenkurseClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbexportstufenkurseClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCbexportstufenClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnBtnexportstufenkursClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCbfehlersusokClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCbfehlerlulokClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCbfehlerleerekurseClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCbfehlersusClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCbfehlerlulClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCbfehlerkurseClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnBtnfehlersucheClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnBtnfehlerexportClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbfehlerlisteClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbsettingmailplatzhalterClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbsettingkurssuffixClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbsettingkursersetzungClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbsettingfachkurzClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnTbsettingfachlangClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnBtnsettingsaveClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnCbSuSZweitaccountClick(object? sender, RoutedEventArgs e)
        {
        }
    }
}
