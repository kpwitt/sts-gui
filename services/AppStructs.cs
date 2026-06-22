/*SchildToSchule (StS) - Programm zur Verwaltung von Schüler:innen/Lehrkräfte und Kursdaten
   Copyright (C) 2026 Kay-Patrick Wittbold

   This program is free software: you can redistribute it and/or modify
   it under the terms of the GNU General Public License as published by
   the Free Software Foundation, either version 3 of the License, or
   (at your option) any later version.

   This program is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
   GNU General Public License for more details.

   You should have received a copy of the GNU General Public License
   along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

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
