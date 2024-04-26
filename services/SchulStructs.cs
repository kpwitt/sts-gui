namespace SchulDB
{
#pragma warning disable CS1591
    public struct SuS
    {
        public int ID { get; set; }
        public string Vorname { get; set; }
        public string Nachname { get; set; }
        public string Mail { get; set; }
        public string Klasse { get; set; }
        public string Nutzername { get; set; }
        public string Aixmail { get; set; }
        public string Zweitmail { get; set; }
        public bool Zweitaccount { get; set; }

        public SuS(int id, string vorname, string nachname, string mail, string klasse, string nutzername,
            string aixmail, string zweitmail, bool zweitaccount)
        {
            ID = id;
            Vorname = vorname;
            Nachname = nachname;
            Mail = mail;
            Klasse = klasse;
            Nutzername = nutzername;
            Aixmail = aixmail;
            Zweitmail = zweitmail;
            Zweitaccount = zweitaccount;
        }

        public string GetStufe()
        {
            var stufen = new string[] { "5", "6", "7", "8", "9", "10", "EF", "Q1", "Q2" };
            foreach (var stufe in stufen)
            {
                if (Klasse.StartsWith(stufe)) return stufe;
            }

            return "";
        }
    }

    public struct LuL
    {
        public int ID { get; set; }
        public string Vorname { get; set; }
        public string Nachname { get; set; }
        public string Mail { get; set; }
        public string Kuerzel { get; set; }
        public string Fakultas { get; set; }
        public string Pwttemp { get; set; }
        public string Favo { get; set; }
        public string SFavo { get; set; }


        public LuL(int id, string vorname, string nachname, string mail, string kuerzel, string fakultas,
            string pwttemp)
        {
            ID = id;
            Vorname = vorname;
            Nachname = nachname;
            Mail = mail;
            Kuerzel = kuerzel;
            Fakultas = fakultas;
            Pwttemp = pwttemp;
            Favo = "";
            SFavo = "";
        }

        public LuL(int id, string vorname, string nachname, string mail, string kuerzel, string fakultas,
            string pwttemp, string favo, string sfavo)
        {
            ID = id;
            Vorname = vorname;
            Nachname = nachname;
            Mail = mail;
            Kuerzel = kuerzel;
            Fakultas = fakultas;
            Pwttemp = pwttemp;
            Favo = favo;
            SFavo = sfavo;
        }
    }

    public struct Kurs
    {
        public string Bezeichnung { get; set; }
        public string Fach { get; set; }
        public string Klasse { get; set; }
        public string Stufe { get; set; }
        public string Suffix { get; set; }
        public bool IstKurs { get; set; }
        public string Art { get; set; }


        public Kurs(string bezeichnung, string fach, string klasse, string stufe, string suffix, bool istKurs,
            string art)
        {
            Bezeichnung = bezeichnung;
            Fach = fach;
            Klasse = klasse;
            Stufe = stufe;
            Suffix = suffix;
            IstKurs = istKurs;
            Art = art;
        }
    }

    public struct Settings
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
    }
}