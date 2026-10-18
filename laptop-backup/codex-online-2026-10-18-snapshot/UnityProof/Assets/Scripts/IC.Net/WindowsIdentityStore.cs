using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace IC.Net {
    // DPAPI CurrentUser: private seeds never go into PlayerPrefs, logs or portable builds.
    public static class WindowsIdentityStore {
        [StructLayout(LayoutKind.Sequential)] struct Blob {public int size;public IntPtr data;}
        [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)] static extern bool CryptProtectData(ref Blob input,string description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
        [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)] static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
        [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr value);
        static byte[] Transform(byte[] value,bool protect) {
            var input=new Blob{size=value.Length,data=Marshal.AllocHGlobal(value.Length)};Blob output=default;
            try {
                Marshal.Copy(value,0,input.data,value.Length);
                bool ok=protect?CryptProtectData(ref input,"Infinite Conquest ranked identity",IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
                if(!ok)throw new Win32Exception(Marshal.GetLastWin32Error(),"Unable to unlock the ranked identity for this Windows account");
                var result=new byte[output.size];Marshal.Copy(output.data,result,0,result.Length);return result;
            } finally {
                for(int i=0;i<input.size;i++)Marshal.WriteByte(input.data,i,0);
                Marshal.FreeHGlobal(input.data);
                if(output.data!=IntPtr.Zero){for(int i=0;i<output.size;i++)Marshal.WriteByte(output.data,i,0);LocalFree(output.data);}
            }
        }
        public static RankedIdentity LoadOrCreate(string path) {
            byte[] seed;
            if(File.Exists(path))seed=Transform(File.ReadAllBytes(path),false);
            else {
                seed=RankedIdentity.NewSeed();Directory.CreateDirectory(Path.GetDirectoryName(path));
                // CreateNew refuses an accidental concurrent account replacement.
                using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){var encrypted=Transform(seed,true);file.Write(encrypted,0,encrypted.Length);file.Flush(true);}
            }
            try{return new RankedIdentity(seed);}finally{Array.Clear(seed,0,seed.Length);}
        }
    }
}
