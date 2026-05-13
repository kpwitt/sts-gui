using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SchulDB;

public struct SearchFields {
    public bool Vorname { get; init; }
    public bool Nachname { get; init; }
    public bool Mailadrese { get; init; }
    public bool Nutzername { get; init; }
    public bool ID { get; init; }
    public bool Seriennummer { get; init; }
    public bool GrossKleinschreibung { get; init; }
}

public struct AppSettings() {
    public Theme Theme { get; set; } = Theme.Dark;
    public List<string> LastFiles { get; init; } = [];

    public void AddLastFile(string filename) {
        if (LastFiles.Contains(filename)) return;
        LastFiles.Insert(0, filename);
        if (LastFiles.Count > 5) {
            LastFiles.RemoveAt(6);
        }
    }
}

public enum Theme {
    Dark,
    Light
}

public record struct ExportParameters(
    string Folder,
    string TargetSystems,
    string WhatToExport,
    bool WithPasswort,
    string Passwort,
    bool ExpandFiles,
    bool NurMoodleSuffix,
    string[] KursVorlage,
    ReadOnlyCollection<int> SusIdListe,
    ReadOnlyCollection<int> LulIdListe,
    ReadOnlyCollection<string> KursListe
);
