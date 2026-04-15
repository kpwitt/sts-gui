using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace StS_GUI_Avalonia;

public static class LocalCryptoService {
    /// <summary>
    /// Decrypts an encrypted file with the FileEncrypt method through its path and the plain password.
    /// </summary>
    /// <param name="inputFile"></param>
    /// <param name="outputFile"></param>
    /// <param name="password"></param>
    public static void FileDecrypt(string inputFile, string outputFile, string password) {
        var salt = new byte[32];
        using var fsCrypt = new FileStream(inputFile, FileMode.Open, FileAccess.Read);
        fsCrypt.ReadExactly(salt, 0, salt.Length);
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.BlockSize = 128;
        var keyBytes = aes.KeySize / 8;
        var ivBytes = aes.BlockSize / 8;
        var totalLen = keyBytes + ivBytes;
        var pbkdf2Result = Rfc2898DeriveBytes.Pbkdf2(password, salt, 50_000, HashAlgorithmName.SHA256, totalLen);
        aes.Key = pbkdf2Result.AsSpan(0, keyBytes).ToArray();
        aes.IV = pbkdf2Result.AsSpan(keyBytes, ivBytes).ToArray();
        aes.Padding = PaddingMode.PKCS7;
        aes.Mode = CipherMode.CFB;
        using var cs = new CryptoStream(fsCrypt, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var fsOut = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
        var buffer = new byte[1 << 20];
        int read;
        while ((read = cs.Read(buffer, 0, buffer.Length)) > 0) {
            fsOut.Write(buffer, 0, read);
        }
    }

    /// <summary>
    /// Encrypts a file from its path and a plain password.
    /// </summary>
    /// <param name="inputFile"></param>
    /// <param name="password"></param>
    /// <param name="outputFile"></param>
    public static void FileEncrypt(string inputFile, string outputFile, string password) {
        {
            const int iterations = 50_000;
            const int keySizeBits = 256;
            var salt = new byte[32];
            RandomNumberGenerator.Fill(salt);
            using var aes = Aes.Create();
            aes.KeySize = keySizeBits;
            aes.BlockSize = 128;
            aes.Padding = PaddingMode.PKCS7;
            aes.Mode = CipherMode.CFB;
            var keyBytes = aes.KeySize / 8;
            var ivBytes = aes.BlockSize / 8;
            var totalLen = keyBytes + ivBytes;
            var derived = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, totalLen);
            aes.Key = derived.AsSpan(0, keyBytes).ToArray();
            aes.IV = derived.AsSpan(keyBytes, ivBytes).ToArray();
            using FileStream fsOut = new(outputFile, FileMode.Create, FileAccess.Write);
            fsOut.Write(salt, 0, salt.Length);
            using CryptoStream crypto = new(fsOut, aes.CreateEncryptor(), CryptoStreamMode.Write);
            using FileStream fsIn = new(inputFile, FileMode.Open, FileAccess.Read);
            var buffer = new byte[1 << 20];
            try {
                int read;
                while ((read = fsIn.Read(buffer, 0, buffer.Length)) > 0) {
                    crypto.Write(buffer, 0, read);
                }
            }
            catch (Exception ex) {
                File.WriteAllText("error.log", $"Error: {ex.Message}");
                Debug.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}