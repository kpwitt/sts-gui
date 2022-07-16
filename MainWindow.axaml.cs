using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using SDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace StS_GUI_Avalonia
{
    public partial class MainWindow : Window
    {
        private SchulDB myschool;
        SaveFileDialog sfd1;
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            myschool = new SchulDB(":memory:");
            sfd1 = new SaveFileDialog();
            var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
            if (assets != null)
            {
                this.Icon = new WindowIcon(assets.Open(new Uri("avares://StS-GUI-Avalonia/gfx/school-building.ico")));
            }
            //GUI und Methoden verknüpfen
            this.FindControl<MenuItem>("mnuSchuleLaden").Click += OnMnuSchoolLoadClick;
            this.FindControl<MenuItem>("mnuSchuleSpeichern").Click += OnMnuschulespeichernClick;
            this.FindControl<MenuItem>("mnuSchuleSpeichernUnter").Click += OnMnuschulespeichernunterClick;
            this.FindControl<MenuItem>("mnuSchuleVersSpeichern").Click += OnMnuschuleversspeichernClick;
            this.FindControl<MenuItem>("mnuVersSchuleLaden").Click += OnMnuversschuleladenClick;
            this.FindControl<MenuItem>("mnuExit").Click += OnMnuexitClick;
            this.FindControl<MenuItem>("mnuLoadFolder").Click += OnMnuloadfolderClick;
            this.FindControl<MenuItem>("mnuLoadSuSFromFile").Click += OnMnuloadsusfromfileClick;
            this.FindControl<MenuItem>("mnuLoadLuLFromFile").Click += OnMnuloadlulfromfileClick;
            this.FindControl<MenuItem>("mnuLoadKurseFromFile").Click += OnMnuloadkursefromfileClick;
            this.FindControl<MenuItem>("mnuLoadUsernamesMail").Click += OnMnuloadusernamesmailClick;
            this.FindControl<MenuItem>("mnuLoadZweitaccounts").Click += OnMnuloadzweitaccountsClick;
            this.FindControl<MenuItem>("mnuReadDiffs").Click += OnMnureaddiffsClick;
            this.FindControl<MenuItem>("mnuExportToCSV").Click += OnMnuexporttocsvClick;
            this.FindControl<MenuItem>("mnuAbout").Click += OnMnuaboutClick;
            this.FindControl<ComboBox>("cboxDataLeft").KeyUp += OnCbleftClick;
            this.FindControl<TextBox>("tbLeftSearch").KeyUp += OnTbleftsearchClick;
            this.FindControl<ComboBox>("cboxDataRigth").KeyUp += OnCbrightClick;
            this.FindControl<TextBox>("tbRightSearch").KeyUp += OnTbrightsearchClick;
            this.FindControl<TextBox>("tbSuSID").KeyUp += OnTbsusidClick;
            this.FindControl<TextBox>("tbSuSVorname").KeyUp += OnTbsusvornameClick;
            this.FindControl<TextBox>("tbSuSnachname").KeyUp += OnTbsusnachnameClick;
            this.FindControl<TextBox>("tbSuSElternadresse").KeyUp += OnTbsuselternadresseClick;
            this.FindControl<TextBox>("tbSuSAIXMail").KeyUp += OnTbsusaixmailClick;
            this.FindControl<TextBox>("tbSuSNutzername").KeyUp += OnTbsusnutzernameClick;
            this.FindControl<TextBox>("tbSuSKurse").KeyUp += OnTbsuskurseClick;
            this.FindControl<Button>("btnSuSAdd").Click += OnBtnsusaddClick;
            this.FindControl<Button>("btnSuSDel").Click += OnBtnsusdelClick;
            this.FindControl<TextBox>("tbLuLID").KeyUp += OnTblulidClick;
            this.FindControl<TextBox>("tbLuLVorname").KeyUp += OnTblulvornameClick;
            this.FindControl<TextBox>("tbLuLnachname").KeyUp += OnTblulnachnameClick;
            this.FindControl<TextBox>("tbLuLKuerzel").KeyUp += OnTblulkuerzelClick;
            this.FindControl<TextBox>("tbLuLFach").KeyUp += OnTblulfachClick;
            this.FindControl<TextBox>("tbLuLMail").KeyUp += OnTblulmailClick;
            this.FindControl<TextBox>("tbLuLtmpPwd").KeyUp += OnTblultmppwdClick;
            this.FindControl<TextBox>("tbLuLKurse").KeyUp += OnTblulkurseClick;
            this.FindControl<Button>("btnLuLAdd").Click += OnBtnluladdClick;
            this.FindControl<Button>("btnLuLDel").Click += OnBtnluldelClick;
            this.FindControl<TextBox>("tbKursbezeichnung").KeyUp += OnTbkursbezeichnungClick;
            this.FindControl<TextBox>("tbKursLuL").KeyUp += OnTbkurslulClick;
            this.FindControl<TextBox>("tbKursFach").KeyUp += OnTbkursfachClick;
            this.FindControl<TextBox>("tbKursSuffix").KeyUp += OnTbkurssuffixClick;
            this.FindControl<TextBox>("tbKursKlasse").KeyUp += OnTbkursklasseClick;
            this.FindControl<TextBox>("tbKursStufe").KeyUp += OnTbkursstufeClick;
            this.FindControl<CheckBox>("cbKursIstKurs").Click += OnCbkursistkursClick;
            this.FindControl<CheckBox>("cbKursMarkierteSuSEinschreiben").Click += OnCbkursmarkiertesuseinschreibenClick;
            this.FindControl<CheckBox>("cbKursSuSdKlasseEinschreiben").Click += OnCbkurssusdklasseeinschreibenClick;
            this.FindControl<CheckBox>("cbKursSuSdStufeEinschreiben").Click += OnCbkurssusdstufeeinschreibenClick;
            this.FindControl<Button>("btnKurseAdd").Click += OnBtnkurseaddClick;
            this.FindControl<Button>("btnKurseDel").Click += OnBtnkursedelClick;
            this.FindControl<CheckBox>("cbSuS").Click += OnCbsusClick;
            this.FindControl<CheckBox>("cbLuL").Click += OnCblulClick;
            this.FindControl<CheckBox>("cbKurs").Click += OnCbkursClick;
            this.FindControl<CheckBox>("cbEltern").Click += OnCbelternClick;
            this.FindControl<CheckBox>("cbMoodle").Click += OnCbmoodleClick;
            this.FindControl<CheckBox>("cbAIX").Click += OnCbaixClick;
            this.FindControl<CheckBox>("cbLog").Click += OnCblogClick;
            this.FindControl<CheckBox>("cbExportwithPasswort").Click += OnCbexportwithpasswortClick;
            this.FindControl<Button>("btnExport").Click += OnBtnexportClick;
            this.FindControl<TextBox>("tbExportKl").KeyUp += OnTbexportklClick;
            this.FindControl<TextBox>("tbExportF").KeyUp += OnTbexportfClick;
            this.FindControl<CheckBox>("cbExportVorlagenkurse").Click += OnCbexportvorlagenkurseClick;
            this.FindControl<TextBox>("tbExportStufenkurse").KeyUp += OnTbexportstufenkurseClick;
            this.FindControl<CheckBox>("cbExportStufen").Click += OnCbexportstufenClick;
            this.FindControl<Button>("btnExportStufenkurs").Click += OnBtnexportstufenkursClick;
            this.FindControl<CheckBox>("cbFehlerSuSoK").Click += OnCbfehlersusokClick;
            this.FindControl<CheckBox>("cbFehlerLuLoK").Click += OnCbfehlerlulokClick;
            this.FindControl<CheckBox>("cbFehlerLeereKurse").Click += OnCbfehlerleerekurseClick;
            this.FindControl<CheckBox>("cbFehlerSuS").Click += OnCbfehlersusClick;
            this.FindControl<CheckBox>("cbFehlerLuL").Click += OnCbfehlerlulClick;
            this.FindControl<CheckBox>("cbFehlerKurse").Click += OnCbfehlerkurseClick;
            this.FindControl<Button>("btnFehlerSuche").Click += OnBtnfehlersucheClick;
            this.FindControl<Button>("btnFehlerExport").Click += OnBtnfehlerexportClick;
            this.FindControl<ListBox>("lbFehlerliste").KeyUp += OnTbfehlerlisteClick;
            this.FindControl<TextBox>("tbSettingMailplatzhalter").KeyUp += OnTbsettingmailplatzhalterClick;
            this.FindControl<TextBox>("tbSettingKurssuffix").KeyUp += OnTbsettingkurssuffixClick;
            this.FindControl<TextBox>("tbSettingKursersetzung").KeyUp += OnTbsettingkursersetzungClick;
            this.FindControl<ListBox>("lbSettingFachkurz").KeyUp += OnTbsettingfachkurzClick;
            this.FindControl<ListBox>("lbSettingFachlang").KeyUp += OnTbsettingfachlangClick;
            this.FindControl<Button>("btnSettingSave").Click += OnBtnsettingsaveClick;
        }

        public async void OnMnuSchoolLoadClick(object? sender, RoutedEventArgs e)
        {
            sfd1.DefaultExtension = "sqlite";
            sfd1.Title = "Lade Datenbank";
            List<FileDialogFilter> Filters = new();
            FileDialogFilter filter = new();
            List<string> extension = new()
            {
                "sqlite"
            };
            filter.Extensions = extension;
            filter.Name = "Datenbankdatei";
            Filters.Add(filter);
            sfd1.Filters = Filters;

            sfd1.DefaultExtension = "sqlite";

            var SettingsFileName = await sfd1.ShowAsync(this);
           Debug.WriteLine(SettingsFileName);
        }
        public void OnMnuschulespeichernClick(object? sender, RoutedEventArgs e)
        {
        }
        public void OnMnuschulespeichernunterClick(object? sender, RoutedEventArgs e)
        {
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
        public void OnCboxdatarigthClick(object? sender, RoutedEventArgs e)
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
    }
}
