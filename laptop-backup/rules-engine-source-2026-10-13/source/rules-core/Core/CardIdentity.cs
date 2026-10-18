using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>Full deterministic UUIDs, byte-for-byte compatible with Java nameUUIDFromBytes.</summary>
    public static class CardIdentity
    {
        public static Guid FromName(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            byte[] bytes;
            using (var md5 = MD5.Create()) bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(name));
            bytes[6] = (byte)((bytes[6] & 15) | 48);
            bytes[8] = (byte)((bytes[8] & 63) | 128);
            // Parsing network-order hex avoids Guid's mixed-endian byte-array constructor.
            var hex = new StringBuilder(32);
            foreach (var b in bytes) hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return Guid.ParseExact(hex.ToString(), "N");
        }

        /// <summary>Deterministic version-zero identity for isolated hypothetical cards.</summary>
        internal static Guid FromCounter(long counter)
        {
            byte[] bytes = new byte[8];
            for (int i=7;i>=0;i--) { bytes[i]=(byte)counter; counter >>= 8; }
            return new Guid(0,0,0,bytes);
        }
    }
}
