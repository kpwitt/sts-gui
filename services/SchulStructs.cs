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

using System;
using System.Collections.Generic;

// ReSharper disable InconsistentNaming

namespace SchulDB;
#pragma warning disable CS1591
public record struct SuS(
    int ID,
    string Vorname,
    string Nachname,
    string Mail,
    string Klasse,
    string Nutzername,
    string Aixmail,
    string Zweitmail,
    bool Zweitaccount,
    string Seriennummer,
    string Bemerkung,
    bool HasM365Account = false,
    bool AllowJAMF = false,
    bool IstAktiv = true) {
    public string GetStufe() {
        var stufen = new[] { "5", "6", "7", "8", "9", "10", "EF", "Q1", "Q2" };
        foreach (var stufe in stufen) {
            if (Klasse.StartsWith(stufe)) return stufe;
        }

        return "";
    }
}

public record struct Lehrkraft(
    int ID,
    string Vorname,
    string Nachname,
    string Mail,
    string Kuerzel,
    string Fakultas,
    string Pwttemp,
    string Favo,
    string SFavo,
    string Seriennummer,
    string Bemerkung,
    bool IstAktiv = true
) : IComparable<Lehrkraft> {
    public int CompareTo(Lehrkraft other) {
        return string.Compare(Kuerzel, other.Kuerzel, StringComparison.Ordinal);
    }
}

public record struct Kurs(
    string Bezeichnung,
    string Fach,
    string Klasse,
    string Stufe,
    string Suffix,
    bool IstKurs,
    string Art,
    string Bemerkung,
    bool IstLKKurs = false);

public struct DBEinstellungen {
    public DBEinstellungen() {
        Mailsuffix = "@schule.local";
        Kurssuffix = string.Empty;
        Fachersetzung = string.Empty;
        Kurzfaecher = [
            "D", "E", "M", "BI", "CH", "EK", "F7", "GE", "IF", "I0", "KU", "L7", "MU", "PH", "PK", "PS", "SN", "SP"
        ];
        Langfaecher = [
            "Deutsch", "Englisch", "Mathematik", "Biologie", "Chemie", "Erdkunde", "Französisch", "Geschichte",
            "Informatik", "Italienisch", "Kunst", "Latein", "Musik", "Physik", "Politik", "Psychologie", "Schwimmen",
            "Sport"
        ];
        Erprobungstufenleitung = string.Empty;
        Mittelstufenleitung = string.Empty;
        EFStufenleitung = string.Empty;
        Q1Stufenleitung = string.Empty;
        Q2Stufenleitung = string.Empty;
        Oberstufenkoordination = string.Empty;
        Version = "0.72";
        StuBos = string.Empty;
        Erprobungsstufe = ["5", "6"];
        Mittelstufe = ["7", "8", "9", "10"];
        Oberstufe = ["EF", "Q1", "Q2"];
        StuboStufen = ["8", "9", "10", "EF", "Q1", "Q2"];
        JAMFStufen = ["8", "9", "10", "EF", "Q1", "Q2"];
    }

    public string Mailsuffix { get; set; }
    public string Kurssuffix { get; set; }
    public string Fachersetzung { get; set; }
    public string[] Kurzfaecher { get; set; }
    public string[] Langfaecher { get; set; }
    public string Erprobungstufenleitung { get; set; }
    public string Mittelstufenleitung { get; set; }
    public string EFStufenleitung { get; set; }
    public string Q1Stufenleitung { get; set; }
    public string Q2Stufenleitung { get; set; }
    public string Oberstufenkoordination { get; set; }
    public string Version { get; set; }
    public string StuBos { get; set; } = "";
    public string[] Erprobungsstufe { get; set; }
    public string[] Mittelstufe { get; set; }
    public string[] Oberstufe { get; set; }

    public string[] StuboStufen { get; set; }
    public string[] JAMFStufen { get; set; }
}

public readonly record struct LogEintrag(string Warnstufe, DateTime Eintragsdatum, string Nachricht) {
    public string Datumsstring() {
        return $"{Eintragsdatum.ToShortDateString()} {Eintragsdatum.ToLongTimeString()}";
    }

    public override string ToString() {
        return $"{Warnstufe} {Datumsstring()} {Nachricht}";
    }
}

public readonly struct FaKo(string fach, Lehrkraft vorsitz, Lehrkraft stellvertretung, List<Lehrkraft> mitglieder)
    : IComparable<FaKo> {
    public string Fach { get; } = fach;
    public Lehrkraft Vorsitz { get; } = vorsitz;
    public Lehrkraft Stellvertretung { get; } = stellvertretung;
    public List<Lehrkraft> Mitglieder { get; } = mitglieder;

    public int CompareTo(FaKo andere) {
        return string.Compare(Fach, andere.Fach, StringComparison.Ordinal);
    }
}