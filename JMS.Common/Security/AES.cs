using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace JMS.Common.Security
{
    public class AES
    {
        /// <summary>
        /// 用任意密钥长度加密内容
        /// </summary>
        /// <param name="data"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string EncryptWithAnyKey(string data, string key)
        {
            string originalKey = key;

            if (key.Length < 16)
                key = key.PadRight(16, '0');
            else if (key.Length > 16 && key.Length < 24)
                key = key.PadRight(24, '0');
            else if (key.Length > 24 && key.Length < 32)
                key = key.PadRight(32, '0');
            else if (key.Length > 32)
                key = key.Substring(0, 32);

            int index = originalKey.Length - 16;
            if (index < 0)
                index = 0;
            string iv = originalKey.Substring(index);

            if (iv.Length < 16) iv = iv.PadRight(16, '0');
            else if (iv.Length > 16) iv = iv.Substring(0, 16);

            var _valueByte = Encoding.UTF8.GetBytes(data);
            using (var aes = new RijndaelManaged())
            {
                aes.IV = Encoding.UTF8.GetBytes(iv);
                aes.Key = Encoding.UTF8.GetBytes(key);
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                var cryptoTransform = aes.CreateEncryptor();
                var resultArray = cryptoTransform.TransformFinalBlock(_valueByte, 0, _valueByte.Length);
                return Convert.ToBase64String(resultArray, 0, resultArray.Length);
            }
        }

        /// <summary>
        /// 用任意密钥长度解密内容
        /// </summary>
        /// <param name="data"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string DecryptWithAnyKey(string data, string key)
        {
            string originalKey = key;

            if (key.Length < 16)
                key = key.PadRight(16, '0');
            else if (key.Length > 16 && key.Length < 24)
                key = key.PadRight(24, '0');
            else if (key.Length > 24 && key.Length < 32)
                key = key.PadRight(32, '0');
            else if (key.Length > 32)
                key = key.Substring(0, 32);

            int index = originalKey.Length - 16;
            if (index < 0)
                index = 0;
            string iv = originalKey.Substring(index);

            if (iv.Length < 16) iv = iv.PadRight(16, '0');
            else if (iv.Length > 16) iv = iv.Substring(0, 16);

            var _valueByte = Convert.FromBase64String(data);
            using (var aes = new RijndaelManaged())
            {
                aes.IV = Encoding.UTF8.GetBytes(iv);
                aes.Key = Encoding.UTF8.GetBytes(key);
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                var cryptoTransform = aes.CreateDecryptor();
                var resultArray = cryptoTransform.TransformFinalBlock(_valueByte, 0, _valueByte.Length);
                return Encoding.UTF8.GetString(resultArray);
            }
        }

        /// <summary>
        ///  加密
        /// </summary>
        /// <param name="str">明文（待加密）</param>
        /// <param name="key">密钥 16或32个字母</param>
        /// <returns></returns>
        public static string Encrypt(string str, string key)
        {
            if (string.IsNullOrEmpty(str)) return null;
            Byte[] toEncryptArray = Encoding.UTF8.GetBytes(str);

            using (RijndaelManaged rm = new RijndaelManaged
            {
                Key = Encoding.UTF8.GetBytes(key),
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            })
            {
                using (ICryptoTransform cTransform = rm.CreateEncryptor())
                {
                    Byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
                    return Convert.ToBase64String(resultArray);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="content"></param>
        /// <param name="key">密钥 16或32个字母</param>
        /// <returns></returns>
        public static byte[] Encrypt(byte[] content, string key)
        {
            if (content == null) return null;

            using (RijndaelManaged rm = new RijndaelManaged
            {
                Key = Encoding.UTF8.GetBytes(key),
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            })
            {
                using (ICryptoTransform cTransform = rm.CreateEncryptor())
                {
                    Byte[] resultArray = cTransform.TransformFinalBlock(content, 0, content.Length);
                    return resultArray;
                }
            }
        }


        /// <summary>
        ///  解密
        /// </summary>
        /// <param name="str">明文（待解密）</param>
        /// <param name="key">密钥 16或32个字母</param>
        /// <returns></returns>
        public static string Decrypt(string str, string key)
        {
            if (string.IsNullOrEmpty(str)) return null;
            Byte[] toEncryptArray = Convert.FromBase64String(str);

            using (RijndaelManaged rm = new RijndaelManaged
            {
                Key = Encoding.UTF8.GetBytes(key),
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            })
            {

                using (ICryptoTransform cTransform = rm.CreateDecryptor())
                {
                    Byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);

                    return Encoding.UTF8.GetString(resultArray);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="content"></param>
        /// <param name="key">密钥 16或32个字母</param>
        /// <returns></returns>
        public static byte[] Decrypt(byte[] content, string key)
        {
            if (content == null) return null;

            using (RijndaelManaged rm = new RijndaelManaged
            {
                Key = Encoding.UTF8.GetBytes(key),
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            })
            {
                using (ICryptoTransform cTransform = rm.CreateDecryptor())
                {
                    return cTransform.TransformFinalBlock(content, 0, content.Length);
                }
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="toDecryptArray"></param>
        /// <param name="key">24个字符的密钥</param>
        /// <returns></returns>
        public static byte[] TripleDESDecrypt(byte[] toDecryptArray, string key)
        {
            using (var tripleDES = TripleDES.Create())
            {
                var byteKey = Encoding.UTF8.GetBytes(key);
                tripleDES.Key = byteKey;
                tripleDES.Mode = CipherMode.ECB;
                tripleDES.Padding = PaddingMode.PKCS7;

                using (ICryptoTransform cTransform = tripleDES.CreateDecryptor())
                {
                    return cTransform.TransformFinalBlock(toDecryptArray, 0, toDecryptArray.Length);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="toEncryptArray"></param>
        /// <param name="key">24个字符的密钥</param>
        /// <returns></returns>
        public static byte[] TripleDESEncrypt(byte[] toEncryptArray, string key)
        {
            using (var tripleDES = TripleDES.Create())
            {
                var byteKey = Encoding.UTF8.GetBytes(key);
                tripleDES.Key = byteKey;
                tripleDES.Mode = CipherMode.ECB;
                tripleDES.Padding = PaddingMode.PKCS7;

                using (ICryptoTransform cTransform = tripleDES.CreateEncryptor())
                {
                    return cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
                }
            }
        }

    }
}