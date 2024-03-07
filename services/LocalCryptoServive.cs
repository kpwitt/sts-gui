using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace StS_GUI_Avalonia;

public class LocalCryptoServive
{
    //quelle: https://ourcodeworld.com/articles/read/471/how-to-encrypt-and-decrypt-files-using-the-aes-encryption-algorithm-in-c-sharp
    /// <summary>
    /// Creates a random salt that will be used to encrypt your file. This method is required on FileEncrypt.
    /// </summary>
    /// <returns></returns>
    private static byte[] GenerateRandomSalt()
    {
        var data = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        for (var i = 0; i < 10; i++)
        {
            // Fill the buffer with the generated data
            rng.GetBytes(data);
        }

        return data;
    }

    /// <summary>
    /// Decrypts an encrypted file with the FileEncrypt method through its path and the plain password.
    /// </summary>
    /// <param name="inputFile"></param>
    /// <param name="outputFile"></param>
    /// <param name="password"></param>
    public static void FileDecrypt(string inputFile, string outputFile, string password)
    {
        var passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
        var salt = new byte[32];
        FileStream fsCrypt = new(inputFile, FileMode.Open);
        fsCrypt.Read(salt, 0, salt.Length);
        var aes = Aes.Create();
        aes.KeySize = 256;
        aes.BlockSize = 128;
        var key = new Rfc2898DeriveBytes(passwordBytes, salt, 50000);
        aes.Key = key.GetBytes(aes.KeySize / 8);
        aes.IV = key.GetBytes(aes.BlockSize / 8);
        aes.Padding = PaddingMode.PKCS7;
        aes.Mode = CipherMode.CFB;
        CryptoStream cs = new(fsCrypt, aes.CreateDecryptor(), CryptoStreamMode.Read);
        FileStream fsOut = new(outputFile, FileMode.Create);
        var buffer = new byte[1048576];
        try
        {
            int read;
            while ((read = cs.Read(buffer, 0, buffer.Length)) > 0)
            {
                fsOut.Write(buffer, 0, read);
            }
        }
        catch (Exception ex)
        {
            File.WriteAllText( "error.log", "Error: " + ex.Message);
            Debug.WriteLine("Error: " + ex.Message);
        }

        try
        {
            cs.Close();
        }
        catch (Exception ex)
        {
            File.WriteAllText( "error.log", "Error: " + ex.Message);
            Debug.WriteLine("Error by closing CryptoStream: " + ex.Message);
        }
        finally
        {
            fsOut.Close();
            fsCrypt.Close();
        }
    }

    /// <summary>
    /// Encrypts a file from its path and a plain password.
    /// </summary>
    /// <param name="inputFile"></param>
    /// <param name="password"></param>
    /// <param name="outputFile"></param>
    public static void FileEncrypt(string inputFile, string outputFile, string password)
    {
        //http://stackoverflow.com/questions/27645527/aes-encryption-on-large-files
        //generate random salt
        var salt = GenerateRandomSalt();
        //create output file name
        FileStream fsCrypt = new(outputFile, FileMode.Create);
        //convert password string to byte arrray
        var passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
        //Set Rijndael symmetric encryption algorithm
        var aes = Aes.Create();
        aes.KeySize = 256;
        aes.BlockSize = 128;
        aes.Padding = PaddingMode.PKCS7;
        //http://stackoverflow.com/questions/2659214/why-do-i-need-to-use-the-rfc2898derivebytes-class-in-net-instead-of-directly
        //"What it does is repeatedly hash the user password along with the salt." High iteration counts.
        var key = new Rfc2898DeriveBytes(passwordBytes, salt, 50000);
        aes.Key = key.GetBytes(aes.KeySize / 8);
        aes.IV = key.GetBytes(aes.BlockSize / 8);
        //Cipher modes: http://security.stackexchange.com/questions/52665/which-is-the-best-cipher-mode-and-padding-mode-for-aes-encryption
        aes.Mode = CipherMode.CFB;
        // write salt to the begining of the output file, so in this case can be random every time
        fsCrypt.Write(salt, 0, salt.Length);
        CryptoStream cs = new(fsCrypt, aes.CreateEncryptor(), CryptoStreamMode.Write);
        FileStream fsIn = new(inputFile, FileMode.Open);
        //create a buffer (1mb) so only this amount will allocate in the memory and not the whole file
        var buffer = new byte[1048576];
        try
        {
            int read;
            while ((read = fsIn.Read(buffer, 0, buffer.Length)) > 0)
            {
                cs.Write(buffer, 0, read);
            }

            // Close up
            fsIn.Close();
        }
        catch (Exception ex)
        {
            File.WriteAllText( "error.log", "Error: " + ex.Message);
            Debug.WriteLine("Error: " + ex.Message);
        }
        finally
        {
            cs.Close();
            fsCrypt.Close();
        }
    }
}