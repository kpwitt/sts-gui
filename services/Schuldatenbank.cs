using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

// ReSharper disable InconsistentNaming

#pragma warning disable CS1998

namespace SchulDB;

/// <summary>
/// Wrapperklasse zur Verwaltung der SQLite-Datenbank
/// </summary>
public class Schuldatenbank : IDisposable
{
    private const string version = "0.6";
    private readonly string _dbpath;
    private readonly SqliteConnection _sqliteConn;
    private SqliteTransaction? _dbtrans;
    private bool _ActiveTransaction;

    /// <summary>
    /// erstellt, falls nicht vorhanden, die Datenbankstruktur und öffnet die Verbindung
    /// </summary>
    public Schuldatenbank(string path)
    {
        _dbpath = path;
        var strconnection = "Data Source=" + _dbpath;
        _sqliteConn = new SqliteConnection(strconnection);
        _sqliteConn.Open();
        var sqliteCmd = _sqliteConn.CreateCommand();
        upgradeDB(sqliteCmd);
        try
        {
            sqliteCmd.CommandText = """
                                    CREATE TABLE IF NOT EXISTS
                                            [lehrkraft] (
                                            [id]   INTEGER NOT NULL PRIMARY KEY,
                                            [nachname]  NVARCHAR(512) NOT NULL,
                                            [vorname]  NVARCHAR(512) NOT NULL,
                                            [mail]  NVARCHAR(512) NOT NULL UNIQUE,
                                            [kuerzel]  NVARCHAR(8) NOT NULL UNIQUE,
                                            [fakultas]  NVARCHAR(16) NOT NULL,
                                            [pwtemp] NVARCHAR(16) NOT NULL,
                                            [favo] NVARCHAR(8) NOT NULL,
                                            [sfavo] NVARCHAR(8) NOT NULL
                                          )
                                    """;
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.CommandText = "CREATE INDEX IF NOT EXISTS lindex ON lehrkraft(id);";
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.CommandText = """
                                    CREATE TABLE IF NOT EXISTS
                                            [schueler] (
                                            [id]   INTEGER NOT NULL PRIMARY KEY,
                                            [nachname]  NVARCHAR(512) NOT NULL,
                                            [vorname]  NVARCHAR(512) NOT NULL,
                                            [mail]  NVARCHAR(512) NOT NULL,
                                            [klasse]  NVARCHAR(16) NOT NULL,
                                            [nutzername]  NVARCHAR(7) NOT NULL,
                                            [aixmail] NVARCHAR(128) NOT NULL,
                                            [zweitaccount] INTEGER DEFAULT 0,
                                            [zweitmail] NVARCHAR(512),
                                            [m365] INTEGER DEFAULT 1
                                          )
                                    """;
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.CommandText = "CREATE INDEX IF NOT EXISTS sindex ON schueler(id);";
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.CommandText = """
                                    CREATE TABLE IF NOT EXISTS
                                            [kurse] (
                                            [bez]   NVARCHAR(512) NOT NULL PRIMARY KEY,
                                            [fach]  NVARCHAR(512) NOT NULL,
                                            [klasse]  NVARCHAR(16) NOT NULL,
                                            [stufe]  NVARCHAR(16) NOT NULL,
                                            [suffix]  NVARCHAR(16) NOT NULL,
                                            [istkurs]  INTEGER NOT NULL
                                          )
                                    """;
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.CommandText = "CREATE INDEX IF NOT EXISTS kindex ON kurse(bez);";
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.CommandText = """
                                    CREATE TABLE IF NOT EXISTS
                                            [unterrichtet] (
                                            [lehrerid]   INTEGER NOT NULL,
                                            [kursbez]  NVARCHAR(32) NOT NULL,
                                            PRIMARY KEY(lehrerid,kursbez),
                                            FOREIGN KEY(lehrerid) REFERENCES lehrkraft(id),
                                            FOREIGN KEY(kursbez) REFERENCES kurse(bez)
                                          )
                                    """;
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.CommandText = """
                                    CREATE TABLE IF NOT EXISTS
                                            [nimmtteil] (
                                            [schuelerid]   INTEGER NOT NULL,
                                            [kursbez]  NVARCHAR(32) NOT NULL,
                                            PRIMARY KEY(schuelerid,kursbez),
                                            FOREIGN KEY(schuelerid) REFERENCES schueler(id),
                                            FOREIGN KEY(kursbez) REFERENCES kurse(bez)
                                          )
                                    """;
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.CommandText = """
                                    CREATE TABLE IF NOT EXISTS
                                            [settings] (
                                            [setting]   NVARCHAR(512) NOT NULL UNIQUE,
                                            [value]  NVARCHAR(512) NOT NULL
                                          )
                                    """;
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.CommandText = """
                                    CREATE TABLE IF NOT EXISTS
                                            [fachersatz] (
                                            [kurzfach]   NVARCHAR(16) NOT NULL,
                                            [langfach]  NVARCHAR(64) NOT NULL,
                                            PRIMARY KEY(kurzfach,langfach)
                                          )
                                    """;
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.CommandText = """
                                    CREATE TABLE IF NOT EXISTS
                                            [log] (
                                            [id]  INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                                            [stufe]   NVARCHAR(16) NOT NULL,
                                            [datum]    NVARCHAR(128) NOT NULL,   
                                            [nachricht]  NVARCHAR(4096) NOT NULL
                                          )
                                    """;
            sqliteCmd.ExecuteNonQuery();

            var fachk = new[]
            {
                "D", "E", "M", "BI", "CH", "EK", "F7", "GE", "IF", "I0", "KU", "L7", "MU", "PH", "PK", "PS",
                "SN", "SP"
            };
            var fachl = new[]
            {
                "Deutsch", "Englisch", "Mathematik", "Biologie", "Chemie", "Erdkunde", "Französisch", "Geschichte",
                "Informatik", "Italienisch", "Kunst", "Latein", "Musik", "Physik", "Politik", "Psychologie",
                "Schwimmen", "Sport"
            };
            if (_dbpath != ":memory:") return;
            var calcSuffix = DateTime.Now.Month < 8
                ? (DateTime.Now.Year - 2001) + "" + (DateTime.Now.Year - 2000)
                : (DateTime.Now.Year - 2000) + "" + (DateTime.Now.Year - 1999);
            Settings settings = new()
            {
                Mailsuffix = "@schule.local",
                Fachersetzung = "",
                Kurzfaecher = fachk,
                Langfaecher = fachl,
                Kurssuffix = "_" + calcSuffix,
                Erprobungstufenleitung = "",
                Mittelstufenleitung = "",
                EFStufenleitung = "",
                Q1Stufenleitung = "",
                Q2Stufenleitung = "",
                Oberstufenkoordination = "",
                Version = version
            };
            sqliteCmd.Parameters.AddWithValue("$mailsuffixparam", settings.Mailsuffix);
            sqliteCmd.Parameters.AddWithValue("$kurssuffixparam", settings.Kurssuffix);
            sqliteCmd.Parameters.AddWithValue("$fachersatzparam", settings.Fachersetzung);

            sqliteCmd.Parameters.AddWithValue("$mailsuffix", "mailsuffix");
            sqliteCmd.Parameters.AddWithValue("$kurssuffix", "kurssuffix");
            sqliteCmd.Parameters.AddWithValue("$fachersatz", "fachersatz");

            sqliteCmd.Parameters.AddWithValue("$erprobungstufenleitung",
                "erprobungsstufenleitung");
            sqliteCmd.Parameters.AddWithValue("$mittelstufenleitung", "mittelstufenleitung");
            sqliteCmd.Parameters.AddWithValue("$efstufenleitung", "efstufenleitung");
            sqliteCmd.Parameters.AddWithValue("$q1stufenleitung", "q1stufenleitung");
            sqliteCmd.Parameters.AddWithValue("$q2stufenleitung", "q2stufenleitung");
            sqliteCmd.Parameters.AddWithValue("$oberstufenkoordination", "oberstufenkoordination");
            sqliteCmd.Parameters.AddWithValue("$version", "version");

            sqliteCmd.Parameters.AddWithValue("$erprobungstufenleitungparam",
                settings.Erprobungstufenleitung);
            sqliteCmd.Parameters.AddWithValue("$mittelstufenleitungparam",
                settings.Mittelstufenleitung);
            sqliteCmd.Parameters.AddWithValue("$efstufenleitungparam", settings.EFStufenleitung);
            sqliteCmd.Parameters.AddWithValue("$q1stufenleitungparam", settings.Q1Stufenleitung);
            sqliteCmd.Parameters.AddWithValue("$q2stufenleitungparam", settings.Q2Stufenleitung);
            sqliteCmd.Parameters.AddWithValue("$oberstufenkoordinationparam",
                settings.Oberstufenkoordination);
            sqliteCmd.Parameters.AddWithValue("$versionparam", settings.Version);
            sqliteCmd.CommandText =
                "INSERT OR REPLACE INTO settings (setting,value) VALUES($mailsuffix, $mailsuffixparam)";
            sqliteCmd.ExecuteNonQuery();
            sqliteCmd.CommandText =
                "INSERT OR REPLACE INTO settings (setting,value) VALUES($kurssuffix, $kurssuffixparam)";
            sqliteCmd.ExecuteNonQuery();
            sqliteCmd.CommandText =
                "INSERT OR REPLACE INTO settings (setting,value) VALUES($fachersatz, $fachersatzparam)";
            sqliteCmd.ExecuteNonQuery();
            sqliteCmd.CommandText =
                "INSERT OR REPLACE INTO settings (setting,value) VALUES($erprobungstufenleitung, $erprobungstufenleitungparam)";
            sqliteCmd.ExecuteNonQuery();
            sqliteCmd.CommandText =
                "INSERT OR REPLACE INTO settings (setting,value) VALUES($mittelstufenleitung, $mittelstufenleitungparam)";
            sqliteCmd.ExecuteNonQuery();
            sqliteCmd.CommandText =
                "INSERT OR REPLACE INTO settings (setting,value) VALUES($efstufenleitung, $efstufenleitungparam)";
            sqliteCmd.ExecuteNonQuery();
            sqliteCmd.CommandText =
                "INSERT OR REPLACE INTO settings (setting,value) VALUES($q1stufenleitung, $q1stufenleitungparam)";
            sqliteCmd.ExecuteNonQuery();
            sqliteCmd.CommandText =
                "INSERT OR REPLACE INTO settings (setting,value) VALUES($q2stufenleitung, $q2stufenleitungparam)";
            sqliteCmd.ExecuteNonQuery();
            sqliteCmd.CommandText =
                "INSERT OR REPLACE INTO settings (setting,value) VALUES($oberstufenkoordination, $oberstufenkoordinationparam)";
            sqliteCmd.ExecuteNonQuery();
            sqliteCmd.CommandText =
                "INSERT OR REPLACE INTO settings (setting,value) VALUES($version, $versionparam)";
            sqliteCmd.ExecuteNonQuery();
            sqliteCmd.Parameters.Clear();
            if (fachk.Length != fachl.Length) return;
            for (var i = 0; i < fachk.Length; i++)
            {
                if (fachl[i] == "" || fachk[i] == "") continue;
                var kurzesfach = fachk[i];
                var langesfach = fachl[i];
                sqliteCmd.CommandText =
                    "INSERT OR IGNORE INTO fachersatz (kurzfach, langfach) VALUES ($kfach, $lfach);";
                sqliteCmd.Parameters.AddWithValue("$kfach", kurzesfach);
                sqliteCmd.Parameters.AddWithValue("$lfach", langesfach);
                sqliteCmd.ExecuteNonQuery();
                sqliteCmd.Parameters.Clear();
            }
        }
        catch (SqliteException ex)
        {
            throw new ApplicationException("Kritischer Fehler beim Erstellen der SQL-Datei: " + ex.Message);
        }
    }

    /// <summary>
    /// updatet alte DBs von 0.5 auf 0.6
    /// </summary>
    /// <param name="sqliteCmd"></param>
    private static void upgradeDB(SqliteCommand sqliteCmd)
    {
        //upgrade DB to 0.6
        sqliteCmd.CommandText =
            $"SELECT COUNT(*) AS m365_col_count FROM pragma_table_info('schueler') WHERE name='m365'";
        sqliteCmd.ExecuteNonQuery();
        var sqliteDatareader = sqliteCmd.ExecuteReader();
        var output = 0;
        while (sqliteDatareader.Read())
        {
            output = Convert.ToInt32(sqliteDatareader.GetString("m365_col_count"));
        }

        sqliteDatareader.Close();
        sqliteCmd.CommandText =
            $"SELECT COUNT(*) AS id_col_count FROM pragma_table_info('schueler') WHERE name='id'";
        sqliteCmd.ExecuteNonQuery();
        sqliteDatareader = sqliteCmd.ExecuteReader();
        var db_version = 0;
        while (sqliteDatareader.Read())
        {
            db_version = Convert.ToInt32(sqliteDatareader.GetString("id_col_count"));
        }

        sqliteDatareader.Close();
        if (output != 0 || db_version <= 0) return;
        try
        {
            sqliteCmd.CommandText =
                $"ALTER TABLE schueler ADD COLUMN m365 INTEGER NOT NULL DEFAULT 1";
            sqliteCmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Fehler: " + ex.Message);
            Environment.Exit(-1);
        }
    }

    /// <summary>
    /// fügt den Kurs hinzu
    /// </summary>
    /// <param name="bez"></param>
    /// <param name="fach"></param>
    /// <param name="klasse"></param>
    /// <param name="stufe"></param>
    /// <param name="suffix"></param>
    /// <param name="istkurs"></param>
    public async Task AddKurs(string bez, string fach, string klasse, string stufe, string suffix, int istkurs)
    {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "INSERT OR IGNORE INTO kurse (bez, fach, klasse, stufe, suffix, istkurs) VALUES ($bez, $fach, $klasse, $stufe, $suffix, $istkurs);";
        sqliteCmd.Parameters.AddWithValue("$fach", fach);
        sqliteCmd.Parameters.AddWithValue("$bez", bez);
        sqliteCmd.Parameters.AddWithValue("$klasse", klasse);
        sqliteCmd.Parameters.AddWithValue("$stufe", stufe);
        sqliteCmd.Parameters.AddWithValue("$suffix", suffix);
        sqliteCmd.Parameters.AddWithValue("$istkurs", istkurs);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// fügt den Kurs hinzu
    /// </summary>
    /// <param name="kurs"></param>
    /// <returns></returns>
    public async Task AddKurs(Kurs kurs)
    {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "INSERT OR IGNORE INTO kurse (bez, fach, klasse, stufe, suffix, istkurs) VALUES ($bez, $fach, $klasse, $stufe, $suffix, $istkurs);";
        sqliteCmd.Parameters.AddWithValue("$fach", kurs.Fach);
        sqliteCmd.Parameters.AddWithValue("$bez", kurs.Bezeichnung);
        sqliteCmd.Parameters.AddWithValue("$klasse", kurs.Klasse);
        sqliteCmd.Parameters.AddWithValue("$stufe", kurs.Stufe);
        sqliteCmd.Parameters.AddWithValue("$suffix", kurs.Suffix);
        sqliteCmd.Parameters.AddWithValue("$istkurs", kurs.IstKurs);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// fügt die Lehrperson hinzu
    /// </summary>
    /// <param name="id"></param>
    /// <param name="vorname"></param>
    /// <param name="nachname"></param>
    /// <param name="kuerzel"></param>
    /// <param name="mail"></param>
    /// <param name="fakultas"></param>
    /// <param name="favo"></param>
    /// <param name="sfavo"></param>
    public async Task Addlehrkraft(int id, string vorname, string nachname, string kuerzel, string mail,
        string fakultas, string favo, string sfavo)
    {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "INSERT OR IGNORE INTO lehrkraft (id, nachname, vorname, kuerzel, mail, fakultas, pwtemp, favo, sfavo) VALUES ($id, $nachname, $vorname, $kuerzel, $mail, $fakultas, $pwtemp, $favo, $sfavo);";
        sqliteCmd.Parameters.AddWithValue("$id", id);
        sqliteCmd.Parameters.AddWithValue("$vorname", vorname);
        sqliteCmd.Parameters.AddWithValue("$nachname", nachname);
        sqliteCmd.Parameters.AddWithValue("$kuerzel", kuerzel.ToUpper());
        sqliteCmd.Parameters.AddWithValue("$mail", mail);
        sqliteCmd.Parameters.AddWithValue("$fakultas", fakultas.TrimEnd(';'));
        sqliteCmd.Parameters.AddWithValue("$pwtemp", GeneratePasswort(8));
        sqliteCmd.Parameters.AddWithValue("$favo", favo);
        sqliteCmd.Parameters.AddWithValue("$sfavo", sfavo);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// fügt die Lehrperson hinzu
    /// </summary>
    /// <param name="lehrkraft"></param>
    /// <returns></returns>
    public async Task Addlehrkraft(LuL lehrkraft)
    {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "INSERT OR IGNORE INTO lehrkraft (id, nachname, vorname, kuerzel, mail, fakultas, pwtemp, favo, sfavo) VALUES ($id, $nachname, $vorname, $kuerzel, $mail, $fakultas, $pwtemp, $favo, $sfavo);";
        sqliteCmd.Parameters.AddWithValue("$id", lehrkraft.ID);
        sqliteCmd.Parameters.AddWithValue("$vorname", lehrkraft.Vorname);
        sqliteCmd.Parameters.AddWithValue("$nachname", lehrkraft.Nachname);
        sqliteCmd.Parameters.AddWithValue("$kuerzel", lehrkraft.Kuerzel.ToUpper());
        sqliteCmd.Parameters.AddWithValue("$mail", lehrkraft.Mail);
        sqliteCmd.Parameters.AddWithValue("$fakultas", lehrkraft.Fakultas.TrimEnd(';'));
        sqliteCmd.Parameters.AddWithValue("$pwtemp",
            lehrkraft.Pwttemp.Length > 7 ? lehrkraft.Pwttemp : GeneratePasswort(8));
        sqliteCmd.Parameters.AddWithValue("$favo", lehrkraft.Favo);
        sqliteCmd.Parameters.AddWithValue("$sfavo", lehrkraft.SFavo);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// fügt eine Nachricht ins Log hinzu, Stufe entweder Info, Hinweis oder Fehler
    /// </summary>
    /// <param name="stufe"></param>
    /// <param name="nachricht"></param>
    public async Task<int> AddLogMessage(string stufe, string nachricht)
    {
        var sqliteCmd = _sqliteConn.CreateCommand();
        var dnow = DateTime.Now.ToLongDateString() + " " + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" +
                   DateTime.Now.Second;
        sqliteCmd.CommandText =
            "INSERT OR IGNORE INTO log (stufe, datum, nachricht) VALUES ($stufe, $dnow, $nachricht);";
        sqliteCmd.Parameters.AddWithValue("$stufe", stufe);
        sqliteCmd.Parameters.AddWithValue("$dnow", dnow);
        sqliteCmd.Parameters.AddWithValue("$nachricht", nachricht);
        sqliteCmd.ExecuteNonQuery();
        return 0;
    }

    /// <summary>
    /// fügt die angegebene Lehrperson zum angegebene Kurs hinzu
    /// </summary>
    /// <param name="lid"></param>
    /// <param name="kbez"></param>
    public async Task AddLtoK(int lid, string kbez)
    {
        if (lid == 0 || kbez == "") return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "INSERT OR IGNORE INTO unterrichtet (lehrerid, kursbez) VALUES ($lid, $kbez);";
        sqliteCmd.Parameters.AddWithValue("$lid", lid);
        sqliteCmd.Parameters.AddWithValue("$kbez", kbez);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// fügt die angegebene Lehrperson zum angegebene Kurs hinzu
    /// </summary>
    /// <param name="lehrkraft"></param>
    /// <param name="kurs"></param>
    /// <returns></returns>
    public async Task AddLtoK(LuL lehrkraft, Kurs kurs)
    {
        if (string.IsNullOrEmpty(kurs.Bezeichnung) || lehrkraft.ID == 0) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "INSERT OR IGNORE INTO unterrichtet (lehrerid, kursbez) VALUES ($lid, $kbez);";
        sqliteCmd.Parameters.AddWithValue("$lid", lehrkraft.ID);
        sqliteCmd.Parameters.AddWithValue("$kbez", kurs.Bezeichnung);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// fügt den Schüler/die Schülerin hinzu
    /// </summary>
    /// <param name="id"></param>
    /// <param name="vorname"></param>
    /// <param name="nachname"></param>
    /// <param name="mail"></param>
    /// <param name="klasse"></param>
    /// <param name="nutzername"></param>
    /// <param name="aixmail"></param>
    /// <param name="zweitaccount"></param>
    /// <param name="zweitmail"></param>
    public async Task AddSchuelerIn(int id, string vorname, string nachname, string mail, string klasse,
        string nutzername, string aixmail, int zweitaccount, string zweitmail)
    {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "INSERT OR IGNORE INTO schueler (id, vorname, nachname, mail, klasse, nutzername, aixmail, zweitaccount, zweitmail) VALUES ($id, $vorname, $nachname, $mail, $klasse, $nutzername, $aixmail,$zweitaccount, $zweitmail);";
        sqliteCmd.Parameters.AddWithValue("$id", id);
        sqliteCmd.Parameters.AddWithValue("$vorname", vorname);
        sqliteCmd.Parameters.AddWithValue("$nachname", nachname);
        sqliteCmd.Parameters.AddWithValue("$mail", mail);
        sqliteCmd.Parameters.AddWithValue("$klasse", klasse);
        sqliteCmd.Parameters.AddWithValue("$nutzername", nutzername);
        sqliteCmd.Parameters.AddWithValue("$aixmail", aixmail);
        sqliteCmd.Parameters.AddWithValue("$zweitaccount", zweitaccount);
        sqliteCmd.Parameters.AddWithValue("$zweitmail", zweitmail);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// fügt den Schüler/die Schülerin hinzu
    /// </summary>
    /// <param name="schulerin"></param>
    /// <returns></returns>
    public async Task AddSchuelerIn(SuS schulerin)
    {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "INSERT OR IGNORE INTO schueler (id, vorname, nachname, mail, klasse, nutzername, aixmail, zweitaccount, zweitmail) VALUES ($id, $vorname, $nachname, $mail, $klasse, $nutzername, $aixmail,$zweitaccount, $zweitmail);";
        sqliteCmd.Parameters.AddWithValue("$id", schulerin.ID);
        sqliteCmd.Parameters.AddWithValue("$vorname", schulerin.Vorname);
        sqliteCmd.Parameters.AddWithValue("$nachname", schulerin.Nachname);
        sqliteCmd.Parameters.AddWithValue("$mail", schulerin.Mail);
        sqliteCmd.Parameters.AddWithValue("$klasse", schulerin.Klasse);
        sqliteCmd.Parameters.AddWithValue("$nutzername", schulerin.Nutzername);
        sqliteCmd.Parameters.AddWithValue("$aixmail", schulerin.Aixmail);
        sqliteCmd.Parameters.AddWithValue("$zweitaccount", schulerin.Zweitaccount);
        sqliteCmd.Parameters.AddWithValue("$zweitmail", schulerin.Zweitmail);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// fügt den/die angegebenen Schüler/Schülerin zum angegebene Kurs hinzu
    /// </summary>
    /// <param name="sid"></param>
    /// <param name="kbez"></param>
    public async Task AddStoK(int sid, string kbez)
    {
        if (sid == 0 || kbez == "") return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "INSERT OR IGNORE INTO nimmtteil (schuelerid, kursbez) VALUES ($sid, $kbez);";
        sqliteCmd.Parameters.AddWithValue("$sid", sid);
        sqliteCmd.Parameters.AddWithValue("$kbez", kbez);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// fügt den/die angegebenen Schüler/Schülerin zum angegebene Kurs hinzu
    /// </summary>
    /// <param name="schulerin"></param>
    /// <param name="kurs"></param>
    /// <returns></returns>
    public async Task AddStoK(SuS schulerin, Kurs kurs)
    {
        if (schulerin.ID == 0 || string.IsNullOrEmpty(kurs.Bezeichnung)) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "INSERT OR IGNORE INTO nimmtteil (schuelerid, kursbez) VALUES ($sid, $kbez);";
        sqliteCmd.Parameters.AddWithValue("$sid", schulerin.ID);
        sqliteCmd.Parameters.AddWithValue("$kbez", kurs.Bezeichnung);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Fügt den SuS zum Klassenunterricht der übergebenen Klasse hinzu
    /// </summary>
    /// <param name="schulerin"></param>
    /// <param name="klasse"></param>
    /// <returns></returns>
    public async Task AddStoKlassenKurse(SuS schulerin, string klasse)
    {
        if (klasse.StartsWith('5') || klasse.StartsWith('6') || klasse.StartsWith('7') || klasse.StartsWith('8') ||
            klasse.StartsWith('9') ||
            klasse.StartsWith("10"))
        {
            var kliste = GetKursListe().Result.ToList();
            kliste = kliste.FindAll(k => k.Bezeichnung.StartsWith(klasse) && k.IstKurs == false);
            foreach (var k in kliste)
            {
                var sqliteCmd = _sqliteConn.CreateCommand();
                sqliteCmd.CommandText =
                    "INSERT OR IGNORE INTO nimmtteil (schuelerid, kursbez) VALUES ($sid, $kbez);";
                sqliteCmd.Parameters.AddWithValue("$sid", schulerin.ID);
                sqliteCmd.Parameters.AddWithValue("$kbez", k.Bezeichnung);
                sqliteCmd.ExecuteNonQuery();
                sqliteCmd.Parameters.Clear();
            }
        }
    }

    /// <summary>
    /// optimiert die DB und schließt die Verbindung
    /// </summary>
    private void CloseDB()
    {
        if (_ActiveTransaction)
        {
            _dbtrans?.Commit();
            _ActiveTransaction = false;
        }

        if (_sqliteConn.State != ConnectionState.Open) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "pragma optimize;";
        sqliteCmd.ExecuteNonQuery();
        _sqliteConn.Close();
    }

    /// <summary>
    /// Pflichtimplementierung um sicherzustellen, dass beim Löschen des Objekt Ressourcen etc. freigegeben werden. 
    /// Schließt die Datenbank
    /// </summary>
    public void Dispose()
    {
        CloseDB();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Export die Daten so, dass sie als Ausgangspunkt für einen Reimport genutzt werden können
    /// </summary>
    /// <param name="folder"></param>
    /// <returns></returns>
    public async Task<int> DumpDataToCSVs(string folder)
    {
        return await DumpDataToCSVs(folder, await GetSchuelerListe(), await GetLehrerListe());
    }

    /// <summary>
    /// Export die Daten so, dass sie als Ausgangspunkt für einen Reimport genutzt werden können
    /// </summary>
    /// <param name="folder"></param>
    /// <param name="lehrerliste"></param>
    /// <param name="susliste"></param>
    private async Task<int> DumpDataToCSVs(string folder, ReadOnlyCollection<SuS> susliste,
        IEnumerable<LuL> lehrerliste)
    {
        try
        {
            List<string> lulliste = ["firstname;lastname;idnumber;username;fakultas;email"];
            lulliste.AddRange(lehrerliste.Select(lehrer =>
                lehrer.Vorname + ";" + lehrer.Nachname + ";" + lehrer.ID + ";" + lehrer.Kuerzel + ";" +
                lehrer.Fakultas + ";" + lehrer.Mail));

            List<string> sliste = ["Vorname;Nachname;Interne ID-Nummer;E-Mail;Klasse"];
            List<string> kurse = ["Vorname|Nachname|Fach|Fachlehrer|Kursart|Kurs"];
            List<string> ids = ["Anmeldename;Referenz-Id;E-Mail"];
            List<string> zweitaccounts = ["Interne ID-Nummer"];
            await Parallel.ForEachAsync(susliste, async (schueler, cancellationToken) =>
                //foreach (var schueler in susliste)
            {
                sliste.Add(schueler.Vorname + ";" + schueler.Nachname + ";" + schueler.ID + ";" + schueler.Mail +
                           ";" + schueler.Klasse);
                ids.Add(schueler.Nutzername + ";" + schueler.ID + ";" + schueler.Mail);
                if (schueler.Zweitaccount)
                {
                    zweitaccounts.Add(schueler.ID + "");
                }

                await Parallel.ForEachAsync(GetKursVonSuS(schueler.ID).Result, cancellationToken, async (kurs, _) =>
                    //foreach (var kurs in await GetKursVonSuS(schueler.ID))
                {
                    var luls = await GetLuLAusKurs(kurs.Bezeichnung);
                    if (luls.Count > 0)
                    {
                        var l = await GetLehrkraft(luls[0].ID);
                        var fach = kurs.Fach.IndexOf('-') > 0 ? kurs.Fach[..kurs.Fach.IndexOf('-')] : kurs.Fach;
                        kurse.Add(schueler.Nachname + "|" + schueler.Vorname + "|" + fach + "|" +
                                  l.Kuerzel.ToUpper() +
                                  "|" + (kurs.IstKurs ? "PUK|" : "GKM|") +
                                  (kurs.IstKurs == false ? "" : kurs.Fach));
                    }
                });
            });

            await File.WriteAllLinesAsync(folder + "/sus.csv", sliste.Distinct().ToList(), Encoding.UTF8);
            await File.WriteAllLinesAsync(folder + "/lul.csv", lulliste.Distinct().ToList(), Encoding.UTF8);
            await File.WriteAllLinesAsync(folder + "/kurse.csv", kurse.Distinct().ToList(), Encoding.UTF8);
            await File.WriteAllLinesAsync(folder + "/ids.csv", ids.Distinct().ToList(), Encoding.UTF8);
            await File.WriteAllLinesAsync(folder + "/zweitaccount.csv", zweitaccounts.Distinct().ToList(),
                Encoding.UTF8);
            return 1;
        }
        catch (Exception ex)
        {
#if DEBUG
            await AddLogMessage("Debug", ex.StackTrace + ";" + ex.Message);
#endif
            return -1;
        }
    }

    /// <summary>
    /// Speichert die Schuldaten im Schildformat
    /// </summary>
    /// <param name="folder"></param>
    /// <param name="kursliste"></param>
    /// <returns></returns>
    public async Task<int> DumpKursDataToCSVs(string folder, List<string> kursliste)
    {
        try
        {
            List<string> kurse = ["Vorname|Nachname|Fach|Fachlehrer|Kursart|Kurs"];
            foreach (var k in kursliste)
            {
                var kurs = await GetKurs(k);
                foreach (var schueler in await GetSuSAusKurs(k))
                {
                    var l = (await GetLuLAusKurs(k))[0];
                    var fach = kurs.Fach.IndexOf('-') > 0 ? kurs.Fach[..kurs.Fach.IndexOf('-')] : kurs.Fach;
                    kurse.Add(schueler.Nachname + "|" + schueler.Vorname + "|" + fach + "|" + l.Kuerzel.ToUpper() +
                              "|" + (kurs.IstKurs ? "PUK|" : "GKM|") + (kurs.IstKurs == false ? "" : kurs.Fach));
                }
            }

            await File.WriteAllLinesAsync(folder + "/kurse.csv", kurse.Distinct().ToList(), Encoding.UTF8);
            return 1;
        }
        catch (Exception ex)
        {
#if DEBUG
            await AddLogMessage("Debug", ex.StackTrace + ";" + ex.Message);
#endif
            return -1;
        }
    }

    /// <summary>
    /// speichert die in-memory-datenbank in eine Datei
    /// </summary>
    /// <param name="importfrom">Die Datenbank von der die Daten importiert werden</param>
    public async Task<int> Import(Schuldatenbank importfrom)
    {
        await StartTransaction();
        //kurse übertragen
        foreach (var kurs in await importfrom.GetKursListe())
        {
            await AddKurs(kurs.Bezeichnung, kurs.Fach, kurs.Klasse, kurs.Stufe, kurs.Suffix,
                Convert.ToInt32(kurs.IstKurs));
        }

        //sus übertragen
        foreach (var schueler in await importfrom.GetSchuelerListe())
        {
            await AddSchuelerIn(schueler.ID, schueler.Vorname, schueler.Nachname, schueler.Mail, schueler.Klasse,
                schueler.Nutzername, schueler.Aixmail, Convert.ToInt32(schueler.Zweitaccount), schueler.Zweitmail);
            foreach (var kurs in await importfrom.GetKursVonSuS(Convert.ToInt32(schueler.ID)))
            {
                await AddStoK(Convert.ToInt32(schueler.ID), kurs.Bezeichnung);
            }
        }

        //lul übertragen
        foreach (var lehrkraft in await importfrom.GetLehrerListe())
        {
            await Addlehrkraft(Convert.ToInt32(lehrkraft.ID), lehrkraft.Vorname, lehrkraft.Nachname,
                lehrkraft.Kuerzel, lehrkraft.Mail, lehrkraft.Fakultas, lehrkraft.Favo, lehrkraft.SFavo);
            foreach (var kurs in await importfrom.GetKursVonLuL(lehrkraft.ID))
            {
                await AddLtoK(Convert.ToInt32(lehrkraft.ID), kurs.Bezeichnung);
            }
        }

        //log übertragen
        foreach (var m in await importfrom.GetLog())
        {
            var message = m.Split('\t');
            var logentry = "";
            for (var i = 2; i < message.Length; i++)
            {
                logentry += message[i] + "\t";
            }

            await AddLogMessage(message[0], logentry);
        }

        await StopTransaction();

        //Settings übertragen
        await SetSettings(await importfrom.GetSettings());
        return 0;
    }

    /// <summary>
    /// liest die Daten der Eltern/ERrziehungsberechtigten ein
    /// </summary>
    /// <param name="elternfile"></param>
    /// <returns></returns>
    public async Task ElternEinlesen(string elternfile)
    {
        var lines = await File.ReadAllLinesAsync(elternfile);
        int isk = -1, isn = -1, isv = -1, imail = -1;
        var header = lines[0].Split(';');
        for (var i = 0; i < header.Length; i++)
        {
            if (header[i].Equals("Schüler-Klasse"))
            {
                isk = i;
            }

            if (header[i].Equals("Schüler-Nachname"))
            {
                isn = i;
            }

            if (header[i].Equals("Schüler-Vorname"))
            {
                isv = i;
            }

            if (header[i].Equals("2. Person: E-Mail"))
            {
                imail = i;
            }
        }

        await StartTransaction();
        for (var i = 1; i < lines.Length; i++)
        {
            try
            {
                var line = lines[i].Split(';');
                if (line[imail] == "") continue;
                try
                {
                    var susliste = await GetSchueler(line[isv], line[isn]);
                    foreach (var sus in susliste.Where(sus => sus.Klasse == line[isk]))
                    {
                        await UpdateSchueler(sus.ID, sus.Vorname, sus.Nachname, sus.Mail, sus.Klasse,
                            sus.Nutzername, sus.Aixmail, Convert.ToInt32(sus.Zweitaccount), line[imail]);
                    }
                }
                catch (Exception e)
                {
                    await AddLogMessage("Fehler", e.Message);
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                await AddLogMessage("Debug", ex.StackTrace + ";" + ex.Message);
#endif
                await AddLogMessage("Error", "Fehler beim Einlesen der Eltern");
            }
        }

        await StopTransaction();
    }

    /// <summary>
    /// exportiert die SuS/Lehrkräfte und Kursdaten für AIX und Moodle; baut dazu die nötigen Strings auf und schreibt diese in CSV-Dateien
    /// </summary>
    /// <param name="folder">Zielordner</param>
    /// <param name="destsys">m für Moodle, a für AIX</param>
    /// <param name="whattoexport">s für SuS, l für Lehrkräfte, e für Eltern, k für Kurse</param>
    /// <param name="withPasswort">mit Erstpasswort: true für ja, false für nein</param>
    /// <param name="passwort">das Passwort, welches gesetzt werden soll</param>
    /// <param name="expandfiles">Dateien erweitern: true für ja, false für nein</param>
    /// <param name="nurMoodleSuffix">Soll das Suffix nur für Moodle-Kurse verwendet werden</param>
    /// <param name="kursvorlage">Stringarray mit ShortID aus Moodle für die Vorlagenkurse</param>
    /// <param name="susidliste">Liste mit SuS-IDs</param>
    /// <param name="lulidliste">Liste mit LuL-IDs</param>
    /// <param name="kursliste">Liste mit Kurs-Bezeichnungen</param>
    public async Task<int> ExportCSV(string folder, string destsys, string whattoexport, bool withPasswort,
        string passwort,
        bool expandfiles, bool nurMoodleSuffix, string[] kursvorlage, List<int> susidliste,
        ReadOnlyCollection<int> lulidliste,
        ReadOnlyCollection<string> kursliste)
    {
        try
        {
            foreach (var id in GetM365Blacklist().Result)
            {
                susidliste.Remove(id);
            }

            if (destsys.Equals("all"))
            {
                return await ExportCSV(folder, "ami", whattoexport, withPasswort, passwort, expandfiles,
                    nurMoodleSuffix,
                    kursvorlage, susidliste,
                    lulidliste, kursliste);
            }

            if (whattoexport.Equals("all"))
            {
                return await ExportCSV(folder, destsys, "ksle", withPasswort, passwort, expandfiles,
                    nurMoodleSuffix, kursvorlage,
                    susidliste,
                    lulidliste, kursliste);
            }

            if (whattoexport.Contains('e'))
            {
                withPasswort = true;
            }

            List<string> ausgabeAIXL = [];
            List<string> ausgabeAIXS = [];
            List<string> ausgabeMoodleEinschreibungen = [];
            List<string> ausgabeMoodleKurse = [];
            List<string> ausgabeMoodleUser = [];
            List<string> ausgabeIntern =
            [
                "kuerzel;nachname;vorname;plz_ort_;adresse;tel_privat;tel_mobil;email_privat;email_dienst;gebdatum_;status_;mail_Adresse;fach1;fach2;fach3;fakult;funktion_;pw_temp;aktiv;gebdatum;plz;ort;titel;nachname;pop3_dienst;pop3_menge"
            ];
            var kursvorlagen = kursvorlage[0].Equals("") && kursvorlage[1].Equals("");
            ausgabeMoodleKurse.Add(kursvorlagen
                ? "shortname;fullname;idnumber;category_idnumber;format"
                : "shortname;fullname;idnumber;category_idnumber;format;templatecourse");

            if (withPasswort)
            {
                ausgabeMoodleUser.Add("email;password;username;idnumber;lastname;firstname;cohort1");
                ausgabeAIXS.Add(
                    "\"Vorname\";\"Nachname\";\"Klasse\";\"Referenz-ID\";\"Kennwort\";\"Arbeitsgruppen\"");
                ausgabeAIXL.Add("\"Vorname\";\"Nachname\";\"Referenz-ID\";\"Kennwort\";\"Arbeitsgruppen\"");
            }
            else
            {
                ausgabeMoodleUser.Add("email;username;idnumber;lastname;firstname;cohort1");
                ausgabeAIXS.Add("\"Vorname\";\"Nachname\";\"Klasse\";\"Referenz-ID\";\"Arbeitsgruppen\"");
                ausgabeAIXL.Add("\"Vorname\";\"Nachname\";\"Referenz-ID\";\"Arbeitsgruppen\"");
            }

            string[] sekI = ["5", "6", "7", "8", "9", "10"];
            if (whattoexport.Contains('k'))
            {
                foreach (var kurs in kursliste)
                {
                    if (kurs.EndsWith('-')) continue;
                    var k = GetKurs(kurs.Split(';')[0]).Result;
                    if (k.Bezeichnung.Contains("Erprobungsstufe") || k.Bezeichnung.Contains("Mittelstufe") ||
                        k.Bezeichnung.Contains("Einführungsphase") || k.Bezeichnung.Contains("Qualifikationsphase"))
                    {
                        ausgabeMoodleKurse.Add(k.Bezeichnung + k.Suffix + ";" + k.Bezeichnung +
                                               " SJ" + k.Suffix.Substring(1, 2) + "/" +
                                               k.Suffix.Substring(3, 2) + ";" + k.Bezeichnung +
                                               k.Suffix + ";SJ" + k.Suffix + ";tiles");
                    }
                    else if (k.Bezeichnung.Contains("konferenz"))
                    {
                        ausgabeMoodleKurse.Add(k.Bezeichnung + k.Suffix + ";" + k.Bezeichnung +
                                               " SJ" + k.Suffix.Substring(1, 2) + "/" +
                                               k.Suffix.Substring(3, 2) + ";" + k.Bezeichnung +
                                               k.Suffix + ";lehrkraefte;tiles");
                    }
                    else if (kursvorlagen)
                    {
                        if (k.IstKurs)
                        {
                            if (sekI.Contains(k.Stufe))
                            {
                                ausgabeMoodleKurse.Add(k.Bezeichnung + k.Suffix + ";" + k.Klasse + " " +
                                                       GetLangeFachbezeichnung(k.Fach).Result + "-" +
                                                       k.Art.Substring(k.Art.Length - 1, 1) + " SJ" +
                                                       k.Suffix.Substring(1, 2) + "/" +
                                                       k.Suffix.Substring(3, 2) + ";" + k.Bezeichnung +
                                                       k.Suffix + ";stufe_" + k.Stufe + k.Suffix + ";tiles");
                            }
                            else
                            {
                                ausgabeMoodleKurse.Add(k.Bezeichnung + k.Suffix + ";" + k.Klasse + " " +
                                                       GetLangeFachbezeichnung(k.Fach).Result + "-" + k.Art +
                                                       " SJ" + k.Suffix.Substring(1, 2) + "/" +
                                                       k.Suffix.Substring(3, 2) + ";" + k.Bezeichnung +
                                                       k.Suffix + ";stufe_" + k.Stufe + k.Suffix + ";tiles");
                            }
                        }
                        else
                        {
                            ausgabeMoodleKurse.Add(k.Bezeichnung + k.Suffix + ";" + k.Klasse + " " +
                                                   GetLangeFachbezeichnung(k.Fach).Result + " SJ" +
                                                   k.Suffix.Substring(1, 2) + "/" + k.Suffix.Substring(3, 2) +
                                                   ";" + k.Bezeichnung + k.Suffix + ";" + k.Klasse + k.Suffix +
                                                   ";tiles");
                        }
                    }
                    else
                    {
                        var strkursvorlage = k.Bezeichnung.Contains("KL") ? kursvorlage[0] : kursvorlage[1];
                        if (k.IstKurs)
                        {
                            if (sekI.Contains(k.Stufe))
                            {
                                ausgabeMoodleKurse.Add(k.Bezeichnung + k.Suffix + ";" + k.Klasse + " " +
                                                       GetLangeFachbezeichnung(k.Fach).Result + "-" +
                                                       k.Art.Substring(k.Art.Length - 1, 1) + " SJ" +
                                                       k.Suffix.Substring(1, 2) + "/" +
                                                       k.Suffix.Substring(3, 2) + ";" + k.Bezeichnung +
                                                       k.Suffix + ";stufe_" + k.Stufe + k.Suffix + ";tiles;" +
                                                       strkursvorlage);
                            }
                            else
                            {
                                ausgabeMoodleKurse.Add(k.Bezeichnung + k.Suffix + ";" + k.Klasse + " " +
                                                       GetLangeFachbezeichnung(k.Fach).Result + "-" + k.Art +
                                                       " SJ" + k.Suffix.Substring(1, 2) + "/" +
                                                       k.Suffix.Substring(3, 2) + ";" + k.Bezeichnung +
                                                       k.Suffix + ";stufe_" + k.Stufe + k.Suffix + ";tiles;" +
                                                       strkursvorlage);
                            }
                        }
                        else
                        {
                            ausgabeMoodleKurse.Add(k.Bezeichnung + k.Suffix + ";" + k.Klasse + " " +
                                                   GetLangeFachbezeichnung(k.Fach).Result + " SJ" +
                                                   k.Suffix.Substring(1, 2) + "/" + k.Suffix.Substring(3, 2) +
                                                   ";" + k.Bezeichnung + k.Suffix + ";" + k.Klasse + k.Suffix +
                                                   ";tiles;" + strkursvorlage);
                        }
                    }
                }
            }

            if (whattoexport.Contains('s'))
            {
                await Parallel.ForEachAsync(susidliste, CancellationToken.None, async (sus, CancellationToken) =>
                    //   foreach (var sus in susidliste)
                {
                    var s = GetSchueler(sus).Result;
                    var kListe = "";
                    foreach (var kk in await GetKursVonSuS(s.ID))
                    {
                        if (string.IsNullOrEmpty(kk.Bezeichnung))
                        {
                            break;
                        }

                        kListe += kk.Bezeichnung + kk.Suffix + "|";
                        if (kk.Fach.Equals("KL") || kk.Fach.Equals("StuBo"))
                        {
                            ausgabeMoodleEinschreibungen.Add("add,schueler," + s.ID + "," + kk.Bezeichnung +
                                                             kk.Suffix);
                        }
                        else
                        {
                            ausgabeMoodleEinschreibungen.Add("add,student," + s.ID + "," + kk.Bezeichnung +
                                                             kk.Suffix);
                        }

                        if (s.Klasse.StartsWith('5') || s.Klasse.StartsWith('6'))
                        {
                            ausgabeMoodleEinschreibungen.Add("add,schueler," + s.ID + ",erprobungsstufe" +
                                                             GetKursSuffix().Result);
                        }
                        else if (s.Klasse.StartsWith('7') || s.Klasse.StartsWith('8') || s.Klasse.StartsWith('9') ||
                                 s.Klasse.StartsWith("10"))
                        {
                            ausgabeMoodleEinschreibungen.Add("add,schueler," + s.ID + ",mittelstufe" +
                                                             GetKursSuffix().Result);
                        }
                        else
                        {
                            ausgabeMoodleEinschreibungen.Add("add,schueler," + s.ID + ",Stufenkurs" + s.Klasse +
                                                             GetKursSuffix().Result);
                        }
                    }

                    kListe = kListe.TrimEnd('|');
                    if (nurMoodleSuffix)
                    {
                        kListe = kListe.Replace(GetKursSuffix().Result, "");
                    }

                    var susmail = s.Mail.Contains(' ') ? s.Mail.Split(' ')[0] : s.Mail;
                    if (withPasswort)
                    {
                        var pwd = passwort.Length > 7
                            ? passwort
                            : "Klasse" + s.Klasse + DateTime.Now.Year + "!";
                        ausgabeMoodleUser.Add(susmail + ";" + pwd + ";" +
                                              s.Nutzername + ";" + s.ID + ";" + s.Nachname + ";" + s.Vorname +
                                              ";schueler");
                        ausgabeAIXS.Add("\"" + s.Vorname + "\";\"" + s.Nachname + "\";\"" + s.Klasse + "\";\"" +
                                        s.ID + "\";\"" +
                                        pwd + "\";\"" + kListe + "\"");
                    }
                    else
                    {
                        ausgabeMoodleUser.Add(susmail + ";" + s.Nutzername + ";" + s.ID + ";" + s.Nachname + ";" +
                                              s.Vorname + ";schueler");
                        ausgabeAIXS.Add("\"" + s.Vorname + "\";\"" + s.Nachname + "\";\"" + s.Klasse + "\";\"" +
                                        s.ID + "\";\"" + kListe + "\"");
                    }
                });
            }

            if (whattoexport.Contains('e'))
            {
                if (!whattoexport.Contains('m'))
                {
                    whattoexport += "m";
                }

                foreach (var s in susidliste)
                {
                    var sus = GetSchueler(s).Result;
                    var susmail = sus.Mail.Contains(' ') ? sus.Mail.Split(' ')[0] : sus.Mail;
                    switch (sus.Zweitaccount)
                    {
                        case true when sus.Zweitmail.Contains(','):
                        {
                            if (sus.Klasse.StartsWith('5') || sus.Klasse.StartsWith('6'))
                            {
                                var zweitmails = sus.Zweitmail.Split(',');
                                var zweitmail = zweitmails[0].Trim() != sus.Mail.Trim()
                                    ? zweitmails[0].Trim()
                                    : zweitmails[1].Trim();
                                ausgabeMoodleUser.Add(zweitmail + ";Klasse" + sus.Klasse +
                                                      DateTime.Now.Year + "!" + ";" + sus.Nutzername + "_E1;" +
                                                      "E_" +
                                                      sus.ID + "1;" + sus.Nachname + "_Eltern;" + sus.Vorname +
                                                      ";eltern");
                                ausgabeMoodleEinschreibungen.Add("add,eltern,E_" + sus.ID + "1," + sus.Klasse +
                                                                 "KL" +
                                                                 GetKursSuffix().Result);
                                ausgabeMoodleEinschreibungen.Add("add,eltern,E_" + sus.ID + "1,erprobungsstufe" +
                                                                 GetKursSuffix().Result);
                            }
                            else if (sus.Klasse.StartsWith('7') || sus.Klasse.StartsWith('8') ||
                                     sus.Klasse.StartsWith('9') | sus.Klasse.StartsWith("10"))
                            {
                                ausgabeMoodleUser.Add(sus.Zweitmail.Split(',')[0] + ";Klasse" + sus.Klasse +
                                                      DateTime.Now.Year + "!" +
                                                      ";" + sus.Nutzername + "_E1;" + "E_" + sus.ID + "1;" +
                                                      sus.Nachname + "_Eltern;" + sus.Vorname + ";eltern");
                                ausgabeMoodleEinschreibungen.Add("add,eltern,E_" + sus.ID + "1," + sus.Klasse +
                                                                 "KL" +
                                                                 GetKursSuffix().Result);
                                ausgabeMoodleEinschreibungen.Add("add,eltern,E_" + sus.ID + "1,mittelstufe" +
                                                                 GetKursSuffix().Result);
                            }

                            break;
                        }
                        case true when !sus.Zweitmail.Contains(','):
                            await AddLogMessage("Error",
                                sus.Klasse + ":" + sus.Nachname + ", " + sus.Vorname +
                                " ohne Zweitmail trotz gesetzter Flag");
                            break;
                    }

                    if (sus.Klasse.StartsWith('5') || sus.Klasse.StartsWith('6'))
                    {
                        ausgabeMoodleUser.Add(susmail + ";Klasse" + sus.Klasse + DateTime.Now.Year + "!" + ";" +
                                              sus.Nutzername + "_E;" + "E_" + sus.ID + ";" + sus.Nachname +
                                              "_Eltern;" + sus.Vorname + ";eltern");
                        ausgabeMoodleEinschreibungen.Add("add,eltern,E_" + sus.ID + "," + sus.Klasse + "KL" +
                                                         GetKursSuffix().Result);
                        ausgabeMoodleEinschreibungen.Add("add,eltern,E_" + sus.ID + ",erprobungsstufe" +
                                                         GetKursSuffix().Result);
                    }
                    else if (sus.Klasse.StartsWith('7') || sus.Klasse.StartsWith('8') ||
                             sus.Klasse.StartsWith('9') | sus.Klasse.StartsWith("10"))
                    {
                        ausgabeMoodleUser.Add(susmail + ";Klasse" + sus.Klasse + DateTime.Now.Year + "!" + ";" +
                                              sus.Nutzername + "_E;" + "E_" + sus.ID + ";" + sus.Nachname +
                                              "_Eltern;" + sus.Vorname + ";eltern");
                        ausgabeMoodleEinschreibungen.Add("add,eltern,E_" + sus.ID + "," + sus.Klasse + "KL" +
                                                         GetKursSuffix().Result);
                        ausgabeMoodleEinschreibungen.Add("add,eltern,E_" + sus.ID + ",mittelstufe" +
                                                         GetKursSuffix().Result);
                    }
                }
            }

            if (whattoexport.Contains('l'))
            {
                await Parallel.ForEachAsync(lulidliste, CancellationToken.None, async (l, CancellationToken) =>
                    //foreach (var l in lulidliste)
                {
                    var lt = await GetLehrkraft(l);
                    var kListe = "";
                    var fakultas = lt.Fakultas.Split(',');
                    var fak = fakultas.Aggregate("", (current, fa) => current + ("|^Fako " + fa));

                    fak += fak.Replace("^", "");
                    fak = fak.TrimStart('|');
                    foreach (var kurs in await GetKursVonLuL(lt.ID))
                    {
                        if (string.IsNullOrEmpty(kurs.Bezeichnung)) continue;
                        if (kurs.Bezeichnung.Contains("Jahrgangsstufenkonferenz"))
                        {
                            var stufenleitungen = await getOberstufenleitung(kurs.Stufe);
                            var role = stufenleitungen.Contains(lt) ||
                                       GetSettings().Result.Oberstufenkoordination.Contains(lt.Kuerzel)
                                ? "editingteacher"
                                : "student";
                            ausgabeMoodleEinschreibungen.Add("add," + role + "," + lt.ID + "," +
                                                             kurs.Bezeichnung + kurs.Suffix);
                        }
                        else if (kurs.IstKurs)
                        {
                            ausgabeMoodleEinschreibungen.Add("add,editingteacher," + lt.ID + "," +
                                                             kurs.Bezeichnung + kurs.Suffix);
                        }
                        else
                        {
                            ausgabeMoodleEinschreibungen.Add("add,editingteacher," + lt.ID + "," +
                                                             kurs.Bezeichnung + kurs.Suffix);
                        }

                        if (kurs.Bezeichnung.Length > 20) continue;
                        kListe += "^" + kurs.Bezeichnung + kurs.Suffix + "|";
                    }

                    ausgabeMoodleEinschreibungen.AddRange(lt.Fakultas.Split(',')
                        .Select(fach => "add,editingteacher," + lt.ID + ",FS_" + fach));

                    if (kListe == "^|")
                    {
                        kListe = "";
                    }

                    if (nurMoodleSuffix && kListe != "")
                    {
                        kListe = kListe.Replace(GetKursSuffix().Result, "");
                    }

                    if (withPasswort)
                    {
                        ausgabeMoodleUser.Add(lt.Mail + ";" + await GetTempPasswort(lt.ID) + ";" + lt.Kuerzel +
                                              ";" + lt.ID + ";" + lt.Nachname + ";" + lt.Vorname + ";lehrer");
                        ausgabeAIXL.Add("\"" + lt.Vorname + "\";\"" + lt.Nachname + "\";\"" + lt.ID + "\";\"" +
                                        await GetTempPasswort(lt.ID) + "\";\"*|" + kListe + fak + "\"");
                    }
                    else
                    {
                        ausgabeMoodleUser.Add(lt.Mail + ";" + lt.Kuerzel + ";" + lt.ID + ";" + lt.Nachname + ";" +
                                              lt.Vorname);
                        ausgabeAIXL.Add("\"" + lt.Vorname + "\";\"" + lt.Nachname + "\";\"" + lt.ID + "\";\"*|" +
                                        kListe + fak + "\"");
                    }
                });
            }

            if (destsys.Contains('i'))
            {
                foreach (var l in lulidliste)
                {
                    var lt = await GetLehrkraft(l);
                    var fakultas = lt.Fakultas.Split(',');
                    var maildienst = lt.Mail.Split('@')[0];
                    var firstChar = maildienst[0];
                    var UpperCaseFirstCharacter = char.ToUpper(firstChar);
                    maildienst = UpperCaseFirstCharacter + maildienst[1..];
                    var fakult = fakultas.Aggregate("", (current, t) => current + (t + ";"));
                    switch (fakultas.Length)
                    {
                        case 2:
                            fakult += ";" + lt.Fakultas;
                            break;
                        case 3:
                            fakult += lt.Fakultas;
                            break;
                    }

                    ausgabeIntern.Add(lt.Kuerzel + ";" + lt.Nachname + ";" + lt.Vorname + ";;;;;" + ";" +
                                      maildienst + ";;;" + lt.Mail + ";" + fakult + ";;" +
                                      GetTempPasswort(l).Result + ";1;;;;;" + lt.Nachname + ";" +
                                      maildienst.ToLower() + ";1");
                }
            }

            if (expandfiles)
            {
                try
                {
                    if (destsys.Contains('a'))
                    {
                        if (File.Exists(folder + "/aix_sus.csv"))
                        {
                            var aixSus = (await File.ReadAllLinesAsync(folder + "/aix_sus.csv")).ToList();
                            aixSus.RemoveAt(0);
                            ausgabeAIXS.AddRange(aixSus);
                            await File.WriteAllLinesAsync(folder + "/aix_sus.csv", ausgabeAIXS.Distinct().ToList(),
                                Encoding.UTF8);
                        }

                        if (File.Exists(folder + "/aix_lul.csv"))
                        {
                            var aixLul = (await File.ReadAllLinesAsync(folder + "/aix_lul.csv")).ToList();
                            aixLul.RemoveAt(0);
                            ausgabeAIXL.AddRange(aixLul);
                            await File.WriteAllLinesAsync(folder + "/aix_lul.csv", ausgabeAIXL.Distinct().ToList(),
                                Encoding.UTF8);
                        }
                    }

                    if (destsys.Contains('m'))
                    {
                        if (File.Exists(folder + "/mdl_einschreibungen.csv"))
                        {
                            var mdlEin =
                                (await File.ReadAllLinesAsync(folder + "/mdl_einschreibungen.csv")).ToList();
                            ausgabeMoodleKurse.RemoveAt(0);
                            ausgabeMoodleEinschreibungen.AddRange(mdlEin);
                            await File.WriteAllLinesAsync(folder + "/mdl_einschreibungen.csv",
                                ausgabeMoodleEinschreibungen.Distinct().ToList(), Encoding.UTF8);
                        }

                        if (File.Exists(folder + "/mdl_kurse.csv"))
                        {
                            var mdlKurse = (await File.ReadAllLinesAsync(folder + "/mdl_kurse.csv")).ToList();
                            if (mdlKurse.Count > 0)
                            {
                                mdlKurse.RemoveAt(0);
                            }

                            ausgabeMoodleKurse.AddRange(mdlKurse);
                            await File.WriteAllLinesAsync(folder + "/mdl_kurse.csv",
                                ausgabeMoodleKurse.Distinct().ToList(),
                                Encoding.UTF8);
                        }

                        if (File.Exists(folder + "/mdl_nutzer.csv"))
                        {
                            var mdlNutzer = (await File.ReadAllLinesAsync(folder + "/mdl_nutzer.csv")).ToList();
                            if (mdlNutzer.Count > 0)
                            {
                                mdlNutzer.RemoveAt(0);
                            }

                            ausgabeMoodleUser.AddRange(mdlNutzer);
                            await File.WriteAllLinesAsync(folder + "/mdl_nutzer.csv",
                                ausgabeMoodleUser.Distinct().ToList(),
                                Encoding.UTF8);
                        }
                    }

                    if (destsys.Contains('i'))
                    {
                        if (File.Exists(folder + "/Lehrerdaten_anschreiben.csv"))
                        {
                            var llgIntern =
                                (await File.ReadAllLinesAsync(folder + "/Lehrerdaten_anschreiben.csv")).ToList();
                            llgIntern.RemoveAt(0);
                            ausgabeIntern.AddRange(llgIntern);
                            await File.WriteAllLinesAsync(folder + "/Lehrerdaten_anschreiben.csv",
                                ausgabeIntern.Distinct().ToList(), Encoding.UTF8);
                        }
                    }
                }

                catch (Exception ex)
                {
#if DEBUG
                    await AddLogMessage("Debug", ex.StackTrace + ";" + ex.Message);
#endif
                    return -1;
                }
            }
            else
            {
                if (destsys.Contains('a'))
                {
                    await File.WriteAllLinesAsync(folder + "/aix_sus.csv", ausgabeAIXS.Distinct().ToList(),
                        Encoding.UTF8);
                    await File.WriteAllLinesAsync(folder + "/aix_lul.csv", ausgabeAIXL.Distinct().ToList(),
                        Encoding.UTF8);
                }

                if (destsys.Contains('m'))
                {
                    await File.WriteAllLinesAsync(folder + "/mdl_einschreibungen.csv",
                        ausgabeMoodleEinschreibungen.Distinct().ToList(), Encoding.UTF8);
                    await File.WriteAllLinesAsync(folder + "/mdl_kurse.csv", ausgabeMoodleKurse.Distinct().ToList(),
                        Encoding.UTF8);
                    await File.WriteAllLinesAsync(folder + "/mdl_nutzer.csv", ausgabeMoodleUser.Distinct().ToList(),
                        Encoding.UTF8);
                }

                if (destsys.Contains('i'))
                {
                    await File.WriteAllLinesAsync(folder + "/Lehrerdaten_anschreiben.csv",
                        ausgabeIntern.Distinct().ToList(),
                        Encoding.UTF8);
                }
            }

            return 1;
        }
        catch (Exception ex)
        {
#if DEBUG
            await AddLogMessage("Debug", ex.StackTrace + ";" + ex.Message);
#endif
            return -1;
        }
    }

    /// <summary>
    /// generiert ein Passwort bestehend aus Buchstaben, Ziffern und Sonderzeichen
    /// </summary>
    /// <param name="laenge">Länge des zu generierendes Passwort</param>
    /// <returns>String das generierte Passwort aus Zufallszeichen</returns>
    public static string GeneratePasswort(int laenge)
    {
        //erlaubt beim Hoster: /-_#*+!§,()=:.$äöüÄÖÜß
        const string validPasswordChars = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ1234567890+-.,()!*/_#";
        StringBuilder res = new();
        while (0 < laenge--)
        {
            res.Append(validPasswordChars[RandomNumberGenerator.GetInt32(validPasswordChars.Length)]);
        }

        return res.ToString();
    }

    /// <summary>
    /// gibt die die Gegenüberstellung der Fächer in Kurz- und Langschreibweise  als Liste zurück
    /// </summary>
    /// <returns>String-Liste der Schreibweise, pro Zeile ein Fach mit ;-getrennt </returns>
    public async Task<ReadOnlyCollection<string>> GetFachersatz()
    {
        List<string> flist = new();
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT kurzfach,langfach FROM fachersatz;";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            var returnstr = "";
            for (var i = 0; i < sqliteDatareader.FieldCount; i++)
            {
                returnstr += sqliteDatareader.GetString(i) + ";";
            }

            flist.Add(returnstr);
        }

        return new ReadOnlyCollection<string>(flist);
    }

    /// <summary>
    /// gibt den Pfad zur Datenbankdatei zurück
    /// </summary>
    public async Task<string> GetFilePath()
    {
        return _dbpath;
    }

    /// <summary>
    /// gibt die Informationen Bezeichnung, Fach, Klasse, Stufe, Kurssuffix und istKurs zur übergebenen Kursbezeichnung zurück
    /// </summary>
    /// <param name="kbez"></param>
    public async Task<Kurs> GetKurs(string kbez)
    {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT bez,fach,klasse,stufe,suffix,istkurs FROM kurse WHERE bez = '" + kbez + "';";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        Kurs retKurs = new();
        while (sqliteDatareader.Read())
        {
            retKurs.Bezeichnung = sqliteDatareader.GetString(0);
            retKurs.Fach = sqliteDatareader.GetString(1);
            retKurs.Klasse = sqliteDatareader.GetString(2);
            retKurs.Stufe = sqliteDatareader.GetString(3);
            retKurs.Suffix = sqliteDatareader.GetString(4);
            retKurs.IstKurs = Convert.ToBoolean(sqliteDatareader.GetInt32(5));
            retKurs.Art = retKurs.IstKurs ? "PUT" : "PUK";

            if (retKurs.IstKurs)
            {
                retKurs.Art = retKurs.Bezeichnung.Substring((retKurs.Bezeichnung.Length - 3), 3);
            }
        }

        return retKurs;
    }

    /// <summary>
    /// gibt alle Kursbezeichnungen zurück
    /// </summary>
    public async Task<ReadOnlyCollection<string>> GetKursBezListe()
    {
        List<string> klist = new();
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT bez FROM kurse;";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            klist.Add(sqliteDatareader.GetString(0));
        }

        return new ReadOnlyCollection<string>(klist);
    }

    /// <summary>
    /// gibt den vollständigen Inhalt der Tabelle kurse in der Reihenfolge Bezeichnung, Fach, Klasse, Stufe, Kurssuffix und istKurs zurück
    /// </summary>
    public async Task<ReadOnlyCollection<Kurs>> GetKursListe()
    {
        List<Kurs> kliste = new();
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT bez,fach,klasse,stufe,suffix,istkurs FROM kurse;";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            Kurs retKurs = new()
            {
                Bezeichnung = sqliteDatareader.GetString(0),
                Fach = sqliteDatareader.GetString(1),
                Klasse = sqliteDatareader.GetString(2),
                Stufe = sqliteDatareader.GetString(3),
                Suffix = sqliteDatareader.GetString(4),
                IstKurs = Convert.ToBoolean(sqliteDatareader.GetInt32(5))
            };
            retKurs.Art = retKurs.IstKurs ? "PUT" : "PUK";

            kliste.Add(retKurs);
        }

        return new ReadOnlyCollection<Kurs>(kliste);
    }

    /// <summary>
    /// gibt das globale Kurssuffix zurück
    /// </summary>
    /// <returns>String Kurssuffix</returns>
    private async Task<string> GetKursSuffix()
    {
        return GetSettings().Result.Kurssuffix;
    }

    /// <summary>
    /// gibt die Kurse der Lehrperson als Liste zurück
    /// </summary>
    /// <param name="lulid"></param>
    /// <returns>String-Liste der Kursbezeichnungen </returns>
    public async Task<ReadOnlyCollection<Kurs>> GetKursVonLuL(int lulid)
    {
        List<Kurs> kliste = new();
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT DISTINCT kursbez FROM unterrichtet WHERE lehrerid = $lulid;";
        sqliteCmd.Parameters.AddWithValue("$lulid", lulid);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            kliste.Add(await GetKurs(sqliteDatareader.GetString(0)));
        }

        return new ReadOnlyCollection<Kurs>(kliste);
    }

    /// <summary>
    /// gibt die Kurse des Schülers/der Schülerin als Liste zurück
    /// </summary>
    /// <param name="susid"></param>
    /// <returns>String-Liste der Kursbezeichnungen </returns>
    public async Task<ReadOnlyCollection<Kurs>> GetKursVonSuS(int susid)
    {
        List<Kurs> kliste = new();
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT kursbez FROM nimmtteil WHERE schuelerid = $susid;";
        sqliteCmd.Parameters.AddWithValue("$susid", susid);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            kliste.Add(await GetKurs(sqliteDatareader.GetString(0)));
        }

        return new ReadOnlyCollection<Kurs>(kliste);
    }

    /// <summary>
    /// gibt die Informationen ID, Nachname, Vorname, Mail, Kürzel und Fakultas der Lehrkraft zur übergebenen ID zurück
    /// </summary>
    /// <param name="id"></param>
    private async Task<LuL> GetLehrkraft(int id)
    {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT id,nachname,vorname,mail,kuerzel,fakultas,pwtemp,favo,sfavo FROM lehrkraft WHERE id = $id;";
        sqliteCmd.Parameters.AddWithValue("$id", id);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        LuL lehrkraft = new();
        while (sqliteDatareader.Read())
        {
            lehrkraft.ID = sqliteDatareader.GetInt32(0);
            lehrkraft.Nachname = sqliteDatareader.GetString(1);
            lehrkraft.Vorname = sqliteDatareader.GetString(2);
            lehrkraft.Mail = sqliteDatareader.GetString(3);
            lehrkraft.Kuerzel = sqliteDatareader.GetString(4);
            lehrkraft.Fakultas = sqliteDatareader.GetString(5);
            lehrkraft.Pwttemp = sqliteDatareader.GetString(6);
            lehrkraft.Favo = sqliteDatareader.GetString(7);
            lehrkraft.SFavo = sqliteDatareader.GetString(8);
        }

        return lehrkraft;
    }

    /// <summary>
    /// gibt die Informationen ID, Nachname, Vorname, Mail, Kürzel und Fakultas der Lehrkraft zum übergebenen Kürzel zurück
    /// </summary>
    /// <param name="kuerzel"></param>
    public async Task<LuL> GetLehrkraft(string kuerzel)
    {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT id,nachname,vorname,mail,kuerzel,fakultas,pwtemp,favo, sfavo FROM lehrkraft WHERE kuerzel = $kuerzel;";
        sqliteCmd.Parameters.AddWithValue("$kuerzel", kuerzel);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        LuL lehrkraft = new();
        while (sqliteDatareader.Read())
        {
            lehrkraft.ID = sqliteDatareader.GetInt32(0);
            lehrkraft.Nachname = sqliteDatareader.GetString(1);
            lehrkraft.Vorname = sqliteDatareader.GetString(2);
            lehrkraft.Mail = sqliteDatareader.GetString(3);
            lehrkraft.Kuerzel = sqliteDatareader.GetString(4);
            lehrkraft.Fakultas = sqliteDatareader.GetString(5);
            lehrkraft.Pwttemp = sqliteDatareader.GetString(6);
            lehrkraft.Favo = sqliteDatareader.GetString(7);
            lehrkraft.SFavo = sqliteDatareader.GetString(8);
        }

        return lehrkraft;
    }

    /// <summary>
    /// gibt die IDs aller Lehrkräfte zurück
    /// </summary>
    public async Task<ReadOnlyCollection<int>> GetLehrerIDListe()
    {
        List<int> llist = new();
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT id FROM lehrkraft;";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            llist.Add(sqliteDatareader.GetInt32(0));
        }

        return new ReadOnlyCollection<int>(llist);
    }

    /// <summary>
    /// gibt den vollständigen Inhalt der Tabelle lehrer in der Reihenfolge ID, Nachname, Vorname, Mail, Kürzel und Fakultas zurück
    /// </summary>
    public async Task<ReadOnlyCollection<LuL>> GetLehrerListe()
    {
        List<LuL> llist = new();
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT id,nachname,vorname,mail,kuerzel,fakultas,pwtemp,favo,sfavo FROM lehrkraft;";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            LuL lehrkraft = new()
            {
                ID = sqliteDatareader.GetInt32(0),
                Nachname = sqliteDatareader.GetString(1),
                Vorname = sqliteDatareader.GetString(2),
                Mail = sqliteDatareader.GetString(3),
                Kuerzel = sqliteDatareader.GetString(4),
                Fakultas = sqliteDatareader.GetString(5),
                Pwttemp = sqliteDatareader.GetString(6),
                Favo = sqliteDatareader.GetString(7),
                SFavo = sqliteDatareader.GetString(8),
            };
            llist.Add(lehrkraft);
        }

        return new ReadOnlyCollection<LuL>(llist);
    }

    /// <summary>
    /// gibt die Log-Meldungen zurück
    /// </summary>
    /// <returns>String-Liste der Nachrichten </returns>
    public async Task<ReadOnlyCollection<string>> GetLog()
    {
        List<string> log = new();
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT stufe,datum, nachricht FROM log;";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            var returnstr = "";
            for (var i = 0; i < sqliteDatareader.FieldCount; i++)
            {
                returnstr += sqliteDatareader.GetString(i) + "\t";
            }

            log.Add(returnstr);
        }

        return new ReadOnlyCollection<string>(log);
    }

    /// <summary>
    /// gibt die Log-Meldungen zurück
    /// </summary>
    /// <param name="stufe">Das Log-Level (Info, Hinweis oder Fehler)</param>
    /// <returns>String-Liste der Nachrichten </returns>
    public async Task<ReadOnlyCollection<string>> GetLog(string stufe)
    {
        List<string> log = new();
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT stufe,datum, nachricht FROM log WHERE stufe = $stufe;";
        sqliteCmd.Parameters.AddWithValue("$stufe", stufe);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            var returnstr = "";
            for (var i = 0; i < sqliteDatareader.FieldCount; i++)
            {
                returnstr += sqliteDatareader.GetString(i) + "\t";
            }

            log.Add(returnstr);
        }

        return new ReadOnlyCollection<string>(log);
    }

    /// <summary>
    /// gibt den Langnamen zur übergebenen Kurzform des Faches zurück
    /// </summary>
    /// <param name="shortsubject"></param>
    /// <returns>String Langfach-Bezeichnung</returns>
    private async Task<string> GetLangeFachbezeichnung(string shortsubject)
    {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT langfach FROM fachersatz WHERE kurzfach = $shortsubject;";
        sqliteCmd.Parameters.AddWithValue("$shortsubject", shortsubject);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            return sqliteDatareader.GetString(0);
        }

        return shortsubject;
    }

    /// <summary>
    /// gibt die IDs der LuL in des Kurses als Liste zurück
    /// </summary>
    /// <param name="kbez"></param>
    /// <returns>Interger-Liste der LuL des Kurses</returns>
    public async Task<ReadOnlyCollection<LuL>> GetLuLAusKurs(string kbez)
    {
        List<LuL> lliste = new();
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT lehrerid FROM unterrichtet WHERE kursbez = $kbez;";
        sqliteCmd.Parameters.AddWithValue("$kbez", kbez);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            lliste.Add(await GetLehrkraft(sqliteDatareader.GetInt32(0)));
        }

        return new ReadOnlyCollection<LuL>(lliste);
    }

    /// <summary>
    /// gibt die LuL des Schülers/der Schülerin zurück
    /// </summary>
    /// <param name="susid"></param>
    /// <returns>Interger-Liste der LuL-IDs</returns>
    public async Task<ReadOnlyCollection<LuL>> GetLuLvonSuS(int susid)
    {
        List<LuL> lliste = new();
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT unterrichtet.lehrerid FROM unterrichtet JOIN nimmtteil ON nimmtteil.kursbez = unterrichtet.kursbez WHERE schuelerid = $susid;";
        sqliteCmd.Parameters.AddWithValue("$susid", susid);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            for (var i = 0; i < sqliteDatareader.FieldCount; i++)
            {
                lliste.Add(await GetLehrkraft(sqliteDatareader.GetInt32(0)));
            }
        }

        return new ReadOnlyCollection<LuL>(lliste);
    }

    /// <summary>
    /// gibt die LuL der Stufe zurück
    /// </summary>
    /// <param name="stufe"></param>
    /// <returns>Interger-Liste der LuL-IDs</returns>
    public async Task<ReadOnlyCollection<LuL>> GetLuLAusStufe(string stufe)
    {
        List<LuL> lliste = new();
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT DISTINCT unterrichtet.lehrerid FROM unterrichtet WHERE kursbez LIKE $stufe;";
        sqliteCmd.Parameters.AddWithValue("$stufe", stufe + "%");
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            for (var i = 0; i < sqliteDatareader.FieldCount; i++)
            {
                lliste.Add(await GetLehrkraft(sqliteDatareader.GetInt32(0)));
            }
        }

        return new ReadOnlyCollection<LuL>(lliste);
    }

    /// <summary>
    /// gibt die Informationen ID, Nachname, Vorname, Mail, Klasse, Nutzername, aixmail, zweitaccount(1/0) und zweitmailadresse des Schülers/Schülerin zur übergebenen ID zurück
    /// </summary>
    /// <param name="id"></param>
    public async Task<SuS> GetSchueler(int id)
    {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT id,nachname,vorname,mail,klasse,nutzername,aixmail,zweitaccount,zweitmail FROM schueler WHERE id = $id;";
        sqliteCmd.Parameters.AddWithValue("$id", id);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        SuS schuelerin = new();
        while (sqliteDatareader.Read())
        {
            schuelerin.ID = sqliteDatareader.GetInt32(0);
            schuelerin.Nachname = sqliteDatareader.GetString(1);
            schuelerin.Vorname = sqliteDatareader.GetString(2);
            schuelerin.Mail = sqliteDatareader.GetString(3);
            schuelerin.Klasse = sqliteDatareader.GetString(4);
            schuelerin.Nutzername = sqliteDatareader.GetString(5);
            schuelerin.Aixmail = sqliteDatareader.GetString(6);
            schuelerin.Zweitaccount = Convert.ToBoolean(sqliteDatareader.GetInt32(7));
            schuelerin.Zweitmail = sqliteDatareader.GetString(8);
        }

        return schuelerin;
    }

    /// <summary>
    /// gibt die Informationen ID, Nachname, Vorname, Mail, Klasse und Nutzername des Schülers/Schülerin zur übergebenen Kombination aus Vor- und Nachname zurück
    /// </summary>
    /// <param name="vorname"></param>
    /// <param name="nachname"></param>
    private async Task<List<SuS>> GetSchueler(string vorname, string nachname)
    {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT id,nachname,vorname,mail,klasse,nutzername,aixmail,zweitaccount,zweitmail FROM schueler WHERE vorname = $vorname AND nachname = $nachname;";
        sqliteCmd.Parameters.AddWithValue("$vorname", vorname);
        sqliteCmd.Parameters.AddWithValue("$nachname", nachname);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        var susliste = new List<SuS>();
        while (sqliteDatareader.Read())
        {
            SuS schuelerin = new()
            {
                ID = sqliteDatareader.GetInt32(0),
                Nachname = sqliteDatareader.GetString(1),
                Vorname = sqliteDatareader.GetString(2),
                Mail = sqliteDatareader.GetString(3),
                Klasse = sqliteDatareader.GetString(4),
                Nutzername = sqliteDatareader.GetString(5),
                Aixmail = sqliteDatareader.GetString(6),
                Zweitaccount = Convert.ToBoolean(sqliteDatareader.GetInt32(7)),
                Zweitmail = sqliteDatareader.GetString(8)
            };
            susliste.Add(schuelerin);
        }

        return susliste;
    }

    /// <summary>
    /// gibt die IDs aller SchülerInnen zurück
    /// </summary>
    public async Task<ReadOnlyCollection<int>> GetSchuelerIDListe()
    {
        List<int> slist = new();
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT id FROM schueler;";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            slist.Add(sqliteDatareader.GetInt32(0));
        }

        return new ReadOnlyCollection<int>(slist);
    }

    /// <summary>
    /// gibt den vollständigen Inhalt der Tabelle schueler in der Reihenfolge ID, Nachname, Vorname, Mail, Klasse, Nutzername, AIXmail, zweitaccount(1/0) und zweitmailadresse zurück
    /// </summary>
    public async Task<ReadOnlyCollection<SuS>> GetSchuelerListe()
    {
        List<SuS> slist = new();
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT id,nachname,vorname,mail,klasse,nutzername,aixmail,zweitaccount,zweitmail FROM schueler;";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            SuS schuelerin = new()
            {
                ID = sqliteDatareader.GetInt32(0),
                Nachname = sqliteDatareader.GetString(1),
                Vorname = sqliteDatareader.GetString(2),
                Mail = sqliteDatareader.GetString(3),
                Klasse = sqliteDatareader.GetString(4),
                Nutzername = sqliteDatareader.GetString(5),
                Aixmail = sqliteDatareader.GetString(6),
                Zweitaccount = Convert.ToBoolean(sqliteDatareader.GetInt32(7)),
                Zweitmail = sqliteDatareader.GetString(8)
            };
            slist.Add(schuelerin);
        }

        return new ReadOnlyCollection<SuS>(slist);
    }

    /// <summary>
    /// gibt die schulspezifischen Einstellungen der Datenbank als Liste zurück
    /// </summary>
    public async Task<Settings> GetSettings()
    {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT setting,value FROM settings;";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        Settings settingsResult = new();
        while (sqliteDatareader.Read())
        {
            var key = sqliteDatareader.GetString(0);
            var value = string.IsNullOrEmpty(sqliteDatareader.GetString(1)) ? "" : sqliteDatareader.GetString(1);
            switch (key)
            {
                case "mailsuffix":
                    settingsResult.Mailsuffix = value;
                    break;
                case "kurssuffix":
                    settingsResult.Kurssuffix = value;
                    break;
                case "fachersatz":
                    settingsResult.Fachersetzung = value;
                    break;
                case "erprobungsstufenleitung":
                    settingsResult.Erprobungstufenleitung = value;
                    break;
                case "mittelstufenleitung":
                    settingsResult.Mittelstufenleitung = value;
                    break;
                case "efstufenleitung":
                    settingsResult.EFStufenleitung = value;
                    break;
                case "q1stufenleitung":
                    settingsResult.Q1Stufenleitung = value;
                    break;
                case "q2stufenleitung":
                    settingsResult.Q2Stufenleitung = value;
                    break;
                case "oberstufenkoordination":
                    settingsResult.Oberstufenkoordination = value;
                    break;
            }
        }

        List<string> flist = new();
        sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT kurzfach,langfach FROM fachersatz;";
        sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            var returnstr = "";
            for (var i = 0; i < sqliteDatareader.FieldCount; i++)
            {
                returnstr += sqliteDatareader.GetString(i) + ";";
            }

            flist.Add(returnstr);
        }

        var fachk = new List<string>();
        var fachl = new List<string>();
        foreach (var faecher in flist)
        {
            fachk.Add(faecher.Split(';')[0]);
            fachl.Add(faecher.Split(';')[1]);
        }

        settingsResult.Kurzfaecher = fachk.ToArray();
        settingsResult.Langfaecher = fachl.ToArray();
        settingsResult.Version = version;
        return settingsResult;
    }

    /// <summary>
    /// gibt die Statistik als fertigen String zurück
    /// </summary>
    public async Task<string> GetStat()
    {
        var result = "Kurse: " + GetKursListe().Result.Count + "; Lehrer:Innen: " +
                     GetLehrerListe().Result.Count + "; Schüler:Innen: " + GetSchuelerListe().Result.Count;
        return result;
    }

    private async Task<List<LuL>> getOberstufenleitung(string stufe)
    {
        if (string.IsNullOrEmpty(stufe) || (stufe != "EF" && stufe != "Q1" && stufe != "Q2"))
            return new List<LuL>();
        List<LuL> luls = new();
        switch (stufe)
        {
            case "EF":
                luls.AddRange(GetSettings().Result.EFStufenleitung.Split(',')
                    .Select(krz => GetLehrkraft(krz).Result));
                return luls;
            case "Q1":
                luls.AddRange(GetSettings().Result.Q1Stufenleitung.Split(',')
                    .Select(krz => GetLehrkraft(krz).Result));
                return luls;
            case "Q2":
                luls.AddRange(GetSettings().Result.Q2Stufenleitung.Split(',')
                    .Select(krz => GetLehrkraft(krz).Result));
                return luls;
            default:
                return new List<LuL>();
        }
    }

    /// <summary>
    /// gibt die IDs der SuS in der Klasse als Liste zurück
    /// </summary>
    /// <param name="klasse"></param>
    /// <returns>Integer-Liste der SuS-IDs</returns>
    public async Task<ReadOnlyCollection<SuS>> GetSuSAusKlasse(string klasse)
    {
        List<SuS> sliste = new();
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT id FROM schueler WHERE klasse = $klasse;";
        sqliteCmd.Parameters.AddWithValue("$klasse", klasse);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            sliste.Add(await GetSchueler(sqliteDatareader.GetInt32(0)));
        }

        return new ReadOnlyCollection<SuS>(sliste);
    }

    /// <summary>
    /// gibt die IDs der SchülerInnen des Kurses als Liste zurück
    /// </summary>
    /// <param name="kbez"></param>
    /// <returns>Integer-Liste der SuS-IDs</returns>
    public async Task<ReadOnlyCollection<SuS>> GetSuSAusKurs(string kbez)
    {
        List<SuS> sliste = new();
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT schuelerid FROM nimmtteil WHERE kursbez = $kbez;";
        sqliteCmd.Parameters.AddWithValue("$kbez", kbez);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            for (var i = 0; i < sqliteDatareader.FieldCount; i++)
            {
                sliste.Add(await GetSchueler(sqliteDatareader.GetInt32(0)));
            }
        }

        return new ReadOnlyCollection<SuS>(sliste);
    }

    /// <summary>
    /// gibt alle SuS der Stufe zurück
    /// </summary>
    /// <param name="stufe"></param>
    /// <returns>Integer-Liste der Schüler der Stufe</returns>
    public async Task<ReadOnlyCollection<SuS>> GetSusAusStufe(string stufe)
    {
        List<SuS> sliste = new();
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT DISTINCT id FROM schueler WHERE klasse LIKE $stufe;";
        sqliteCmd.Parameters.AddWithValue("$stufe", stufe + "%");
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            sliste.Add(await GetSchueler(sqliteDatareader.GetInt32(0)));
        }

        return new ReadOnlyCollection<SuS>(sliste);
    }

    /// <summary>
    /// gibt die SuS der Lehrperson zurück
    /// </summary>
    /// <param name="lulid"></param>
    /// <returns>Interger-Liste der SuS-IDs</returns>
    public async Task<ReadOnlyCollection<SuS>> GetSuSVonLuL(int lulid)
    {
        List<SuS> sliste = new();
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT nimmtteil.schuelerid FROM unterrichtet JOIN nimmtteil ON nimmtteil.kursbez = unterrichtet.kursbez WHERE lehrerid = $lulid;";
        sqliteCmd.Parameters.AddWithValue("$lulid", lulid);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            sliste.Add(await GetSchueler(sqliteDatareader.GetInt32(0)));
        }

        return new ReadOnlyCollection<SuS>(sliste);
    }


    /// <summary>
    /// gibt true zurück wenn es den SuS mit der id gibt
    /// </summary>
    /// <param name="sid"></param>
    /// <returns>boolean</returns>
    public async Task<bool> GibtEsSchueler(int sid)
    {
        return GetSchueler(sid).Result.ID > 0;
    }

    /// <summary>
    /// gibt true zurück wenn es die Lehrkraft mit der id gibt
    /// </summary>
    /// <param name="lid"></param>
    /// <returns></returns>
    public async Task<bool> GibtEsLehrkraft(int lid)
    {
        return GetLehrkraft(lid).Result.ID > 0;
    }

    /// <summary>
    /// gibt true zurück wenn es den Kurs mit der Bezeichnung gibt
    /// </summary>
    /// <param name="kbez"></param>
    /// <returns></returns>
    public async Task<bool> GibtEsKurs(string kbez)
    {
        return GetKurs(kbez).Result.Bezeichnung == kbez;
    }

    /// <summary>
    /// liest die Nutzernamen und AIX-Mailadressen für SuS aus einem Suite-Export ein und updated diese (inkrementell oder gesamt)
    /// </summary>
    /// <param name="idfile"></param>
    public async Task IdsEinlesen(string idfile)
    {
        var lines = await File.ReadAllLinesAsync(idfile);
        int ina = -1, inid = -1, imail = -1;
        var header = lines[0].Split(';');
        for (var i = 0; i < header.Length; i++)
        {
            if (header[i].Equals("Referenz-Id"))
            {
                inid = i;
            }

            if (header[i].Equals("Anmeldename"))
            {
                ina = i;
            }

            if (header[i].Equals("E-Mail"))
            {
                imail = i;
            }
        }

        await StartTransaction();
        for (var i = 1; i < lines.Length; i++)
        {
            try
            {
                var line = lines[i].Split(';');
                if (line[inid] == "") continue;
                try
                {
                    var sus = await GetSchueler(Convert.ToInt32(line[inid]));
                    var id = Convert.ToInt32(sus.ID);
                    UpdateSchuelerNutzername(id, line[ina]);
                    UpdateAIXSuSAdressen(id, line[imail]);
                }
                catch (Exception e)
                {
                    await AddLogMessage("Debug", e.StackTrace + ";" + e.Message);
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                await AddLogMessage("Debug", ex.StackTrace + ";" + ex.Message);
#endif
                await AddLogMessage("Error", "Fehler beim Einlesen der IDs");
            }
        }

        await StopTransaction();
    }

    /// <summary>
    /// liest die Kurse aus der übergebenen Datei ein (inkrementell oder gesamt)
    /// </summary>
    /// <param name="kursfile"></param>
    public async Task KurseEinlesen(string kursfile)
    {
        var fachersetzung = await GetFachersatz();
        var fachsuffix = await GetKursSuffix();
        var lines = await File.ReadAllLinesAsync(kursfile);
        int inv = -1, inn = -1, inf = -1, inl = -1, inka = -1, ink = -1;
        var header = lines[0].Split('|');
        for (var i = 0; i < header.Length; i++)
        {
            if (header[i].Equals("Vorname"))
            {
                inv = i;
            }

            if (header[i].Equals("Nachname"))
            {
                inn = i;
            }

            if (header[i].Equals("Fach"))
            {
                inf = i;
            }

            if (header[i].Equals("Fachlehrer"))
            {
                inl = i;
            }

            if (header[i].Equals("Kursart"))
            {
                inka = i;
            }

            if (header[i].Equals("Kurs"))
            {
                ink = i;
            }
        }

        await StartTransaction();
        for (var i = 1; i < lines.Length; i++)
        {
            try
            {
                var tmpkurs = lines[i].Split('|');
                for (var j = 0; j < tmpkurs.Length; j++)
                {
                    tmpkurs[j] = tmpkurs[j].Trim('"');
                }

                var krz = tmpkurs[inl];
                string nachname;
                var kursklasse = "";
                if (tmpkurs[inn].Contains('#'))
                {
                    nachname = tmpkurs[inn].Split('#')[0];
                    kursklasse = tmpkurs[inn].Split('#')[1];
                }
                else
                {
                    nachname = tmpkurs[inn];
                }

                var susliste = await GetSchueler(tmpkurs[inv].Replace("'", ""), nachname.Replace("'", ""));
                var ltmp = await GetLehrkraft(krz);
                foreach (var stmp in susliste)
                {
                    if (stmp.ID > 50000 && ltmp.ID > 0)
                    {
                        var klasse = stmp.Klasse;
                        if (klasse != kursklasse && kursklasse != "")
                        {
                            continue;
                        }

                        var stufe = klasse[..2];
                        if (!(stufe.Equals("EF") || stufe.Equals("Q1") || stufe.Equals("Q2")))
                        {
                            if (!stufe.Equals("10"))
                            {
                                stufe = klasse[..1];
                            }

                            //Klassenkurse
                            var klkurs = klasse + "KL";
                            if (string.IsNullOrEmpty(GetKurs(klkurs).Result.Bezeichnung)) //Kurs nicht existent
                            {
                                await AddKurs(klkurs, "KL", klasse, stufe, fachsuffix, 0);
                            }

                            await AddStoK(Convert.ToInt32(stmp.ID), klkurs);
                            await AddLtoK(Convert.ToInt32(ltmp.ID), klkurs);
                        }
                        else
                        {
                            klasse = stufe;
                        }

                        var kursart = tmpkurs[inka];
                        if (kursart != "")
                        {
                            string fach;
                            if (!kursart.Equals("PUK") &&
                                !kursart.Equals("ZUV") &&
                                //ToDo: Auf Feedback von Schild warten
                                !kursart.Equals("WPII")) //PUK = Klassenunterricht; ZUV = Zusatzveranstaltung
                            {
                                fach = tmpkurs[inf];
                                for (var k = 0; k < fachersetzung.Count - 1; k++)
                                {
                                    if (fach.Equals(fachersetzung[k].Split(':')[0]))
                                    {
                                        fach = fachersetzung[k].Split(':')[1];
                                    }
                                }

                                //ToDo: Auf Feedback von Schild warten
                                var bez = stufe + "-" + (string.IsNullOrEmpty(tmpkurs[ink])
                                    ? fach + "-" + klasse
                                    : tmpkurs[ink]);
                                if (string.IsNullOrEmpty(GetKurs(bez).Result.Bezeichnung))
                                {
                                    await AddKurs(bez, fach, stufe, stufe, fachsuffix, 1);
                                }

                                await AddStoK(Convert.ToInt32(stmp.ID), bez);
                                await AddLtoK(Convert.ToInt32(ltmp.ID), bez);
                            }
                            else
                            {
                                fach = tmpkurs[inf];
                                for (var k = 0; k < fachersetzung.Count - 1; k++)
                                {
                                    if (fach.Equals(fachersetzung[k].Split(':')[0]))
                                    {
                                        fach = fachersetzung[k].Split(':')[1];
                                    }
                                }

                                var bez = klasse + fach;
                                if (string.IsNullOrEmpty(GetKurs(bez).Result.Bezeichnung))
                                {
                                    await AddKurs(bez, fach, klasse, stufe, fachsuffix, 0);
                                }

                                await AddStoK(Convert.ToInt32(stmp.ID), bez);
                                await AddLtoK(Convert.ToInt32(ltmp.ID), bez);
                            }
                        }
                        else
                        {
                            await AddLogMessage("Fehler",
                                "SuS" + stmp.ID + ":" + stmp.Nachname + "," + stmp.Vorname + " aus " + stmp.Klasse +
                                " hat invalide Kurs-Art");
                        }
                    }
                    else
                    {
                        await AddLogMessage("Hinweis",
                            "LehrerIn\t" + krz + " oder SchülerIn " + stmp.ID + " " + tmpkurs[inv] + " " +
                            tmpkurs[inn] + "\tunbekannt");
                    }
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                await AddLogMessage("Debug", ex.StackTrace + ";" + ex.Message + "at " + i);
#endif
                await AddLogMessage("Error", "Fehler beim Einlesen der Kurse");
                await StopTransaction();
                return;
            }
        }

        await StopTransaction();
    }

    /// <summary>
    /// löscht das komplette Log
    /// </summary>
    public async Task LoescheLog()
    {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "DELETE FROM log WHERE stufe = 'Info' OR stufe = 'Hinweis' OR stufe = 'Fehler';";
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// löscht das Log für die übergebene Stufe
    /// </summary>
    /// <param name="stufe">"Fehler", "Hinweis", oder "Info"</param>
    public async Task LoescheLog(string stufe)
    {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "DELETE FROM log WHERE stufe = $stufe;";
        sqliteCmd.Parameters.AddWithValue("$stufe", stufe);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// liest die Lehrkräfte aus der übergebenen Datei ein (inkrementell oder gesamt)
    /// </summary>
    /// <param name="lulfile"></param>
    public async Task LulEinlesen(string lulfile)
    {
        int inv = -1, inn = -1, ini = -1, inkrz = -1, infak = -1, inm = -1;
        var lines = await File.ReadAllLinesAsync(lulfile);
        var header = lines[0].Split(';');
        for (var i = 0; i < header.Length; i++)
        {
            if (header[i].Equals("firstname"))
            {
                inv = i;
            }

            if (header[i].Equals("lastname"))
            {
                inn = i;
            }

            if (header[i].Equals("idnumber"))
            {
                ini = i;
            }

            if (header[i].Equals("username"))
            {
                inkrz = i;
            }

            if (header[i].Equals("fakultas"))
            {
                infak = i;
            }

            if (header[i].Contains("email"))
            {
                inm = i;
            }
        }

        for (var i = 1; i < lines.Length; i++)
        {
            await StartTransaction();
            try
            {
                if (lines[i] != "")
                {
                    var tmpkuk = lines[i].Split(';');
                    for (var j = 0; j < tmpkuk.Length; j++)
                    {
                        tmpkuk[j] = tmpkuk[j].Trim('"');
                    }

                    await Addlehrkraft(Convert.ToInt32(tmpkuk[ini]), tmpkuk[inv], tmpkuk[inn],
                        tmpkuk[inkrz].ToUpper(), tmpkuk[inm], tmpkuk[infak].TrimEnd(';'), "", "");
                    await AddLogMessage("Info",
                        "LehrerIn\t" + tmpkuk[inn] + "\t" + tmpkuk[inv] + "\t" + tmpkuk[inm] + "\t angelegt");
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                await AddLogMessage("Debug", ex.StackTrace + ";" + ex.Message);
                // Debug.WriteLine("Zeile " + i + ": " + lines[i]);
#endif
            }

            await StopTransaction();
        }
    }

    /// <summary>
    /// löscht den angegeben Kurs und alle dazugehörigen Einträge
    /// </summary>
    /// <param name="kbez"></param>
    public async Task RemoveK(string kbez)
    {
        if (string.IsNullOrEmpty(kbez)) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "DELETE FROM nimmtteil WHERE kursbez = $kbez;";
        sqliteCmd.Parameters.AddWithValue("$kbez", kbez);
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.Parameters.Clear();
        sqliteCmd.CommandText = "DELETE FROM unterrichtet WHERE kursbez = $kbez;";
        sqliteCmd.Parameters.AddWithValue("$kbez", kbez);
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.Parameters.Clear();
        sqliteCmd.CommandText = "DELETE FROM kurse WHERE bez =$kbez;";
        sqliteCmd.Parameters.AddWithValue("$kbez", kbez);
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.Parameters.Clear();
        await AddLogMessage("Info", "Kurs mit der Bezeichnung " + kbez + " gelöscht");
    }

    /// <summary>
    /// löscht den angegeben Kurs und alle dazugehörigen Einträge
    /// </summary>
    /// <param name="kurs"></param>
    /// <returns></returns>
    public async Task RemoveK(Kurs kurs)
    {
        await RemoveK(kurs.Bezeichnung);
    }

    /// <summary>
    /// löscht die angegebene Lehrperson und die Kurszuordnungen
    /// </summary>
    /// <param name="lid"></param>
    public async Task RemoveL(int lid)
    {
        if (lid <= 0) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "DELETE FROM unterrichtet WHERE lehrerid = $lid;";
        sqliteCmd.Parameters.AddWithValue("$lid", lid);
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.Parameters.Clear();
        sqliteCmd.CommandText = "DELETE FROM lehrkraft INDEXED BY lindex WHERE id = $lid;";
        sqliteCmd.Parameters.AddWithValue("$lid", lid);
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.Parameters.Clear();
        await AddLogMessage("Info", "Lehrkraft mit der ID " + lid + " gelöscht");
    }

    /// <summary>
    /// löscht die angegebene Lehrperson und die Kurszuordnungen
    /// </summary>
    /// <param name="lehrkraft"></param>
    /// <returns></returns>
    private async Task RemoveL(LuL lehrkraft)
    {
        await RemoveL(lehrkraft.ID);
    }

    /// <summary>
    /// löscht die angegebene Lehrperson und die Kurszuordnungen
    /// </summary>
    /// <param name="kuerzel"></param>
    /// <returns></returns>
    public async Task RemoveL(string kuerzel)
    {
        await RemoveL(GetLehrkraft(kuerzel).Result);
    }

    /// <summary>
    /// löscht die angegebene Lehrperson aus dem angegeben Kurs
    /// </summary>
    /// <param name="lid"></param>
    /// <param name="kbez"></param>
    public async Task RemoveLfromK(int lid, string kbez)
    {
        if (lid <= 0) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "DELETE FROM unterrichtet WHERE lehrerid = $lid AND kursbez = $kbez;";
        sqliteCmd.Parameters.AddWithValue("$lid", lid);
        sqliteCmd.Parameters.AddWithValue("$kbez", kbez);
        sqliteCmd.ExecuteNonQuery();
        await AddLogMessage("Info", "Lehrkraft mit der ID " + lid + " aus Kurs " + kbez + " gelöscht");
    }

    /// <summary>
    /// löscht die angegebene Lehrperson aus dem angegeben Kurs
    /// </summary>
    /// <param name="lehrkraft"></param>
    /// <param name="kurs"></param>
    /// <returns></returns>
    public async Task RemoveLfromK(LuL lehrkraft, Kurs kurs)
    {
        await RemoveLfromK(lehrkraft.ID, kurs.Bezeichnung);
    }

    /// <summary>
    /// löscht den/die angegebene Schüler/Schülerin und die Kurszuordnungen
    /// </summary>
    /// <param name="sid"></param>
    public async Task RemoveS(int sid)
    {
        if (sid <= 0) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "DELETE FROM nimmtteil WHERE schuelerid = $sid;";
        sqliteCmd.Parameters.AddWithValue("$sid", sid);
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.Parameters.Clear();
        sqliteCmd.CommandText = "DELETE FROM schueler WHERE id = $sid;";
        sqliteCmd.Parameters.AddWithValue("$sid", sid);
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.Parameters.Clear();
        await AddLogMessage("Info", "SuS mit der ID " + sid + " gelöscht");
    }

    /// <summary>
    /// löscht den/die angegebene Schüler/Schülerin und die Kurszuordnungen
    /// </summary>
    /// <param name="schulerin"></param>
    /// <returns></returns>
    public async Task RemoveS(SuS schulerin)
    {
        await RemoveS(schulerin.ID);
    }

    /// <summary>
    /// löscht den/die angegebenen Schüler/Schülerin aus dem angegeben Kurs
    /// </summary>
    /// <param name="sid"></param>
    /// <param name="kbez"></param>
    public async Task RemoveSfromK(int sid, string kbez)
    {
        if (sid <= 0) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "DELETE FROM nimmtteil WHERE schuelerid = $sid AND kursbez = $kbez;";
        sqliteCmd.Parameters.AddWithValue("$sid", sid);
        sqliteCmd.Parameters.AddWithValue("$kbez", kbez);
        sqliteCmd.ExecuteNonQuery();
        await AddLogMessage("Info", "SuS mit der ID " + sid + " aus Kurs " + kbez + " gelöscht");
    }

    /// <summary>
    /// löscht den/die angegebenen Schüler/Schülerin aus dem angegeben Kurs
    /// </summary>
    /// <param name="schuelerin"></param>
    /// <param name="kurs"></param>
    /// <returns></returns>
    public async Task RemoveSfromK(SuS schuelerin, Kurs kurs)
    {
        await RemoveSfromK(schuelerin.ID, kurs.Bezeichnung);
    }

    /// <summary>
    /// setzt die Liste der Kurz- und Langschreibweisen der Fächer
    /// </summary>
    /// <param name="fachk"></param>
    /// <param name="fachl"></param>
    private async Task SetKurzLangFach(IReadOnlyList<string> fachk, IReadOnlyList<string> fachl)
    {
        if (fachk.Count == fachl.Count)
        {
            await StartTransaction();
            var sqliteCmd = _sqliteConn.CreateCommand();
            for (var i = 0; i < fachk.Count; i++)
            {
                if (fachl[i] == "" || fachk[i] == "") continue;
                var kurzesfach = fachk[i];
                var langesfach = fachl[i];
                sqliteCmd.CommandText =
                    "INSERT OR IGNORE INTO fachersatz (kurzfach, langfach) VALUES ($kfach, $lfach);";
                sqliteCmd.Parameters.AddWithValue("$kfach", kurzesfach);
                sqliteCmd.Parameters.AddWithValue("$lfach", langesfach);
                sqliteCmd.ExecuteNonQuery();
                sqliteCmd.Parameters.Clear();
            }

            await StopTransaction();
        }
    }

    /// <summary>
    /// setzt die Einstellungen der Schule in der Datenbank
    /// </summary>
    /// <param name="settings"></param>
    public async Task SetSettings(Settings settings)
    {
        var sqliteCmd = _sqliteConn.CreateCommand();
        // sqliteCmd.CommandText = "INSERT OR IGNORE INTO settings (mailsuffix, kurssuffix, fachersetzung) VALUES ($mailsuffix, $kurssuffix, $fachersatz);";
        sqliteCmd.Parameters.AddWithValue("$mailsuffixparam", settings.Mailsuffix);
        sqliteCmd.Parameters.AddWithValue("$kurssuffixparam", settings.Kurssuffix);
        sqliteCmd.Parameters.AddWithValue("$fachersatzparam", settings.Fachersetzung);

        sqliteCmd.Parameters.AddWithValue("$mailsuffix", "mailsuffix");
        sqliteCmd.Parameters.AddWithValue("$kurssuffix", "kurssuffix");
        sqliteCmd.Parameters.AddWithValue("$fachersatz", "fachersatz");

        sqliteCmd.Parameters.AddWithValue("$erprobungstufenleitung", "erprobungsstufenleitung");
        sqliteCmd.Parameters.AddWithValue("$mittelstufenleitung", "mittelstufenleitung");
        sqliteCmd.Parameters.AddWithValue("$efstufenleitung", "efstufenleitung");
        sqliteCmd.Parameters.AddWithValue("$q1stufenleitung", "q1stufenleitung");
        sqliteCmd.Parameters.AddWithValue("$q2stufenleitung", "q2stufenleitung");
        sqliteCmd.Parameters.AddWithValue("$oberstufenkoordination", "oberstufenkoordination");
        sqliteCmd.Parameters.AddWithValue("$version", "version");

        sqliteCmd.Parameters.AddWithValue("$erprobungstufenleitungparam",
            settings.Erprobungstufenleitung);
        sqliteCmd.Parameters.AddWithValue("$mittelstufenleitungparam", settings.Mittelstufenleitung);
        sqliteCmd.Parameters.AddWithValue("$efstufenleitungparam", settings.EFStufenleitung);
        sqliteCmd.Parameters.AddWithValue("$q1stufenleitungparam", settings.Q1Stufenleitung);
        sqliteCmd.Parameters.AddWithValue("$q2stufenleitungparam", settings.Q2Stufenleitung);
        sqliteCmd.Parameters.AddWithValue("$oberstufenkoordinationparam",
            settings.Oberstufenkoordination);
        sqliteCmd.Parameters.AddWithValue("versionparam", version);
        sqliteCmd.CommandText =
            "INSERT OR REPLACE INTO settings (setting,value) VALUES($mailsuffix, $mailsuffixparam)";
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.CommandText =
            "INSERT OR REPLACE INTO settings (setting,value) VALUES($kurssuffix, $kurssuffixparam)";
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.CommandText =
            "INSERT OR REPLACE INTO settings (setting,value) VALUES($fachersatz, $fachersatzparam)";
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.CommandText =
            "INSERT OR REPLACE INTO settings (setting,value) VALUES($erprobungstufenleitung, $erprobungstufenleitungparam)";
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.CommandText =
            "INSERT OR REPLACE INTO settings (setting,value) VALUES($mittelstufenleitung, $mittelstufenleitungparam)";
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.CommandText =
            "INSERT OR REPLACE INTO settings (setting,value) VALUES($efstufenleitung, $efstufenleitungparam)";
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.CommandText =
            "INSERT OR REPLACE INTO settings (setting,value) VALUES($q1stufenleitung, $q1stufenleitungparam)";
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.CommandText =
            "INSERT OR REPLACE INTO settings (setting,value) VALUES($q2stufenleitung, $q2stufenleitungparam)";
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.CommandText =
            "INSERT OR REPLACE INTO settings (setting,value) VALUES($oberstufenkoordination, $oberstufenkoordinationparam)";
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.CommandText =
            "INSERT OR REPLACE INTO settings (setting,value) VALUES($version, $versionparam)";
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.Parameters.Clear();
        await SetKurzLangFach(settings.Kurzfaecher, settings.Langfaecher);
    }

    /// <summary>
    /// start von SQLite-Transaktionen zur Beschleunigung von Inserts oder Updates
    /// </summary>
    public async Task StartTransaction()
    {
        _ActiveTransaction = true;
        _dbtrans = _sqliteConn.BeginTransaction();
    }

    /// <summary>
    /// ende von SQLite-Transaktionen zur Beschleunigung von Inserts oder Updates
    /// </summary>
    public async Task StopTransaction()
    {
        _ActiveTransaction = false;
        if (_dbtrans == null) return;
        await _dbtrans.CommitAsync();
    }

    /// <summary>
    /// liest die SchülerInnen aus der übergebenen Datei ein (inkrementell oder gesamt)
    /// </summary>
    /// <param name="susfile"></param>
    public async Task SusEinlesen(string susfile)
    {
        int inv = -1, inn = -1, ini = -1, ink = -1;
        List<int> inm = new();
        var lines = await File.ReadAllLinesAsync(susfile);
        var header = lines[0].Split(';');
        for (var i = 0; i < header.Length; i++)
        {
            header[i] = header[i].Trim('"');
            if (header[i].Equals("Vorname"))
            {
                inv = i;
            }

            if (header[i].Equals("Nachname"))
            {
                inn = i;
            }

            if (header[i].Equals("Interne ID-Nummer"))
            {
                ini = i;
            }

            if (header[i].Contains("E-Mail") && !header[i].Contains("schulisch"))
            {
                inm.Add(i);
            }

            if (header[i].Equals("Klasse"))
            {
                ink = i;
            }
        }

        for (var i = 1; i < lines.Length; i++)
        {
            try
            {
                var tmpsus = lines[i].Split(';');
                for (var j = 0; j < tmpsus.Length; j++)
                {
                    tmpsus[j] = tmpsus[j].Trim('"');
                }

                var settings = GetSettings().Result;
                var mail = tmpsus[ini] + settings.Mailsuffix;
                var maillist = (from idm in inm where !tmpsus[idm].Equals("") select tmpsus[idm]).ToList();

                if (maillist.Count > 0)
                {
                    mail = maillist[0];
                }
                else
                {
                    await AddLogMessage("Hinweis", "SuS\t" + tmpsus[ini] + "\tohne primäre Mailadresse");
                }

                maillist = maillist.Distinct().ToList();
                /*if (tmpsus[ink].StartsWith("EF"))
                {
                    tmpsus[ink] = "EF";
                }*/

                if (mail.Contains(';'))
                {
                    await AddLogMessage("Fehler", "Mailfehler");
                }

                maillist.Remove(mail);
                var mails = maillist.Aggregate("", (current, maileintrag) => current + (maileintrag + ","));
                mails = mails.TrimEnd(',');
                await AddSchuelerIn(Convert.ToInt32(tmpsus[ini]), tmpsus[inv].Replace("'", ""),
                    tmpsus[inn].Replace("'", ""), mail, tmpsus[ink], "", "", 0, mails);
                await AddLogMessage("Info",
                    "SchülerIn\t" + tmpsus[inn] + "\t" + tmpsus[inv] + "\t" + mail + "\t angelegt");
            }
            catch (Exception ex)
            {
#if DEBUG
                await AddLogMessage("Debug", ex.StackTrace + ";" + ex.Message);
                //Debug.WriteLine("Zeile " + i + ": " + lines[i]);
#endif
                await AddLogMessage("Error", "Fehler beim Einlesen der SuS");
            }
        }
    }

    /// <summary>
    /// setzt für den per Bezeichnung angegeben Kurs die Daten neu
    /// </summary>
    /// <param name="bez"></param>
    /// <param name="fach"></param>
    /// <param name="klasse"></param>
    /// <param name="stufe"></param>
    /// <param name="suffix"></param>
    /// <param name="istkurs"></param>
    public async Task UpdateKurs(string bez, string fach, string klasse, string stufe, string suffix, int istkurs)
    {
        if (string.IsNullOrEmpty(bez)) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "UPDATE kurse SET fach = $fach, klasse = $klasse, stufe = $stufe, suffix = $suffix, istkurs = $istkurs WHERE bez=$bez;";
        sqliteCmd.Parameters.AddWithValue("$fach", fach);
        sqliteCmd.Parameters.AddWithValue("$bez", bez);
        sqliteCmd.Parameters.AddWithValue("$klasse", klasse);
        sqliteCmd.Parameters.AddWithValue("$stufe", stufe);
        sqliteCmd.Parameters.AddWithValue("$suffix", suffix);
        sqliteCmd.Parameters.AddWithValue("$istkurs", istkurs);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// setzt für per ID angebenen Lehrperson die Daten neu
    /// </summary>
    /// <param name="id"></param>
    /// <param name="vorname"></param>
    /// <param name="nachname"></param>
    /// <param name="kuerzel"></param>
    /// <param name="mail"></param>
    /// <param name="fakultas"></param>
    /// <param name="pwtemp"></param>
    /// <param name="favo"></param>
    /// <param name="sfavo"></param>
    public async Task UpdateLehrkraft(int id, string vorname, string nachname, string kuerzel, string mail,
        string fakultas, string pwtemp, string favo, string sfavo)
    {
        if (string.IsNullOrEmpty(kuerzel) || id <= 0) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "UPDATE lehrkraft SET nachname=$nachname, vorname=$vorname, kuerzel= $kuerzel, mail=$mail, fakultas=$fakultas,pwtemp = $pwtemp, favo = $favo, sfavo=$sfavo WHERE id=$id;";
        sqliteCmd.Parameters.AddWithValue("$id", id);
        sqliteCmd.Parameters.AddWithValue("$vorname", vorname);
        sqliteCmd.Parameters.AddWithValue("$nachname", nachname);
        sqliteCmd.Parameters.AddWithValue("$kuerzel", kuerzel.ToUpper());
        sqliteCmd.Parameters.AddWithValue("$mail", mail);
        sqliteCmd.Parameters.AddWithValue("$fakultas", fakultas.TrimEnd(';'));
        sqliteCmd.Parameters.AddWithValue("$pwtemp", pwtemp);
        sqliteCmd.Parameters.AddWithValue("$favo", favo);
        sqliteCmd.Parameters.AddWithValue("$sfavo", sfavo);
        sqliteCmd.ExecuteNonQuery();
    }

    public async void UpdateLehrkraft(LuL l)
    {
        if (string.IsNullOrEmpty(l.Kuerzel) || l.ID <= 0) return;
        await UpdateLehrkraft(l.ID, l.Vorname, l.Nachname, l.Kuerzel, l.Mail, l.Fakultas, l.Pwttemp, l.Favo,
            l.SFavo);
    }

    /// <summary>
    /// setzt für per ID angebenen Schüler/Schülerin die Daten neu
    /// </summary>
    /// <param name="id"></param>
    /// <param name="vorname"></param>
    /// <param name="nachname"></param>
    /// <param name="mail"></param>
    /// <param name="klasse"></param>
    /// <param name="nutzername"></param>
    /// <param name="aixmail"></param>
    /// <param name="zweitaccount"></param>
    /// <param name="zweitmail"></param>
    public async Task UpdateSchueler(int id, string vorname, string nachname, string mail, string klasse,
        string nutzername, string aixmail, int zweitaccount, string zweitmail)
    {
        if (id <= 0) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "UPDATE schueler SET nachname=$nachname, vorname=$vorname, mail=$mail, klasse=$klasse, nutzername=$nutzername, aixmail=$aixmail, zweitaccount = $zweitaccount, zweitmail=$zweitmail WHERE id=$id;";
        sqliteCmd.Parameters.AddWithValue("$id", id);
        sqliteCmd.Parameters.AddWithValue("$vorname", vorname);
        sqliteCmd.Parameters.AddWithValue("$nachname", nachname);
        sqliteCmd.Parameters.AddWithValue("$mail", mail);
        sqliteCmd.Parameters.AddWithValue("$klasse", klasse);
        sqliteCmd.Parameters.AddWithValue("$nutzername", nutzername);
        sqliteCmd.Parameters.AddWithValue("$aixmail", aixmail);
        sqliteCmd.Parameters.AddWithValue("$zweitaccount", zweitaccount);
        sqliteCmd.Parameters.AddWithValue("$zweitmail", zweitmail);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// setzt den AIXMailadresse des per ID angegebenen Schülers/Schülerin
    /// </summary>
    /// <param name="id"></param>
    /// <param name="mail"></param>
    private void UpdateAIXSuSAdressen(int id, string mail)
    {
        if (id <= 0) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "UPDATE schueler SET aixmail = $mail WHERE id = $id;";
        sqliteCmd.Parameters.AddWithValue("$id", id);
        sqliteCmd.Parameters.AddWithValue("$mail", mail);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// setzt den Nutzername des per ID angegebenen Schülers/Schülerin
    /// </summary>
    /// <param name="id"></param>
    /// <param name="nutzername"></param>
    private void UpdateSchuelerNutzername(int id, string nutzername)
    {
        if (id <= 0) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "UPDATE schueler SET nutzername = $nutzername WHERE id = $id;";
        sqliteCmd.Parameters.AddWithValue("$id", id);
        sqliteCmd.Parameters.AddWithValue("$nutzername", nutzername);
        sqliteCmd.ExecuteNonQuery();
    }

    public async Task<ReadOnlyCollection<int>> GetM365Blacklist()
    {
        List<int> ids = [];
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT id FROM schueler WHERE m365 = 0;";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read())
        {
            ids.Add(int.Parse(sqliteDatareader["id"].ToString() ?? string.Empty));
        }

        return new ReadOnlyCollection<int>(ids);
    }

    /// <summary>
    /// Liest zur lehrerid, das temp. Passwort aus der Datenbank aus und gibt es zurück
    /// </summary>
    /// <param name="lehrerid">Die lehrerid, für die das temporäre Passwort ausgelesen werden soll</param>
    /// <returns>String temporäre Passwort</returns>
    private async Task<string> GetTempPasswort(int lehrerid)
    {
        if (lehrerid <= 0) return "";
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT pwtemp FROM lehrkraft WHERE id=" + lehrerid + ";";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        var tpwd = "";
        while (sqliteDatareader.Read())
        {
            tpwd = sqliteDatareader.GetString(0);
        }

        return tpwd;
    }

    public async void SetM365(int susid, int has_m365)
    {
        if (susid <= 0) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "UPDATE schueler SET m365 = $has_m365 WHERE id = $susid;";
        sqliteCmd.Parameters.AddWithValue("$has_m365", has_m365);
        sqliteCmd.Parameters.AddWithValue("$susid", susid);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Setzt das temp.Password für die angegebene lehrerid
    /// </summary>
    /// <param name="lehrerid">lehrerid, für die das Passwort geändert werden soll</param>
    /// <param name="pwd">das neue Passwort</param>
    public async void SetTPwd(int lehrerid, string pwd)
    {
        if (lehrerid <= 0) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "UPDATE lehrkraft SET pwtemp = $pwd WHERE id = $lehrerid;";
        sqliteCmd.Parameters.AddWithValue("$pwd", pwd);
        sqliteCmd.Parameters.AddWithValue("$lehrerid", lehrerid);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Liest die Schüler-IDs ein, zu denen ein Zweitaccount zu erstellen ist
    /// </summary>
    /// <param name="fileName"></param>
    public async Task ZweitAccountsEinlesen(string fileName)
    {
        var lines = await File.ReadAllLinesAsync(fileName);
        var inid = -1;
        var header = lines[0].Split(';');
        for (var i = 0; i < header.Length; i++)
        {
            if (header[i].Equals("Interne ID-Nummer"))
            {
                inid = i;
            }
        }

        if (inid == -1)
        {
            return;
        }

        await StartTransaction();
        for (var i = 1; i < lines.Length; i++)
        {
            try
            {
                var line = lines[i].Split(';');
                if (line[inid] != "")
                {
                    await SetZweitAccount(Convert.ToInt32(line[inid]), 1);
                }
                //else throw new Exception("SuS mit der id " + line[inid] + " nicht gefunden.");
            }
            catch (Exception ex)
            {
#if DEBUG
                await AddLogMessage("Debug", ex.StackTrace + ";" + ex.Message);
                // Debug.WriteLine("Zeile " + i + ": " + lines[i]);
#endif
            }
        }

        await StopTransaction();
    }

    /// <summary>
    /// Setzt die Flag für den Zweitaccount der id auf status (0 oder 1)
    /// </summary>
    /// <param name="id"></param>
    /// <param name="pStatus"></param>
    private async Task SetZweitAccount(int id, int pStatus)
    {
        if (id <= 0) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "UPDATE schueler SET zweitaccount = $status WHERE id = $susid;";
        sqliteCmd.Parameters.AddWithValue("$status", pStatus);
        sqliteCmd.Parameters.AddWithValue("$susid", id);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Gibt die Fachvorsitzenden und Stellvertreter zurück
    /// </summary>
    /// <returns>Liste der Fachvorsitzenden und Stellvertreter</returns>
    public async Task<List<LuL>> getFavos()
    {
        return GetLehrerListe().Result.Where(l => !string.IsNullOrEmpty(l.Favo) || !string.IsNullOrEmpty(l.SFavo))
            .ToList();
    }
}