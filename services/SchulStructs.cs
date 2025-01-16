using System;

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
    bool HasM365Account = default)
{
    public string GetStufe()
    {
        var stufen = new[] { "5", "6", "7", "8", "9", "10", "EF", "Q1", "Q2" };
        foreach (var stufe in stufen)
        {
            if (Klasse.StartsWith(stufe)) return stufe;
        }

        return "";
    }
}

public record struct LuL(
    int ID,
    string Vorname,
    string Nachname,
    string Mail,
    string Kuerzel,
    string Fakultas,
    string Pwttemp,
    string Favo,
    string SFavo);

public record struct Kurs(
    string Bezeichnung,
    string Fach,
    string Klasse,
    string Stufe,
    string Suffix,
    bool IstKurs,
    string Art);

public record struct Einstellungen
{
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
    public string StuBos { get; set; }
}

public record struct LogEintrag
{
    public string Warnstufe { get; set; }
    public DateTime Eintragsdatum { get; set; }
    public string Nachricht { get; set; }

    public string Datumsstring() {
        return Eintragsdatum.ToLongDateString() +" "+ Eintragsdatum.ToLongTimeString();
    }

    public override string ToString()
    {
        return Warnstufe + "\t" + Datumsstring() + "\t" + Nachricht;
    }
}
