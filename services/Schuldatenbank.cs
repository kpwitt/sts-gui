using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming

#pragma warning disable CS1998

namespace SchulDB
{
    /// <summary>
    /// Wrapperklasse zur Verwaltung der SQLite-Datenbank
    /// </summary>
    public class Schuldatenbank : IDisposable
    {
        private readonly string dbpath;
        private SQLiteTransaction dbtrans;
        private readonly SQLiteConnection sqlite_conn;
        private bool trans;
        private int status = 100;

        /// <summary>
        /// erstellt, falls nicht vorhanden, die Datenbankstruktur und öffnet die Verbindung
        /// </summary>
        public Schuldatenbank(string path)
        {
            dbpath = path;
            var strconnection = "Data Source=" + dbpath + ";Version=3;Pooling=True;Max Pool Size=100;";
            sqlite_conn = new SQLiteConnection(strconnection);
            sqlite_conn.Open();
            var sqlite_cmd = sqlite_conn.CreateCommand();
            try
            {
                sqlite_cmd.CommandText = @"CREATE TABLE IF NOT EXISTS
        [lehrkraft] (
        [id]   INTEGER NOT NULL PRIMARY KEY,
        [nachname]  NVARCHAR(512) NOT NULL,
        [vorname]  NVARCHAR(512) NOT NULL,
        [mail]  NVARCHAR(512) NOT NULL UNIQUE,
        [kuerzel]  NVARCHAR(8) NOT NULL UNIQUE,
        [fakultas]  NVARCHAR(16) NOT NULL,
        [pwtemp] NVARCHAR(16) NOT NULL
      )";
                sqlite_cmd.ExecuteNonQuery();

                sqlite_cmd.CommandText = @"CREATE INDEX IF NOT EXISTS lindex ON lehrkraft(id);";
                sqlite_cmd.ExecuteNonQuery();

                sqlite_cmd.CommandText = @"CREATE TABLE IF NOT EXISTS
        [schueler] (
        [id]   INTEGER NOT NULL PRIMARY KEY,
        [nachname]  NVARCHAR(512) NOT NULL,
        [vorname]  NVARCHAR(512) NOT NULL,
        [mail]  NVARCHAR(512) NOT NULL,
        [klasse]  NVARCHAR(16) NOT NULL,
        [nutzername]  NVARCHAR(7) NOT NULL,
        [aixmail] NVARCHAR(128) NOT NULL,
        [zweitaccount] INTEGER DEFAULT 0,
        [zweitmail] NVARCHAR(512)
      )";
                sqlite_cmd.ExecuteNonQuery();

                sqlite_cmd.CommandText = @"CREATE INDEX IF NOT EXISTS sindex ON schueler(id);";
                sqlite_cmd.ExecuteNonQuery();

                sqlite_cmd.CommandText = @"CREATE TABLE IF NOT EXISTS
        [kurse] (
        [bez]   NVARCHAR(512) NOT NULL PRIMARY KEY,
        [fach]  NVARCHAR(512) NOT NULL,
        [klasse]  NVARCHAR(16) NOT NULL,
        [stufe]  NVARCHAR(16) NOT NULL,
        [suffix]  NVARCHAR(16) NOT NULL,
        [istkurs]  INTEGER NOT NULL
      )";
                sqlite_cmd.ExecuteNonQuery();

                sqlite_cmd.CommandText = @"CREATE INDEX IF NOT EXISTS kindex ON kurse(bez);";
                sqlite_cmd.ExecuteNonQuery();

                sqlite_cmd.CommandText = @"CREATE TABLE IF NOT EXISTS
        [unterrichtet] (
        [lehrerid]   INTEGER NOT NULL,
        [kursbez]  NVARCHAR(32) NOT NULL,
        PRIMARY KEY(lehrerid,kursbez),
        FOREIGN KEY(lehrerid) REFERENCES lehrkraft(id),
        FOREIGN KEY(kursbez) REFERENCES kurse(bez)
      )";
                sqlite_cmd.ExecuteNonQuery();

                sqlite_cmd.CommandText = @"CREATE TABLE IF NOT EXISTS
        [nimmtteil] (
        [schuelerid]   INTEGER NOT NULL,
        [kursbez]  NVARCHAR(32) NOT NULL,
        PRIMARY KEY(schuelerid,kursbez),
        FOREIGN KEY(schuelerid) REFERENCES schueler(id),
        FOREIGN KEY(kursbez) REFERENCES kurse(bez)
      )";
                sqlite_cmd.ExecuteNonQuery();

                sqlite_cmd.CommandText = @"CREATE TABLE IF NOT EXISTS
        [settings] (
        [setting]   NVARCHAR(512) NOT NULL UNIQUE,
        [value]  NVARCHAR(512) NOT NULL
      )";
                sqlite_cmd.ExecuteNonQuery();

                sqlite_cmd.CommandText = @"CREATE TABLE IF NOT EXISTS
        [fachersatz] (
        [kurzfach]   NVARCHAR(16) NOT NULL,
        [langfach]  NVARCHAR(64) NOT NULL,
        PRIMARY KEY(kurzfach,langfach)
      )";
                sqlite_cmd.ExecuteNonQuery();

                sqlite_cmd.CommandText = @"CREATE TABLE IF NOT EXISTS
        [log] (
        [id]  INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
        [stufe]   NVARCHAR(16) NOT NULL,
        [datum]    NVARCHAR(128) NOT NULL,   
        [nachricht]  NVARCHAR(4096) NOT NULL
      )";
                sqlite_cmd.ExecuteNonQuery();

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
                Settings settings = new()
                {
                    Mailsuffix = "@schule.local",
                    Fachersetzung = "",
                    Kurzfaecher = fachk,
                    Langfaecher = fachl,
                    Kurssuffix = "_" + (DateTime.Now.Year - 2000) + "" + (DateTime.Now.Year - 1999),
                    Erprobungstufenleitung = "",
                    Mittelstufenleitung = "",
                    EFStufenleitung = "",
                    Q1Stufenleitung = "",
                    Q2Stufenleitung = "",
                    Oberstufenkoordination = ""
                };
                SetSettings(settings);
            }
            catch (SQLiteException ex)
            {
                throw new ApplicationException("Kritischer Fehler beim Erstellen der SQL-Datei: " + ex.Message);
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText =
                "INSERT OR IGNORE INTO kurse (bez, fach, klasse, stufe, suffix, istkurs) VALUES (@bez, @fach, @klasse, @stufe, @suffix, @istkurs);";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@fach", fach));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@bez", bez));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@klasse", klasse));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@stufe", stufe));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@suffix", suffix));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@istkurs", istkurs));
            sqlite_cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// fügt den Kurs hinzu
        /// </summary>
        /// <param name="kurs"></param>
        /// <returns></returns>
        public async Task AddKurs(Kurs kurs)
        {
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText =
                "INSERT OR IGNORE INTO kurse (bez, fach, klasse, stufe, suffix, istkurs) VALUES (@bez, @fach, @klasse, @stufe, @suffix, @istkurs);";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@fach", kurs.Fach));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@bez", kurs.Bezeichnung));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@klasse", kurs.Klasse));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@stufe", kurs.Stufe));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@suffix", kurs.Suffix));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@istkurs", kurs.Istkurs));
            sqlite_cmd.ExecuteNonQuery();
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
        public async Task Addlehrkraft(int id, string vorname, string nachname, string kuerzel, string mail,
            string fakultas)
        {
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText =
                "INSERT OR IGNORE INTO lehrkraft (id, nachname, vorname, kuerzel, mail, fakultas, pwtemp) VALUES (@id, @nachname, @vorname, @kuerzel, @mail, @fakultas, @pwtemp);";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@id", id));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@vorname", vorname));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@nachname", nachname));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@kuerzel", kuerzel.ToUpper()));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@mail", mail));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@fakultas", fakultas.TrimEnd(';')));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@pwtemp", GeneratePasswort(8)));
            sqlite_cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// fügt die Lehrperson hinzu
        /// </summary>
        /// <param name="lehrkraft"></param>
        /// <returns></returns>
        public async Task Addlehrkraft(LuL lehrkraft)
        {
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText =
                "INSERT OR IGNORE INTO lehrkraft (id, nachname, vorname, kuerzel, mail, fakultas, pwtemp) VALUES (@id, @nachname, @vorname, @kuerzel, @mail, @fakultas, @pwtemp);";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@id", lehrkraft.ID));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@vorname", lehrkraft.Vorname));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@nachname", lehrkraft.Nachname));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@kuerzel", lehrkraft.Kuerzel.ToUpper()));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@mail", lehrkraft.Mail));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@fakultas", lehrkraft.Fakultas.TrimEnd(';')));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@pwtemp",
                lehrkraft.Pwttemp.Length > 7 ? lehrkraft.Pwttemp : GeneratePasswort(8)));
            sqlite_cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// fügt eine Nachricht ins Log hinzu, Stufe entweder Info, Hinweis oder Fehler
        /// </summary>
        /// <param name="stufe"></param>
        /// <param name="nachricht"></param>
        public async Task<int> AddLogMessage(string stufe, string nachricht)
        {
            var sqlite_cmd = sqlite_conn.CreateCommand();
            var dnow = DateTime.Now.ToLongDateString() + " " + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" +
                       DateTime.Now.Second;
            sqlite_cmd.CommandText =
                "INSERT OR IGNORE INTO log (stufe, datum, nachricht) VALUES (@stufe, @dnow, @nachricht);";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@stufe", stufe));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@dnow", dnow));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@nachricht", nachricht));
            sqlite_cmd.ExecuteNonQuery();
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "INSERT OR IGNORE INTO unterrichtet (lehrerid, kursbez) VALUES (@lid, @kbez);";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@lid", lid));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@kbez", kbez));
            sqlite_cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// fügt die angegebene Lehrperson zum angegebene Kurs hinzu
        /// </summary>
        /// <param name="lehrkraft"></param>
        /// <param name="kurs"></param>
        /// <returns></returns>
        public async Task AddLtoK(LuL lehrkraft, Kurs kurs)
        {
            if (string.IsNullOrEmpty(kurs.Bezeichnung)) return;
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "INSERT OR IGNORE INTO unterrichtet (lehrerid, kursbez) VALUES (@lid, @kbez);";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@lid", lehrkraft.ID));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@kbez", kurs.Bezeichnung));
            sqlite_cmd.ExecuteNonQuery();
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText =
                "INSERT OR IGNORE INTO schueler (id, vorname, nachname, mail, klasse, nutzername, aixmail, zweitaccount, zweitmail) VALUES (@id, @vorname, @nachname, @mail, @klasse, @nutzername, @aixmail,@zweitaccount, @zweitmail);";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@id", id));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@vorname", vorname));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@nachname", nachname));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@mail", mail));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@klasse", klasse));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@nutzername", nutzername));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@aixmail", aixmail));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@zweitaccount", zweitaccount));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@zweitmail", zweitmail));
            sqlite_cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// fügt den Schüler/die Schülerin hinzu
        /// </summary>
        /// <param name="schulerin"></param>
        /// <returns></returns>
        public async Task AddSchuelerIn(SuS schulerin)
        {
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText =
                "INSERT OR IGNORE INTO schueler (id, vorname, nachname, mail, klasse, nutzername, aixmail, zweitaccount, zweitmail) VALUES (@id, @vorname, @nachname, @mail, @klasse, @nutzername, @aixmail,@zweitaccount, @zweitmail);";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@id", schulerin.ID));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@vorname", schulerin.Vorname));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@nachname", schulerin.Nachname));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@mail", schulerin.Mail));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@klasse", schulerin.Klasse));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@nutzername", schulerin.Nutzername));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@aixmail", schulerin.Aixmail));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@zweitaccount", schulerin.Zweitaccount));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@zweitmail", schulerin.Zweitmail));
            sqlite_cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// fügt den/die angegebenen Schüler/Schülerin zum angegebene Kurs hinzu
        /// </summary>
        /// <param name="sid"></param>
        /// <param name="kbez"></param>
        public async Task AddStoK(int sid, string kbez)
        {
            if (sid == 0 || kbez == "") return;
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "INSERT OR IGNORE INTO nimmtteil (schuelerid, kursbez) VALUES (@sid, @kbez);";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@sid", sid));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@kbez", kbez));
            sqlite_cmd.ExecuteNonQuery();
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "INSERT OR IGNORE INTO nimmtteil (schuelerid, kursbez) VALUES (@sid, @kbez);";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@sid", schulerin.ID));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@kbez", kurs.Bezeichnung));
            sqlite_cmd.ExecuteNonQuery();
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
                klasse.StartsWith('9')||
                klasse.StartsWith("10"))
            {
                var kliste = GetKursListe().Result.ToList();
                kliste = kliste.FindAll(k => k.Bezeichnung.StartsWith(klasse) && k.Istkurs == false);
                foreach (var k in kliste)
                {
                    var sqlite_cmd = sqlite_conn.CreateCommand();
                    sqlite_cmd.CommandText =
                        "INSERT OR IGNORE INTO nimmtteil (schuelerid, kursbez) VALUES (@sid, @kbez);";
                    sqlite_cmd.Parameters.Add(new SQLiteParameter("@sid", schulerin.ID));
                    sqlite_cmd.Parameters.Add(new SQLiteParameter("@kbez", k.Bezeichnung));
                    sqlite_cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// optimiert die DB und schließt die Verbindung
        /// </summary>
        private void CloseDB()
        {
            if (trans)
            {
                dbtrans.Commit();
                trans = false;
            }

            if (sqlite_conn.State != System.Data.ConnectionState.Open) return;
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "pragma optimize;";
            sqlite_cmd.ExecuteNonQuery();
            sqlite_conn.Close();
        }

        /// <summary>
        /// Pflichtimplementierung um sicherzustellen, dass beim Löschen des Objekt Ressourcen etc. freigegeben werden 
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
                List<string> lulliste = new()
                {
                    "firstname;lastname;idnumber;username;fakultas;email"
                };
                lulliste.AddRange(lehrerliste.Select(lehrer =>
                    lehrer.Vorname + ";" + lehrer.Nachname + ";" + lehrer.ID + ";" + lehrer.Kuerzel + ";" +
                    lehrer.Fakultas + ";" + lehrer.Mail));

                List<string> sliste = new()
                {
                    "Vorname;Nachname;Interne ID-Nummer;E-Mail;Klasse"
                };
                List<string> kurse = new()
                {
                    "Vorname|Nachname|Fach|Fachlehrer|Kursart|Kurs"
                };
                List<string> ids = new()
                {
                    "Anmeldename;Referenz-Id;E-Mail"
                };
                List<string> zweitaccounts = new()
                {
                    "Interne ID-Nummer"
                };
                status = 0;
                var mystatus = 0;
                var maxstatus = susliste.Count;
                foreach (var schueler in susliste)
                {
                    sliste.Add(schueler.Vorname + ";" + schueler.Nachname + ";" + schueler.ID + ";" + schueler.Mail +
                               ";" + schueler.Klasse);
                    ids.Add(schueler.Nutzername + ";" + schueler.ID + ";" + schueler.Mail);
                    if (schueler.Zweitaccount)
                    {
                        zweitaccounts.Add(schueler.ID + "");
                    }

                    foreach (var kurs in await GetKursVonSuS(schueler.ID))
                    {
                        var luls = await GetLuLAusKurs(kurs.Bezeichnung);
                        if (luls.Count <= 0) continue;
                        var l = await GetLehrkraft(luls[0].ID);
                        var fach = kurs.Fach.IndexOf('-') > 0 ? kurs.Fach[..kurs.Fach.IndexOf('-')] : kurs.Fach;
                        kurse.Add(schueler.Nachname + "|" + schueler.Vorname + "|" + fach + "|" +
                                  l.Kuerzel.ToUpper() +
                                  "|" + (kurs.Istkurs ? "PUK|" : "GKM|") +
                                  (kurs.Istkurs == false ? "" : kurs.Fach));
                    }

                    mystatus++;
                    status = mystatus / maxstatus;
                }

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
                List<string> kurse = new()
                {
                    "Vorname|Nachname|Fach|Fachlehrer|Kursart|Kurs"
                };
                foreach (var k in kursliste)
                {
                    var kurs = (await GetKurs(k));
                    foreach (var schueler in await GetSuSAusKurs(k))
                    {
                        var l = (await GetLuLAusKurs(k))[0];
                        var fach = kurs.Fach.IndexOf('-') > 0 ? kurs.Fach[..kurs.Fach.IndexOf('-')] : kurs.Fach;
                        kurse.Add(schueler.Nachname + "|" + schueler.Vorname + "|" + fach + "|" + l.Kuerzel.ToUpper() +
                                  "|" + (kurs.Istkurs ? "PUK|" : "GKM|") + (kurs.Istkurs == false ? "" : kurs.Fach));
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
                    Convert.ToInt32(kurs.Istkurs));
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
                    lehrkraft.Kuerzel, lehrkraft.Mail, lehrkraft.Fakultas);
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
                        var sus = await GetSchueler(line[isv], line[isn]);
                        if (sus.Klasse == line[isk])
                        {
                            await UpdateSchueler(sus.ID, sus.Vorname, sus.Nachname, sus.Mail, sus.Klasse,
                                sus.Nutzername, sus.Aixmail, Convert.ToInt32(sus.Zweitaccount), line[imail]);
                        }
                        //oder:
                        //await UpdateSchueler(sus.ID, sus.Vorname, sus.Nachname, sus.Mail, sus.Klasse, sus.Nutzername, sus.Aixmail, 1, line[imail]);
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
        /// <param name="passwort">mit Erstpasswort: true für ja, false für nein</param>
        /// <param name="expandfiles">Dateien erweitern: true für ja, false für nein</param>
        /// <param name="nurMoodleSuffix">Soll das Suffix nur für Moodle-Kurse verwendet werden</param>
        /// <param name="kursvorlage">Stringarray mit ShortID aus Moodle für die Vorlagenkurse</param>
        /// <param name="susidliste">Liste mit SuS-IDs</param>
        /// <param name="lulidliste">Liste mit LuL-IDs</param>
        /// <param name="kursliste">Liste mit Kurs-Bezeichnungen</param>
        public async Task<int> ExportCSV(string folder, string destsys, string whattoexport, bool passwort,
            bool expandfiles, bool nurMoodleSuffix, string[] kursvorlage, ReadOnlyCollection<int> susidliste,
            ReadOnlyCollection<int> lulidliste,
            ReadOnlyCollection<string> kursliste)
        {
            try
            {
                if (destsys.Equals("all"))
                {
                    return await ExportCSV(folder, "ami", whattoexport, passwort, expandfiles, nurMoodleSuffix,
                        kursvorlage, susidliste,
                        lulidliste, kursliste);
                }

                if (whattoexport.Equals("all"))
                {
                    return await ExportCSV(folder, destsys, "ksle", passwort, expandfiles, nurMoodleSuffix, kursvorlage,
                        susidliste,
                        lulidliste, kursliste);
                }

                if (whattoexport.Contains('e'))
                {
                    passwort = true;
                }

                List<string> ausgabeAIXL = new();
                List<string> ausgabeAIXS = new();
                List<string> ausgabeMoodleEinschreibungen = new();
                List<string> ausgabeMoodleKurse = new();
                List<string> ausgabeMoodleUser = new();
                List<string> ausgabeIntern = new()
                {
                    "kuerzel;nachname;vorname;plz_ort_;adresse;tel_privat;tel_mobil;email_privat;email_dienst;gebdatum_;status_;mail_Adresse;fach1;fach2;fach3;fakult;funktion_;pw_temp;aktiv;gebdatum;plz;ort;titel;nachname;pop3_dienst;pop3_menge"
                };
                var kursvorlagen = kursvorlage[0].Equals("") && kursvorlage[1].Equals("");
                ausgabeMoodleKurse.Add(kursvorlagen
                    ? "shortname;fullname;idnumber;category_idnumber;format"
                    : "shortname;fullname;idnumber;category_idnumber;format;templatecourse");

                if (passwort)
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

                status = 0;
                var mystatus = 0;
                var maxstatus = susidliste.Count + lulidliste.Count + kursliste.Count;
                string[] sekI = { "5", "6", "7", "8", "9", "10" };
                if (whattoexport.Contains('k'))
                {
                    foreach (var kurs in kursliste)
                    {
                        if (kurs.EndsWith('-')) continue;
                        var k = GetKurs(kurs.Split(';')[0]).Result;
                        if (kursvorlagen)
                        {
                            if (k.Istkurs)
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
                            if (k.Istkurs)
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

                        mystatus++;
                        status = 100 * mystatus / maxstatus;
                    }
                }

                if (whattoexport.Contains('s'))
                {
                    foreach (var sus in susidliste)
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
                                ausgabeMoodleEinschreibungen.Add("add,schueler," + s.ID + "," + kk.Klasse + kk.Fach +
                                                                 kk.Suffix);
                            }
                            else
                            {
                                ausgabeMoodleEinschreibungen.Add("add,student," + s.ID + "," + kk.Bezeichnung +
                                                                 kk.Suffix);
                            }

                            if (s.Klasse.StartsWith("5") || s.Klasse.StartsWith("6"))
                            {
                                ausgabeMoodleEinschreibungen.Add("add,schueler," + s.ID + ",erprobungsstufe" +
                                                                 GetKursSuffix().Result);
                            }
                            else if (s.Klasse.StartsWith("7") || s.Klasse.StartsWith("8") || s.Klasse.StartsWith("9")||
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

                        if (passwort)
                        {
                            ausgabeMoodleUser.Add(s.Mail + ";Klasse" + s.Klasse + DateTime.Now.Year + "!;" +
                                                  s.Nutzername + ";" + s.ID + ";" + s.Nachname + ";" + s.Vorname +
                                                  ";schueler");
                            ausgabeAIXS.Add("\"" + s.Vorname + "\";\"" + s.Nachname + "\";\"" + s.Klasse + "\";\"" +
                                            s.ID + "\";\"Klasse" +
                                            s.Klasse + DateTime.Now.Year + "!\";\"" + kListe + "\"");
                        }
                        else
                        {
                            ausgabeMoodleUser.Add(s.Mail + ";" + s.Nutzername + ";" + s.ID + ";" + s.Nachname + ";" +
                                                  s.Vorname);
                            ausgabeAIXS.Add("\"" + s.Vorname + "\";\"" + s.Nachname + "\";\"" + s.Klasse + "\";\"" +
                                            s.ID + "\";\"" + kListe + "\"");
                        }

                        mystatus++;
                        status = mystatus * 100 / maxstatus;
                    }
                }

                if (whattoexport.Contains('e'))
                {
                    if (!whattoexport.Contains('m'))
                    {
                        whattoexport += "m";
                    }

                    maxstatus += susidliste.Count;
                    foreach (var s in susidliste)
                    {
                        var sus = GetSchueler(s).Result;
                        if (sus.Zweitaccount)
                        {
                            if (sus.Klasse.StartsWith("5") || sus.Klasse.StartsWith("6"))
                            {
                                ausgabeMoodleUser.Add(sus.Zweitmail.Split(',')[0] + ";Klasse" + sus.Klasse +
                                                      DateTime.Now.Year + "!" + ";" + sus.Nutzername + "_E1;" + "E_" +
                                                      sus.ID + "1;" + sus.Nachname + "_Eltern;" + sus.Vorname +
                                                      ";eltern");
                                ausgabeMoodleEinschreibungen.Add("add,eltern,E_" + sus.ID + "1," + sus.Klasse + "KL" +
                                                                 GetKursSuffix().Result);
                                ausgabeMoodleEinschreibungen.Add("add,eltern,E_" + sus.ID + "1,erprobungsstufe" +
                                                                 GetKursSuffix().Result);
                            }
                            else if (sus.Klasse.StartsWith("7") || sus.Klasse.StartsWith("8") ||
                                     sus.Klasse.StartsWith("9")| sus.Klasse.StartsWith("10"))
                            {
                                ausgabeMoodleUser.Add(sus.Zweitmail + ";Klasse" + sus.Klasse + DateTime.Now.Year + "!" +
                                                      ";" + sus.Nutzername + "_E1;" + "E_" + sus.ID + "1;" +
                                                      sus.Nachname + "_Eltern;" + sus.Vorname + ";eltern");
                                ausgabeMoodleEinschreibungen.Add("add,eltern,E_" + sus.ID + "1," + sus.Klasse + "KL" +
                                                                 GetKursSuffix().Result);
                                ausgabeMoodleEinschreibungen.Add("add,eltern,E_" + sus.ID + "1,mittelstufe" +
                                                                 GetKursSuffix().Result);
                            }
                        }

                        if (sus.Klasse.StartsWith("5") || sus.Klasse.StartsWith("6"))
                        {
                            ausgabeMoodleUser.Add(sus.Mail + ";Klasse" + sus.Klasse + DateTime.Now.Year + "!" + ";" +
                                                  sus.Nutzername + "_E;" + "E_" + sus.ID + ";" + sus.Nachname +
                                                  "_Eltern;" + sus.Vorname + ";eltern");
                            ausgabeMoodleEinschreibungen.Add("add,eltern,E_" + sus.ID + "," + sus.Klasse + "KL" +
                                                             GetKursSuffix().Result);
                            ausgabeMoodleEinschreibungen.Add("add,eltern,E_" + sus.ID + ",erprobungsstufe" +
                                                             GetKursSuffix().Result);
                        }
                        else if (sus.Klasse.StartsWith("7") || sus.Klasse.StartsWith("8") || sus.Klasse.StartsWith("9")| sus.Klasse.StartsWith("10"))
                        {
                            ausgabeMoodleUser.Add(sus.Mail + ";Klasse" + sus.Klasse + DateTime.Now.Year + "!" + ";" +
                                                  sus.Nutzername + "_E;" + "E_" + sus.ID + ";" + sus.Nachname +
                                                  "_Eltern;" + sus.Vorname + ";eltern");
                            ausgabeMoodleEinschreibungen.Add("add,eltern,E_" + sus.ID + "," + sus.Klasse + "KL" +
                                                             GetKursSuffix().Result);
                            ausgabeMoodleEinschreibungen.Add("add,eltern,E_" + sus.ID + ",mittelstufe" +
                                                             GetKursSuffix().Result);
                        }

                        mystatus++;
                        status = mystatus * 100 / maxstatus;
                    }
                }

                if (whattoexport.Contains('l'))
                {
                    foreach (var l in lulidliste)
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
                            if (kurs.Istkurs)
                            {
                                ausgabeMoodleEinschreibungen.Add("add,editingteacher," + lt.ID + "," +
                                                                 kurs.Bezeichnung + kurs.Suffix);
                            }
                            else
                            {
                                ausgabeMoodleEinschreibungen.Add("add,editingteacher," + lt.ID + "," +
                                                                 kurs.Bezeichnung + kurs.Suffix);
                            }

                            kListe += "^" + kurs.Bezeichnung + kurs.Suffix + "|";
                        }

                        if (kListe == "^|")
                        {
                            kListe = "";
                        }

                        if (nurMoodleSuffix && kListe != "")
                        {
                            kListe = kListe.Replace(GetKursSuffix().Result, "");
                        }

                        if (passwort)
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

                        //kListe = kListe.TrimEnd('|');
                        mystatus++;
                        status = mystatus * 100 / maxstatus;
                    }
                }

                if (destsys.Contains('i'))
                {
                    maxstatus += lulidliste.Count;
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
                        mystatus++;
                        status = 100 * mystatus / maxstatus;
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
                                var aix_sus = (await File.ReadAllLinesAsync(folder + "/aix_sus.csv")).ToList();
                                aix_sus.RemoveAt(0);
                                ausgabeAIXS.AddRange(aix_sus);
                                await File.WriteAllLinesAsync(folder + "/aix_sus.csv", ausgabeAIXS.Distinct().ToList(),
                                    Encoding.UTF8);
                            }

                            if (File.Exists(folder + "/aix_lul.csv"))
                            {
                                var aix_lul = (await File.ReadAllLinesAsync(folder + "/aix_lul.csv")).ToList();
                                aix_lul.RemoveAt(0);
                                ausgabeAIXL.AddRange(aix_lul);
                                await File.WriteAllLinesAsync(folder + "/aix_lul.csv", ausgabeAIXL.Distinct().ToList(),
                                    Encoding.UTF8);
                            }
                        }

                        if (destsys.Contains('m'))
                        {
                            if (File.Exists(folder + "/mdl_einschreibungen.csv"))
                            {
                                var mdl_ein =
                                    (await File.ReadAllLinesAsync(folder + "/mdl_einschreibungen.csv")).ToList();
                                ausgabeMoodleKurse.RemoveAt(0);
                                ausgabeMoodleEinschreibungen.AddRange(mdl_ein);
                                await File.WriteAllLinesAsync(folder + "/mdl_einschreibungen.csv",
                                    ausgabeMoodleEinschreibungen.Distinct().ToList(), Encoding.UTF8);
                            }

                            if (File.Exists(folder + "/mdl_kurse.csv"))
                            {
                                var mdl_kurse = (await File.ReadAllLinesAsync(folder + "/mdl_kurse.csv")).ToList();
                                mdl_kurse.RemoveAt(0);
                                ausgabeMoodleKurse.AddRange(mdl_kurse);
                                await File.WriteAllLinesAsync(folder + "/mdl_kurse.csv",
                                    ausgabeMoodleKurse.Distinct().ToList(),
                                    Encoding.UTF8);
                            }

                            if (File.Exists(folder + "/mdl_nutzer.csv"))
                            {
                                var mdl_nutzer = (await File.ReadAllLinesAsync(folder + "/mdl_nutzer.csv")).ToList();
                                mdl_nutzer.RemoveAt(0);
                                ausgabeMoodleUser.AddRange(mdl_nutzer);
                                await File.WriteAllLinesAsync(folder + "/mdl_nutzer.csv",
                                    ausgabeMoodleUser.Distinct().ToList(),
                                    Encoding.UTF8);
                            }
                        }

                        if (destsys.Contains('i'))
                        {
                            if (File.Exists(folder + "/Lehrerdaten_anschreiben.csv"))
                            {
                                var llg_intern =
                                    (await File.ReadAllLinesAsync(folder + "/Lehrerdaten_anschreiben.csv")).ToList();
                                llg_intern.RemoveAt(0);
                                ausgabeIntern.AddRange(llg_intern);
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
            //erlaubt beim Hoster: /-_#*+!§,()=:.@äöüÄÖÜß
            const string valid = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ1234567890+-.,()!*/_#";
            StringBuilder res = new();
            while (0 < laenge--)
            {
                res.Append(valid[RandomNumberGenerator.GetInt32(valid.Length)]);
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "SELECT kurzfach,langfach FROM fachersatz;";
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            while (sqlite_datareader.Read())
            {
                var returnstr = "";
                for (var i = 0; i < sqlite_datareader.FieldCount; i++)
                {
                    returnstr += sqlite_datareader.GetString(i) + ";";
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
            return dbpath;
        }

        /// <summary>
        /// gibt die Informationen Bezeichnung, Fach, Klasse, Stufe, Kurssuffix und istKurs zur übergebenen Kursbezeichnung zurück
        /// </summary>
        /// <param name="kbez"></param>
        public async Task<Kurs> GetKurs(string kbez)
        {
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText =
                "SELECT bez,fach,klasse,stufe,suffix,istkurs FROM kurse WHERE bez = '" + kbez + "';";
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            Kurs retKurs = new();
            while (sqlite_datareader.Read())
            {
                retKurs.Bezeichnung = sqlite_datareader.GetString(0);
                retKurs.Fach = sqlite_datareader.GetString(1);
                retKurs.Klasse = sqlite_datareader.GetString(2);
                retKurs.Stufe = sqlite_datareader.GetString(3);
                retKurs.Suffix = sqlite_datareader.GetString(4);
                retKurs.Istkurs = Convert.ToBoolean(sqlite_datareader.GetInt32(5));
                retKurs.Art = retKurs.Istkurs ? "PUT" : "PUK";

                if (retKurs.Istkurs)
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "SELECT bez FROM kurse;";
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            while (sqlite_datareader.Read())
            {
                klist.Add(sqlite_datareader.GetString(0));
            }

            return new ReadOnlyCollection<string>(klist);
        }

        /// <summary>
        /// gibt den vollständigen Inhalt der Tabelle kurse in der Reihenfolge Bezeichnung, Fach, Klasse, Stufe, Kurssuffix und istKurs zurück
        /// </summary>
        public async Task<ReadOnlyCollection<Kurs>> GetKursListe()
        {
            List<Kurs> kliste = new();
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "SELECT bez,fach,klasse,stufe,suffix,istkurs FROM kurse;";
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            while (sqlite_datareader.Read())
            {
                Kurs retKurs = new()
                {
                    Bezeichnung = sqlite_datareader.GetString(0),
                    Fach = sqlite_datareader.GetString(1),
                    Klasse = sqlite_datareader.GetString(2),
                    Stufe = sqlite_datareader.GetString(3),
                    Suffix = sqlite_datareader.GetString(4),
                    Istkurs = Convert.ToBoolean(sqlite_datareader.GetInt32(5))
                };
                retKurs.Art = retKurs.Istkurs ? "PUT" : "PUK";

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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "SELECT DISTINCT kursbez FROM unterrichtet WHERE lehrerid = @lulid;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@lulid", lulid));
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            while (sqlite_datareader.Read())
            {
                kliste.Add(await GetKurs(sqlite_datareader.GetString(0)));
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "SELECT kursbez FROM nimmtteil WHERE schuelerid = @susid;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@susid", susid));
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            while (sqlite_datareader.Read())
            {
                kliste.Add(await GetKurs(sqlite_datareader.GetString(0)));
            }

            return new ReadOnlyCollection<Kurs>(kliste);
        }

        /// <summary>
        /// gibt die Informationen ID, Nachname, Vorname, Mail, Kürzel und Fakultas der Lehrkraft zur übergebenen ID zurück
        /// </summary>
        /// <param name="id"></param>
        private async Task<LuL> GetLehrkraft(int id)
        {
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText =
                "SELECT id,nachname,vorname,mail,kuerzel,fakultas,pwtemp FROM lehrkraft WHERE id = @id;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@id", id));
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            LuL lehrkraft = new();
            while (sqlite_datareader.Read())
            {
                lehrkraft.ID = sqlite_datareader.GetInt32(0);
                lehrkraft.Nachname = sqlite_datareader.GetString(1);
                lehrkraft.Vorname = sqlite_datareader.GetString(2);
                lehrkraft.Mail = sqlite_datareader.GetString(3);
                lehrkraft.Kuerzel = sqlite_datareader.GetString(4);
                lehrkraft.Fakultas = sqlite_datareader.GetString(5);
                lehrkraft.Pwttemp = sqlite_datareader.GetString(6);
            }

            return lehrkraft;
        }

        /// <summary>
        /// gibt die Informationen ID, Nachname, Vorname, Mail, Kürzel und Fakultas der Lehrkraft zum übergebenen Kürzel zurück
        /// </summary>
        /// <param name="kuerzel"></param>
        public async Task<LuL> GetLehrkraft(string kuerzel)
        {
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText =
                "SELECT id,nachname,vorname,mail,kuerzel,fakultas,pwtemp FROM lehrkraft WHERE kuerzel = @kuerzel;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@kuerzel", kuerzel));
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            LuL lehrkraft = new();
            while (sqlite_datareader.Read())
            {
                lehrkraft.ID = sqlite_datareader.GetInt32(0);
                lehrkraft.Nachname = sqlite_datareader.GetString(1);
                lehrkraft.Vorname = sqlite_datareader.GetString(2);
                lehrkraft.Mail = sqlite_datareader.GetString(3);
                lehrkraft.Kuerzel = sqlite_datareader.GetString(4);
                lehrkraft.Fakultas = sqlite_datareader.GetString(5);
                lehrkraft.Pwttemp = sqlite_datareader.GetString(6);
            }

            return lehrkraft;
        }

        /// <summary>
        /// gibt die IDs aller Lehrkräfte zurück
        /// </summary>
        public async Task<ReadOnlyCollection<int>> GetLehrerIDListe()
        {
            List<int> llist = new();
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "SELECT id FROM lehrkraft;";
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            while (sqlite_datareader.Read())
            {
                llist.Add(sqlite_datareader.GetInt32(0));
            }

            return new ReadOnlyCollection<int>(llist);
        }

        /// <summary>
        /// gibt den vollständigen Inhalt der Tabelle lehrer in der Reihenfolge ID, Nachname, Vorname, Mail, Kürzel und Fakultas zurück
        /// </summary>
        public async Task<ReadOnlyCollection<LuL>> GetLehrerListe()
        {
            List<LuL> llist = new();
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "SELECT id,nachname,vorname,mail,kuerzel,fakultas,pwtemp FROM lehrkraft;";
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            while (sqlite_datareader.Read())
            {
                LuL lehrkraft = new()
                {
                    ID = sqlite_datareader.GetInt32(0),
                    Nachname = sqlite_datareader.GetString(1),
                    Vorname = sqlite_datareader.GetString(2),
                    Mail = sqlite_datareader.GetString(3),
                    Kuerzel = sqlite_datareader.GetString(4),
                    Fakultas = sqlite_datareader.GetString(5),
                    Pwttemp = sqlite_datareader.GetString(6)
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "SELECT stufe,datum, nachricht FROM log;";
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            while (sqlite_datareader.Read())
            {
                var returnstr = "";
                for (var i = 0; i < sqlite_datareader.FieldCount; i++)
                {
                    returnstr += sqlite_datareader.GetString(i) + "\t";
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "SELECT stufe,datum, nachricht FROM log WHERE stufe = @stufe;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@stufe", stufe));
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            while (sqlite_datareader.Read())
            {
                var returnstr = "";
                for (var i = 0; i < sqlite_datareader.FieldCount; i++)
                {
                    returnstr += sqlite_datareader.GetString(i) + "\t";
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "SELECT langfach FROM fachersatz WHERE kurzfach = @shortsubject;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@shortsubject", shortsubject));
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            while (sqlite_datareader.Read())
            {
                return sqlite_datareader.GetString(0);
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "SELECT lehrerid FROM unterrichtet WHERE kursbez = @kbez;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@kbez", kbez));
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            while (sqlite_datareader.Read())
            {
                lliste.Add(await GetLehrkraft(sqlite_datareader.GetInt32(0)));
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText =
                "SELECT unterrichtet.lehrerid FROM unterrichtet JOIN nimmtteil ON nimmtteil.kursbez = unterrichtet.kursbez WHERE schuelerid = @susid;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@susid", susid));
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            while (sqlite_datareader.Read())
            {
                for (var i = 0; i < sqlite_datareader.FieldCount; i++)
                {
                    lliste.Add(await GetLehrkraft(sqlite_datareader.GetInt32(0)));
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText =
                "SELECT id,nachname,vorname,mail,klasse,nutzername,aixmail,zweitaccount,zweitmail FROM schueler WHERE id = @id;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@id", id));
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            SuS schuelerin = new();
            while (sqlite_datareader.Read())
            {
                schuelerin.ID = sqlite_datareader.GetInt32(0);
                schuelerin.Nachname = sqlite_datareader.GetString(1);
                schuelerin.Vorname = sqlite_datareader.GetString(2);
                schuelerin.Mail = sqlite_datareader.GetString(3);
                schuelerin.Klasse = sqlite_datareader.GetString(4);
                schuelerin.Nutzername = sqlite_datareader.GetString(5);
                schuelerin.Aixmail = sqlite_datareader.GetString(6);
                schuelerin.Zweitaccount = Convert.ToBoolean(sqlite_datareader.GetInt32(7));
                schuelerin.Zweitmail = sqlite_datareader.GetString(8);
            }

            return schuelerin;
        }

        /// <summary>
        /// gibt die Informationen ID, Nachname, Vorname, Mail, Klasse und Nutzername des Schülers/Schülerin zur übergebenen Kombination aus Vor- und Nachname zurück
        /// </summary>
        /// <param name="vorname"></param>
        /// <param name="nachname"></param>
        private async Task<SuS> GetSchueler(string vorname, string nachname)
        {
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText =
                "SELECT id,nachname,vorname,mail,klasse,nutzername,aixmail,zweitaccount,zweitmail FROM schueler WHERE vorname = @vorname AND nachname = @nachname;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@vorname", vorname));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@nachname", nachname));
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            SuS schuelerin = new();
            while (sqlite_datareader.Read())
            {
                schuelerin.ID = sqlite_datareader.GetInt32(0);
                schuelerin.Nachname = sqlite_datareader.GetString(1);
                schuelerin.Vorname = sqlite_datareader.GetString(2);
                schuelerin.Mail = sqlite_datareader.GetString(3);
                schuelerin.Klasse = sqlite_datareader.GetString(4);
                schuelerin.Nutzername = sqlite_datareader.GetString(5);
                schuelerin.Aixmail = sqlite_datareader.GetString(6);
                schuelerin.Zweitaccount = Convert.ToBoolean(sqlite_datareader.GetInt32(7));
                schuelerin.Zweitmail = sqlite_datareader.GetString(8);
            }

            return schuelerin;
        }

        /// <summary>
        /// gibt die IDs aller SchülerInnen zurück
        /// </summary>
        public async Task<ReadOnlyCollection<int>> GetSchuelerIDListe()
        {
            List<int> slist = new();
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "SELECT id FROM schueler;";
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            while (sqlite_datareader.Read())
            {
                slist.Add(sqlite_datareader.GetInt32(0));
            }

            return new ReadOnlyCollection<int>(slist);
        }

        /// <summary>
        /// gibt den vollständigen Inhalt der Tabelle schueler in der Reihenfolge ID, Nachname, Vorname, Mail, Klasse, Nutzername, AIXmail, zweitaccount(1/0) und zweitmailadresse zurück
        /// </summary>
        public async Task<ReadOnlyCollection<SuS>> GetSchuelerListe()
        {
            List<SuS> slist = new();
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText =
                "SELECT id,nachname,vorname,mail,klasse,nutzername,aixmail,zweitaccount,zweitmail FROM schueler;";
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            while (sqlite_datareader.Read())
            {
                SuS schuelerin = new()
                {
                    ID = sqlite_datareader.GetInt32(0),
                    Nachname = sqlite_datareader.GetString(1),
                    Vorname = sqlite_datareader.GetString(2),
                    Mail = sqlite_datareader.GetString(3),
                    Klasse = sqlite_datareader.GetString(4),
                    Nutzername = sqlite_datareader.GetString(5),
                    Aixmail = sqlite_datareader.GetString(6),
                    Zweitaccount = Convert.ToBoolean(sqlite_datareader.GetInt32(7)),
                    Zweitmail = sqlite_datareader.GetString(8)
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "SELECT setting,value FROM settings;";
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            Settings settings_result = new();
            while (sqlite_datareader.Read())
            {
                var key = sqlite_datareader.GetString(0);
                var value = string.IsNullOrEmpty(sqlite_datareader.GetString(1)) ? "" : sqlite_datareader.GetString(1);
                switch (key)
                {
                    case "mailsuffix":
                        settings_result.Mailsuffix = value;
                        break;
                    case "kurssuffix":
                        settings_result.Kurssuffix = value;
                        break;
                    case "fachersatz":
                        settings_result.Fachersetzung = value;
                        break;
                    case "erprobungsstufenleitung":
                        settings_result.Erprobungstufenleitung = value;
                        break;
                    case "mittelstufenleitung":
                        settings_result.Mittelstufenleitung = value;
                        break;
                    case "efstufenleitung":
                        settings_result.EFStufenleitung = value;
                        break;
                    case "q1stufenleitung":
                        settings_result.Q1Stufenleitung = value;
                        break;
                    case "q2stufenleitung":
                        settings_result.Q2Stufenleitung = value;
                        break;
                    case "oberstufenkoordination":
                        settings_result.Oberstufenkoordination = value;
                        break;
                }
            }

            return settings_result;
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

        /// <summary>
        /// gibt die IDs der SuS in der Klasse als Liste zurück
        /// </summary>
        /// <param name="klasse"></param>
        /// <returns>Integer-Liste der SuS-IDs</returns>
        public async Task<ReadOnlyCollection<SuS>> GetSuSAusKlasse(string klasse)
        {
            List<SuS> sliste = new();
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "SELECT id FROM schueler WHERE klasse = @klasse;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@klasse", klasse));
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            while (sqlite_datareader.Read())
            {
                sliste.Add(await GetSchueler(sqlite_datareader.GetInt32(0)));
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "SELECT schuelerid FROM nimmtteil WHERE kursbez = @kbez;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@kbez", kbez));
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            while (sqlite_datareader.Read())
            {
                for (var i = 0; i < sqlite_datareader.FieldCount; i++)
                {
                    sliste.Add(await GetSchueler(sqlite_datareader.GetInt32(0)));
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "SELECT DISTINCT id FROM schueler WHERE klasse LIKE @stufe;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@stufe", stufe + "%"));
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            while (sqlite_datareader.Read())
            {
                sliste.Add(await GetSchueler(sqlite_datareader.GetInt32(0)));
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText =
                "SELECT nimmtteil.schuelerid FROM unterrichtet JOIN nimmtteil ON nimmtteil.kursbez = unterrichtet.kursbez WHERE lehrerid = @lulid;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@lulid", lulid));
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            while (sqlite_datareader.Read())
            {
                sliste.Add(await GetSchueler(sqlite_datareader.GetInt32(0)));
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

            for (var i = 1; i < lines.Length; i++)
            {
                await StartTransaction();
                try
                {
                    var tmpkurs = lines[i].Split('|');
                    for (var j = 0; j < tmpkurs.Length; j++)
                    {
                        tmpkurs[j] = tmpkurs[j].Trim('"');
                    }

                    var krz = tmpkurs[inl];
                    var stmp = await GetSchueler(tmpkurs[inv].Replace("'", ""), tmpkurs[inn].Replace("'", ""));
                    var ltmp = await GetLehrkraft(krz);
                    if (stmp.ID > 50000 && ltmp.ID > 0)
                    {
                        var klasse = stmp.Klasse;
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
                                !kursart.Equals("ZUV")) //PUK = Klassenunterricht; ZUV = Zusatzveranstaltung
                            {
                                fach = tmpkurs[inf];
                                for (var k = 0; k < fachersetzung.Count - 1; k++)
                                {
                                    if (fach.Equals(fachersetzung[k].Split(':')[0]))
                                    {
                                        fach = fachersetzung[k].Split(':')[1];
                                    }
                                }

                                var bez = stufe + "-" + tmpkurs[ink];
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
                catch (Exception ex)
                {
#if DEBUG
                    await AddLogMessage("Debug", ex.StackTrace + ";" + ex.Message + "at " + i);
#endif
                    await AddLogMessage("Error", "Fehler beim Einlesen der Kurse");
                }

                await StopTransaction();
            }
        }

        /// <summary>
        /// löscht das komplette Log
        /// </summary>
        public async Task LoescheLog()
        {
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "DELETE FROM log WHERE stufe = 'Info' OR stufe = 'Hinweis' OR stufe = 'Fehler';";
            sqlite_cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// löscht das Log für die übergebene Stufe
        /// </summary>
        /// <param name="stufe">"Fehler", "Hinweis", oder "Info"</param>
        public async Task LoescheLog(string stufe)
        {
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "DELETE FROM log WHERE stufe = @stufe;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@stufe", stufe));
            sqlite_cmd.ExecuteNonQuery();
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
                            tmpkuk[inkrz].ToUpper(), tmpkuk[inm], tmpkuk[infak].TrimEnd(';'));
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "DELETE FROM nimmtteil WHERE kursbez = @kbez;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@kbez", kbez));
            sqlite_cmd.ExecuteNonQuery();
            sqlite_cmd.CommandText = "DELETE FROM unterrichtet WHERE kursbez = @kbez;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@kbez", kbez));
            sqlite_cmd.ExecuteNonQuery();
            sqlite_cmd.CommandText = "DELETE FROM kurse WHERE bez =@kbez;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@kbez", kbez));
            sqlite_cmd.ExecuteNonQuery();
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "DELETE FROM unterrichtet WHERE lehrerid = @lid;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@lid", lid));
            sqlite_cmd.ExecuteNonQuery();
            sqlite_cmd.CommandText = "DELETE FROM lehrkraft INDEXED BY lindex WHERE id = @lid;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@lid", lid));
            sqlite_cmd.ExecuteNonQuery();
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "DELETE FROM unterrichtet WHERE lehrerid = @lid AND kursbez = @kbez;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@lid", lid));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@kbez", kbez));
            sqlite_cmd.ExecuteNonQuery();
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "DELETE FROM nimmtteil WHERE schuelerid = @sid;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@sid", sid));
            sqlite_cmd.ExecuteNonQuery();
            sqlite_cmd.CommandText = "DELETE FROM schueler WHERE id = @sid;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@sid", sid));
            sqlite_cmd.ExecuteNonQuery();
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "DELETE FROM nimmtteil WHERE schuelerid = @sid AND kursbez = @kbez;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@sid", sid));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@kbez", kbez));
            sqlite_cmd.ExecuteNonQuery();
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
        private async Task SetKurzLangFach(string[] fachk, string[] fachl)
        {
            if (fachk.Length == fachl.Length)
            {
                await StartTransaction();
                var sqlite_cmd = sqlite_conn.CreateCommand();
                for (var i = 0; i < fachk.Length; i++)
                {
                    var kurzesfach = fachk[i];
                    var langesfach = fachl[i];
                    sqlite_cmd.CommandText =
                        "INSERT OR IGNORE INTO fachersatz (kurzfach, langfach) VALUES (@kfach, @lfach);";
                    sqlite_cmd.Parameters.Add(new SQLiteParameter("@kfach", kurzesfach));
                    sqlite_cmd.Parameters.Add(new SQLiteParameter("@lfach", langesfach));
                    sqlite_cmd.ExecuteNonQuery();
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            // sqlite_cmd.CommandText = "INSERT OR IGNORE INTO settings (mailsuffix, kurssuffix, fachersetzung) VALUES (@mailsuffix, @kurssuffix, @fachersatz);";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@mailsuffixparam", settings.Mailsuffix));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@kurssuffixparam", settings.Kurssuffix));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@fachersatzparam", settings.Fachersetzung));

            sqlite_cmd.Parameters.Add(new SQLiteParameter("@mailsuffix", "mailsuffix"));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@kurssuffix", "kurssuffix"));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@fachersatz", "fachersatz"));

            sqlite_cmd.Parameters.Add(new SQLiteParameter("@erprobungstufenleitung", "erprobungsstufenleitung"));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@mittelstufenleitung", "mittelstufenleitung"));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@efstufenleitung", "efstufenleitung"));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@q1stufenleitung", "q1stufenleitung"));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@q2stufenleitung", "q2stufenleitung"));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@oberstufenkoordination", "oberstufenkoordination"));

            sqlite_cmd.Parameters.Add(new SQLiteParameter("@erprobungstufenleitungparam",
                settings.Erprobungstufenleitung));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@mittelstufenleitungparam", settings.Mittelstufenleitung));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@efstufenleitungparam", settings.EFStufenleitung));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@q1stufenleitungparam", settings.Q1Stufenleitung));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@q2stufenleitungparam", settings.Q2Stufenleitung));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@oberstufenkoordinationparam",
                settings.Oberstufenkoordination));
            // sqlite_cmd.ExecuteNonQuery();
            sqlite_cmd.CommandText =
                "INSERT OR REPLACE INTO settings (setting,value) VALUES(@mailsuffix, @mailsuffixparam)";
            sqlite_cmd.ExecuteNonQuery();
            sqlite_cmd.CommandText =
                "INSERT OR REPLACE INTO settings (setting,value) VALUES(@kurssuffix, @kurssuffixparam)";
            sqlite_cmd.ExecuteNonQuery();
            sqlite_cmd.CommandText =
                "INSERT OR REPLACE INTO settings (setting,value) VALUES(@fachersatz, @fachersatzparam)";
            sqlite_cmd.ExecuteNonQuery();
            sqlite_cmd.CommandText =
                "INSERT OR REPLACE INTO settings (setting,value) VALUES(@erprobungstufenleitung, @erprobungstufenleitungparam)";
            sqlite_cmd.ExecuteNonQuery();
            sqlite_cmd.CommandText =
                "INSERT OR REPLACE INTO settings (setting,value) VALUES(@mittelstufenleitung, @mittelstufenleitungparam)";
            sqlite_cmd.ExecuteNonQuery();
            sqlite_cmd.CommandText =
                "INSERT OR REPLACE INTO settings (setting,value) VALUES(@efstufenleitung, @efstufenleitungparam)";
            sqlite_cmd.ExecuteNonQuery();
            sqlite_cmd.CommandText =
                "INSERT OR REPLACE INTO settings (setting,value) VALUES(@q1stufenleitung, @q1stufenleitungparam)";
            sqlite_cmd.ExecuteNonQuery();
            sqlite_cmd.CommandText =
                "INSERT OR REPLACE INTO settings (setting,value) VALUES(@q2stufenleitung, @q2stufenleitungparam)";
            sqlite_cmd.ExecuteNonQuery();
            sqlite_cmd.CommandText =
                "INSERT OR REPLACE INTO settings (setting,value) VALUES(@oberstufenkoordination, @oberstufenkoordinationparam)";
            sqlite_cmd.ExecuteNonQuery();

            await SetKurzLangFach(settings.Kurzfaecher, settings.Langfaecher);
        }


        /// <summary>
        /// start von SQLite-Transaktionen zur Beschleunigung von Inserts oder Updates
        /// </summary>
        public async Task StartTransaction()
        {
            trans = true;
            dbtrans = sqlite_conn.BeginTransaction();
        }

        /// <summary>
        /// ende von SQLite-Transaktionen zur Beschleunigung von Inserts oder Updates
        /// </summary>
        public async Task StopTransaction()
        {
            trans = false;
            await dbtrans.CommitAsync();
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
                    if (tmpsus[ink].StartsWith("EF"))
                    {
                        tmpsus[ink] = "EF";
                    }

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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText =
                "UPDATE kurse SET fach = @fach, klasse = @klasse, stufe = @stufe, suffix = @suffix, istkurs = @istkurs WHERE bez=@bez;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@fach", fach));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@bez", bez));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@klasse", klasse));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@stufe", stufe));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@suffix", suffix));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@istkurs", istkurs));
            sqlite_cmd.ExecuteNonQuery();
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
        public async Task UpdateLehrkraft(int id, string vorname, string nachname, string kuerzel, string mail,
            string fakultas, string pwtemp)
        {
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText =
                "UPDATE lehrkraft SET nachname=@nachname, vorname=@vorname, kuerzel= @kuerzel, mail=@mail, fakultas=@fakultas,pwtemp = @pwtemp WHERE id=@id;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@id", id));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@vorname", vorname));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@nachname", nachname));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@kuerzel", kuerzel.ToUpper()));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@mail", mail));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@fakultas", fakultas.TrimEnd(';')));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@pwtemp", pwtemp));

            sqlite_cmd.ExecuteNonQuery();
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText =
                "UPDATE schueler SET nachname=@nachname, vorname=@vorname, mail=@mail, klasse=@klasse, nutzername=@nutzername, aixmail=@aixmail, zweitaccount = @zweitaccount, zweitmail=@zweitmail WHERE id=@id;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@id", id));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@vorname", vorname));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@nachname", nachname));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@mail", mail));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@klasse", klasse));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@nutzername", nutzername));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@aixmail", aixmail));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@zweitaccount", zweitaccount));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@zweitmail", zweitmail));
            sqlite_cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// setzt den AIXMailadresse des per ID angegebenen Schülers/Schülerin
        /// </summary>
        /// <param name="id"></param>
        /// <param name="mail"></param>
        private void UpdateAIXSuSAdressen(int id, string mail)
        {
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "UPDATE schueler SET aixmail = @mail WHERE id = @id;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@id", id));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@mail", mail));
            sqlite_cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// setzt den Nutzername des per ID angegebenen Schülers/Schülerin
        /// </summary>
        /// <param name="id"></param>
        /// <param name="nutzername"></param>
        private void UpdateSchuelerNutzername(int id, string nutzername)
        {
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "UPDATE schueler SET nutzername = @nutzername WHERE id = @id;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@id", id));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@nutzername", nutzername));
            sqlite_cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Liest zur lehrerid, das temp. Passwort aus der Datenbank aus und gibt es zurück
        /// </summary>
        /// <param name="lehrerid">Die lehrerid, für die das temporäre Passwort ausgelesen werden soll</param>
        /// <returns>String temporäre Passwort</returns>
        private async Task<string> GetTempPasswort(int lehrerid)
        {
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "SELECT pwtemp FROM lehrkraft WHERE id=" + lehrerid + ";";
            var sqlite_datareader = sqlite_cmd.ExecuteReader();
            var tpwd = "";
            while (sqlite_datareader.Read())
            {
                tpwd = sqlite_datareader.GetString(0);
            }

            return tpwd;
        }

        /// <summary>
        /// Setzt das temp.Password für die angegebene lehrerid
        /// </summary>
        /// <param name="lehrerid">lehrerid, für die das Passwort geändert werden soll</param>
        /// <param name="pwd">das neue Passwort</param>
        public async void SetTPwd(int lehrerid, string pwd)
        {
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "UPDATE lehrkraft SET pwtemp = @pwd WHERE id = @lehrerid;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@pwd", pwd));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@lehrerid", lehrerid));
            sqlite_cmd.ExecuteNonQuery();
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
            var sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = "UPDATE schueler SET zweitaccount = @status WHERE id = @susid;";
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@status", pStatus));
            sqlite_cmd.Parameters.Add(new SQLiteParameter("@susid", id));
            sqlite_cmd.ExecuteNonQuery();
        }

        /// <summary>
        ///         /// 
        /// </summary>
        /// <returns></returns>
        public async Task<int> GetCurrentStatusAsync()
        {
            return status;
        }
    }
}