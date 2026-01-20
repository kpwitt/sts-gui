using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SyslogLogging;

// ReSharper disable InconsistentNaming

#pragma warning disable CS1998

namespace SchulDB;

/// <summary>
/// Wrapperklasse zur Verwaltung der SQLite-Datenbank
/// </summary>
public class Schuldatenbank : IDisposable {
    private const string version = "0.73";
    private readonly string _dbpath;
    private string _logpath;
    private readonly LoggingModule log;
    private readonly SqliteConnection _sqliteConn;
    private SqliteTransaction? _dbtrans;
    private bool _activeTransaction = false;
    private bool _disposed = false;
    private string[] erprobungsstufe = ["5", "6"];
    private string[] mittelstufe = ["7", "8", "9", "10"];
    private string[] oberstufe = ["EF", "Q1", "Q2"];
    private string[] stubostufen = ["8", "9", "10", "EF", "Q1", "Q2"];
    private string[] jamfstufen = ["9", "10", "EF", "Q1", "Q2"];
    private readonly HashSet<Changes> ausstehende_aenderungen = [];

    public string[] Stubostufen {
        get => stubostufen;
        set => stubostufen = value ?? throw new ArgumentNullException(nameof(value));
    }

    public string[] Jamfstufen {
        get => jamfstufen;
        set => jamfstufen = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// erstellt, falls nicht vorhanden, die Datenbankstruktur und öffnet die Verbindung
    /// </summary>
    public Schuldatenbank(string path) {
        _dbpath = path;
        _logpath = _dbpath == ":memory:"
            ? Path.Combine(Path.GetTempPath(), Path.ChangeExtension($"StS_{Guid.NewGuid()}_tmp", "log"))
            : _dbpath.Replace("sqlite", "log");

        log = new LoggingModule(_logpath, FileLoggingMode.SingleLogFile);
        log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
        log.Settings.EnableConsole = false;
        log.Settings.UseUtcTime = false;
        var strconnection = $"Data Source={_dbpath}";
        _sqliteConn = new SqliteConnection(strconnection);
        _sqliteConn.Open();
        var sqliteCmd = _sqliteConn.CreateCommand();
        upgradeDB(sqliteCmd);
        try {
            sqliteCmd.CommandText = """
                                    CREATE TABLE IF NOT EXISTS
                                        [lehrkraft] (
                                        [id] INTEGER NOT NULL PRIMARY KEY,
                                        [nachname] NVARCHAR(512) NOT NULL,
                                        [vorname] NVARCHAR(512) NOT NULL,
                                        [mail] NVARCHAR(512) NOT NULL UNIQUE,
                                        [kuerzel] NVARCHAR(8) NOT NULL UNIQUE,
                                        [fakultas] NVARCHAR(16) NOT NULL,
                                        [pwtemp] NVARCHAR(16) NOT NULL,
                                        [favo] NVARCHAR(8) NOT NULL,
                                        [sfavo] NVARCHAR(8) NOT NULL,
                                        [aktiv] BOOLEAN NOT NULL DEFAULT TRUE,
                                        [seriennummer] NVARCHAR(64) NOT NULL DEFAULT '',
                                        [bemerkung] NVARCHAR(512) NOT NULL DEFAULT ''
                                       )
                                    """;
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.CommandText = "CREATE INDEX IF NOT EXISTS lindex ON lehrkraft(id);";
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.CommandText = """
                                    CREATE TABLE IF NOT EXISTS
                                        [schueler] (
                                        [id] INTEGER NOT NULL PRIMARY KEY,
                                        [nachname] NVARCHAR(512) NOT NULL,
                                        [vorname] NVARCHAR(512) NOT NULL,
                                        [mail] NVARCHAR(512) NOT NULL,
                                        [klasse] NVARCHAR(16) NOT NULL,
                                        [nutzername] NVARCHAR(7) NOT NULL,
                                        [aixmail] NVARCHAR(128) NOT NULL,
                                        [zweitaccount] INTEGER DEFAULT 0 NOT NULL,
                                        [zweitmail] NVARCHAR(512) DEFAULT '' NOT NULL,
                                        [m365] INTEGER DEFAULT 1 NOT NULL,
                                        [aktiv] BOOLEAN NOT NULL DEFAULT TRUE,
                                        [seriennummer] NVARCHAR(64) NOT NULL DEFAULT '',
                                        [jamf]	INTEGER NOT NULL DEFAULT 0,
                                        [bemerkung] NVARCHAR(512) NOT NULL DEFAULT ''
                                       )
                                    """;
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.CommandText = "CREATE INDEX IF NOT EXISTS sindex ON schueler(id);";
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.CommandText = """
                                    CREATE TABLE IF NOT EXISTS
                                        [kurse] (
                                        [bez] NVARCHAR(512) NOT NULL PRIMARY KEY,
                                        [fach] NVARCHAR(512) NOT NULL,
                                        [klasse] NVARCHAR(16) NOT NULL,
                                        [stufe] NVARCHAR(16) NOT NULL,
                                        [suffix] NVARCHAR(16) NOT NULL,
                                        [istkurs] INTEGER NOT NULL,
                                        [bemerkung] NVARCHAR(512) NOT NULL DEFAULT ''
                                       )
                                    """;
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.CommandText = "CREATE INDEX IF NOT EXISTS kindex ON kurse(bez);";
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.CommandText = """
                                    CREATE TABLE IF NOT EXISTS
                                        [unterrichtet] (
                                        [lehrerid] INTEGER NOT NULL,
                                        [kursbez] NVARCHAR(32) NOT NULL,
                                        PRIMARY KEY(lehrerid,kursbez),
                                        FOREIGN KEY(lehrerid) REFERENCES lehrkraft(id),
                                        FOREIGN KEY(kursbez) REFERENCES kurse(bez)
                                       )
                                    """;
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.CommandText = """
                                    CREATE TABLE IF NOT EXISTS
                                        [nimmtteil] (
                                        [schuelerid] INTEGER NOT NULL,
                                        [kursbez] NVARCHAR(32) NOT NULL,
                                        PRIMARY KEY(schuelerid,kursbez),
                                        FOREIGN KEY(schuelerid) REFERENCES schueler(id),
                                        FOREIGN KEY(kursbez) REFERENCES kurse(bez)
                                       )
                                    """;
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.CommandText = """
                                    CREATE TABLE IF NOT EXISTS
                                        [settings] (
                                        [setting] NVARCHAR(512) NOT NULL UNIQUE,
                                        [value] NVARCHAR(512) NOT NULL
                                       )
                                    """;
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.CommandText = """
                                    CREATE TABLE IF NOT EXISTS
                                        [fachersatz] (
                                        [kurzfach] NVARCHAR(16) NOT NULL,
                                        [langfach] NVARCHAR(64) NOT NULL,
                                        PRIMARY KEY(kurzfach,langfach)
                                       )
                                    """;
            sqliteCmd.ExecuteNonQuery();

            var fachk = new[] {
                "D", "E", "M", "BI", "CH", "EK", "F7", "GE", "IF", "I0", "KU", "L7", "MU", "PH", "PK", "PS",
                "SN", "SP"
            };
            var fachl = new[] {
                "Deutsch", "Englisch", "Mathematik", "Biologie", "Chemie", "Erdkunde", "Französisch", "Geschichte",
                "Informatik", "Italienisch", "Kunst", "Latein", "Musik", "Physik", "Politik", "Psychologie",
                "Schwimmen", "Sport"
            };
            if (_dbpath != ":memory:") return;
            var calcSuffix = DateTime.Now.Month < 8
                ? $"{DateTime.Now.Year - 2001}{DateTime.Now.Year - 2000}"
                : $"{DateTime.Now.Year - 2000}{DateTime.Now.Year - 1999}";
            Einstellungen einstellungen = new() {
                Mailsuffix = "@schule.local",
                Fachersetzung = "",
                Kurzfaecher = fachk,
                Langfaecher = fachl,
                Kurssuffix = $"_{calcSuffix}",
                Erprobungstufenleitung = "",
                Mittelstufenleitung = "",
                EFStufenleitung = "",
                Q1Stufenleitung = "",
                Q2Stufenleitung = "",
                Oberstufenkoordination = "",
                Erprobungsstufe = erprobungsstufe,
                Mittelstufe = mittelstufe,
                Oberstufe = oberstufe,
                StuboStufen = stubostufen,
                JAMFStufen = jamfstufen,
                StuBos = "",
                Version = version
            };
            SetSettings(ref sqliteCmd, ref einstellungen);
            if (fachk.Length != fachl.Length) return;
            for (var i = 0; i < fachk.Length; i++) {
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
        catch (SqliteException ex) {
            throw new ApplicationException($"Kritischer Fehler beim Erstellen der SQL-Datei: {ex.Message}");
        }
    }

    ~Schuldatenbank() {
        Dispose(true);
    }

    /// <summary>
    /// Räumt beim schließen der Datenbankverbindung auf
    /// </summary>
    /// <param name="disposing"></param>
    private void Dispose(bool disposing) {
        if (_disposed) return;
        // If disposing equals true, dispose all managed
        // and unmanaged resources.
        if (disposing) {
            CloseDB();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        // Note disposing has been done.
        _disposed = true;
    }

    /// <summary>
    /// updatet alte DBs von 0.5 auf 0.6
    /// </summary>
    /// <param name="sqliteCmd"></param>
    private void upgradeDB(SqliteCommand sqliteCmd) {
        //Überprüfung ob Datenbank initialisiert ist
        sqliteCmd.CommandText =
            "SELECT COUNT(*) AS id_col_count FROM pragma_table_info('schueler') WHERE name='id'";
        sqliteCmd.ExecuteNonQuery();
        var sqliteDatareader = sqliteCmd.ExecuteReader();
        var db_version = 0;
        while (sqliteDatareader.Read()) {
            db_version = Convert.ToInt32(sqliteDatareader.GetString("id_col_count"));
        }

        sqliteDatareader.Close();

        if (db_version != 1) return;
/*
        //upgrade DB to 0.6
        sqliteCmd.CommandText =
            "SELECT COUNT(*) AS m365_col_count FROM pragma_table_info('schueler') WHERE name='m365'";
        sqliteCmd.ExecuteNonQuery();
        sqliteDatareader = sqliteCmd.ExecuteReader();
        var output = 0;
        while (sqliteDatareader.Read()) {
            output = Convert.ToInt32(sqliteDatareader.GetString("m365_col_count"));
        }

        sqliteDatareader.Close();
        if (output == 0) {
            try {
                sqliteCmd.CommandText =
                    "ALTER TABLE schueler ADD COLUMN m365 INTEGER NOT NULL DEFAULT 1";
                sqliteCmd.ExecuteNonQuery();
                sqliteDatareader.Close();
                sqliteCmd.CommandText =
                    "INSERT OR REPLACE INTO settings(setting, value) VALUES ('version', '0.6')";
                sqliteCmd.ExecuteNonQuery();
            }
            catch (Exception ex) {
                sqliteDatareader.Close();
                AddLogMessage(new LogEintrag
                    { Warnstufe = "Fehler", Eintragsdatum = DateTime.Now, Nachricht = ex.Message });
                Environment.Exit(-1);
            }
        }
        //Ende Update 0.6

        //Begin Update 0.7
        sqliteCmd.CommandText =
            "SELECT COUNT(*) AS sus_column_count FROM pragma_table_info('schueler') WHERE name='aktiv'";
        sqliteCmd.ExecuteNonQuery();
        sqliteDatareader = sqliteCmd.ExecuteReader();
        var sus_column_count = 0;
        while (sqliteDatareader.Read()) {
            sus_column_count = Convert.ToInt32(sqliteDatareader.GetString("sus_column_count"));
        }

        sqliteDatareader.Close();
        sqliteCmd.CommandText =
            "SELECT COUNT(*) AS lul_column_count FROM pragma_table_info('lehrkraft') WHERE name='aktiv'";
        sqliteCmd.ExecuteNonQuery();
        sqliteDatareader = sqliteCmd.ExecuteReader();
        var lul_column_count = 0;
        while (sqliteDatareader.Read()) {
            lul_column_count = Convert.ToInt32(sqliteDatareader.GetString("lul_column_count"));
        }

        sqliteDatareader.Close();

        if (sus_column_count == 0) {
            try {
                sqliteCmd.CommandText =
                    "ALTER TABLE schueler ADD COLUMN aktiv BOOLEAN NOT NULL DEFAULT TRUE";
                sqliteCmd.ExecuteNonQuery();
                sqliteDatareader.Close();
                sqliteCmd.CommandText =
                    "INSERT OR REPLACE INTO settings(setting, value) VALUES ('version', '0.7')";
                sqliteCmd.ExecuteNonQuery();
            }
            catch (Exception ex) {
                sqliteDatareader.Close();
                AddLogMessage(new LogEintrag
                    { Warnstufe = "Fehler", Eintragsdatum = DateTime.Now, Nachricht = ex.Message });
                Environment.Exit(-1);
            }
        }

        sqliteDatareader.Close();

        if (lul_column_count == 0) {
            try {
                sqliteCmd.CommandText =
                    "ALTER TABLE lehrkraft ADD COLUMN aktiv BOOLEAN NOT NULL DEFAULT TRUE";
                sqliteCmd.ExecuteNonQuery();
                sqliteDatareader.Close();
                sqliteCmd.CommandText =
                    "INSERT OR REPLACE INTO settings(setting, value) VALUES ('version', '0.7')";
                sqliteCmd.ExecuteNonQuery();
            }
            catch (Exception ex) {
                sqliteDatareader.Close();
                AddLogMessage(new LogEintrag
                    { Warnstufe = "Fehler", Eintragsdatum = DateTime.Now, Nachricht = ex.Message });
                Environment.Exit(-1);
            }
        }

        sqliteDatareader.Close();

        //Ende Update 0.7

        //Begin Update 0.71
        sqliteCmd.CommandText =
            "SELECT COUNT(*) AS log_column_count FROM pragma_table_info('log') WHERE name='id'";
        sqliteCmd.ExecuteNonQuery();
        sqliteDatareader = sqliteCmd.ExecuteReader();
        var log_count = 0;
        while (sqliteDatareader.Read()) {
            log_count = Convert.ToInt32(sqliteDatareader.GetString("log_column_count"));
        }

        sqliteDatareader.Close();
        if (log_count > 1) {
            sqliteCmd.CommandText = "SELECT stufe, datum, nachricht FROM log;";
            sqliteDatareader = sqliteCmd.ExecuteReader();
            while (sqliteDatareader.Read()) {
                var level = sqliteDatareader.GetString(0);
                var message = sqliteDatareader.GetString(2);
                switch (level) {
                    case "Info":
                        log.Info(message);
                        break;
                    case "Fehler":
                        log.Error(message);
                        break;
                    case "Debug":
                        log.Debug(message);
                        break;
                }
            }

            sqliteDatareader.Close();
            sqliteCmd.CommandText = "DROP TABLE IF EXISTS log; VACUUM";
            sqliteDatareader = sqliteCmd.ExecuteReader();
            sqliteDatareader.Close();
            sqliteCmd.CommandText =
                "INSERT OR REPLACE INTO settings(setting, value) VALUES ('version', '0.71')";
            sqliteCmd.ExecuteNonQuery();
            sqliteDatareader.Close();
        }
        //Ende Update 0.71

        //upgrade DB to 0.72
        sqliteCmd.CommandText =
            "SELECT COUNT(*) AS sn_col_count FROM pragma_table_info('schueler') WHERE name='seriennummer'";
        sqliteCmd.ExecuteNonQuery();
        sqliteDatareader = sqliteCmd.ExecuteReader();
        output = 0;
        while (sqliteDatareader.Read()) {
            output = Convert.ToInt32(sqliteDatareader.GetString("sn_col_count"));
        }

        sqliteDatareader.Close();
        if (output != 1) {
            try {
                sqliteCmd.CommandText =
                    "ALTER TABLE schueler ADD COLUMN seriennummer NVARCHAR(64) NOT NULL DEFAULT ''";
                sqliteCmd.ExecuteNonQuery();
                sqliteDatareader.Close();
                sqliteCmd.CommandText =
                    "ALTER TABLE lehrkraft ADD COLUMN seriennummer NVARCHAR(64) NOT NULL DEFAULT ''";
                sqliteCmd.ExecuteNonQuery();
                sqliteDatareader.Close();
                sqliteCmd.CommandText =
                    "INSERT OR REPLACE INTO settings(setting, value) VALUES ('version', '0.72')";
                sqliteCmd.ExecuteNonQuery();
            }
            catch (Exception ex) {
                sqliteDatareader.Close();
                AddLogMessage(new LogEintrag
                    { Warnstufe = "Fehler", Eintragsdatum = DateTime.Now, Nachricht = ex.Message });
                Environment.Exit(-1);
            }
        }
        //Ende Update 0.72

        //upgrade DB to 0.73
        sqliteCmd.CommandText =
            "SELECT COUNT(*) AS sn_col_count FROM pragma_table_info('schueler') WHERE name='bemerkung'";
        sqliteCmd.ExecuteNonQuery();
        sqliteDatareader = sqliteCmd.ExecuteReader();
        output = 0;
        while (sqliteDatareader.Read()) {
            output = Convert.ToInt32(sqliteDatareader.GetString("sn_col_count"));
        }

        sqliteDatareader.Close();
        if (output == 1) return;
        {
            try {
                sqliteCmd.CommandText =
                    "ALTER TABLE schueler ADD COLUMN bemerkung NVARCHAR(512) NOT NULL DEFAULT ''";
                sqliteCmd.ExecuteNonQuery();
                sqliteDatareader.Close();
                sqliteCmd.CommandText =
                    "ALTER TABLE lehrkraft ADD COLUMN bemerkung NVARCHAR(512) NOT NULL DEFAULT ''";
                sqliteCmd.ExecuteNonQuery();
                sqliteDatareader.Close();
                sqliteCmd.CommandText =
                    "ALTER TABLE kurse ADD COLUMN bemerkung NVARCHAR(512) NOT NULL DEFAULT ''";
                sqliteCmd.ExecuteNonQuery();
                sqliteDatareader.Close();
                sqliteCmd.CommandText =
                    "INSERT OR REPLACE INTO settings(setting, value) VALUES ('version', '0.73')";
                sqliteCmd.ExecuteNonQuery();
            }
            catch (Exception ex) {
                sqliteDatareader.Close();
                AddLogMessage(new LogEintrag
                    { Warnstufe = "Fehler", Eintragsdatum = DateTime.Now, Nachricht = ex.Message });
                Environment.Exit(-1);
            }
        }
        //Ende Update 0.73

 */
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
    /// <param name="bemerkung"></param>
    public async Task AddKurs(string bez, string fach, string klasse, string stufe, string suffix, int istkurs,
        string bemerkung) {
        if (await GibtEsKurs(bez)) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "INSERT OR IGNORE INTO kurse (bez, fach, klasse, stufe, suffix, istkurs,bemerkung) VALUES ($bez, $fach, $klasse, $stufe, $suffix, $istkurs, $bemerkung);";
        sqliteCmd.Parameters.AddWithValue("$bez", bez);
        sqliteCmd.Parameters.AddWithValue("$fach", fach);
        sqliteCmd.Parameters.AddWithValue("$klasse", klasse);
        sqliteCmd.Parameters.AddWithValue("$stufe", stufe);
        sqliteCmd.Parameters.AddWithValue("$suffix", suffix);
        sqliteCmd.Parameters.AddWithValue("$istkurs", istkurs);
        sqliteCmd.Parameters.AddWithValue("$bemerkung", bemerkung);
        sqliteCmd.ExecuteNonQuery();
        AddLogMessage(new LogEintrag {
            Eintragsdatum = DateTime.Now, Nachricht = $"Kurs {bez} angelegt",
            Warnstufe = "Info"
        });
    }

    /// <summary>
    /// fügt den Kurs hinzu
    /// </summary>
    /// <param name="kurs"></param>
    /// <returns></returns>
    public async Task AddKurs(Kurs kurs) {
        if (await GibtEsKurs(kurs.Bezeichnung)) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "INSERT OR IGNORE INTO kurse (bez, fach, klasse, stufe, suffix, istkurs,bemerkung) VALUES ($bez, $fach, $klasse, $stufe, $suffix, $istkurs,$bemerkung);";
        sqliteCmd.Parameters.AddWithValue("$fach", kurs.Fach);
        sqliteCmd.Parameters.AddWithValue("$bez", kurs.Bezeichnung);
        sqliteCmd.Parameters.AddWithValue("$klasse", kurs.Klasse);
        sqliteCmd.Parameters.AddWithValue("$stufe", kurs.Stufe);
        sqliteCmd.Parameters.AddWithValue("$suffix", kurs.Suffix);
        sqliteCmd.Parameters.AddWithValue("$istkurs", kurs.IstKurs);
        sqliteCmd.Parameters.AddWithValue("$bemerkung", kurs.Bemerkung);
        sqliteCmd.ExecuteNonQuery();
        AddLogMessage(new LogEintrag {
            Eintragsdatum = DateTime.Now, Nachricht = $"Kurs {kurs.Bezeichnung} angelegt",
            Warnstufe = "Info"
        });
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
    /// <param name="seriennummer"></param>
    /// <param name="bemerkung"></param>
    public async Task Addlehrkraft(int id, string vorname, string nachname, string kuerzel, string mail,
        string fakultas, string favo, string sfavo, string seriennummer, string bemerkung) {
        if (GibtEsLehrkraft(id)) return;
        seriennummer = string.IsNullOrEmpty(seriennummer) ? "" : seriennummer;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "INSERT OR IGNORE INTO lehrkraft (id, nachname, vorname, kuerzel, mail, fakultas, pwtemp, favo, sfavo, seriennummer, bemerkung)" +
            " VALUES ($id,$nachname,$vorname,$kuerzel,$mail,$fakultas,$pwtemp,$favo,$sfavo,$seriennummer,$bemerkung);";
        sqliteCmd.Parameters.AddWithValue("$id", id);
        sqliteCmd.Parameters.AddWithValue("$vorname", vorname);
        sqliteCmd.Parameters.AddWithValue("$nachname", nachname);
        sqliteCmd.Parameters.AddWithValue("$kuerzel", kuerzel.ToUpper());
        sqliteCmd.Parameters.AddWithValue("$mail", mail.ToLower());
        sqliteCmd.Parameters.AddWithValue("$fakultas", fakultas.TrimEnd(';'));
        sqliteCmd.Parameters.AddWithValue("$pwtemp", Tooling.GeneratePasswort(8));
        sqliteCmd.Parameters.AddWithValue("$favo", favo);
        sqliteCmd.Parameters.AddWithValue("$sfavo", sfavo);
        sqliteCmd.Parameters.AddWithValue("$seriennummer", seriennummer);
        sqliteCmd.Parameters.AddWithValue("$bemerkung", bemerkung);
        sqliteCmd.ExecuteNonQuery();
        AddLogMessage(new LogEintrag {
            Eintragsdatum = DateTime.Now,
            Nachricht =
                $" Lehrkraft {nachname} {vorname} {kuerzel} angelegt",
            Warnstufe = "Info"
        });
    }

    /// <summary>
    /// fügt die Lehrperson hinzu
    /// </summary>
    /// <param name="lehrkraft"></param>
    /// <returns></returns>
    public async Task Addlehrkraft(Lehrkraft lehrkraft) {
        if (GibtEsLehrkraft(lehrkraft.ID)) return;
        lehrkraft.Seriennummer = string.IsNullOrEmpty(lehrkraft.Seriennummer) ? "" : lehrkraft.Seriennummer;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "INSERT OR IGNORE INTO lehrkraft (id, nachname, vorname, kuerzel, mail, fakultas, pwtemp, favo, sfavo, seriennummer,bemerkung)" +
            "VALUES ($id, $nachname, $vorname, $kuerzel, $mail, $fakultas, $pwtemp, $favo, $sfavo, $seriennummer,$bemerkung);";
        sqliteCmd.Parameters.AddWithValue("$id", lehrkraft.ID);
        sqliteCmd.Parameters.AddWithValue("$vorname", lehrkraft.Vorname);
        sqliteCmd.Parameters.AddWithValue("$nachname", lehrkraft.Nachname);
        sqliteCmd.Parameters.AddWithValue("$kuerzel", lehrkraft.Kuerzel.ToUpper());
        sqliteCmd.Parameters.AddWithValue("$mail", lehrkraft.Mail.ToLower());
        sqliteCmd.Parameters.AddWithValue("$fakultas", lehrkraft.Fakultas.TrimEnd(';'));
        sqliteCmd.Parameters.AddWithValue("$pwtemp",
            lehrkraft.Pwttemp.Length > 7 ? lehrkraft.Pwttemp : Tooling.GeneratePasswort(8));
        sqliteCmd.Parameters.AddWithValue("$favo", lehrkraft.Favo);
        sqliteCmd.Parameters.AddWithValue("$sfavo", lehrkraft.SFavo);
        sqliteCmd.Parameters.AddWithValue("$seriennummer", lehrkraft.Seriennummer);
        sqliteCmd.Parameters.AddWithValue("$bemerkung", lehrkraft.Bemerkung);
        sqliteCmd.ExecuteNonQuery();
        AddLogMessage(new LogEintrag {
            Eintragsdatum = DateTime.Now,
            Nachricht =
                $" Lehrkraft {lehrkraft.Nachname} {lehrkraft.Vorname} {lehrkraft.Mail} angelegt",
            Warnstufe = "Info"
        });
    }

    /// <summary>
    /// fügt eine Nachricht ins Log hinzu, Stufe entweder Info, Hinweis oder Fehler
    /// </summary>
    /// <param name="eintrag"></param>
    public void AddLogMessage(LogEintrag eintrag) {
        switch (eintrag.Warnstufe) {
            case "Info":
                log.Info(eintrag.Nachricht);
                break;
            case "Fehler":
                log.Error(eintrag.Nachricht);
                break;
            case "Debug":
                log.Debug(eintrag.Nachricht);
                break;
        }
    }

    /// <summary>
    /// fügt die angegebene Lehrperson zum angegebene Kurs hinzu
    /// </summary>
    /// <param name="lid"></param>
    /// <param name="kbez"></param>
    public async Task AddLtoK(int lid, string kbez) {
        if (lid == 0 || string.IsNullOrEmpty(kbez)) return;
        var kursliste = GetKurseVonLuL(lid).Result.Select(k => k.Bezeichnung).ToList();
        if (kursliste.Contains(kbez)) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "INSERT OR IGNORE INTO unterrichtet (lehrerid, kursbez) VALUES ($lid, $kbez);";
        sqliteCmd.Parameters.AddWithValue("$lid", lid);
        sqliteCmd.Parameters.AddWithValue("$kbez", kbez);
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.Parameters.Clear();
        var kurs = await GetKurs(kbez);
        ausstehende_aenderungen.Add(new Changes {
            kind = ChangeKind.add, person = ChangePerson.LuL, kurs = kurs,
            id = lid
        });
        if (string.IsNullOrEmpty(kurs.Bezeichnung)) return;
        var klkurs = $"{kurs.Klasse}KL";
        if (!await GibtEsKurs(klkurs)) {
            await AddKurs(klkurs, "KL", kurs.Klasse, kurs.Stufe, kurs.Suffix, 0, "");
        }

        var kurse = GetKurseVonLuL(lid).Result;
        if (!kurs.IstKurs && !oberstufe.Contains(kurs.Stufe) && kurse.All(k => k.Bezeichnung != klkurs)) {
            sqliteCmd.CommandText = "INSERT OR IGNORE INTO unterrichtet (lehrerid, kursbez) VALUES ($lid, $kbez);";
            sqliteCmd.Parameters.AddWithValue("$lid", lid);
            sqliteCmd.Parameters.AddWithValue("$kbez", klkurs);
            sqliteCmd.ExecuteNonQuery();
            ausstehende_aenderungen.Add(new Changes {
                kind = ChangeKind.add, person = ChangePerson.LuL, kurs = await GetKurs(klkurs),
                id = lid
            });
        }

        var lul = await GetLehrkraft(lid);
        AddLogMessage(new LogEintrag {
            Eintragsdatum = DateTime.Now,
            Nachricht = $"Lehrkraft {lul.Kuerzel} {lid} zu Kurs {kbez} hinzugefügt",
            Warnstufe = "Info"
        });
    }

    /// <summary>
    /// fügt die angegebene Lehrperson zum angegebene Kurs hinzu
    /// </summary>
    /// <param name="lehrkraft"></param>
    /// <param name="kurs"></param>
    /// <returns></returns>
    public async Task AddLtoK(Lehrkraft lehrkraft, Kurs kurs) {
        if (string.IsNullOrEmpty(kurs.Bezeichnung) || lehrkraft.ID == 0) return;
        await AddLtoK(lehrkraft.ID, kurs.Bezeichnung);
    }

    public async Task AddSchuelerIn(int id, string vorname, string nachname, string mail, string klasse,
        string nutzername, string aixmail, int zweitaccount, string zweitmail, string seriennummer, bool m365,
        bool jamf, bool aktiv, string bemerkung) {
        if (GibtEsSchueler(id)) return;
        seriennummer = string.IsNullOrEmpty(seriennummer) ? "" : seriennummer;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "INSERT OR IGNORE INTO schueler (id, vorname, nachname, mail, klasse, nutzername, aixmail, zweitaccount, zweitmail, seriennummer, m365, jamf, aktiv,bemerkung)" +
            " VALUES ($id, $vorname, $nachname, $mail, $klasse, $nutzername, $aixmail,$zweitaccount, $zweitmail, $seriennummer, $m365, $jamf, $aktiv,$bemerkung);";
        sqliteCmd.Parameters.AddWithValue("$id", id);
        sqliteCmd.Parameters.AddWithValue("$vorname", vorname);
        sqliteCmd.Parameters.AddWithValue("$nachname", nachname);
        sqliteCmd.Parameters.AddWithValue("$mail", mail.ToLower());
        sqliteCmd.Parameters.AddWithValue("$klasse", klasse);
        sqliteCmd.Parameters.AddWithValue("$nutzername", nutzername.ToLower());
        sqliteCmd.Parameters.AddWithValue("$aixmail", aixmail.ToLower());
        sqliteCmd.Parameters.AddWithValue("$zweitaccount", zweitaccount);
        sqliteCmd.Parameters.AddWithValue("$zweitmail", zweitmail.ToLower());
        sqliteCmd.Parameters.AddWithValue("$seriennummer", seriennummer);
        sqliteCmd.Parameters.AddWithValue("$m365", m365);
        sqliteCmd.Parameters.AddWithValue("$jamf", jamf);
        sqliteCmd.Parameters.AddWithValue("$aktiv", aktiv);
        sqliteCmd.Parameters.AddWithValue("$bemerkung", bemerkung);
        sqliteCmd.ExecuteNonQuery();
        AddLogMessage(new LogEintrag {
            Eintragsdatum = DateTime.Now,
            Nachricht = $" SuS {nachname} {vorname} {mail} angelegt",
            Warnstufe = "Info"
        });
    }

    /// <summary>
    /// fügt den Schüler/die  SuS hinzu
    /// </summary>
    /// <param name="schuelerin"></param>
    /// <returns></returns>
    public async Task AddSchuelerIn(SuS schuelerin) {
        if (GibtEsSchueler(schuelerin.ID)) return;
        schuelerin.Seriennummer = string.IsNullOrEmpty(schuelerin.Seriennummer) ? "" : schuelerin.Seriennummer;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "INSERT OR IGNORE INTO schueler (id, vorname, nachname, mail, klasse, nutzername, aixmail, zweitaccount, zweitmail, seriennummer, m365, jamf, aktiv,bemerkung) " +
            "VALUES ($id, $vorname, $nachname, $mail, $klasse, $nutzername, $aixmail,$zweitaccount, $zweitmail, $seriennummer, $m365, $jamf, $aktiv,$bemerkung);";
        sqliteCmd.Parameters.AddWithValue("$id", schuelerin.ID);
        sqliteCmd.Parameters.AddWithValue("$vorname", schuelerin.Vorname);
        sqliteCmd.Parameters.AddWithValue("$nachname", schuelerin.Nachname);
        sqliteCmd.Parameters.AddWithValue("$mail", schuelerin.Mail.ToLower());
        sqliteCmd.Parameters.AddWithValue("$klasse", schuelerin.Klasse);
        sqliteCmd.Parameters.AddWithValue("$nutzername", schuelerin.Nutzername.ToLower());
        sqliteCmd.Parameters.AddWithValue("$aixmail", schuelerin.Aixmail.ToLower());
        sqliteCmd.Parameters.AddWithValue("$zweitaccount", schuelerin.Zweitaccount);
        sqliteCmd.Parameters.AddWithValue("$zweitmail", schuelerin.Zweitmail.ToLower());
        sqliteCmd.Parameters.AddWithValue("$seriennummer", schuelerin.Seriennummer);
        sqliteCmd.Parameters.AddWithValue("$m365", schuelerin.HasM365Account);
        sqliteCmd.Parameters.AddWithValue("$jamf", schuelerin.AllowJAMF);
        sqliteCmd.Parameters.AddWithValue("$aktiv", schuelerin.IstAktiv);
        sqliteCmd.Parameters.AddWithValue("$bemerkung", schuelerin.Bemerkung);
        sqliteCmd.ExecuteNonQuery();
        AddLogMessage(new LogEintrag {
            Eintragsdatum = DateTime.Now,
            Nachricht = $" SuS {schuelerin.Nachname} {schuelerin.Vorname} {schuelerin.Mail} angelegt",
            Warnstufe = "Info"
        });
    }

    /// <summary>
    /// fügt den/die angegebenen Schüler/ SuS zum angegebene Kurs hinzu
    /// </summary>
    /// <param name="sid"></param>
    /// <param name="kbez"></param>
    public async Task AddStoK(int sid, string kbez) {
        if (sid == 0 || !await GibtEsKurs(kbez)) return;
        var kursliste = GetKurseVonSuS(sid).Result.Select(k => k.Bezeichnung).ToList();
        if (kursliste.Contains(kbez)) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "INSERT OR IGNORE INTO nimmtteil (schuelerid, kursbez) VALUES ($sid, $kbez);";
        sqliteCmd.Parameters.AddWithValue("$sid", sid);
        sqliteCmd.Parameters.AddWithValue("$kbez", kbez);
        sqliteCmd.ExecuteNonQuery();
        ausstehende_aenderungen.Add(new Changes {
            kind = ChangeKind.add, person = ChangePerson.SuS, kurs = await GetKurs(kbez),
            id = sid
        });
        AddLogMessage(new LogEintrag {
            Eintragsdatum = DateTime.Now,
            Nachricht = $" SuS {sid} zu Kurs {kbez} hinzugefügt",
            Warnstufe = "Info"
        });
    }

    /// <summary>
    /// fügt den/die angegebenen Schüler/ SuS zum angegebene Kurs hinzu
    /// </summary>
    /// <param name="schulerin"></param>
    /// <param name="kurs"></param>
    /// <returns></returns>
    public async Task AddStoK(SuS schulerin, Kurs kurs) {
        if (schulerin.ID == 0 || string.IsNullOrEmpty(kurs.Bezeichnung)) return;
        await AddStoK(schulerin.ID, kurs.Bezeichnung);
    }

    /// <summary>
    /// Fügt den SuS zum Klassenunterricht der übergebenen Klasse hinzu
    /// </summary>
    /// <param name="schulerin"></param>
    /// <param name="klasse"></param>
    /// <returns></returns>
    public async Task AddStoKlassenKurse(SuS schulerin, string klasse) {
        if (erprobungsstufe.Concat(mittelstufe).ToArray().Contains(Tooling.KlasseToStufe(klasse))) {
            var kliste = GetKursListe().Result.ToList();
            kliste = kliste.FindAll(k => k.Bezeichnung.StartsWith(klasse) && k.IstKurs == false);
            foreach (var k in kliste) {
                var sqliteCmd = _sqliteConn.CreateCommand();
                sqliteCmd.CommandText =
                    "INSERT OR IGNORE INTO nimmtteil (schuelerid, kursbez) VALUES ($sid, $kbez);";
                sqliteCmd.Parameters.AddWithValue("$sid", schulerin.ID);
                sqliteCmd.Parameters.AddWithValue("$kbez", k.Bezeichnung);
                sqliteCmd.ExecuteNonQuery();
                sqliteCmd.Parameters.Clear();
                ausstehende_aenderungen.Add(new Changes {
                    kind = ChangeKind.add, person = ChangePerson.SuS, kurs = await GetKurs(k.Bezeichnung),
                    id = schulerin.ID
                });
                AddLogMessage(new LogEintrag {
                    Eintragsdatum = DateTime.Now,
                    Nachricht = $" SuS {schulerin.ID} zu Klassenkurs {k.Bezeichnung} hinzugefügt",
                    Warnstufe = "Info"
                });
            }
        }
    }

    /// <summary>
    /// optimiert die DB und schließt die Verbindung
    /// </summary>
    private void CloseDB() {
        if (_activeTransaction) {
            _dbtrans?.Commit();
            _activeTransaction = false;
        }

        if (_sqliteConn.State != ConnectionState.Open) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "pragma optimize;VACUUM ";
        sqliteCmd.ExecuteNonQuery();
        _sqliteConn.Close();
    }

    /// <summary>
    /// Pflichtimplementierung um sicherzustellen, dass beim Löschen des Objekt Ressourcen etc. freigegeben werden.
    /// Schließt die Datenbank
    /// </summary>
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Export die Daten so, dass sie als Ausgangspunkt für einen Reimport genutzt werden können
    /// </summary>
    /// <param name="folder"></param>
    /// <returns></returns>
    public async Task<int> DumpDataToCSVs(string folder) {
        return await DumpDataToCSVs(folder, await GetSchuelerListe(), await GetLehrkraftListe());
    }

    /// <summary>
    /// Export die Daten so, dass sie als Ausgangspunkt für einen Reimport genutzt werden können
    /// </summary>
    /// <param name="folder"></param>
    /// <param name="lehrerliste"></param>
    /// <param name="susliste"></param>
    private async Task<int> DumpDataToCSVs(string folder, ReadOnlyCollection<SuS> susliste,
        IEnumerable<Lehrkraft> lehrerliste) {
        try {
            List<string> lulliste = ["firstname;lastname;idnumber;username;fakultas;email;seriennummer"];
            lulliste.AddRange(lehrerliste.Select(lehrer =>
                string.Join(";", lehrer.Vorname, lehrer.Nachname, lehrer.ID, lehrer.Kuerzel, lehrer.Fakultas,
                    lehrer.Mail, lehrer.Seriennummer)));

            List<string> sliste = ["Vorname;Nachname;Interne ID-Nummer;E-Mail;Klasse"];
            List<string> kurse = ["Vorname|Nachname|Fach|Fachlehrer|Kursart|Kurs"];
            List<string> ids = ["Anmeldename;Referenz-Id;E-Mail"];
            List<string> zweitaccounts = ["Interne ID-Nummer"];
            List<string> temp_accounts = ["id;accountname"];
            List<string> jamf_sus_seriennummern = ["Vorname;Nachname;Klasse;Seriennummer;ID"];
            List<string> aix_usernames = ["Referenz-Id;Anmeldenamen;E-Mail"];
            List<string> jamf = ["Vorname;Nachname;Klasse;JAMF (ja/nein/fehlt)"];

            await Parallel.ForEachAsync(susliste, async (schueler, cancellationToken) =>
                //foreach (var schueler in susliste)
            {
                sliste.Add(string.Join(";", schueler.Vorname, schueler.Nachname, schueler.ID, schueler.Mail,
                    schueler.Klasse));
                ids.Add($"{schueler.Nutzername};{schueler.ID};{schueler.Mail}");
                aix_usernames.Add($"{schueler.ID};{schueler.Nutzername};{schueler.Aixmail}");
                if (!string.IsNullOrEmpty(schueler.Seriennummer)) {
                    jamf_sus_seriennummern.Add(string.Join(";", schueler.Vorname, schueler.Nachname, schueler.Klasse,
                        schueler.Seriennummer, schueler.ID));
                }

                if (schueler.Zweitaccount) {
                    zweitaccounts.Add($"{schueler.ID}");
                }

                if (schueler.Nutzername.Length == 8) {
                    temp_accounts.Add($"{schueler.ID};{schueler.Nutzername}");
                }

                jamf.Add($"{schueler.Vorname};{schueler.Nachname};{schueler.Klasse};" +
                         (schueler.AllowJAMF ? "ja" : "nein"));

                await Parallel.ForEachAsync(GetKurseVonSuS(schueler.ID).Result, cancellationToken, async (kurs, _) =>
                    //foreach (var kurs in await GetKurseVonSuS(schueler.ID))
                {
                    var luls = await GetLuLAusKurs(kurs.Bezeichnung);
                    if (luls.Count > 0) {
                        var l = await GetLehrkraft(luls[0].ID);
                        var fach = kurs.Fach.IndexOf('-') > 0 ? kurs.Fach[..kurs.Fach.IndexOf('-')] : kurs.Fach;
                        kurse.Add(string.Join("|", $"{schueler.Vorname}|{schueler.Nachname}", fach,
                            l.Kuerzel.ToUpper(),
                            (kurs.IstKurs ? "PUK|" : "GKM|") + (!kurs.IstKurs ? "" : kurs.Fach)));
                    }
                });
            });

            await File.WriteAllLinesAsync($"{folder}/sus.csv", sliste.Distinct().ToList(), Encoding.UTF8);
            await File.WriteAllLinesAsync($"{folder}/lul.csv", lulliste.Distinct().ToList(), Encoding.UTF8);
            await File.WriteAllLinesAsync($"{folder}/kurse.csv", kurse.Distinct().ToList(), Encoding.UTF8);
            await File.WriteAllLinesAsync($"{folder}/ids.csv", ids.Distinct().ToList(), Encoding.UTF8);
            await File.WriteAllLinesAsync($"{folder}/zweitaccount.csv", zweitaccounts.Distinct().ToList(),
                Encoding.UTF8);
            await File.WriteAllLinesAsync($"{folder}/temp_accounts.csv", temp_accounts.Distinct().ToList(),
                Encoding.UTF8);
            await File.WriteAllLinesAsync($"{folder}/jamf_sus_seriennummern.csv",
                jamf_sus_seriennummern.Distinct().ToList(),
                Encoding.UTF8);
            await File.WriteAllLinesAsync($"{folder}/aix_nutzernamen.csv",
                aix_usernames.Distinct().ToList(),
                Encoding.UTF8);
            await File.WriteAllLinesAsync($"{folder}/jamf.csv", jamf.Distinct().ToList(), Encoding.UTF8);
            return 1;
        }
        catch (Exception ex) {
#if DEBUG
            AddLogMessage(new LogEintrag
                { Eintragsdatum = DateTime.Now, Nachricht = ex.Message, Warnstufe = "Debug" });
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
    public async Task<int> DumpKursDataToCSVs(string folder, List<string> kursliste) {
        try {
            List<string> kurse = ["Vorname|Nachname|Fach|Fachlehrer|Kursart|Kurs"];
            foreach (var k in kursliste) {
                var kurs = await GetKurs(k);
                foreach (var schueler in await GetSuSAusKurs(k)) {
                    var l = (await GetLuLAusKurs(k))[0];
                    var fach = kurs.Fach.IndexOf('-') > 0 ? kurs.Fach[..kurs.Fach.IndexOf('-')] : kurs.Fach;
                    kurse.Add(string.Join("|", $"{schueler.Nachname}|{schueler.Vorname}", fach, l.Kuerzel.ToUpper(),
                        (kurs.IstKurs ? "PUK|" : "GKM|") + (kurs.IstKurs == false ? "" : kurs.Fach)
                    ));
                }
            }

            await File.WriteAllLinesAsync($"{folder}/kurse.csv", kurse.Distinct().ToList(), Encoding.UTF8);
            return 1;
        }
        catch (Exception ex) {
#if DEBUG
            AddLogMessage(new LogEintrag
                { Eintragsdatum = DateTime.Now, Nachricht = ex.Message, Warnstufe = "Debug" });
#endif
            return -1;
        }
    }

    /// <summary>
    /// speichert die in-memory-datenbank in eine Datei
    /// </summary>
    /// <param name="importfrom">Die Datenbank von der die Daten importiert werden</param>
    public async Task<int> Import(Schuldatenbank importfrom) {
        await StartTransaction();
        //kurse übertragen
        foreach (var kurs in await importfrom.GetKursListe()) {
            await AddKurs(kurs.Bezeichnung, kurs.Fach, kurs.Klasse, kurs.Stufe, kurs.Suffix,
                Convert.ToInt32(kurs.IstKurs), kurs.Bemerkung);
        }

        //sus übertragen
        foreach (var schueler in await importfrom.GetSchuelerListe()) {
            await AddSchuelerIn(schueler.ID, schueler.Vorname, schueler.Nachname, schueler.Mail, schueler.Klasse,
                schueler.Nutzername, schueler.Aixmail, Convert.ToInt32(schueler.Zweitaccount), schueler.Zweitmail,
                schueler.Seriennummer, schueler.HasM365Account, schueler.AllowJAMF,schueler.IstAktiv, schueler.Bemerkung );
            foreach (var kurs in await importfrom.GetKurseVonSuS(Convert.ToInt32(schueler.ID))) {
                await AddStoK(Convert.ToInt32(schueler.ID), kurs.Bezeichnung);
            }
        }

        //lul übertragen
        foreach (var lehrkraft in await importfrom.GetLehrkraftListe()) {
            await Addlehrkraft(Convert.ToInt32(lehrkraft.ID), lehrkraft.Vorname, lehrkraft.Nachname,
                lehrkraft.Kuerzel, lehrkraft.Mail, lehrkraft.Fakultas, lehrkraft.Favo, lehrkraft.SFavo,
                lehrkraft.Seriennummer, lehrkraft.Bemerkung);
            foreach (var kurs in await importfrom.GetKurseVonLuL(lehrkraft.ID)) {
                await AddLtoK(Convert.ToInt32(lehrkraft.ID), kurs.Bezeichnung);
            }
        }

        //Settings übertragen
        await SetSettings(await importfrom.GetSettings());
        await StopTransaction();

        //log übertragen
        var logs = importfrom.GetLog().Result.Select(l => l.ToString());
        await File.WriteAllLinesAsync(_logpath, logs, Encoding.UTF8);

        return 0;
    }

    /// <summary>
    /// liest die Daten der Eltern/ERrziehungsberechtigten ein
    /// </summary>
    /// <param name="elternfile"></param>
    /// <returns></returns>
    public async Task ElternEinlesen(string elternfile) {
        var lines = await File.ReadAllLinesAsync(elternfile);
        int isk = -1, isn = -1, isv = -1, imail = -1;
        var header = lines[0].Split(';');
        for (var i = 0; i < header.Length; i++) {
            if (header[i].Equals("Schüler-Klasse")) {
                isk = i;
            }

            if (header[i].Equals("Schüler-Nachname")) {
                isn = i;
            }

            if (header[i].Equals("Schüler-Vorname")) {
                isv = i;
            }

            if (header[i].Equals("2. Person: E-Mail")) {
                imail = i;
            }
        }

        await StartTransaction();
        for (var i = 1; i < lines.Length; i++) {
            try {
                var line = lines[i].Split(';');
                if (line[imail] == "") continue;
                try {
                    var susliste = await GetSchueler(line[isv], line[isn]);
                    foreach (var sus in susliste.Where(sus => sus.Klasse == line[isk])) {
                        await UpdateSchueler(sus.ID, sus.Vorname, sus.Nachname, sus.Mail, sus.Klasse,
                            sus.Nutzername, sus.Aixmail, Convert.ToInt32(sus.Zweitaccount), line[imail], false, true,
                            sus.Seriennummer, sus.AllowJAMF, sus.Bemerkung);
                    }
                }
                catch (Exception e) {
                    AddLogMessage(new LogEintrag
                        { Eintragsdatum = DateTime.Now, Nachricht = e.Message, Warnstufe = "Fehler" });
                }
            }
            catch (Exception ex) {
#if DEBUG
                AddLogMessage(new LogEintrag
                    { Eintragsdatum = DateTime.Now, Nachricht = ex.Message, Warnstufe = "Debug" });
#endif
                AddLogMessage(new LogEintrag
                    { Eintragsdatum = DateTime.Now, Nachricht = ex.Message, Warnstufe = "Fehler" });
            }
        }

        await StopTransaction();
    }

    /// <summary>
    /// exportiert die SuS/Lehrkräfte und Kursdaten für AIX und Moodle; baut dazu die nötigen Strings auf und schreibt diese in CSV-Dateien
    /// </summary>
    /// <param name="folder">Zielordner</param>
    /// <param name="targetSystems">m für Moodle, a für AIX</param>
    /// <param name="whattoexport">s für SuS, l für Lehrkräfte, e für Eltern, k für Kurse</param>
    /// <param name="withPasswort">mit Erstpasswort: true für ja, false für nein</param>
    /// <param name="passwort">das Passwort, welches gesetzt werden soll</param>
    /// <param name="expandfiles">Dateien erweitern: true für ja, false für nein</param>
    /// <param name="nurMoodleSuffix">Soll das Suffix nur für Moodle-Kurse verwendet werden</param>
    /// <param name="kursvorlage">Stringarray mit ShortID aus Moodle für die Vorlagenkurse</param>
    /// <param name="susidliste">Liste mit SuS-IDs</param>
    /// <param name="lulidliste">Liste mit LuL-IDs</param>
    /// <param name="kursliste">Liste mit Kurs-Bezeichnungen</param>
    public async Task<int> ExportToCSV(string folder, string targetSystems, string whattoexport, bool withPasswort,
        string passwort,
        bool expandfiles, bool nurMoodleSuffix, string[] kursvorlage, ReadOnlyCollection<int> susidliste,
        ReadOnlyCollection<int> lulidliste,
        ReadOnlyCollection<string> kursliste) {
        try {
            if (targetSystems.Equals("all")) {
                return await ExportToCSV(folder, "amij", whattoexport, withPasswort, passwort, expandfiles,
                    nurMoodleSuffix,
                    kursvorlage, susidliste,
                    lulidliste, kursliste);
            }

            if (whattoexport.Equals("all")) {
                return await ExportToCSV(folder, targetSystems, "ksle", withPasswort, passwort, expandfiles,
                    nurMoodleSuffix, kursvorlage,
                    susidliste,
                    lulidliste, kursliste);
            }

            if (whattoexport.Contains('e')) {
                withPasswort = true;
            }

            List<string> ausgabeAIXL = [];
            List<string> ausgabeAIXS = [];
            List<string> ausgabeMoodleEinschreibungen = [];
            List<string> ausgabeMoodleKurse = [];
            List<string> ausgabeMoodleUser = [];
            List<string> ausgabeIntern = [
                "kuerzel;nachname;mail_Adresse;pw_temp"
            ];

            var ohne_kursvorlagen = kursvorlage[0].Equals("") && kursvorlage[1].Equals("");
            ausgabeMoodleKurse.Add(ohne_kursvorlagen
                ? "shortname;fullname;idnumber;category_idnumber;format"
                : "shortname;fullname;idnumber;category_idnumber;format;templatecourse");

            if (withPasswort) {
                ausgabeMoodleUser.Add("email;password;username;idnumber;lastname;firstname;cohort1;suspended");
                ausgabeAIXS.Add(
                    "Vorname;Nachname;Klasse;Referenz-ID;Kennwort;Arbeitsgruppen");
                ausgabeAIXL.Add("Vorname;Nachname;Referenz-ID;Kennwort;Arbeitsgruppen");
            }
            else {
                ausgabeMoodleUser.Add("email;username;idnumber;lastname;firstname;cohort1;suspended");
                ausgabeAIXS.Add("Vorname;Nachname;Klasse;Referenz-ID;Arbeitsgruppen");
                ausgabeAIXL.Add("Vorname;Nachname;Referenz-ID;Arbeitsgruppen");
            }

            if (whattoexport.Contains('k') && targetSystems.Contains('m')) {
                ExportKurse(ref ausgabeMoodleKurse, kursliste, kursvorlage);
            }

            if (whattoexport.Contains('s') && (targetSystems.Contains('a') || targetSystems.Contains('m'))) {
                ExportSuS(ref ausgabeMoodleUser, ref ausgabeMoodleEinschreibungen, ref ausgabeAIXS, susidliste,
                    targetSystems, withPasswort, passwort, nurMoodleSuffix);
            }

            if (whattoexport.Contains('e') && targetSystems.Contains('m')) {
                ExportEltern(ref ausgabeMoodleUser, ref ausgabeMoodleEinschreibungen, susidliste);
            }

            if (whattoexport.Contains('l') && (targetSystems.Contains('a') || targetSystems.Contains('m'))) {
                ExportLuL(ref ausgabeMoodleUser, ref ausgabeMoodleEinschreibungen, ref ausgabeAIXL, lulidliste,
                    targetSystems, withPasswort, nurMoodleSuffix);
            }

            if (targetSystems.Contains('i')) {
                foreach (var l in lulidliste) {
                    var lt = await GetLehrkraft(l);
                    ausgabeIntern.Add(
                        $"{lt.Kuerzel};{lt.Nachname};{lt.Mail};{GetTempPasswort(l).Result}");
                }
            }

            if (targetSystems.Contains('j')) {
                ExportJAMF(susidliste, lulidliste, withPasswort, folder, expandfiles);
            }

            if (expandfiles) {
                try {
                    if (targetSystems.Contains('a')) {
                        if (File.Exists($"{folder}/aix_sus.csv")) {
                            var aixSus = (await File.ReadAllLinesAsync($"{folder}/aix_sus.csv")).ToList();
                            aixSus.RemoveAt(0);
                            ausgabeAIXS.AddRange(aixSus);
                            await File.WriteAllLinesAsync($"{folder}/aix_sus.csv", ausgabeAIXS.Distinct().ToList(),
                                Encoding.UTF8);
                        }

                        if (File.Exists($"{folder}/aix_lul.csv")) {
                            var aixLul = (await File.ReadAllLinesAsync($"{folder}/aix_lul.csv")).ToList();
                            aixLul.RemoveAt(0);
                            ausgabeAIXL.AddRange(aixLul);
                            await File.WriteAllLinesAsync($"{folder}/aix_lul.csv", ausgabeAIXL.Distinct().ToList(),
                                Encoding.UTF8);
                        }
                    }

                    if (targetSystems.Contains('m')) {
                        if (File.Exists($"{folder}/mdl_einschreibungen.csv")) {
                            var mdlEin =
                                (await File.ReadAllLinesAsync($"{folder}/mdl_einschreibungen.csv")).ToList();
                            ausgabeMoodleKurse.RemoveAt(0);
                            ausgabeMoodleEinschreibungen.AddRange(mdlEin);
                            await File.WriteAllLinesAsync($"{folder}/mdl_einschreibungen.csv",
                                ausgabeMoodleEinschreibungen.Distinct().ToList(), Encoding.UTF8);
                        }

                        if (File.Exists($"{folder}/mdl_kurse.csv")) {
                            var mdlKurse = (await File.ReadAllLinesAsync($"{folder}/mdl_kurse.csv")).ToList();
                            if (mdlKurse.Count > 0) {
                                mdlKurse.RemoveAt(0);
                            }

                            ausgabeMoodleKurse.AddRange(mdlKurse);
                            await File.WriteAllLinesAsync($"{folder}/mdl_kurse.csv",
                                ausgabeMoodleKurse.Distinct().ToList(),
                                Encoding.UTF8);
                        }

                        if (File.Exists($"{folder}/mdl_nutzer.csv")) {
                            var mdlNutzer = (await File.ReadAllLinesAsync($"{folder}/mdl_nutzer.csv")).ToList();
                            if (mdlNutzer.Count > 0) {
                                mdlNutzer.RemoveAt(0);
                            }

                            ausgabeMoodleUser.AddRange(mdlNutzer);
                            await File.WriteAllLinesAsync($"{folder}/mdl_nutzer.csv",
                                ausgabeMoodleUser.Distinct().ToList(),
                                Encoding.UTF8);
                        }
                    }

                    if (targetSystems.Contains('i')) {
                        if (File.Exists($"{folder}/Lehrerdaten_anschreiben.csv")) {
                            var llgIntern =
                                (await File.ReadAllLinesAsync($"{folder}/Lehrerdaten_anschreiben.csv")).ToList();
                            llgIntern.RemoveAt(0);
                            ausgabeIntern.AddRange(llgIntern);
                            await File.WriteAllLinesAsync($"{folder}/Lehrerdaten_anschreiben.csv",
                                ausgabeIntern.Distinct().ToList(), Encoding.UTF8);
                        }
                    }
                }

                catch (Exception ex) {
#if DEBUG
                    AddLogMessage(new LogEintrag
                        { Eintragsdatum = DateTime.Now, Nachricht = ex.Message, Warnstufe = "Debug" });
#endif
                    return -1;
                }
            }
            else {
                if (targetSystems.Contains('a')) {
                    await File.WriteAllLinesAsync($"{folder}/aix_sus.csv", ausgabeAIXS.Distinct().ToList(),
                        Encoding.UTF8);
                    await File.WriteAllLinesAsync($"{folder}/aix_lul.csv", ausgabeAIXL.Distinct().ToList(),
                        Encoding.UTF8);
                }

                if (targetSystems.Contains('m')) {
                    await File.WriteAllLinesAsync($"{folder}/mdl_einschreibungen.csv",
                        ausgabeMoodleEinschreibungen.Distinct().ToList(), Encoding.UTF8);
                    await File.WriteAllLinesAsync($"{folder}/mdl_kurse.csv", ausgabeMoodleKurse.Distinct().ToList(),
                        Encoding.UTF8);
                    await File.WriteAllLinesAsync($"{folder}/mdl_nutzer.csv", ausgabeMoodleUser.Distinct().ToList(),
                        Encoding.UTF8);
                }

                if (targetSystems.Contains('i')) {
                    await File.WriteAllLinesAsync($"{folder}/Lehrerdaten_anschreiben.csv",
                        ausgabeIntern.Distinct().ToList(),
                        Encoding.UTF8);
                }
            }

            return 1;
        }
        catch (Exception ex) {
#if DEBUG
            AddLogMessage(new LogEintrag
                { Eintragsdatum = DateTime.Now, Nachricht = ex.Message, Warnstufe = "Debug" });
#endif
            return -1;
        }
    }

    /// <summary>
    /// Fügt die Eltern zum Export hinzu
    /// </summary>
    /// <param name="ausgabeMoodleUser"></param>
    /// <param name="ausgabeMoodleEinschreibungen"></param>
    /// <param name="susids"></param>
    private void ExportEltern(ref List<string> ausgabeMoodleUser, ref List<string> ausgabeMoodleEinschreibungen,
        ReadOnlyCollection<int> susids) {
        // header: email;username;idnumber;lastname;firstname;cohort1
        var suffix = GetKursSuffix().Result;
        foreach (var s in susids) {
            var sus = GetSchueler(s).Result;
            if (!sus.IstAktiv) continue;
            var susmail = sus.Mail.Contains(' ') ? sus.Mail.Split(' ')[0] : sus.Mail;
            var schuelerstufe = Tooling.KlasseToStufe(sus.Klasse);
            switch (sus.Zweitaccount) {
                case true when sus.Zweitmail.Contains(','): {
                    if (erprobungsstufe.Contains(schuelerstufe)) {
                        var zweitmails = sus.Zweitmail.Split(',');
                        var zweitmail = zweitmails[0].Trim() != sus.Mail.Trim()
                            ? zweitmails[0].Trim()
                            : zweitmails[1].Trim();
                        ausgabeMoodleUser.Add(string.Join(";",
                            zweitmail,
                            sus.Klasse,
                            DateTime.Now.Year.ToString(),
                            $"{sus.Nutzername}_E1",
                            $"E_{sus.ID}1",
                            $"{sus.Nachname}_Eltern," +
                            sus.Vorname));
                        ausgabeMoodleEinschreibungen.Add(
                            $"add,eltern,E_{sus.ID}1,{sus.Klasse}KL{suffix}");
                        ausgabeMoodleEinschreibungen.Add(
                            $"add,eltern,E_{sus.ID}1,erprobungsstufe{suffix}");
                    }
                    else if (mittelstufe.Contains(schuelerstufe)) {
                        ausgabeMoodleUser.Add(
                            $"{sus.Zweitmail.Split(',')[0]};Klasse{sus.Klasse}{DateTime.Now.Year}!;{sus.Nutzername}_E1;E_{sus.ID}1;{sus.Nachname}_Eltern;{sus.Vorname};eltern");
                        ausgabeMoodleEinschreibungen.Add(
                            $"add,eltern,E_{sus.ID}1,{sus.Klasse}KL{suffix}");
                        ausgabeMoodleEinschreibungen.Add($"add,eltern,E_{sus.ID}1,mittelstufe{suffix}");
                    }

                    break;
                }
                case true when !sus.Zweitmail.Contains(','):
                    AddLogMessage(new LogEintrag {
                        Eintragsdatum = DateTime.Now, Nachricht =
                            $"{sus.Klasse}:{sus.Nachname}, {sus.Vorname} ohne Zweitmail trotz gesetzter Flag",
                        Warnstufe = "Fehler"
                    });
                    break;
            }

            if (erprobungsstufe.Contains(schuelerstufe)) {
                ausgabeMoodleUser.Add(
                    $"{susmail};Klasse{sus.Klasse}{DateTime.Now.Year}!;{sus.Nutzername}_E;E_{sus.ID};{sus.Nachname}_Eltern;{sus.Vorname};eltern");
                ausgabeMoodleEinschreibungen.Add($"add,eltern,E_{sus.ID},{sus.Klasse}KL{suffix}");
                ausgabeMoodleEinschreibungen.Add($"add,eltern,E_{sus.ID},erprobungsstufe{suffix}");
            }
            else if (mittelstufe.Contains(schuelerstufe)) {
                ausgabeMoodleUser.Add(
                    $"{susmail};Klasse{sus.Klasse}{DateTime.Now.Year}!;{sus.Nutzername}_E;E_{sus.ID};{sus.Nachname}_Eltern;{sus.Vorname};eltern");
                ausgabeMoodleEinschreibungen.Add($"add,eltern,E_{sus.ID},{sus.Klasse}KL{suffix}");
                ausgabeMoodleEinschreibungen.Add($"add,eltern,E_{sus.ID},mittelstufe{suffix}");
            }
        }
    }

    /// <summary>
    /// Fügt die SuS zum Export hinzu
    /// </summary>
    /// <param name="ausgabeMoodleUser"></param>
    /// <param name="ausgabeMoodleEinschreibungen"></param>
    /// <param name="ausgabeAIXS"></param>
    /// <param name="susidliste"></param>
    /// <param name="targets"></param>
    /// <param name="withPasswort"></param>
    /// <param name="passwort"></param>
    /// <param name="nurMoodleSuffix"></param>
    private void ExportSuS(ref List<string> ausgabeMoodleUser, ref List<string> ausgabeMoodleEinschreibungen,
        ref List<string> ausgabeAIXS, ReadOnlyCollection<int> susidliste,
        string targets, bool withPasswort, string passwort, bool nurMoodleSuffix) {
        var suffix = GetKursSuffix().Result;
        if (targets != "all" && !targets.Contains('m') && !targets.Contains('a')) return;
        var blacklist = GetM365Blacklist().Result;
        foreach (var sus in susidliste) {
            var s = GetSchueler(sus).Result;
            var schuelerstufe = Tooling.KlasseToStufe(s.Klasse);
            var kListe = "";
            foreach (var kk in GetKurseVonSuS(s.ID).Result) {
                if (string.IsNullOrEmpty(kk.Bezeichnung)) {
                    break;
                }

                if (!s.IstAktiv) continue;
                kListe += $"{kk.Bezeichnung}{kk.Suffix}|";
                if (kk.Fach.Equals("KL") || kk.Fach.Equals("StuBo")) {
                    ausgabeMoodleEinschreibungen.Add($"add,schueler,{s.ID},{kk.Bezeichnung}{kk.Suffix}");
                }
                else {
                    ausgabeMoodleEinschreibungen.Add($"add,student,{s.ID},{kk.Bezeichnung}{kk.Suffix}");
                }

                if (erprobungsstufe.Contains(schuelerstufe)) {
                    ausgabeMoodleEinschreibungen.Add($"add,schueler,{s.ID},erprobungsstufe{suffix}");
                }
                else if (mittelstufe.Contains(schuelerstufe)) {
                    ausgabeMoodleEinschreibungen.Add($"add,schueler,{s.ID},mittelstufe{suffix}");
                }
                else {
                    ausgabeMoodleEinschreibungen.Add($"add,schueler,{s.ID},Stufenkurs{s.Klasse}{suffix}");
                }
            }

            kListe = kListe.TrimEnd('|');
            if (nurMoodleSuffix) {
                kListe = kListe.Replace(suffix, "");
            }

            var susmail = s.Mail.Contains(' ') ? s.Mail.Split(' ')[0] : s.Mail;
            if (withPasswort) {
                var pwd = passwort.Length > 7
                    ? passwort
                    : $"Klasse{s.Klasse}{DateTime.Now.Year}!";
                ausgabeMoodleUser.Add(
                    $"{susmail};{pwd.Replace(" ", "")};{s.Nutzername};{s.ID};{s.Nachname};{s.Vorname};schueler;{Convert.ToInt32(!s.IstAktiv)}");
                if (!blacklist.Contains(s.ID) && targets.Contains('a') && s.IstAktiv) {
                    ausgabeAIXS.Add($"{s.Vorname};{s.Nachname};{s.Klasse};{s.ID};{pwd.Replace(" ", "")};{kListe}");
                }
            }
            else {
                ausgabeMoodleUser.Add(
                    $"{susmail};{s.Nutzername};{s.ID};{s.Nachname};{s.Vorname};schueler;{Convert.ToInt32(!s.IstAktiv)}");
                if (!blacklist.Contains(s.ID) && targets.Contains('a') && s.IstAktiv) {
                    ausgabeAIXS.Add($"{s.Vorname};{s.Nachname};{s.Klasse};{s.ID};{kListe}");
                }
            }
        }
    }

    /// <summary>
    /// Fügt die Lehrkräfte zum Export hinzu
    /// </summary>
    /// <param name="ausgabeMoodleUser"></param>
    /// <param name="ausgabeMoodleEinschreibungen"></param>
    /// <param name="ausgabeAIXL"></param>
    /// <param name="lulidliste"></param>
    /// <param name="targets"></param>
    /// <param name="withPasswort"></param>
    /// <param name="nurMoodleSuffix"></param>
    private void ExportLuL(ref List<string> ausgabeMoodleUser, ref List<string> ausgabeMoodleEinschreibungen,
        ref List<string> ausgabeAIXL, ReadOnlyCollection<int> lulidliste,
        string targets, bool withPasswort, bool nurMoodleSuffix) {
        var suffix = GetKursSuffix().Result;
        foreach (var l in lulidliste) {
            var lt = GetLehrkraft(l).Result;
            var kListe = "";
            var fakultas = lt.Fakultas.Split(',');
            var fak = fakultas.Aggregate("", (current, fa) => $"{current}|^Fako {fa}");

            fak += fak.Replace("^", "");
            fak = fak.TrimStart('|');
            foreach (var kurs in GetKurseVonLuL(lt.ID).Result) {
                if (string.IsNullOrEmpty(kurs.Bezeichnung)) continue;
                if (lt.IstAktiv) {
                    if (kurs.Bezeichnung.Contains("Jahrgangsstufenkonferenz")) {
                        var stufenleitungen = GetOberstufenleitung(kurs.Stufe).Result;
                        var rolle = stufenleitungen.Contains(lt) ||
                                    GetSettings().Result.Oberstufenkoordination.Contains(lt.Kuerzel)
                            ? "editingteacher"
                            : "student";
                        ausgabeMoodleEinschreibungen.Add($"add,{rolle},{lt.ID},{kurs.Bezeichnung}{kurs.Suffix}");
                    }
                    else if (kurs.IstKurs) {
                        ausgabeMoodleEinschreibungen.Add($"add,editingteacher,{lt.ID},{kurs.Bezeichnung}{kurs.Suffix}");
                    }
                    else {
                        ausgabeMoodleEinschreibungen.Add($"add,editingteacher,{lt.ID},{kurs.Bezeichnung}{kurs.Suffix}");
                    }
                }

                if (kurs.Bezeichnung.Length > 20) continue;
                kListe += $"^{kurs.Bezeichnung}{kurs.Suffix}|";
            }

            ausgabeMoodleEinschreibungen.AddRange(lt.Fakultas.Split(',')
                .Select(fach => $"add,editingteacher,{lt.ID},FS_{fach}"));

            if (kListe == "^|") {
                kListe = "";
            }

            if (nurMoodleSuffix && kListe != "") {
                kListe = kListe.Replace(suffix, "");
            }

            if (withPasswort) {
                ausgabeMoodleUser.Add(
                    $"{lt.Mail};{GetTempPasswort(lt.ID).Result};{lt.Kuerzel};{lt.ID};{lt.Nachname};{lt.Vorname};lehrer;{Convert.ToInt32(!lt.IstAktiv)}");
                if (targets.Contains('a') && lt.IstAktiv) {
                    ausgabeAIXL.Add(
                        $"{lt.Vorname};{lt.Nachname};{lt.ID};{GetTempPasswort(lt.ID).Result};*|{kListe}{fak}");
                }
            }
            else {
                ausgabeMoodleUser.Add(
                    $"{lt.Mail};{lt.Kuerzel};{lt.ID};{lt.Nachname};{lt.Vorname};lehrer;{Convert.ToInt32(!lt.IstAktiv)}");
                if (targets.Contains('a') && lt.IstAktiv) {
                    ausgabeAIXL.Add($"{lt.Vorname};{lt.Nachname};{lt.ID};*|{kListe}{fak}");
                }
            }
        }
    }

    /// <summary>
    /// Fügt die Kurse zum Export hinzu
    /// </summary>
    /// <param name="ausgabeMoodleKurse"></param>
    /// <param name="kursBez"></param>
    /// <param name="kursvorlage"></param>
    private void ExportKurse(ref List<string> ausgabeMoodleKurse, ReadOnlyCollection<string> kursBez,
        string[] kursvorlage) {
        var sekI = erprobungsstufe.Concat(mittelstufe).ToArray();
        var ohne_kursvorlagen = kursvorlage[0].Equals("") && kursvorlage[1].Equals("");
        foreach (var kurs in kursBez) {
            if (kurs.EndsWith('-')) continue;
            var k = GetKurs(kurs.Split(';')[0]).Result;

            if (ohne_kursvorlagen) {
                if (k.Bezeichnung.Contains("Erprobungsstufe") || k.Bezeichnung.Contains("Mittelstufe") ||
                    k.Bezeichnung.Contains("Einführungsphase") || k.Bezeichnung.Contains("Qualifikationsphase")) {
                    ausgabeMoodleKurse.Add(
                        $"{k.Bezeichnung}{k.Suffix};{k.Bezeichnung} SJ{k.Suffix.Substring(1, 2)}/{k.Suffix.Substring(3, 2)};{k.Bezeichnung}{k.Suffix};SJ{k.Suffix};tiles");
                }
                else if (k.Bezeichnung.Contains("konferenz")) {
                    ausgabeMoodleKurse.Add(
                        $"{k.Bezeichnung}{k.Suffix};{k.Bezeichnung} SJ{k.Suffix.Substring(1, 2)}/{k.Suffix.Substring(3, 2)};{k.Bezeichnung}{k.Suffix};lehrkraefte;tiles");
                }

                if (k.IstKurs) {
                    ausgabeMoodleKurse.Add(
                        sekI.Contains(k.Stufe)
                            ? $"{k.Bezeichnung}{k.Suffix};{k.Klasse} {GetLangeFachbezeichnung(k.Fach).Result}-{k.Art.Substring(k.Art.Length - 1, 1)} SJ{k.Suffix.Substring(1, 2)}/{k.Suffix.Substring(3, 2)};{k.Bezeichnung}{k.Suffix};stufe_{k.Stufe}{k.Suffix};tiles"
                            : $"{k.Bezeichnung}{k.Suffix};{k.Klasse} {GetLangeFachbezeichnung(k.Fach).Result}-{k.Art} SJ{k.Suffix.Substring(1, 2)}/{k.Suffix.Substring(3, 2)};{k.Bezeichnung}{k.Suffix};stufe_{k.Stufe}{k.Suffix};tiles");
                }
                else {
                    ausgabeMoodleKurse.Add(
                        $"{k.Bezeichnung}{k.Suffix};{k.Klasse} {GetLangeFachbezeichnung(k.Fach).Result} SJ{k.Suffix.Substring(1, 2)}/{k.Suffix.Substring(3, 2)};{k.Bezeichnung}{k.Suffix};{k.Klasse}{k.Suffix};tiles");
                }
            }
            else {
                var strkursvorlage = k.Bezeichnung.Contains("KL") ? kursvorlage[0] : kursvorlage[1];
                if (k.Bezeichnung.Contains("Erprobungsstufe") || k.Bezeichnung.Contains("Mittelstufe") ||
                    k.Bezeichnung.Contains("Einführungsphase") || k.Bezeichnung.Contains("Qualifikationsphase")) {
                    ausgabeMoodleKurse.Add(
                        $"{k.Bezeichnung}{k.Suffix};{k.Bezeichnung} SJ{k.Suffix.Substring(1, 2)}/{k.Suffix.Substring(3, 2)};{k.Bezeichnung}{k.Suffix};SJ{k.Suffix};tiles;{strkursvorlage}");
                }
                else if (k.Bezeichnung.Contains("konferenz")) {
                    ausgabeMoodleKurse.Add(
                        $"{k.Bezeichnung}{k.Suffix};{k.Bezeichnung} SJ{k.Suffix.Substring(1, 2)}/{k.Suffix.Substring(3, 2)};{k.Bezeichnung}{k.Suffix};lehrkraefte;tiles;{strkursvorlage}");
                }

                if (k.IstKurs) {
                    ausgabeMoodleKurse.Add(
                        sekI.Contains(k.Stufe)
                            ? $"{k.Bezeichnung}{k.Suffix};{k.Klasse} {GetLangeFachbezeichnung(k.Fach).Result}-{k.Art.Substring(k.Art.Length - 1, 1)} SJ{k.Suffix.Substring(1, 2)}/{k.Suffix.Substring(3, 2)};{k.Bezeichnung}{k.Suffix};stufe_{k.Stufe}{k.Suffix};tiles;{strkursvorlage}"
                            : $"{k.Bezeichnung}{k.Suffix};{k.Klasse} {GetLangeFachbezeichnung(k.Fach).Result}-{k.Art} SJ{k.Suffix.Substring(1, 2)}/{k.Suffix.Substring(3, 2)};{k.Bezeichnung}{k.Suffix};stufe_{k.Stufe}{k.Suffix};tiles;{strkursvorlage}");
                }
                else {
                    ausgabeMoodleKurse.Add(
                        $"{k.Bezeichnung}{k.Suffix};{k.Klasse} {GetLangeFachbezeichnung(k.Fach).Result} SJ{k.Suffix.Substring(1, 2)}/{k.Suffix.Substring(3, 2)};{k.Bezeichnung}{k.Suffix};{k.Klasse}{k.Suffix};tiles;{strkursvorlage}");
                }
            }
        }
    }

    /// <summary>
    /// exportiert die angegebenen Nutzer nach JAMF
    /// </summary>
    /// <param name="susidliste"></param>
    /// <param name="lulidliste"></param>
    /// <param name="withPasswort"></param>
    /// <param name="folder"></param>
    /// <param name="expand"></param>
    /// <exception cref="NotImplementedException"></exception>
    private async void ExportJAMF(ReadOnlyCollection<int> susidliste,
        ReadOnlyCollection<int> lulidliste, bool withPasswort, string folder, bool expand) {
        try {
            var blacklist = new[] { "Qualifikations", "Einführungs", "KL", "StuBo", "Phase", "stufe", "Jahrgang" };
            var kurs_wl = GetKursBezListe().Result.ToList();
            foreach (var bl_entry in blacklist) {
                kurs_wl.RemoveAll(x =>
                    x.StartsWith(bl_entry) || x.EndsWith(bl_entry) || !Jamfstufen.Contains(GetKurs(x).Result.Stufe));
            }

            List<string> ausgabeSuSJamf = [
                "Username;Email;FirstName;LastName;SerialNumber;Groups" +
                (withPasswort ? ";Password" : "")
            ];
            List<string> ausgabeLuLJamf = [
                "Username;Email;FirstName;LastName;SerialNumber;Groups;TeacherGroups" +
                (withPasswort ? ";Password" : "")
            ];
            List<string> ausgabeTeacherGroupsJAMF = ["TeacherGroup;Usernames"];
            ausgabeSuSJamf.AddRange(from sus_id in susidliste
                select GetSchueler(sus_id).Result
                into sus
                where sus.AllowJAMF
                where sus.IstAktiv
                where Jamfstufen.Contains(sus.GetStufe()) && sus.Seriennummer != ""
                let kbez_liste = GetKurseVonSuS(sus.ID).Result.Where(k => kurs_wl.Contains(k.Bezeichnung)).ToList()
                    .Select(k => k.Bezeichnung).ToList()
                select string.Join(";", sus.Nutzername, !string.IsNullOrEmpty(sus.Aixmail) ? sus.Aixmail : sus.Mail,
                    sus.Vorname, sus.Nachname, sus.Seriennummer, string.Join(',', kbez_liste),
                    withPasswort ? $"Klasse{sus.Klasse}{DateTime.Now.Year}!" : ""));
            ausgabeLuLJamf.AddRange(from lulid in lulidliste
                let lul = GetLehrkraft(lulid).Result
                where lul.IstAktiv
                where lul.Seriennummer != ""
                let kurse = GetKurseVonLuL(lulid)
                    .Result.Where(x =>
                        !string.IsNullOrEmpty(x.Fach) && jamfstufen.Contains(x.Stufe) &&
                        kurs_wl.Contains(x.Bezeichnung))
                    .Select(x => x.Bezeichnung)
                select string.Join(";", lul.Kuerzel, lul.Mail, lul.Vorname, lul.Nachname, lul.Seriennummer,
                    "Lehrer-605",
                    string.Join(',', kurse),
                    withPasswort ? GetTempPasswort(lulid).Result : ""));
            foreach (var stufe in Jamfstufen) {
                var kurse_in_stufe = GetKurseAusStufe(stufe).Result.ToList();
                kurse_in_stufe.RemoveAll(k => !kurs_wl.Contains(k.Bezeichnung));
                ausgabeTeacherGroupsJAMF.AddRange(from kurs in kurse_in_stufe
                    let lul_aus_kurs = GetLuLAusKurs(kurs.Bezeichnung).Result.Where(l => l.IstAktiv)
                    select $"{kurs.Bezeichnung};" + string.Join(',', lul_aus_kurs.Select(l => l.Kuerzel)));
            }

            if (File.Exists($"{folder}/jamf_sus.csv") && expand) {
                var jamf_sus =
                    (await File.ReadAllLinesAsync($"{folder}/jamf_sus.csv")).ToList();
                jamf_sus.RemoveAt(0);
                ausgabeSuSJamf.AddRange(jamf_sus);
                await File.WriteAllLinesAsync($"{folder}/jamf_sus.csv",
                    ausgabeSuSJamf.Distinct().ToList(), Encoding.UTF8);
            }
            else {
                await File.WriteAllLinesAsync($"{folder}/jamf_sus.csv",
                    ausgabeSuSJamf.Distinct().ToList(),
                    Encoding.UTF8);
            }

            if (File.Exists($"{folder}/jamf_lul.csv") && expand) {
                var jamf_lul =
                    (await File.ReadAllLinesAsync($"{folder}/jamf_lul.csv")).ToList();
                jamf_lul.RemoveAt(0);
                ausgabeLuLJamf.AddRange(jamf_lul);
                await File.WriteAllLinesAsync($"{folder}/jamf_lul.csv",
                    ausgabeLuLJamf.Distinct().ToList(), Encoding.UTF8);
            }
            else {
                await File.WriteAllLinesAsync($"{folder}/jamf_lul.csv",
                    ausgabeLuLJamf.Distinct().ToList(),
                    Encoding.UTF8);
            }

            if (File.Exists($"{folder}/jamf_teacher_groups.csv") && expand) {
                var teacher_groups =
                    (await File.ReadAllLinesAsync($"{folder}/jamf_teacher_groups.csv")).ToList();
                teacher_groups.RemoveAt(0);
                ausgabeTeacherGroupsJAMF.AddRange(teacher_groups);
                await File.WriteAllLinesAsync($"{folder}/jamf_teacher_groups.csv",
                    ausgabeTeacherGroupsJAMF.Distinct().ToList(), Encoding.UTF8);
            }
            else {
                await File.WriteAllLinesAsync($"{folder}/jamf_teacher_groups.csv",
                    ausgabeTeacherGroupsJAMF.Distinct().ToList(),
                    Encoding.UTF8);
            }
        }
        catch (Exception e) {
            AddLogMessage(new LogEintrag {
                Eintragsdatum = DateTime.Now, Warnstufe = "Debug",
                Nachricht = e.StackTrace ?? "unbekannter Fehler beim JAMF-Export"
            });
        }
    }

    private async Task<ReadOnlyCollection<Kurs>> GetKurseAusStufe(string stufe) {
        List<Kurs> klist = [];
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT bez FROM kurse WHERE stufe = $stufe;";
        sqliteCmd.Parameters.AddWithValue("$stufe", stufe);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            klist.Add(GetKurs(sqliteDatareader.GetString(0)).Result);
        }

        return new ReadOnlyCollection<Kurs>(klist);
    }

    /// <summary>
    /// gibt die die Gegenüberstellung der Fächer in Kurz- und Langschreibweise als Liste zurück
    /// </summary>
    /// <returns>String-Liste der Schreibweise, pro Zeile ein Fach mit ;-getrennt </returns>
    public async Task<ReadOnlyCollection<string>> GetFachersatz() {
        List<string> flist = [];
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT kurzfach,langfach FROM fachersatz;";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            var returnstr = "";
            for (var i = 0; i < sqliteDatareader.FieldCount; i++) {
                returnstr += $"{sqliteDatareader.GetString(i)};";
            }

            flist.Add(returnstr);
        }

        return new ReadOnlyCollection<string>(flist);
    }

    public async Task<List<FaKo>> GetFaKos() {
        var result = new List<FaKo>();
        var FaVos = GetFavos().Result.Where(l => l.Favo != "").ToList();
        var SFaVos = GetFavos().Result.Where(l => l.SFavo != "").ToList();
        var lulcache = await GetLehrkraftListe();
        foreach (var favo in FaVos) {
            result.AddRange(from fach in favo.Favo.Split(',')
                where !string.IsNullOrEmpty(fach)
                let sfavos = SFaVos.Where(l => l.SFavo.Split(',').Contains(fach)).ToList()
                let favomitglieder = lulcache.Where(l => l.Fakultas.Split(',').Contains(fach)).ToList()
                select new FaKo(fach, favo, sfavos.Count > 0 ? sfavos.First() : new Lehrkraft(),
                    favomitglieder
                ));
        }

        result.Sort();
        return result;
    }

    /// <summary>
    /// gibt den Pfad zur Datenbankdatei zurück
    /// </summary>
    public string GetFilePath() {
        return _dbpath;
    }

    /// <summary>
    /// gibt die Informationen Bezeichnung, Fach, Klasse, Stufe, Kurssuffix und istKurs zur übergebenen Kursbezeichnung zurück
    /// </summary>
    /// <param name="kbez"></param>
    public async Task<Kurs> GetKurs(string kbez) {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT bez,fach,klasse,stufe,suffix,istkurs,bemerkung FROM kurse WHERE bez = $kbez;";
        sqliteCmd.Parameters.AddWithValue("$kbez", kbez);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        Kurs retKurs = new();
        while (sqliteDatareader.Read()) {
            retKurs.Bezeichnung = sqliteDatareader.GetString(0);
            retKurs.Fach = sqliteDatareader.GetString(1);
            retKurs.Klasse = sqliteDatareader.GetString(2);
            retKurs.Stufe = sqliteDatareader.GetString(3);
            retKurs.Suffix = sqliteDatareader.GetString(4);
            retKurs.IstKurs = Convert.ToBoolean(sqliteDatareader.GetInt32(5));
            retKurs.Art = retKurs.IstKurs ? "PUT" : "PUK";
            retKurs.Bemerkung = sqliteDatareader.GetString(6);

            if (retKurs is { IstKurs: true, Bezeichnung.Length: > 3 }) {
                retKurs.Art = retKurs.Bezeichnung.Substring(retKurs.Bezeichnung.Length - 3, 3);
            }
        }

        return retKurs;
    }

    /// <summary>
    /// gibt alle Kursbezeichnungen zurück
    /// </summary>
    public async Task<ReadOnlyCollection<string>> GetKursBezListe() {
        List<string> klist = [];
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT bez FROM kurse;";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            klist.Add(sqliteDatareader.GetString(0));
        }

        return new ReadOnlyCollection<string>(klist);
    }

    /// <summary>
    /// gibt den vollständigen Inhalt der Tabelle kurse in der Reihenfolge Bezeichnung, Fach, Klasse, Stufe, Kurssuffix und istKurs zurück
    /// </summary>
    public async Task<ReadOnlyCollection<Kurs>> GetKursListe() {
        List<Kurs> kliste = [];
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT bez,fach,klasse,stufe,suffix,istkurs,bemerkung FROM kurse;";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            Kurs retKurs = new() {
                Bezeichnung = sqliteDatareader.GetString(0),
                Fach = sqliteDatareader.GetString(1),
                Klasse = sqliteDatareader.GetString(2),
                Stufe = sqliteDatareader.GetString(3),
                Suffix = sqliteDatareader.GetString(4),
                IstKurs = Convert.ToBoolean(sqliteDatareader.GetInt32(5)),
                Bemerkung = sqliteDatareader.GetString(6)
            };
            retKurs.Art = retKurs.IstKurs ? "PUT" : "PUK";
            if (retKurs is { IstKurs: true, Bezeichnung.Length: > 3 }) {
                retKurs.Art = retKurs.Bezeichnung.Substring(retKurs.Bezeichnung.Length - 3, 3);
            }

            kliste.Add(retKurs);
        }

        return new ReadOnlyCollection<Kurs>(kliste);
    }

    /// <summary>
    /// gibt das globale Kurssuffix zurück
    /// </summary>
    /// <returns>String Kurssuffix</returns>
    private async Task<string> GetKursSuffix() {
        return GetSettings().Result.Kurssuffix;
    }

    /// <summary>
    /// gibt die Kurse der Lehrperson als Liste zurück
    /// </summary>
    /// <param name="lulid"></param>
    /// <returns>String-Liste der Kursbezeichnungen </returns>
    public async Task<ReadOnlyCollection<Kurs>> GetKurseVonLuL(int lulid) {
        List<Kurs> kliste = [];
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT kursbez,fach,klasse,stufe,suffix,istkurs,bemerkung FROM unterrichtet JOIN kurse ON kursbez=bez WHERE lehrerid = $lulid;";
        sqliteCmd.Parameters.AddWithValue("lulid", lulid);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            Kurs retKurs = new() {
                Bezeichnung = sqliteDatareader.GetString(0),
                Fach = sqliteDatareader.GetString(1),
                Klasse = sqliteDatareader.GetString(2),
                Stufe = sqliteDatareader.GetString(3),
                Suffix = sqliteDatareader.GetString(4),
                IstKurs = Convert.ToBoolean(sqliteDatareader.GetInt32(5)),
                Art = Convert.ToBoolean(sqliteDatareader.GetInt32(5)) ? "PUT" : "PUK",
                Bemerkung = sqliteDatareader.GetString(6),
            };
            kliste.Add(retKurs);
        }

        return new ReadOnlyCollection<Kurs>(kliste);
    }

    /// <summary>
    /// gibt die SuS-Kurse als Liste zurück
    /// </summary>
    /// <param name="susid"></param>
    /// <returns>String-Liste der Kursbezeichnungen </returns>
    public async Task<ReadOnlyCollection<Kurs>> GetKurseVonSuS(int susid) {
        List<Kurs> kliste = [];
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT kursbez,fach,klasse,stufe,suffix,istkurs,bemerkung FROM nimmtteil JOIN kurse ON kursbez=bez WHERE schuelerid = $susid;";
        sqliteCmd.Parameters.AddWithValue("$susid", susid);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            Kurs retKurs = new() {
                Bezeichnung = sqliteDatareader.GetString(0),
                Fach = sqliteDatareader.GetString(1),
                Klasse = sqliteDatareader.GetString(2),
                Stufe = sqliteDatareader.GetString(3),
                Suffix = sqliteDatareader.GetString(4),
                IstKurs = Convert.ToBoolean(sqliteDatareader.GetInt32(5)),
                Art = Convert.ToBoolean(sqliteDatareader.GetInt32(5)) ? "PUT" : "PUK",
                Bemerkung = sqliteDatareader.GetString(6),
            };
            kliste.Add(retKurs);
        }

        return new ReadOnlyCollection<Kurs>(kliste);
    }

    /// <summary>
    /// gibt die Informationen ID, Nachname, Vorname, Mail, Kürzel und Fakultas der Lehrkraft zur übergebenen ID zurück
    /// </summary>
    /// <param name="id"></param>
    public async Task<Lehrkraft> GetLehrkraft(int id) {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT id,nachname,vorname,mail,kuerzel,fakultas,pwtemp,favo,sfavo,aktiv, seriennummer,bemerkung FROM lehrkraft WHERE id = $id;";
        sqliteCmd.Parameters.AddWithValue("$id", id);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        Lehrkraft lehrkraft = new();
        while (sqliteDatareader.Read()) {
            lehrkraft.ID = sqliteDatareader.GetInt32(0);
            lehrkraft.Nachname = sqliteDatareader.GetString(1);
            lehrkraft.Vorname = sqliteDatareader.GetString(2);
            lehrkraft.Mail = sqliteDatareader.GetString(3);
            lehrkraft.Kuerzel = sqliteDatareader.GetString(4);
            lehrkraft.Fakultas = sqliteDatareader.GetString(5);
            lehrkraft.Pwttemp = sqliteDatareader.GetString(6);
            lehrkraft.Favo = sqliteDatareader.GetString(7);
            lehrkraft.SFavo = sqliteDatareader.GetString(8);
            lehrkraft.IstAktiv = sqliteDatareader.GetBoolean(9);
            lehrkraft.Seriennummer = sqliteDatareader.GetString(10);
            lehrkraft.Bemerkung = sqliteDatareader.GetString(11);
        }

        return lehrkraft;
    }

    /// <summary>
    /// gibt die Informationen ID, Nachname, Vorname, Mail, Kürzel und Fakultas der Lehrkraft zum übergebenen Kürzel zurück
    /// </summary>
    /// <param name="kuerzel"></param>
    public async Task<Lehrkraft> GetLehrkraft(string kuerzel) {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT id,nachname,vorname,mail,kuerzel,fakultas,pwtemp,favo, sfavo,aktiv,seriennummer,bemerkung FROM lehrkraft WHERE kuerzel = $kuerzel;";
        sqliteCmd.Parameters.AddWithValue("$kuerzel", kuerzel);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        Lehrkraft lehrkraft = new();
        while (sqliteDatareader.Read()) {
            lehrkraft.ID = sqliteDatareader.GetInt32(0);
            lehrkraft.Nachname = sqliteDatareader.GetString(1);
            lehrkraft.Vorname = sqliteDatareader.GetString(2);
            lehrkraft.Mail = sqliteDatareader.GetString(3);
            lehrkraft.Kuerzel = sqliteDatareader.GetString(4);
            lehrkraft.Fakultas = sqliteDatareader.GetString(5);
            lehrkraft.Pwttemp = sqliteDatareader.GetString(6);
            lehrkraft.Favo = sqliteDatareader.GetString(7);
            lehrkraft.SFavo = sqliteDatareader.GetString(8);
            lehrkraft.IstAktiv = sqliteDatareader.GetBoolean(9);
            lehrkraft.Seriennummer = sqliteDatareader.GetString(10);
            lehrkraft.Bemerkung = sqliteDatareader.GetString(11);
        }

        return lehrkraft;
    }

    /// <summary>
    /// gibt die IDs aller Lehrkräfte zurück
    /// </summary>
    public async Task<ReadOnlyCollection<int>> GetLehrerIDListe() {
        List<int> llist = [];
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT id FROM lehrkraft;";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            llist.Add(sqliteDatareader.GetInt32(0));
        }

        return new ReadOnlyCollection<int>(llist);
    }

    /// <summary>
    /// gibt den vollständigen Inhalt der Tabelle lehrer in der Reihenfolge ID, Nachname, Vorname, Mail, Kürzel und Fakultas zurück
    /// </summary>
    public async Task<ReadOnlyCollection<Lehrkraft>> GetLehrkraftListe() {
        List<Lehrkraft> llist = [];
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT id,nachname,vorname,mail,kuerzel,fakultas,pwtemp,favo,sfavo,aktiv,seriennummer,bemerkung FROM lehrkraft;";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            Lehrkraft lehrkraft = new() {
                ID = sqliteDatareader.GetInt32(0),
                Nachname = sqliteDatareader.GetString(1),
                Vorname = sqliteDatareader.GetString(2),
                Mail = sqliteDatareader.GetString(3),
                Kuerzel = sqliteDatareader.GetString(4),
                Fakultas = sqliteDatareader.GetString(5),
                Pwttemp = sqliteDatareader.GetString(6),
                Favo = sqliteDatareader.GetString(7),
                SFavo = sqliteDatareader.GetString(8),
                IstAktiv = sqliteDatareader.GetBoolean(9),
                Seriennummer = sqliteDatareader.GetString(10),
                Bemerkung = sqliteDatareader.GetString(11)
            };
            llist.Add(lehrkraft);
        }

        return new ReadOnlyCollection<Lehrkraft>(llist);
    }

    /// <summary>
    /// gibt die Log-Meldungen zurück
    /// </summary>
    /// <returns>Log-Entrag-Liste der Nachrichten </returns>
    public async Task<ReadOnlyCollection<LogEintrag>> GetLog() {
        List<LogEintrag> logentries = [];
        if (File.Exists(log.Settings.LogFilename)) {
            logentries.AddRange(from line in File.ReadAllLinesAsync(log.Settings.LogFilename).Result
                select line.Split(' ')
                into split_line
                let date = split_line[0]
                let time = split_line[1]
                let level = split_line[3]
                let message = string.Join(" ", split_line[4..])
                where !string.IsNullOrEmpty(date) && !string.IsNullOrEmpty(time)
                select new LogEintrag
                    { Eintragsdatum = DateTime.Parse($"{date} {time}"), Warnstufe = level, Nachricht = message });
            return logentries.AsReadOnly();
        }

        _logpath = log.Settings.LogFilename =
            _dbpath == ":memory:"
                ? Path.Combine(Path.GetTempPath(), Path.ChangeExtension($"StS_{Guid.NewGuid()}_tmp", "log"))
                : _dbpath.Replace("sqlite", "log");
        return new ReadOnlyCollection<LogEintrag>(logentries);
    }

    /// <summary>
    /// gibt die Log-Meldungen zurück
    /// </summary>
    /// <param name="stufe">Das Log-Level (Info, Hinweis oder Fehler)</param>
    /// <returns>String-Liste der Nachrichten </returns>
    public async Task<ReadOnlyCollection<LogEintrag>> GetLog(string stufe) {
        List<LogEintrag> logentries = [];
        if (!File.Exists(_logpath)) return logentries.AsReadOnly();
        var entries = await File.ReadAllLinesAsync(_logpath);
        logentries.AddRange(entries.Select(entry => entry.Split(' ')).Select(logentry => new LogEintrag {
            Warnstufe = logentry[0], Eintragsdatum = DateTime.Parse(logentry[1]),
            Nachricht = string.Join(" ", logentry[2..])
        }));
        return logentries.Where(eintrag => eintrag.Warnstufe == stufe).ToList().AsReadOnly();
    }

    /// <summary>
    /// gibt den Langnamen zur übergebenen Kurzform des Faches zurück
    /// </summary>
    /// <param name="shortsubject"></param>
    /// <returns>String Langfach-Bezeichnung</returns>
    private async Task<string> GetLangeFachbezeichnung(string shortsubject) {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT langfach FROM fachersatz WHERE kurzfach = $shortsubject;";
        sqliteCmd.Parameters.AddWithValue("$shortsubject", shortsubject);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            return sqliteDatareader.GetString(0);
        }

        return shortsubject;
    }

    /// <summary>
    /// gibt die LuL im Kurs als Liste zurück
    /// </summary>
    /// <param name="kbez"></param>
    /// <returns>Liste der LuL des Kurses</returns>
    public async Task<ReadOnlyCollection<Lehrkraft>> GetLuLAusKurs(string kbez) {
        List<Lehrkraft> lliste = [];
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT lehrerid FROM unterrichtet WHERE kursbez = $kbez;";
        sqliteCmd.Parameters.AddWithValue("$kbez", kbez);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            lliste.Add(await GetLehrkraft(sqliteDatareader.GetInt32(0)));
        }

        return new ReadOnlyCollection<Lehrkraft>(lliste);
    }

    /// <summary>
    /// gibt die Lehrkäfte von SuS zurück
    /// </summary>
    /// <param name="susid"></param>
    /// <returns>Liste der LuL des Schülers/der SuS</returns>
    public async Task<ReadOnlyCollection<Lehrkraft>> GetLuLvonSuS(int susid) {
        List<Lehrkraft> lliste = [];
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT unterrichtet.lehrerid FROM unterrichtet JOIN nimmtteil ON nimmtteil.kursbez = unterrichtet.kursbez WHERE schuelerid = $susid;";
        sqliteCmd.Parameters.AddWithValue("$susid", susid);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            for (var i = 0; i < sqliteDatareader.FieldCount; i++) {
                lliste.Add(await GetLehrkraft(sqliteDatareader.GetInt32(0)));
            }
        }

        return new ReadOnlyCollection<Lehrkraft>(lliste);
    }

    /// <summary>
    /// gibt die LuL der Stufe zurück
    /// </summary>
    /// <param name="stufe"></param>
    /// <returns>Interger-Liste der LuL-IDs</returns>
    public async Task<ReadOnlyCollection<Lehrkraft>> GetLuLAusStufe(string stufe) {
        List<Lehrkraft> lliste = [];
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT DISTINCT unterrichtet.lehrerid FROM unterrichtet WHERE kursbez LIKE $stufe;";
        sqliteCmd.Parameters.AddWithValue("$stufe", $"{stufe}%");
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            for (var i = 0; i < sqliteDatareader.FieldCount; i++) {
                lliste.Add(await GetLehrkraft(sqliteDatareader.GetInt32(0)));
            }
        }

        return new ReadOnlyCollection<Lehrkraft>(lliste);
    }

    /// <summary>
    /// gibt die Informationen ID, Nachname, Vorname, Mail, Klasse, Nutzername, aixmail, zweitaccount(1/0) und zweitmailadresse von SuS zur übergebenen ID zurück
    /// </summary>
    /// <param name="id"></param>
    public async Task<SuS> GetSchueler(int id) {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT id,nachname,vorname,mail,klasse,nutzername,aixmail,zweitaccount,zweitmail, m365, aktiv, seriennummer, jamf, bemerkung FROM schueler WHERE id = $id;";
        sqliteCmd.Parameters.AddWithValue("$id", id);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        SuS schuelerin = new();
        while (sqliteDatareader.Read()) {
            schuelerin.ID = sqliteDatareader.GetInt32(0);
            schuelerin.Nachname = sqliteDatareader.GetString(1);
            schuelerin.Vorname = sqliteDatareader.GetString(2);
            schuelerin.Mail = sqliteDatareader.GetString(3);
            schuelerin.Klasse = sqliteDatareader.GetString(4);
            schuelerin.Nutzername = sqliteDatareader.GetString(5);
            schuelerin.Aixmail = sqliteDatareader.GetString(6);
            schuelerin.Zweitaccount = Convert.ToBoolean(sqliteDatareader.GetInt32(7));
            schuelerin.Zweitmail = sqliteDatareader.GetString(8);
            schuelerin.HasM365Account = Convert.ToBoolean(sqliteDatareader.GetInt32(9));
            schuelerin.IstAktiv = sqliteDatareader.GetBoolean(10);
            schuelerin.Seriennummer = sqliteDatareader.GetString(11);
            schuelerin.AllowJAMF = sqliteDatareader.GetBoolean(12);
            schuelerin.Bemerkung = sqliteDatareader.GetString(13);
        }

        return schuelerin;
    }

    /// <summary>
    /// gibt die Informationen ID, Nachname, Vorname, Mail, Klasse und Nutzername von SuS zur übergebenen Kombination aus Vor- und Nachname zurück
    /// </summary>
    /// <param name="vorname"></param>
    /// <param name="nachname"></param>
    public async Task<List<SuS>> GetSchueler(string vorname, string nachname) {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT id,nachname,vorname,mail,klasse,nutzername,aixmail,zweitaccount,zweitmail, m365, aktiv, seriennummer, jamf,bemerkung FROM schueler WHERE vorname LIKE $vorname AND nachname = $nachname;";
        sqliteCmd.Parameters.AddWithValue("$vorname", vorname);
        sqliteCmd.Parameters.AddWithValue("$nachname", nachname);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        var susliste = new List<SuS>();
        while (sqliteDatareader.Read()) {
            SuS schuelerin = new() {
                ID = sqliteDatareader.GetInt32(0),
                Nachname = sqliteDatareader.GetString(1),
                Vorname = sqliteDatareader.GetString(2),
                Mail = sqliteDatareader.GetString(3),
                Klasse = sqliteDatareader.GetString(4),
                Nutzername = sqliteDatareader.GetString(5),
                Aixmail = sqliteDatareader.GetString(6),
                Zweitaccount = Convert.ToBoolean(sqliteDatareader.GetInt32(7)),
                Zweitmail = sqliteDatareader.GetString(8),
                HasM365Account = Convert.ToBoolean(sqliteDatareader.GetInt32(9)),
                IstAktiv = sqliteDatareader.GetBoolean(10),
                Seriennummer = sqliteDatareader.GetString(11),
                AllowJAMF = sqliteDatareader.GetBoolean(12),
                Bemerkung = sqliteDatareader.GetString(13)
            };
            susliste.Add(schuelerin);
        }

        return susliste;
    }

    /// <summary>
    /// Gibt SuS mit Vorname, Nachname und Klasse zurück, erwartet, dass die Kombination eindeutig ist
    /// </summary>
    /// <param name="vorname"></param>
    /// <param name="nachname"></param>
    /// <param name="klasse"></param>
    /// <returns></returns>
    private async Task<SuS> GetSchueler(string vorname, string nachname, string klasse) {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT id,nachname,vorname,mail,klasse,nutzername,aixmail,zweitaccount,zweitmail, m365, aktiv, seriennummer, jamf,bemerkung FROM schueler WHERE vorname = $vorname AND nachname = $nachname AND klasse = $klasse;";
        sqliteCmd.Parameters.AddWithValue("$vorname", vorname);
        sqliteCmd.Parameters.AddWithValue("$nachname", nachname);
        sqliteCmd.Parameters.AddWithValue("$klasse", klasse);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            SuS schuelerin = new() {
                ID = sqliteDatareader.GetInt32(0),
                Nachname = sqliteDatareader.GetString(1),
                Vorname = sqliteDatareader.GetString(2),
                Mail = sqliteDatareader.GetString(3),
                Klasse = sqliteDatareader.GetString(4),
                Nutzername = sqliteDatareader.GetString(5),
                Aixmail = sqliteDatareader.GetString(6),
                Zweitaccount = Convert.ToBoolean(sqliteDatareader.GetInt32(7)),
                Zweitmail = sqliteDatareader.GetString(8),
                HasM365Account = Convert.ToBoolean(sqliteDatareader.GetInt32(9)),
                IstAktiv = sqliteDatareader.GetBoolean(10),
                Seriennummer = sqliteDatareader.GetString(11),
                AllowJAMF = sqliteDatareader.GetBoolean(12),
                Bemerkung = sqliteDatareader.GetString(13)
            };

            return schuelerin;
        }

        return new SuS();
    }

    /// <summary>
    /// Gibt eine Liste von SuS zurück, name wird im Format "Vorname Nachname" erwartet
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public async Task<List<SuS>> GetSchueler(string name) {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT id,nachname,vorname,mail,klasse,nutzername,aixmail,zweitaccount,zweitmail, m365, aktiv, seriennummer, jamf,bemerkung FROM schueler WHERE CONCAT(vorname, ' ', nachname)== $name;";
        sqliteCmd.Parameters.AddWithValue("$name", name);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        var susliste = new List<SuS>();
        while (sqliteDatareader.Read()) {
            SuS schuelerin = new() {
                ID = sqliteDatareader.GetInt32(0),
                Nachname = sqliteDatareader.GetString(1),
                Vorname = sqliteDatareader.GetString(2),
                Mail = sqliteDatareader.GetString(3),
                Klasse = sqliteDatareader.GetString(4),
                Nutzername = sqliteDatareader.GetString(5),
                Aixmail = sqliteDatareader.GetString(6),
                Zweitaccount = Convert.ToBoolean(sqliteDatareader.GetInt32(7)),
                Zweitmail = sqliteDatareader.GetString(8),
                HasM365Account = Convert.ToBoolean(sqliteDatareader.GetInt32(9)),
                IstAktiv = sqliteDatareader.GetBoolean(10),
                Seriennummer = sqliteDatareader.GetString(11),
                AllowJAMF = sqliteDatareader.GetBoolean(12),
                Bemerkung = sqliteDatareader.GetString(13)
            };
            susliste.Add(schuelerin);
        }

        return susliste;
    }

    /// <summary>
    /// gibt die IDs aller SuS zurück
    /// </summary>
    public async Task<ReadOnlyCollection<int>> GetSchuelerIDListe() {
        List<int> slist = [];
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT id FROM schueler;";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            slist.Add(sqliteDatareader.GetInt32(0));
        }

        return new ReadOnlyCollection<int>(slist);
    }

    /// <summary>
    /// gibt den vollständigen Inhalt der Tabelle schueler in der Reihenfolge ID, Nachname, Vorname, Mail, Klasse, Nutzername, AIXmail, zweitaccount(1/0) und zweitmailadresse zurück
    /// </summary>
    public async Task<ReadOnlyCollection<SuS>> GetSchuelerListe() {
        List<SuS> slist = [];
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT id,nachname,vorname,mail,klasse,nutzername,aixmail,zweitaccount,zweitmail,m365, aktiv, seriennummer,bemerkung FROM schueler;";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            SuS schuelerin = new() {
                ID = sqliteDatareader.GetInt32(0),
                Nachname = sqliteDatareader.GetString(1),
                Vorname = sqliteDatareader.GetString(2),
                Mail = sqliteDatareader.GetString(3),
                Klasse = sqliteDatareader.GetString(4),
                Nutzername = sqliteDatareader.GetString(5),
                Aixmail = sqliteDatareader.GetString(6),
                Zweitaccount = Convert.ToBoolean(sqliteDatareader.GetInt32(7)),
                Zweitmail = sqliteDatareader.GetString(8),
                HasM365Account = Convert.ToBoolean(sqliteDatareader.GetInt32(9)),
                IstAktiv = sqliteDatareader.GetBoolean(10),
                Seriennummer = sqliteDatareader.GetString(11),
                Bemerkung = sqliteDatareader.GetString(12)
            };
            slist.Add(schuelerin);
        }

        return new ReadOnlyCollection<SuS>(slist);
    }

    /// <summary>
    /// gibt die schulspezifischen Einstellungen der Datenbank als Settings-Struct zurück
    /// </summary>
    public async Task<Einstellungen> GetSettings() {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT setting,value FROM settings;";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        Einstellungen einstellungenResult = new();
        while (sqliteDatareader.Read()) {
            var key = sqliteDatareader.GetString(0);
            var value = string.IsNullOrEmpty(sqliteDatareader.GetString(1)) ? "" : sqliteDatareader.GetString(1);
            switch (key) {
                case "mailsuffix":
                    einstellungenResult.Mailsuffix = value;
                    break;
                case "kurssuffix":
                    einstellungenResult.Kurssuffix = value;
                    break;
                case "fachersatz":
                    einstellungenResult.Fachersetzung = value;
                    break;
                case "erprobungsstufenleitung":
                    einstellungenResult.Erprobungstufenleitung = value;
                    break;
                case "mittelstufenleitung":
                    einstellungenResult.Mittelstufenleitung = value;
                    break;
                case "efstufenleitung":
                    einstellungenResult.EFStufenleitung = value;
                    break;
                case "q1stufenleitung":
                    einstellungenResult.Q1Stufenleitung = value;
                    break;
                case "q2stufenleitung":
                    einstellungenResult.Q2Stufenleitung = value;
                    break;
                case "oberstufenkoordination":
                    einstellungenResult.Oberstufenkoordination = value;
                    break;
                case "stubos":
                    einstellungenResult.StuBos = value;
                    break;
                case "erprobungsstufen":
                    einstellungenResult.Erprobungsstufe = value.Split(',').Length == 0 ? [""] : value.Split(',');
                    break;
                case "mittelstufen":
                    einstellungenResult.Mittelstufe = value.Split(',').Length == 0 ? [""] : value.Split(',');
                    break;
                case "oberstufen":
                    einstellungenResult.Oberstufe = value.Split(',').Length == 0 ? [""] : value.Split(',');
                    break;
                case "stubostufen":
                    einstellungenResult.StuboStufen = value.Split(',').Length == 0 ? [""] : value.Split(',');
                    break;
                case "jamfstufen":
                    einstellungenResult.JAMFStufen = value.Split(',').Length == 0 ? [""] : value.Split(',');
                    break;
            }
        }

        erprobungsstufe = einstellungenResult.Erprobungsstufe;
        mittelstufe = einstellungenResult.Mittelstufe;
        oberstufe = einstellungenResult.Oberstufe;
        stubostufen = einstellungenResult.StuboStufen;
        jamfstufen = einstellungenResult.JAMFStufen;

        List<string> flist = [];
        sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT kurzfach,langfach FROM fachersatz;";
        sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            var returnstr = "";
            for (var i = 0; i < sqliteDatareader.FieldCount; i++) {
                returnstr += $"{sqliteDatareader.GetString(i)};";
            }

            flist.Add(returnstr);
        }

        var fachk = new List<string>();
        var fachl = new List<string>();
        foreach (var faecher in flist) {
            fachk.Add(faecher.Split(';')[0]);
            fachl.Add(faecher.Split(';')[1]);
        }

        einstellungenResult.Kurzfaecher = [.. fachk];
        einstellungenResult.Langfaecher = [.. fachl];
        einstellungenResult.Version = version;
        return einstellungenResult;
    }

    /// <summary>
    /// gibt die Statistik als fertigen String zurück
    /// </summary>
    public async Task<string> GetStat() {
        var result =
            $"Kurse: {GetKursListe().Result.Count}; Lehrer:Innen: {GetLehrkraftListe().Result.Count}; Schüler:Innen: {GetSchuelerListe().Result.Count}";
        return result;
    }

    /// <summary>
    /// Gibt eine Liste mit Lehrkräften zurück, die die Leitung der entsprechenden Stufe haben
    /// </summary>
    /// <param name="stufe"></param>
    /// <returns></returns>
    private async Task<List<Lehrkraft>> GetOberstufenleitung(string stufe) {
        if (string.IsNullOrEmpty(stufe) || !oberstufe.Contains(stufe))
            return [];
        List<Lehrkraft> luls = [];
        switch (stufe) {
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
                return [];
        }
    }

    /// <summary>
    /// gibt die IDs der SuS in der Klasse als Liste zurück
    /// </summary>
    /// <param name="klasse"></param>
    /// <returns>Integer-Liste der SuS-IDs</returns>
    public async Task<ReadOnlyCollection<SuS>> GetSuSAusKlasse(string klasse) {
        List<SuS> sliste = [];
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT id FROM schueler WHERE klasse = $klasse;";
        sqliteCmd.Parameters.AddWithValue("$klasse", klasse);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            sliste.Add(await GetSchueler(sqliteDatareader.GetInt32(0)));
        }

        return new ReadOnlyCollection<SuS>(sliste);
    }

    /// <summary>
    /// gibt die IDs der SuS des Kurses als Liste zurück
    /// </summary>
    /// <param name="kbez"></param>
    /// <returns>Integer-Liste der SuS-IDs</returns>
    public async Task<ReadOnlyCollection<SuS>> GetSuSAusKurs(string kbez) {
        List<SuS> sliste = [];
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT schuelerid FROM nimmtteil WHERE kursbez = $kbez;";
        sqliteCmd.Parameters.AddWithValue("$kbez", kbez);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            for (var i = 0; i < sqliteDatareader.FieldCount; i++) {
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
    public async Task<ReadOnlyCollection<SuS>> GetSusAusStufe(string stufe) {
        List<SuS> sliste = [];
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT DISTINCT id FROM schueler WHERE klasse LIKE $stufe;";
        sqliteCmd.Parameters.AddWithValue("$stufe", $"{stufe}%");
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            sliste.Add(await GetSchueler(sqliteDatareader.GetInt32(0)));
        }

        return new ReadOnlyCollection<SuS>(sliste);
    }

    /// <summary>
    /// gibt die SuS der Lehrkraft zurück
    /// </summary>
    /// <param name="lulid"></param>
    /// <returns>Interger-Liste der SuS-IDs</returns>
    public async Task<ReadOnlyCollection<SuS>> GetSuSVonLuL(int lulid) {
        List<SuS> sliste = [];
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT nimmtteil.schuelerid FROM unterrichtet JOIN nimmtteil ON nimmtteil.kursbez = unterrichtet.kursbez WHERE lehrerid = $lulid;";
        sqliteCmd.Parameters.AddWithValue("$lulid", lulid);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            sliste.Add(await GetSchueler(sqliteDatareader.GetInt32(0)));
        }

        return new ReadOnlyCollection<SuS>(sliste);
    }


    /// <summary>
    /// gibt true zurück wenn es den SuS mit der id gibt
    /// </summary>
    /// <param name="sid"></param>
    /// <returns>boolean</returns>
    public bool GibtEsSchueler(int sid) {
        return GetSchueler(sid).Result.ID > 0;
    }

    /// <summary>
    /// gibt true zurück wenn es die Lehrkraft mit der id gibt
    /// </summary>
    /// <param name="lid"></param>
    /// <returns></returns>
    public bool GibtEsLehrkraft(int lid) {
        return GetLehrkraft(lid).Result.ID > 0;
    }

    /// <summary>
    /// gibt true zurück wenn es den Kurs mit der Bezeichnung gibt
    /// </summary>
    /// <param name="kbez"></param>
    /// <returns></returns>
    public async Task<bool> GibtEsKurs(string kbez) {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT bez FROM kurse WHERE bez = $kbez;";
        sqliteCmd.Parameters.AddWithValue("$kbez", kbez);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        return sqliteDatareader.HasRows;

        // return GetKurs(kbez).Result.Bezeichnung == kbez;
    }

    /// <summary>
    /// liest die Nutzernamen und AIX-Mailadressen für SuS aus einem Suite-Export ein und updated diese (inkrementell oder gesamt)
    /// </summary>
    /// <param name="idfile"></param>
    public async Task AIXDatenEinlesen(string idfile) {
        var lines = await File.ReadAllLinesAsync(idfile);
        int ina = -1, inid = -1, imail = -1;
        var header = lines[0].Split(';');
        for (var i = 0; i < header.Length; i++) {
            if (header[i].Equals("Referenz-Id")) {
                inid = i;
            }

            if (header[i].Equals("Anmeldename")) {
                ina = i;
            }

            if (header[i].Equals("E-Mail")) {
                imail = i;
            }
        }

        await StartTransaction();
        for (var i = 1; i < lines.Length; i++) {
            try {
                var line = lines[i].Split(';');
                if (line[inid] == "" || !line[inid].All(char.IsDigit)) continue;
                try {
                    var id = Convert.ToInt32(line[inid]);
                    UpdateSchuelerNutzername(id, line[ina]);
                    UpdateAIXSuSAdressen(id, line[imail]);
                }
                catch (Exception e) {
                    AddLogMessage(new LogEintrag
                        { Eintragsdatum = DateTime.Now, Nachricht = e.Message, Warnstufe = "Debug" });
                }
            }
            catch (Exception ex) {
#if DEBUG
                AddLogMessage(new LogEintrag
                    { Eintragsdatum = DateTime.Now, Nachricht = ex.Message, Warnstufe = "Debug" });
#endif
                AddLogMessage(new LogEintrag
                    { Eintragsdatum = DateTime.Now, Nachricht = "Fehler beim Einlesen der IDs", Warnstufe = "Fehler" });
            }
        }

        await StopTransaction();
    }

    /// <summary>
    /// liest die Kurse aus der übergebenen Datei ein (inkrementell oder gesamt)
    /// </summary>
    /// <param name="kursfile"></param>
    public async Task KurseEinlesen(string kursfile) {
        var fachersetzung = await GetFachersatz();
        var fachsuffix = await GetKursSuffix();
        var lines = await File.ReadAllLinesAsync(kursfile);
        int inv = -1, inn = -1, inf = -1, inl = -1, inka = -1, ink = -1;
        var header = lines[0].Split('|');
        for (var header_index = 0; header_index < header.Length; header_index++) {
            if (header[header_index].Equals("Vorname")) {
                inv = header_index;
            }

            if (header[header_index].Equals("Nachname")) {
                inn = header_index;
            }

            if (header[header_index].Equals("Fach")) {
                inf = header_index;
            }

            if (header[header_index].Equals("Fachlehrer")) {
                inl = header_index;
            }

            if (header[header_index].Equals("Kursart")) {
                inka = header_index;
            }

            if (header[header_index].Equals("Kurs")) {
                ink = header_index;
            }
        }

        var lehrkraft_cache = await GetLehrkraftListe();
        var sus_cache = await GetSchuelerListe();
        await StartTransaction();
        for (var i = 1; i < lines.Length; i++)
            // Parallel.ForEach(lines, async (line, _) =>
        {
            try {
                var tmpkurs = lines[i].Split('|').Select(x => x.Trim()).ToArray();
                var vorname = tmpkurs[inv];
                string nachname;
                var kursklasse = "";
                if (tmpkurs[inn].Contains('#')) {
                    nachname = tmpkurs[inn].Split('#')[0];
                    kursklasse = tmpkurs[inn].Split('#')[1].Replace(" ", "");
                }
                else {
                    nachname = tmpkurs[inn];
                }

                var sus = sus_cache
                    .Where(s => s.Vorname == vorname && s.Nachname == nachname && s.Klasse == kursklasse).ToArray();
                if (sus.Length == 0) {
                    AddLogMessage(new LogEintrag {
                        Eintragsdatum = DateTime.Now,
                        Nachricht = $"Schüler:in {vorname} {nachname} {kursklasse} in Zeile {i} nicht gefunden",
                        Warnstufe = "Fehler"
                    });
                    continue;
                }

                var stmp = sus[0];
                var klasse = stmp.Klasse;
                var alte_kurse = (await GetKurseVonSuS(stmp.ID)).ToList();
                var lehrkaefte = lehrkraft_cache.Where(l => l.Kuerzel == tmpkurs[inl]).ToArray();
                if (lehrkaefte.Length == 0) {
                    AddLogMessage(new LogEintrag {
                        Eintragsdatum = DateTime.Now,
                        Nachricht = $"Lehrer:In {tmpkurs[inl]} in Zeile {i} nicht gefunden",
                        Warnstufe = "Fehler"
                    });
                    continue;
                }

                var ltmp = lehrkaefte[0];
                HashSet<string> hash_set_neue_kurse = [];
                if (stmp.ID > 50000 && ltmp.ID > 0) {
                    var stufe = stmp.GetStufe();
                    while (i < lines.Length &&
                           (await GetSuSFromLine(lines[i].Split('|').Select(x => x.Trim()).ToArray(), inn, inv)).ID ==
                           stmp.ID) {
                        tmpkurs = lines[i].Split('|').Select(x => x.Trim()).ToArray();
                        lehrkaefte = lehrkraft_cache.Where(l => l.Kuerzel == tmpkurs[inl]).ToArray();
                        if (lehrkaefte.Length == 0) {
                            AddLogMessage(new LogEintrag {
                                Eintragsdatum = DateTime.Now,
                                Nachricht = $"Lehrkraft {tmpkurs[inl]} in Zeile {i} nicht gefunden",
                                Warnstufe = "Fehler"
                            });
                            i++;
                            continue;
                        }

                        ltmp = lehrkaefte[0];
                        if (!oberstufe.Contains(stufe)) {
                            var klkurs_bez = stmp.Klasse + "KL";
                            if (await GibtEsKurs(klkurs_bez)) {
                                await AddSuSAndOrLuLToKursIfNotIn(stmp, ltmp, klkurs_bez);
                                hash_set_neue_kurse.Add(klkurs_bez);
                            }
                            else {
                                await AddKurs(klkurs_bez, "KL", kursklasse, stmp.GetStufe(), await GetKursSuffix(), 0,
                                    "");
                                await AddStoK(stmp.ID, klkurs_bez);
                                hash_set_neue_kurse.Add(klkurs_bez);
                            }
                        }
                        else {
                            klasse = stufe;
                        }

                        var kursart = tmpkurs[inka];
                        if (kursart == "") continue;
                        string fach;
                        if (!kursart.Equals("PUK") &&
                            !kursart.Equals("ZUV")) //PUK = Klassenunterricht; ZUV = Zusatzveranstaltung
                        {
                            fach = tmpkurs[inf];
                            for (var k = 0; k < fachersetzung.Count - 1; k++) {
                                if (!fach.Equals(fachersetzung[k].Split(':')[0])) continue;
                                fach = fachersetzung[k].Split(':')[1];
                                break;
                            }

                            var bez = $"{stufe}-{tmpkurs[ink]}";
                            if (!await GibtEsKurs(bez)) {
                                await AddKurs(bez, fach, stufe, stufe, fachsuffix, 1, "");
                            }

                            hash_set_neue_kurse.Add(bez);
                            await AddSuSAndOrLuLToKursIfNotIn(stmp, ltmp, bez);
                        }
                        else {
                            fach = tmpkurs[inf];
                            for (var k = 0; k < fachersetzung.Count - 1; k++) {
                                if (!fach.Equals(fachersetzung[k].Split(':')[0])) continue;
                                fach = fachersetzung[k].Split(':')[1];
                                break;
                            }

                            var bez = klasse + fach;
                            if (string.IsNullOrEmpty((await GetKurs(bez)).Bezeichnung)) {
                                await AddKurs(bez, fach, klasse, stufe, fachsuffix, 0, "");
                            }

                            hash_set_neue_kurse.Add(bez);
                            await AddSuSAndOrLuLToKursIfNotIn(stmp, ltmp, bez);
                        }

                        i++;
                    }

                    if (stubostufen.Contains(stufe)) {
                        await AddStoK(stmp.ID, $"StuBo-{stufe}");
                        hash_set_neue_kurse.Add($"StuBo-{stufe}");
                    }

                    if (erprobungsstufe.Contains(stufe)) {
                        await AddStoK(stmp.ID, "Erprobungsstufe");
                        hash_set_neue_kurse.Add("Erprobungsstufe");
                    }
                    else if (mittelstufe.Contains(stufe)) {
                        await AddStoK(stmp.ID, "Mittelstufe");
                        hash_set_neue_kurse.Add("Mittelstufe");
                    }
                    else if (oberstufe.Contains(stufe)) {
                        var lange_stufe = stufe switch {
                            "EF" => "Einführungsphase",
                            "Q1" => "Qualifikationsphase 1",
                            "Q2" => "Qualifikationsphase 2",
                            _ => ""
                        };

                        await AddStoK(stmp.ID, lange_stufe);
                        hash_set_neue_kurse.Add(lange_stufe);
                    }

                    var aktuelle_kurse =
                        (await GetKursListe()).Where(k => hash_set_neue_kurse.Contains(k.Bezeichnung)).ToList();
                    var courses_to_add = aktuelle_kurse.Where(k => !alte_kurse.Contains(k)).ToList();
                    var courses_to_delete = alte_kurse.Where(k =>
                        !hash_set_neue_kurse.Contains(k.Bezeichnung)).ToList();
                    foreach (var kurs in courses_to_add) {
                        ausstehende_aenderungen.Add(new Changes
                            { id = stmp.ID, kind = ChangeKind.add, kurs = kurs, person = ChangePerson.SuS });
                    }

                    var _deletions = courses_to_delete.RemoveAll(k =>
                        k.Bezeichnung.Contains("StuBo") || courses_to_add.Exists(l => l.Bezeichnung.Equals(k.Bezeichnung)));
                    foreach (var kurs in courses_to_delete) {
                        await RemoveSfromK(stmp.ID, kurs.Bezeichnung);
                    }

                    if (courses_to_add.Count != courses_to_delete.Count) {
                        AddLogMessage(new LogEintrag {
                            Eintragsdatum = DateTime.Now,
                            Nachricht =
                                $"SuS {stmp.ID}:{stmp.Nachname},{stmp.Vorname} aus {courses_to_delete.Count} Kursen gelöscht und zu {courses_to_add.Count} Kursen hinzugefügt",
                            Warnstufe = "Info"
                        });
                    }

                    hash_set_neue_kurse.Clear();
                }

                else {
                    AddLogMessage(new LogEintrag {
                        Eintragsdatum = DateTime.Now,
                        Nachricht =
                            $" Lehrkraft {ltmp.Kuerzel} oder  SuS {stmp.ID} {tmpkurs[inv]} {tmpkurs[inn]} mit ungültiger ID",
                        Warnstufe = "Hinweis"
                    });
                }
            }
            catch (Exception ex) {
#if DEBUG
                AddLogMessage(new LogEintrag {
                    Eintragsdatum = DateTime.Now,
                    Nachricht = $"Zeile {i}: " + ex.Message, Warnstufe = "Debug"
                });
#endif
                AddLogMessage(new LogEintrag {
                    Eintragsdatum = DateTime.Now, Nachricht = "Fehler beim Einlesen der Kurse", Warnstufe = "Fehler"
                });
                i--;
                continue;
            }

            i--;
        }

        await StopTransaction();
    }

    /// <summary>
    /// Fügt SuS und Lehrkraft in den Kurs hinzu, wenn diese noch nicht drin sind
    /// </summary>
    /// <param name="stmp"></param>
    /// <param name="ltmp"></param>
    /// <param name="kursbez"></param>
    private async Task AddSuSAndOrLuLToKursIfNotIn(SuS stmp, Lehrkraft ltmp, string kursbez) {
        if (!await IstSuSInKurs(Convert.ToInt32(stmp.ID), kursbez)) {
            await AddStoK(Convert.ToInt32(stmp.ID), kursbez);
        }

        if (!await IstLuLInKurs(Convert.ToInt32(ltmp.ID), kursbez)) {
            await AddLtoK(Convert.ToInt32(ltmp.ID), kursbez);
        }
    }

    /// <summary>
    /// Hilfsmethode beim Kurseinlesen, konvertiert die eingelesene Zeile in ein SuS-Objekt
    /// </summary>
    /// <param name="tmpkurs"></param>
    /// <param name="inn"></param>
    /// <param name="inv"></param>
    /// <returns></returns>
    private async Task<SuS> GetSuSFromLine(string[] tmpkurs, int inn, int inv) {
        var vorname = tmpkurs[inv];
        string nachname;
        var kursklasse = "";
        if (tmpkurs[inn].Contains('#')) {
            nachname = tmpkurs[inn].Split('#')[0];
            kursklasse = tmpkurs[inn].Split('#')[1].Replace(" ", "");
        }
        else {
            nachname = tmpkurs[inn];
        }

        var stmp = await GetSchueler(vorname.Replace("'", ""), nachname.Replace("'", ""), kursklasse);
        return stmp;
    }

    /// <summary>
    /// gibt zurück, ob Lehrkraft im Kurs ist
    /// </summary>
    /// <param name="lulid"></param>
    /// <param name="kursbez"></param>
    /// <returns></returns>
    private async Task<bool> IstLuLInKurs(int lulid, string kursbez) {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT lehrerid, kursbez FROM unterrichtet WHERE kursbez = $kursbez AND lehrerid = $lulid";
        sqliteCmd.Parameters.AddWithValue("$kursbez", kursbez);
        sqliteCmd.Parameters.AddWithValue("$lulid", lulid);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            var return_id = sqliteDatareader.GetInt32(0);
            var return_kursbez = sqliteDatareader.GetString(1);
            return return_id == lulid && return_kursbez == kursbez;
        }

        return false;
    }

    /// <summary>
    /// gibt zurück, ob SuS im Kurs ist
    /// </summary>
    /// <param name="schuelerid"></param>
    /// <param name="kursbez"></param>
    /// <returns></returns>
    private async Task<bool> IstSuSInKurs(int schuelerid, string kursbez) {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "SELECT schuelerid, kursbez FROM nimmtteil WHERE kursbez = $kursbez AND schuelerid = $schuelerid";
        sqliteCmd.Parameters.AddWithValue("$kursbez", kursbez);
        sqliteCmd.Parameters.AddWithValue("$schuelerid", schuelerid);
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            var return_id = sqliteDatareader.GetInt32(0);
            var return_kursbez = sqliteDatareader.GetString(1);
            return return_id == schuelerid && return_kursbez == kursbez;
        }

        return false;
    }

    /// <summary>
    /// löscht das komplette Log
    /// </summary>
    public async Task LoescheLog() {
        if (File.Exists(_logpath)) {
            File.Delete(_logpath);
        }
    }

    /// <summary>
    /// löscht das Log für die übergebene Stufe
    /// </summary>
    /// <param name="stufe">"Fehler", "Hinweis", oder "Info"</param>
    public async Task LoescheLog(string stufe) {
        //ToDo: Implementierung
    }

    /// <summary>
    /// liest die Lehrkräfte aus der übergebenen Datei ein (inkrementell oder gesamt)
    /// </summary>
    /// <param name="lulfile"></param>
    public async Task LulEinlesen(string lulfile) {
        int inv = -1, inn = -1, ini = -1, inkrz = -1, infak = -1, inm = -1, isn = -1;
        var lines = await File.ReadAllLinesAsync(lulfile);
        var header = lines[0].Split(';');
        for (var i = 0; i < header.Length; i++) {
            if (header[i].Equals("firstname")) {
                inv = i;
            }

            if (header[i].Equals("lastname")) {
                inn = i;
            }

            if (header[i].Equals("idnumber")) {
                ini = i;
            }

            if (header[i].Equals("username")) {
                inkrz = i;
            }

            if (header[i].Equals("fakultas")) {
                infak = i;
            }

            if (header[i].Contains("email")) {
                inm = i;
            }

            if (header[i].Contains("serialnumber")) {
                isn = i;
            }
        }

        await StartTransaction();
        Parallel.ForEach(lines, async void (line, _) => {
            try {
                if (line == "") return;
                var tmpkuk = line.Split(';');
                for (var j = 0; j < tmpkuk.Length; j++) {
                    tmpkuk[j] = tmpkuk[j].Trim('"');
                }

                await Addlehrkraft(Convert.ToInt32(tmpkuk[ini]), tmpkuk[inv], tmpkuk[inn],
                    tmpkuk[inkrz].ToUpper(), tmpkuk[inm], tmpkuk[infak].TrimEnd(';'), "", "", tmpkuk[isn], "");
            }
            catch (Exception ex) {
#if DEBUG
                AddLogMessage(new LogEintrag
                    { Eintragsdatum = DateTime.Now, Nachricht = ex.Message, Warnstufe = "Debug" });
#endif
                await StopTransaction();
            }
        });
        await StopTransaction();
    }

    /// <summary>
    /// löscht den angegeben Kurs und alle dazugehörigen Einträge
    /// </summary>
    /// <param name="kbez"></param>
    public async Task RemoveK(string kbez) {
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
        AddLogMessage(new LogEintrag {
            Eintragsdatum = DateTime.Now, Nachricht = $"Kurs mit der Bezeichnung {kbez} gelöscht",
            Warnstufe = "Info"
        });
    }

    /// <summary>
    /// löscht den angegeben Kurs und alle dazugehörigen Einträge
    /// </summary>
    /// <param name="kurs"></param>
    /// <returns></returns>
    public async Task RemoveK(Kurs kurs) {
        await RemoveK(kurs.Bezeichnung);
    }

    /// <summary>
    /// löscht die angegebene Lehrperson und die Kurszuordnungen
    /// </summary>
    /// <param name="lid"></param>
    public async Task RemoveL(int lid) {
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
        AddLogMessage(new LogEintrag {
            Eintragsdatum = DateTime.Now, Nachricht = $"Lehrkraft mit der ID {lid} gelöscht", Warnstufe = "Info"
        });
    }

    /// <summary>
    /// löscht die angegebene Lehrperson und die Kurszuordnungen
    /// </summary>
    /// <param name="lehrkraft"></param>
    /// <returns></returns>
    private async Task RemoveL(Lehrkraft lehrkraft) {
        await RemoveL(lehrkraft.ID);
    }

    /// <summary>
    /// löscht die angegebene Lehrperson und die Kurszuordnungen
    /// </summary>
    /// <param name="kuerzel"></param>
    /// <returns></returns>
    public async Task RemoveL(string kuerzel) {
        await RemoveL(GetLehrkraft(kuerzel).Result);
    }

    /// <summary>
    /// löscht die angegebene Lehrperson aus dem angegeben Kurs
    /// </summary>
    /// <param name="lid"></param>
    /// <param name="kbez"></param>
    public async Task RemoveLfromK(int lid, string kbez) {
        if (lid <= 0 || !await GibtEsKurs(kbez)) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "DELETE FROM unterrichtet WHERE lehrerid = $lid AND kursbez = $kbez;";
        sqliteCmd.Parameters.AddWithValue("$lid", lid);
        sqliteCmd.Parameters.AddWithValue("$kbez", kbez);
        sqliteCmd.ExecuteNonQuery();
        ausstehende_aenderungen.Add(new Changes {
            id = lid, kind = ChangeKind.del, person = ChangePerson.LuL, kurs = await GetKurs(kbez)
        });
        AddLogMessage(new LogEintrag {
            Eintragsdatum = DateTime.Now,
            Nachricht = $"Lehrkraft mit der ID {lid} aus Kurs {kbez} gelöscht",
            Warnstufe = "Info"
        });
    }

    /// <summary>
    /// löscht die angegebene Lehrperson aus dem angegeben Kurs
    /// </summary>
    /// <param name="lehrkraft"></param>
    /// <param name="kurs"></param>
    /// <returns></returns>
    public async Task RemoveLfromK(Lehrkraft lehrkraft, Kurs kurs) {
        await RemoveLfromK(lehrkraft.ID, kurs.Bezeichnung);
    }

    /// <summary>
    /// löscht angegebenen SuS und die Kurszuordnungen
    /// </summary>
    /// <param name="sid"></param>
    public async Task RemoveS(int sid) {
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
        AddLogMessage(new LogEintrag
            { Eintragsdatum = DateTime.Now, Nachricht = $"SuS mit der ID {sid} gelöscht", Warnstufe = "Info" });
    }

    /// <summary>
    /// löscht angegebenen SuS und die Kurszuordnungen
    /// </summary>
    /// <param name="schulerin"></param>
    /// <returns></returns>
    public async Task RemoveS(SuS schulerin) {
        await RemoveS(schulerin.ID);
    }

    /// <summary>
    /// löscht angegebenen SuS aus dem angegeben Kurs
    /// </summary>
    /// <param name="sid"></param>
    /// <param name="kbez"></param>
    public async Task RemoveSfromK(int sid, string kbez) {
        if (sid <= 0 || !await GibtEsKurs(kbez)) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "DELETE FROM nimmtteil WHERE schuelerid = $sid AND kursbez = $kbez;";
        sqliteCmd.Parameters.AddWithValue("$sid", sid);
        sqliteCmd.Parameters.AddWithValue("$kbez", kbez);
        sqliteCmd.ExecuteNonQuery();
        ausstehende_aenderungen.Add(new Changes {
            id = sid, kind = ChangeKind.del, person = ChangePerson.SuS, kurs = await GetKurs(kbez)
        });
        AddLogMessage(new LogEintrag {
            Eintragsdatum = DateTime.Now, Nachricht = $"SuS mit der ID {sid} aus Kurs {kbez} gelöscht",
            Warnstufe = "Info"
        });
    }

    /// <summary>
    /// löscht angegebenen SuS aus dem angegeben Kurs
    /// </summary>
    /// <param name="schuelerin"></param>
    /// <param name="kurs"></param>
    /// <returns></returns>
    public async Task RemoveSfromK(SuS schuelerin, Kurs kurs) {
        await RemoveSfromK(schuelerin.ID, kurs.Bezeichnung);
    }

    /// <summary>
    /// setzt die Liste der Kurz- und Langschreibweisen der Fächer
    /// </summary>
    /// <param name="fachk"></param>
    /// <param name="fachl"></param>
    private async Task SetKurzLangFach(IReadOnlyList<string> fachk, IReadOnlyList<string> fachl) {
        if (fachk.Count == fachl.Count) {
            await StartTransaction();
            var sqliteCmd = _sqliteConn.CreateCommand();
            for (var i = 0; i < fachk.Count; i++) {
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
    /// Setzt die Einstellungen in der Datenbank
    /// </summary>
    /// <param name="sqliteCmd"></param>
    /// <param name="einstellungen"></param>
    private void SetSettings(ref SqliteCommand sqliteCmd, ref Einstellungen einstellungen) {
        sqliteCmd.Parameters.AddWithValue("$mailsuffixparam", einstellungen.Mailsuffix);
        sqliteCmd.Parameters.AddWithValue("$kurssuffixparam", einstellungen.Kurssuffix);
        sqliteCmd.Parameters.AddWithValue("$fachersatzparam", einstellungen.Fachersetzung);

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
        sqliteCmd.Parameters.AddWithValue("$erprobungstufen", "erprobungsstufen");
        sqliteCmd.Parameters.AddWithValue("$mittelstufen", "mittelstufen");
        sqliteCmd.Parameters.AddWithValue("$oberstufen", "oberstufen");
        sqliteCmd.Parameters.AddWithValue("$stubostufen", "stubostufen");
        sqliteCmd.Parameters.AddWithValue("$jamfstufen", "jamfstufen");
        sqliteCmd.Parameters.AddWithValue("$erprobungsstufeparam", string.Join(',', einstellungen.Erprobungsstufe));
        sqliteCmd.Parameters.AddWithValue("$mittelstufeparam", string.Join(',', einstellungen.Mittelstufe));
        sqliteCmd.Parameters.AddWithValue("$oberstufeparam", string.Join(',', einstellungen.Oberstufe));
        sqliteCmd.Parameters.AddWithValue("$stubostufenparam", string.Join(',', einstellungen.StuboStufen));
        sqliteCmd.Parameters.AddWithValue("$jamfstufenparam", string.Join(',', einstellungen.JAMFStufen));

        sqliteCmd.Parameters.AddWithValue("$stubos", "stubos");
        sqliteCmd.Parameters.AddWithValue("$version", "version");

        sqliteCmd.Parameters.AddWithValue("$erprobungstufenleitungparam",
            einstellungen.Erprobungstufenleitung);
        sqliteCmd.Parameters.AddWithValue("$mittelstufenleitungparam",
            einstellungen.Mittelstufenleitung);
        sqliteCmd.Parameters.AddWithValue("$efstufenleitungparam", einstellungen.EFStufenleitung);
        sqliteCmd.Parameters.AddWithValue("$q1stufenleitungparam", einstellungen.Q1Stufenleitung);
        sqliteCmd.Parameters.AddWithValue("$q2stufenleitungparam", einstellungen.Q2Stufenleitung);
        sqliteCmd.Parameters.AddWithValue("$oberstufenkoordinationparam",
            einstellungen.Oberstufenkoordination);
        sqliteCmd.Parameters.AddWithValue("$stubosparam", einstellungen.StuBos);
        sqliteCmd.Parameters.AddWithValue("$versionparam", version);
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
            "INSERT OR REPLACE INTO settings (setting,value) VALUES($erprobungstufen, $erprobungsstufeparam)";
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.CommandText =
            "INSERT OR REPLACE INTO settings (setting,value) VALUES($mittelstufen, $mittelstufeparam)";
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.CommandText =
            "INSERT OR REPLACE INTO settings (setting,value) VALUES($oberstufen, $oberstufeparam)";
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.CommandText =
            "INSERT OR REPLACE INTO settings (setting,value) VALUES($stubostufen, $stubostufenparam)";
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.CommandText =
            "INSERT OR REPLACE INTO settings (setting,value) VALUES($jamfstufen, $jamfstufenparam)";
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.CommandText =
            "INSERT OR REPLACE INTO settings (setting,value) VALUES($stubos, $stubosparam)";
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.CommandText =
            "INSERT OR REPLACE INTO settings (setting,value) VALUES($version, $versionparam)";
        sqliteCmd.ExecuteNonQuery();
        sqliteCmd.Parameters.Clear();
        erprobungsstufe = einstellungen.Erprobungsstufe;
        mittelstufe = einstellungen.Mittelstufe;
        oberstufe = einstellungen.Oberstufe;
        jamfstufen = einstellungen.JAMFStufen;
        stubostufen = einstellungen.StuboStufen;
        _ = SetKurzLangFach(einstellungen.Kurzfaecher, einstellungen.Langfaecher);
    }

    /// <summary>
    /// setzt die Einstellungen der Schule in der Datenbank
    /// </summary>
    /// <param name="einstellungen"></param>
    public async Task SetSettings(Einstellungen einstellungen) {
        var sqliteCmd = _sqliteConn.CreateCommand();
        SetSettings(ref sqliteCmd, ref einstellungen);
    }

    /// <summary>
    /// start von SQLite-Transaktionen zur Beschleunigung von Inserts oder Updates
    /// </summary>
    public async Task StartTransaction() {
        if (_activeTransaction) return;
        _activeTransaction = true;
        _dbtrans = _sqliteConn.BeginTransaction();
    }

    /// <summary>
    /// ende von SQLite-Transaktionen zur Beschleunigung von Inserts oder Updates
    /// </summary>
    public async Task StopTransaction() {
        if (_activeTransaction == false) return;
        _activeTransaction = false;
        if (_dbtrans == null) return;
        await _dbtrans.CommitAsync();
    }

    /// <summary>
    /// liest die  SuSnen aus der übergebenen Datei ein (inkrementell oder gesamt)
    /// </summary>
    /// <param name="susfile"></param>
    public async Task SusEinlesen(string susfile) {
        int inv = -1, inn = -1, ini = -1, ink = -1;
        List<int> inm = [];
        var lines = await File.ReadAllLinesAsync(susfile);
        var header = lines[0].Split(';');
        for (var i = 0; i < header.Length; i++) {
            header[i] = header[i].Trim('"');
            if (header[i].Equals("Vorname")) {
                inv = i;
            }

            if (header[i].Equals("Nachname")) {
                inn = i;
            }

            if (header[i].Equals("Interne ID-Nummer")) {
                ini = i;
            }

            if (header[i].Contains("E-Mail") && !header[i].Contains("schulisch")) {
                inm.Add(i);
            }

            if (header[i].Equals("Klasse")) {
                ink = i;
            }
        }

        lines = lines[1..];
        await StartTransaction();
        Parallel.ForEach(lines, async void (line, _) => {
            try {
                var tmpsus = line.Split(';');
                for (var j = 0; j < tmpsus.Length; j++) {
                    tmpsus[j] = tmpsus[j].Trim('"');
                }

                var susid = Convert.ToInt32(tmpsus[ini]);
                var settings = GetSettings().Result;
                var mail = tmpsus[ini] + settings.Mailsuffix;
                var maillist = (from idm in inm where !tmpsus[idm].Equals("") select tmpsus[idm]).ToList();

                if (maillist.Count > 0) {
                    mail = maillist[0];
                }
                else {
                    AddLogMessage(new LogEintrag {
                        Eintragsdatum = DateTime.Now, Nachricht = $"SuS {tmpsus[ini]} ohne primäre Mailadresse",
                        Warnstufe = "Hinweis"
                    });
                }

                maillist = maillist.Distinct().ToList();
                var klasse = tmpsus[ink].Contains(' ') ? tmpsus[ink].Replace(" ", "") : tmpsus[ink];
                if (mail.Contains(';')) {
                    AddLogMessage(new LogEintrag {
                        Eintragsdatum = DateTime.Now, Nachricht = $"Mailfehler bei SuS mit der ID {tmpsus[ini]}",
                        Warnstufe = "Fehler"
                    });
                }

                maillist.Remove(mail);
                var mails = maillist.Aggregate("", (current, maileintrag) => $"{current}{maileintrag},");
                mails = mails.TrimEnd(',');
                if (GibtEsSchueler(susid)) {
                    var sus = GetSchueler(susid).Result;
                    sus.Klasse = klasse;
                    sus.Mail = mail;
                    sus.Zweitmail = mails;
                    sus.IstAktiv = true;
                    UpdateSchueler(sus);
                }
                else {
                    await AddSchuelerIn(susid, tmpsus[inv].Replace("'", ""),
                        tmpsus[inn].Replace("'", ""), mail, klasse, "", "", 0, mails, "", false,false,true,"");
                }
            }
            catch (Exception ex) {
#if DEBUG
                AddLogMessage(new LogEintrag
                    { Eintragsdatum = DateTime.Now, Nachricht = ex.Message, Warnstufe = "Debug" });
#endif
                AddLogMessage(new LogEintrag {
                    Eintragsdatum = DateTime.Now, Nachricht = "Fehler beim Einlesen der SuS", Warnstufe = "Fehler"
                });
            }
        });
        await StopTransaction();
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
    /// <param name="bemerkung"></param>
    public async Task UpdateKurs(string bez, string fach, string klasse, string stufe, string suffix, int istkurs,
        string bemerkung) {
        if (string.IsNullOrEmpty(bez)) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "UPDATE kurse SET fach = $fach, klasse = $klasse, stufe = $stufe, suffix = $suffix, istkurs = $istkurs,bemerkung=$bemerkung WHERE bez=$bez;";
        sqliteCmd.Parameters.AddWithValue("$fach", fach);
        sqliteCmd.Parameters.AddWithValue("$bez", bez);
        sqliteCmd.Parameters.AddWithValue("$klasse", klasse);
        sqliteCmd.Parameters.AddWithValue("$stufe", stufe);
        sqliteCmd.Parameters.AddWithValue("$suffix", suffix);
        sqliteCmd.Parameters.AddWithValue("$istkurs", istkurs);
        sqliteCmd.Parameters.AddWithValue("$bemerkung", bemerkung);
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
    /// <param name="seriennummer"></param>
    /// <param name="bemerkung"></param>
    public async Task UpdateLehrkraft(int id, string vorname, string nachname, string kuerzel, string mail,
        string fakultas, string pwtemp, string favo, string sfavo, string seriennummer, string bemerkung) {
        if (string.IsNullOrEmpty(kuerzel) || id <= 0) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "UPDATE lehrkraft SET nachname=$nachname, vorname=$vorname, kuerzel= $kuerzel, mail=$mail, fakultas=$fakultas, pwtemp = $pwtemp, favo = $favo, sfavo=$sfavo, seriennummer=$seriennummer,bemerkung=$bemerkung WHERE id=$id;";
        sqliteCmd.Parameters.AddWithValue("$id", id);
        sqliteCmd.Parameters.AddWithValue("$vorname", vorname);
        sqliteCmd.Parameters.AddWithValue("$nachname", nachname);
        sqliteCmd.Parameters.AddWithValue("$kuerzel", kuerzel.ToUpper());
        sqliteCmd.Parameters.AddWithValue("$mail", mail);
        sqliteCmd.Parameters.AddWithValue("$fakultas", fakultas.TrimEnd(';'));
        sqliteCmd.Parameters.AddWithValue("$pwtemp", pwtemp);
        sqliteCmd.Parameters.AddWithValue("$favo", favo);
        sqliteCmd.Parameters.AddWithValue("$sfavo", sfavo);
        sqliteCmd.Parameters.AddWithValue("$seriennummer", seriennummer);
        sqliteCmd.Parameters.AddWithValue("$bemerkung", bemerkung);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// setzt für per ID angebenen Lehrperson die Daten neu
    /// </summary>
    /// <param name="l"></param>
    public async void UpdateLehrkraft(Lehrkraft l) {
        try {
            if (string.IsNullOrEmpty(l.Kuerzel) || l.ID <= 0) return;
            await UpdateLehrkraft(l.ID, l.Vorname, l.Nachname, l.Kuerzel, l.Mail, l.Fakultas, l.Pwttemp, l.Favo,
                l.SFavo, l.Seriennummer, l.Bemerkung);
        }
        catch (Exception e) {
            AddLogMessage(new LogEintrag {
                Eintragsdatum = DateTime.Now, Warnstufe = "Debug",
                Nachricht = e.StackTrace ?? "unbekannter Fehler beim Lehrkraft-Update"
            });
        }
    }

    /// <summary>
    /// setzt für per ID angebenen SuS die Daten neu
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
    /// <param name="hasM365"></param>
    /// <param name="aktiv"></param>
    /// <param name="seriennummer"></param>
    /// <param name="jamf"></param>
    /// <param name="bemerkung"></param>
    public async Task UpdateSchueler(int id, string vorname, string nachname, string mail, string klasse,
        string nutzername, string aixmail, int zweitaccount, string zweitmail, bool hasM365, bool aktiv,
        string seriennummer, bool jamf, string bemerkung) {
        if (id <= 0) return;
        seriennummer = string.IsNullOrEmpty(seriennummer) ? "" : seriennummer;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText =
            "UPDATE schueler SET nachname=$nachname, vorname=$vorname, mail=$mail, klasse=$klasse, nutzername=$nutzername, aixmail=$aixmail, zweitaccount = $zweitaccount, zweitmail=$zweitmail, m365=$hasM365, aktiv=$aktiv, seriennummer=$seriennummer, jamf = $jamf,bemerkung=$bemerkung WHERE id=$id;";
        sqliteCmd.Parameters.AddWithValue("$id", id);
        sqliteCmd.Parameters.AddWithValue("$vorname", vorname);
        sqliteCmd.Parameters.AddWithValue("$nachname", nachname);
        sqliteCmd.Parameters.AddWithValue("$mail", mail);
        sqliteCmd.Parameters.AddWithValue("$klasse", klasse);
        sqliteCmd.Parameters.AddWithValue("$nutzername", nutzername);
        sqliteCmd.Parameters.AddWithValue("$aixmail", aixmail);
        sqliteCmd.Parameters.AddWithValue("$zweitaccount", zweitaccount);
        sqliteCmd.Parameters.AddWithValue("$zweitmail", zweitmail);
        sqliteCmd.Parameters.AddWithValue("$hasM365", hasM365);
        sqliteCmd.Parameters.AddWithValue("$aktiv", aktiv);
        sqliteCmd.Parameters.AddWithValue("$seriennummer", seriennummer);
        sqliteCmd.Parameters.AddWithValue("$jamf", jamf);
        sqliteCmd.Parameters.AddWithValue("$bemerkung", bemerkung);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// updated die Werte für übergebenen SuS
    /// </summary>
    /// <param name="sus"></param>
    public async void UpdateSchueler(SuS sus) {
        try {
            await UpdateSchueler(sus.ID, sus.Vorname, sus.Nachname, sus.Mail, sus.Klasse, sus.Nutzername, sus.Aixmail,
                sus.Zweitaccount ? 1 : 0, sus.Zweitmail, sus.HasM365Account, sus.IstAktiv, sus.Seriennummer,
                sus.AllowJAMF,
                sus.Bemerkung);
        }
        catch (Exception e) {
            AddLogMessage(new LogEintrag {
                Eintragsdatum = DateTime.Now, Warnstufe = "Debug",
                Nachricht = e.StackTrace ?? "unbekannter Fehler beim SuS-Update"
            });
        }
    }

    /// <summary>
    /// setzt den AIXMailadresse des per ID angegebenen Schülers/ SuS
    /// </summary>
    /// <param name="id"></param>
    /// <param name="mail"></param>
    private void UpdateAIXSuSAdressen(int id, string mail) {
        if (id <= 0) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "UPDATE schueler SET aixmail = $mail WHERE id = $id;";
        sqliteCmd.Parameters.AddWithValue("$id", id);
        sqliteCmd.Parameters.AddWithValue("$mail", mail);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// setzt den Nutzername des per ID angegebenen Schülers/ SuS
    /// </summary>
    /// <param name="id"></param>
    /// <param name="nutzername"></param>
    private void UpdateSchuelerNutzername(int id, string nutzername) {
        if (id <= 0) return;
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "UPDATE schueler SET nutzername = $nutzername WHERE id = $id;";
        sqliteCmd.Parameters.AddWithValue("$id", id);
        sqliteCmd.Parameters.AddWithValue("$nutzername", nutzername);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Liefert alle SuS-IDs, welche keine M365-Einwilligung vorliegen haben
    /// </summary>
    /// <returns></returns>
    public async Task<ReadOnlyCollection<int>> GetM365Blacklist() {
        List<int> ids = [];
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "SELECT id FROM schueler WHERE m365 = 0;";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        while (sqliteDatareader.Read()) {
            ids.Add(int.Parse(sqliteDatareader["id"].ToString() ?? string.Empty));
        }

        return new ReadOnlyCollection<int>(ids);
    }

    /// <summary>
    /// Liest zur lehrerid, das temp. Passwort aus der Datenbank aus und gibt es zurück
    /// </summary>
    /// <param name="lehrerid">Die lehrerid, für die das temporäre Passwort ausgelesen werden soll</param>
    /// <returns>String temporäre Passwort</returns>
    public async Task<string> GetTempPasswort(int lehrerid) {
        if (lehrerid <= 0) return "";
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = $"SELECT pwtemp FROM lehrkraft WHERE id={lehrerid};";
        var sqliteDatareader = await sqliteCmd.ExecuteReaderAsync();
        var tpwd = "";
        while (sqliteDatareader.Read()) {
            tpwd = sqliteDatareader.GetString(0);
        }

        return tpwd;
    }

    /// <summary>
    /// Setzt für die übergebene ID, ob eine M365-Einwilligung vorliegt (1), oder nicht (0)
    /// </summary>
    /// <param name="susid"></param>
    /// <param name="has_m365"></param>
    public async void SetM365(int susid, int has_m365) {
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
    public async void SetTPwd(int lehrerid, string pwd) {
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
    public async Task ZweitAccountsEinlesen(string fileName) {
        var lines = await File.ReadAllLinesAsync(fileName);
        var inid = -1;
        var header = lines[0].Split(';');
        for (var i = 0; i < header.Length; i++) {
            if (header[i].Equals("Interne ID-Nummer")) {
                inid = i;
            }
        }

        if (inid == -1) {
            return;
        }

        await StartTransaction();
        for (var i = 1; i < lines.Length; i++) {
            try {
                var line = lines[i].Split(';');
                if (line[inid] != "") {
                    await SetZweitAccount(Convert.ToInt32(line[inid]), 1);
                }
            }
            catch (Exception ex) {
#if DEBUG
                AddLogMessage(new LogEintrag
                    { Eintragsdatum = DateTime.Now, Nachricht = ex.Message, Warnstufe = "Debug" });
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
    private async Task SetZweitAccount(int id, int pStatus) {
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
    public async Task<List<Lehrkraft>> GetFavos() {
        return GetLehrkraftListe().Result.Where(l => !string.IsNullOrEmpty(l.Favo) || !string.IsNullOrEmpty(l.SFavo))
            .ToList();
    }

    public string GetLogPath() {
        return _logpath;
    }

    /// <summary>
    /// setzt den Status für die Lehrkraft mit der übergebenen ID
    /// </summary>
    /// <param name="lulid"></param>
    /// <param name="istAktiv"></param>
    public void SetzeAktivstatusLehrkraft(int lulid, bool istAktiv) {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "UPDATE lehrkraft SET aktiv = $istAktiv WHERE id = $lulid;";
        sqliteCmd.Parameters.AddWithValue("$lulid", lulid);
        sqliteCmd.Parameters.AddWithValue("$istAktiv", istAktiv);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// setzt den Status für die Lehrkraft
    /// </summary>
    /// <param name="lehrkraft"></param>
    /// <param name="istAktiv"></param>
    public void SetzeAktivstatusLehrkraft(Lehrkraft lehrkraft, bool istAktiv) {
        SetzeAktivstatusLehrkraft(lehrkraft.ID, istAktiv);
    }

    /// <summary>
    /// setzt den Status für den Schüler:in mit der übergebenen ID
    /// </summary>
    /// <param name="susid"></param>
    /// <param name="istAktiv"></param>
    private void SetzeAktivstatusSchueler(int susid, bool istAktiv) {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "UPDATE schueler SET aktiv = $istAktiv WHERE id = $susid;";
        sqliteCmd.Parameters.AddWithValue("$susid", susid);
        sqliteCmd.Parameters.AddWithValue("$istAktiv", istAktiv);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// setzt den Status für den übergebenen Schüler:in
    /// </summary>
    public void SetzeAktivstatusSchueler(SuS schueler, bool istAktiv) {
        SetzeAktivstatusSchueler(schueler.ID, istAktiv);
    }

    public void SetJAMF(int susid, int i) {
        var sqliteCmd = _sqliteConn.CreateCommand();
        sqliteCmd.CommandText = "UPDATE schueler SET jamf = $jamf WHERE id = $susid;";
        sqliteCmd.Parameters.AddWithValue("$susid", susid);
        sqliteCmd.Parameters.AddWithValue("$jamf", i);
        sqliteCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// führt die Änderungen aus und exportiert diese
    /// </summary>
    /// <param name="exportpath"></param>
    public async Task AenderungenAusfuerenUndExportieren(string exportpath) {
        await AenderungenAusfuerenUndExportieren(ausstehende_aenderungen, exportpath);
    }

    /// <summary>
    /// führt die Änderungen aus und exportiert diese
    /// </summary>
    /// <param name="ausstehendeAenderungen"></param>
    /// <param name="exportpath"></param>
    private async Task AenderungenAusfuerenUndExportieren(HashSet<Changes> ausstehendeAenderungen, string exportpath) {
        var susidliste = new List<int>();
        var lulidliste = new List<int>();
        List<string> moodle_einschreibungen = [];
        await StartTransaction();
        foreach (var change in ausstehendeAenderungen) {
            switch (change.kind) {
                case ChangeKind.add:
                    switch (change.person) {
                        case ChangePerson.SuS:
                            await AddStoK(change.id, change.kurs.Bezeichnung);
                            susidliste.Add(change.id);
                            moodle_einschreibungen.Add($"add,student,{change.id},{change.kurs.Bezeichnung}");
                            break;
                        case ChangePerson.LuL:
                            await AddLtoK(change.id, change.kurs.Bezeichnung);
                            lulidliste.Add(change.id);
                            moodle_einschreibungen.Add($"add,editingteacher,{change.id},{change.kurs.Bezeichnung}");
                            break;
                        default:
                            AddLogMessage(new LogEintrag {
                                Eintragsdatum = DateTime.Now,
                                Nachricht = "Fehler im default-case add.change.person",
                                Warnstufe = "Debug"
                            });
                            break;
                    }

                    break;
                case ChangeKind.del:
                    switch (change.person) {
                        case ChangePerson.SuS:
                            await RemoveSfromK(change.id, change.kurs.Bezeichnung);
                            moodle_einschreibungen.Add($"del,student,{change.id},{change.kurs.Bezeichnung}");
                            break;
                        case ChangePerson.LuL:
                            await RemoveLfromK(change.id, change.kurs.Bezeichnung);
                            moodle_einschreibungen.Add($"del,editingteacher,{change.id},{change.kurs.Bezeichnung}");
                            break;
                        default:
                            AddLogMessage(new LogEintrag {
                                Eintragsdatum = DateTime.Now,
                                Nachricht = "Fehler im default-case del.change.person",
                                Warnstufe = "Debug"
                            });
                            break;
                    }

                    break;
                default:
                    AddLogMessage(new LogEintrag {
                        Eintragsdatum = DateTime.Now, Nachricht = "Fehler im default-case change.kind",
                        Warnstufe = "Debug"
                    });
                    break;
            }
        }

        await StopTransaction();
        await File.WriteAllLinesAsync(exportpath + "/mdl_einschreibungen.csv", moodle_einschreibungen);
        ExportJAMF(susidliste.AsReadOnly(), lulidliste.AsReadOnly(), true, exportpath, false);
        ausstehende_aenderungen.RemoveWhere(ausstehendeAenderungen.Contains);
    }

    /// <summary>
    /// Gibt die nicht exportierten Änderungen zurück
    /// </summary>
    /// <returns></returns>
    public HashSet<Changes> GetNichtExportierteAenderungen() {
        return ausstehende_aenderungen;
    }

    /// <summary>
    /// Löscht alle nicht exportierten Änderungen
    /// </summary>
    public void LoescheAlleNichtExportiertenAenderungen() {
        ausstehende_aenderungen.Clear();
    }

    public bool LoescheAenderung(Changes to_delete) {
        return ausstehende_aenderungen.Remove(to_delete);
    }
}