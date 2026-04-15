using System.Management;
using System.Text;
using XSystem.Security.Cryptography;

namespace OpticEMS.License.Helpers
{
    public static class LicenseHelper
    {
        private static string GetDiskVolumeSerialNumber()
        {
            try
            {
                var disk = new ManagementObject(@"Win32_LogicalDisk.deviceid=""c:""");
                disk.Get();
                return disk["VolumeSerialNumber"].ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetProcessorId()
        {
            try
            {
                var mbs = new ManagementObjectSearcher("Select ProcessorId From Win32_processor");
                var mbsList = mbs.Get();
                var id = string.Empty;
                foreach (var o in mbsList)
                {
                    var mo = (ManagementObject)o;
                    id = mo["ProcessorId"].ToString();
                    break;
                }

                return id;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetMotherboardId()
        {
            try
            {
                var mbs = new ManagementObjectSearcher("Select SerialNumber From Win32_BaseBoard");
                var mbsList = mbs.Get();
                var id = string.Empty;
                foreach (var o in mbsList)
                {
                    var mo = (ManagementObject)o;
                    id = mo["SerialNumber"].ToString();
                    break;
                }

                return id;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string GenerateUid()
        {
            var id = string.Concat("OpticEMS", GetProcessorId(), GetMotherboardId(), GetDiskVolumeSerialNumber());
            var byteIds = Encoding.UTF8.GetBytes(id);

            var md5 = new MD5CryptoServiceProvider();
            var checksum = md5.ComputeHash(byteIds);

            var part1Id = Base36.Encode(BitConverter.ToUInt32(checksum, 0));
            var part2Id = Base36.Encode(BitConverter.ToUInt32(checksum, 4));
            var part3Id = Base36.Encode(BitConverter.ToUInt32(checksum, 8));
            var part4Id = Base36.Encode(BitConverter.ToUInt32(checksum, 12));

            return $"{part1Id}-{part2Id}-{part3Id}-{part4Id}";
        }

        public static byte[] GetUidInBytes(string uid)
        {
            var ids = uid.Split('-');

            if (ids.Length != 4)
            {
                throw new ArgumentException("Error: Incorrect UID format!");
            }

            var value = new byte[16];
            Buffer.BlockCopy(BitConverter.GetBytes(Base36.Decode(ids[0])), 0, value, 0, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(Base36.Decode(ids[1])), 0, value, 8, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(Base36.Decode(ids[2])), 0, value, 16, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(Base36.Decode(ids[3])), 0, value, 24, 8);

            return value;
        }

        public static bool ValidateUidFormat(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
            {
                return false;
            }

            var ids = uid.Split('-');

            return (ids.Length == 4);
        }
    }
}
