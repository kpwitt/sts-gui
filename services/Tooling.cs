using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SchulDB;

public static class Tooling {
    /// <summary>
    /// generiert ein Passwort bestehend aus Buchstaben, Ziffern und Sonderzeichen
    /// </summary>
    /// <param name="laenge">Länge des zu generierendes Passwort</param>
    /// <returns>String das generierte Passwort aus Zufallszeichen</returns>
    public static string GeneratePasswort(int laenge) {
        //erlaubt beim Hoster: /-_#*+!§,()=:.$äöüÄÖÜß
        const string validPasswordChars = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ1234567890+-.()!*/_#";
        StringBuilder res = new();
        while (0 < laenge--) {
            res.Append(validPasswordChars[RandomNumberGenerator.GetInt32(validPasswordChars.Length)]);
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
    public static Regex ToRegex(string pattern, bool ignoreCase = false) {
        ArgumentNullException.ThrowIfNull(pattern);
        var escaped = Regex.Escape(pattern);
        escaped = escaped
            .Replace(@"\.", ".") // genau ein beliebiges Zeichen
            .Replace(@"\+", ".?") // ein oder kein beliebiges Zeichen
            .Replace(@"\*", ".*"); // beliebig viele beliebige Zeichen
        var options = RegexOptions.CultureInvariant;
        if (ignoreCase) options |= RegexOptions.IgnoreCase;
        return new Regex("^" + escaped + "$", options);
    }

    ///<summary>
    /// Sucht in der Liste alle Einträge, die zum Wildcard-Muster passen
    /// </summary>
    public static List<string> FindMatches(List<string> source, string pattern, bool ignoreCase = false) {
        ArgumentNullException.ThrowIfNull(source);
        var regex = ToRegex(pattern, ignoreCase);
        return source.Where(item => regex.IsMatch(item)).ToList();
    }
}