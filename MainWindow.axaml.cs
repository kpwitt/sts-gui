using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using SDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace StS_GUI_Avalonia
{
    public partial class MainWindow : Window
    {
        private SaveFileDialog sfd = new();
        private OpenFileDialog ofd = new();
       public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            
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
            SetupOpenDialog(ofd, "Lade Datenbankdatei", new String[] { "sqlite" }, new String[] { "Datenbankdatei" });

            
        }
       

        public void OnMnuschulespeichernClick(object? sender, RoutedEventArgs e)
        {
        }
        public async void OnMnuschulespeichernunterClick(object? sender, RoutedEventArgs e)
        {
            SetupSaveDialog(sfd, "Lade Datenbankdatei", new String[] { "sqlite" }, new String[] { "Datenbankdatei" });

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
