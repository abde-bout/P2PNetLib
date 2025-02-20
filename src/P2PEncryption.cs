using System.IO;
using System;
using System.Security.Cryptography;
using System.Text;

namespace P2PNet.Cryptography
{
    public static class P2PEncryption
    {
        // Encrypts plainText using a given key of any length
        public static string Encrypt(string plainText, string key)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = GenerateKeyFromString(key, aes.KeySize / 8); // Convert key to proper length
                aes.GenerateIV(); // Generate a random IV for each encryption
                byte[] iv = aes.IV;

                using (MemoryStream ms = new MemoryStream())
                {
                    ms.Write(iv, 0, iv.Length); // Write IV to the beginning of the stream
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                        cs.Write(plainBytes, 0, plainBytes.Length);
                        cs.FlushFinalBlock();
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        // Decrypts cipherText using a given key
        public static string Decrypt(string cipherText, string key)
        {
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            using (Aes aes = Aes.Create())
            {
                aes.Key = GenerateKeyFromString(key, aes.KeySize / 8);

                using (MemoryStream ms = new MemoryStream(cipherBytes))
                {
                    byte[] iv = new byte[aes.BlockSize / 8]; // AES block size is typically 128-bit (16 bytes)
                    ms.Read(iv, 0, iv.Length); // Read the IV from the beginning of the stream
                    aes.IV = iv;

                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                    {
                        using (StreamReader sr = new StreamReader(cs))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                }
            }
        }

        // Generates a random key of specified bit length
        public static string GenerateRandomKey(int bytes)
        {
            byte[] keyBytes = new byte[bytes];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(keyBytes); // Generate random bytes
            }

            // Convert the byte array to a base64 string
            return Convert.ToBase64String(keyBytes);
        }

        // Helper method to generate a key of the required length from the string
        private static byte[] GenerateKeyFromString(string key, int keyLength)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] keyBytes = Encoding.UTF8.GetBytes(key);
                byte[] hashedKey = sha256.ComputeHash(keyBytes);

                // Trim or pad the hash to the required length
                byte[] finalKey = new byte[keyLength];
                Array.Copy(hashedKey, finalKey, Math.Min(hashedKey.Length, keyLength));
                return finalKey;
            }
        }
    }
}
