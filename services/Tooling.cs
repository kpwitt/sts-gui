using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

    public static async Task AppendToFile(string filepath, List<string> content) {
        if (!File.Exists(filepath)) {
            await WriteToFile(filepath, content);
            return;
        }
        File.AppendAllLinesAsync(filepath, content, Encoding.UTF8);
    }
    
    public static async Task WriteToFile(string filepath, List<string> content) {
        await File.WriteAllLinesAsync(filepath, content, Encoding.UTF8);
    }
}