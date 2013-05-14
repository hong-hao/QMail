using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using wos.crypto;

namespace ime.mail.Utils
{
    public class PassUtil
    {
        private static string PASS_KEY = "asde*230DLw^3";

        public static string Encrypt(string pwd)
        {
            return Base64.encode(XXTEA.Encrypt(Encoding.UTF8.GetBytes(pwd), Encoding.UTF8.GetBytes(PASS_KEY)));
        }

        public static string Decrypt(string p)
        {
            return Encoding.UTF8.GetString(XXTEA.Decrypt(Convert.FromBase64String(p), Encoding.UTF8.GetBytes(PASS_KEY)));
        }
    }
}
