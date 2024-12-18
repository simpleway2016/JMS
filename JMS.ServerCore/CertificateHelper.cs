#if NET9_0
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace JMS.ServerCore
{
    public static class CertificateHelper
    {
        private static X509Certificate2 LoadPrivateKey(X509Certificate2 cert, string privateKeyPath)
        {
            // 读取 PEM 文件内容
            var privateKeyPem = File.ReadAllText(privateKeyPath);

            // 移除 PEM 文件的头尾
            privateKeyPem = privateKeyPem.Replace("-----BEGIN PRIVATE KEY-----", "")
                                           .Replace("-----END PRIVATE KEY-----", "")
                                           .Replace("\n", "")
                                           .Replace("\r", "");

            // 将 Base64 编码的字符串转换为字节数组
            var privateKeyBytes = Convert.FromBase64String(privateKeyPem);

            // 使用 RSA 创建私钥
            using (var rsa = RSA.Create())
            {
                try
                {
                    rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
                }
                catch (CryptographicException)
                {
                    try
                    {
                        rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
                    }
                    catch (CryptographicException)
                    {
                        using var ecdsa = LoadEccPrivateKey(privateKeyBytes);
                        throw new NotSupportedException("私钥不支持ECC算法");
                        return cert.CopyWithPrivateKey(ecdsa);
                    }
                }

                cert = cert.CopyWithPrivateKey(rsa);

                //要转为pfx格式，否则无法通讯
                var pfx_file_data = cert.Export(X509ContentType.Pkcs12, "123456");

                return X509CertificateLoader.LoadPkcs12(pfx_file_data , "123456");
            }
        }
        private static ECDsa LoadEccPrivateKey(byte[] privateKeyBytes)
        {

            // 使用 ECDsa 创建私钥
            ECDsa ecdsa = ECDsa.Create();
            ecdsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
            return ecdsa;
        }

        public static X509Certificate2 LoadCertificate(string certificatePath, string privateKeyPath)
        {

            var cert = X509CertificateLoader.LoadCertificateFromFile(certificatePath);
            cert = LoadPrivateKey(cert, privateKeyPath);
            return cert;
        }

        /// <summary>
        /// crt转pfx证书
        /// </summary>
        /// <param name="certificatePath">crt证书</param>
        /// <param name="privateKeyPath">私钥文件</param>
        /// <param name="pfxPath">生成的pfx路径</param>
        /// <param name="pfxPassword">pfx密码</param>
        public static void ConvertToPfx(string certificatePath, string privateKeyPath,string pfxPath,string pfxPassword)
        {

            var cert = LoadCertificate(certificatePath, privateKeyPath);


            var pfx_file_data = cert.Export(X509ContentType.Pkcs12, pfxPassword);
            File.WriteAllBytes(pfxPath, pfx_file_data);

        }
    }
}
#endif