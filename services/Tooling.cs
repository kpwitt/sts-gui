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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SchulDB;

public static class Tooling {
    /// <summary>
    /// generiert ein Passwort bestehend aus Buchstaben, Ziffern und Sonderzeichen
    /// </summary>
    /// <param name="laenge">Länge des zu generierendes Passwort</param>
    /// <returns>String das generierte Passwort aus Zufallszeichen</returns>
    public static string GeneratePasswort(int laenge) {
        //erlaubt beim Hoster: /-_#*+!§,()=:.$äöüÄÖÜß
        const string validChars = "+-.()!*/_#";
        const string validLetters = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
        const string validDigits = "0123456789";
        const string validPasswordChars = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ1234567890+-.()!*/_#";
        StringBuilder res = new();
        var c = 0;
        while (c < laenge) {
            res.Append(validPasswordChars[RandomNumberGenerator.GetInt32(validPasswordChars.Length)]);
            c++;
        }

        while  (!(res.ToString().Any(validChars.Contains) && res.ToString().Any(validLetters.Contains) &&
            res.ToString().Any(validDigits.Contains))) {
            res.Clear();
                    c = 0;
                    while (c < laenge) {
                        res.Append(validPasswordChars[RandomNumberGenerator.GetInt32(validPasswordChars.Length)]);
                        c++;
                    }
        }
        return res.ToString();
    }

    /// <summary>
    /// Hilfsmethode, die die Klasse der Stufe zuordnet
    /// </summary>
    /// <param name="klasse"></param>
    /// <returns></returns>
    public static string KlasseToStufe(string klasse) {
        return klasse.Length switch {
            2 => klasse[..1],
            3 => klasse[..2],
            _ => ""
        };
    }

    ///<summary>
    /// Konvertiert das benutzerdefinierte Wildcard-Muster in einen Regex
    /// </summary>
    public static Regex ToRegex(string pattern, bool followCase = false) {
        ArgumentNullException.ThrowIfNull(pattern);
        var escaped = Regex.Escape(pattern);
        escaped = escaped
            .Replace(@"\.", ".") // genau ein beliebiges Zeichen
            .Replace(@"\?", ".?") // ein oder kein beliebiges Zeichen
            .Replace(@"\*", ".*"); // beliebig viele beliebige Zeichen
        var options = RegexOptions.CultureInvariant;
        if (followCase) return new Regex("^" + escaped + "$", options);
        options |= RegexOptions.IgnoreCase;
        return new Regex(escaped, options);
    }

    public static async Task AppendToFileAsync(string filepath, List<string> content) {
        if (!File.Exists(filepath)) {
            await WriteToFileAsync(filepath, content);
            return;
        }
        await File.AppendAllLinesAsync(filepath, content, Encoding.UTF8);
    }
    
    public static async Task WriteToFileAsync(string filepath, List<string> content) {
        await File.WriteAllLinesAsync(filepath, content, Encoding.UTF8);
    }
    
    public static void AppendToFile(string filepath, List<string> content) {
        if (!File.Exists(filepath)) {
            WriteToFile(filepath, content);
            return;
        }
        File.AppendAllLines(filepath, content, Encoding.UTF8);
    }
    
    public static void WriteToFile(string filepath, List<string> content) {
        File.WriteAllLines(filepath, content, Encoding.UTF8);
    }

    public static AppSettings ReadAppSettings() {
        var loc = Assembly.GetCallingAssembly().Location;
        if (loc == "") return new AppSettings();
        var dInfo = new FileInfo(loc).Directory;
        if (dInfo == null) return new AppSettings();
        
        var settingspath = dInfo.FullName +
                           "\\appsettings.json";
        AppSettings appSettings;
        if (File.Exists(settingspath)) {
            try {
                appSettings =
                    JsonSerializer.Deserialize<AppSettings>(File.ReadAllTextAsync(settingspath).Result);
            }
            catch (Exception exception) {
                throw new Exception("Failed to read appsettings.json", exception);
            }
        }
        else appSettings = new AppSettings();

        return appSettings;
    }
}