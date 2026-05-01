using System.Collections.Generic;

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

public struct AppSettings {
    public Theme Theme { get; set; }
    public List<string> LastFiles { get; set; }

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