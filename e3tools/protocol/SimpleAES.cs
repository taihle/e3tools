using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace e3tools
{
    public class SimpleAES
    {
        private static SimpleAES _instance = null;
        public static SimpleAES Instance
        {
            get
            {
                if (null == _instance)
                {
                    _instance = new SimpleAES();
                }
                return _instance;
            }
        }

        private static byte[] _key = { 116, 108, 101, 64, 97, 108, 108, 101, 110, 116, 101, 107, 46, 99, 111, 109 };
        private static byte[] _vector = { 116, 108, 101, 64, 97, 108, 108, 101, 110, 116, 101, 107, 46, 99, 111, 109 };
        private ICryptoTransform _encryptor, _decryptor;
        private UTF8Encoding _encoder;

        public SimpleAES()
        {
            RijndaelManaged rm = new RijndaelManaged();
            _encryptor = rm.CreateEncryptor(_key, _vector);
            _decryptor = rm.CreateDecryptor(_key, _vector);
            _encoder = new UTF8Encoding();
        }

        byte[] GetKeyFactor(string key)
        {
            if (key.Length > 16) key = key.Substring(0, 16);
            else key = key.PadRight(16, key[0]);
            return _encoder.GetBytes(key);
        }

        public string Encrypt(string unencrypted, string key = null)
        {
            if (string.IsNullOrEmpty(unencrypted)) unencrypted = string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                return Convert.ToBase64String(Encrypt(_encoder.GetBytes(unencrypted)));
            }
            else
            {
                RijndaelManaged rm = new RijndaelManaged();
                byte[] key_vector = GetKeyFactor(key);
                ICryptoTransform encryptor = rm.CreateEncryptor(key_vector, key_vector);
                return Convert.ToBase64String(Transform(_encoder.GetBytes(unencrypted), encryptor));
            }
        }

        public string Decrypt(string encrypted, string key = null)
        {
            if (string.IsNullOrEmpty(encrypted)) encrypted = string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                return _encoder.GetString(Decrypt(Convert.FromBase64String(encrypted)));
            }
            else
            {
                RijndaelManaged rm = new RijndaelManaged();
                byte[] key_vector = GetKeyFactor(key);
                ICryptoTransform decryptor = rm.CreateDecryptor(key_vector, key_vector);
                return _encoder.GetString(Transform(Convert.FromBase64String(encrypted), decryptor));
            }
        }

        public byte[] Encrypt(byte[] buffer)
        {
            return Transform(buffer, _encryptor);
        }

        public byte[] Decrypt(byte[] buffer)
        {
            return Transform(buffer, _decryptor);
        }

        protected byte[] Transform(byte[] buffer, ICryptoTransform transform)
        {
            MemoryStream stream = new MemoryStream();
            using (CryptoStream cs = new CryptoStream(stream, transform, CryptoStreamMode.Write))
            {
                cs.Write(buffer, 0, buffer.Length);
            }
            return stream.ToArray();
        }
    }
}
