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
            this.Icon = new WindowIcon(assets.Open(new Uri("avares://StS-GUI-Avalonia/gfx/school-building.ico")));
            //GUI und Methoden verknüpfen
            this.FindControl<MenuItem>("mnuSchuleLaden").Click += OnMnuSchoolLoad; 
        }

        public async void OnMnuSchoolLoad(object? sender, RoutedEventArgs e)
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

        public void OnMnuExit(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
