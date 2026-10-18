using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace IC.Net {
    // Account identity is independent of the disclosed relay seat identifiers.
    public sealed class RankedIdentity {
        readonly Ed25519PrivateKeyParameters key;
        public string PublicKey { get; }
        public string PlayerId { get; }
        public RankedIdentity(byte[] seed) {
            if(seed == null || seed.Length != 32) throw new ArgumentException("An Ed25519 seed must be 32 bytes");
            key = new Ed25519PrivateKeyParameters(seed,0);
            var pub=key.GeneratePublicKey().GetEncoded(); PublicKey=B64(pub);
            PlayerId=Hash(pub).Substring(0,32);
        }
        public static byte[] NewSeed() {var b=new byte[32];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(b);return b;}
        public static string B64(byte[] value)=>Convert.ToBase64String(value).TrimEnd('=').Replace('+','-').Replace('/','_');
        static string Hash(byte[] bytes) {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        public Dictionary<string,string> Sign(string method, Uri uri, string body="", long? timestamp=null, string nonce=null) {
            var ts=(timestamp??DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).ToString(CultureInfo.InvariantCulture);
            nonce=nonce??B64(NewSeed());
            var message=Encoding.UTF8.GetBytes("ranked-1\n"+method.ToUpperInvariant()+"\n"+uri.PathAndQuery+"\n"+ts+"\n"+nonce+"\n"+Hash(Encoding.UTF8.GetBytes(body??"")));
            var signer=new Ed25519Signer();signer.Init(true,key);signer.BlockUpdate(message,0,message.Length);
            return new Dictionary<string,string>{{"x-ic-player",PlayerId},{"x-ic-timestamp",ts},{"x-ic-nonce",nonce},{"x-ic-signature",B64(signer.GenerateSignature())}};
        }
    }
}
