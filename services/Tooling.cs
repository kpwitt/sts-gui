using System.Security.Cryptography;
using System.Text;

namespace SchulDB;

public static class Tooling
{
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
    /// Hilfsmethode, die die Klasse der Stufe zuordnet
    /// </summary>
    /// <param name="klasse"></param>
    /// <returns></returns>
    public static string KlasseToStufe(string klasse)
    {
        return klasse.Length switch
        {
            2 => klasse[..1],
            3 => klasse[..2],
            _ => ""
        };
    }
}