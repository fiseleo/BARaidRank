using System.IO.Compression;
using System.Text;
namespace BARaidRank
{
    public class BARaidRankMain
    {
        static string rootDirectory = AppDomain.CurrentDomain.BaseDirectory;
        static string mxDirectory = Path.Combine(rootDirectory, "mx");
        static string mxFile = Path.Combine(mxDirectory, "mx.dat");
        public static void Main()
        {

            if (!Directory.Exists(mxDirectory))
            {
                Directory.CreateDirectory(mxDirectory);
            }
            else
            {
                Console.WriteLine("Directory already exists: " + mxDirectory);
            }

            if (!File.Exists(mxFile))
            {
                Console.WriteLine("No fonud mx.dat file.");
                return;
            }
            else
            {
                DecryptMxFile(mxFile);
            }

        }


        // Read the mx.dat file and process its contents

        private static void DecryptMxFile(string filePath)
        {
            byte[] mx = File.ReadAllBytes(filePath);
            byte[] reqBytes = new byte[mx.Length - 12];
            Array.Copy(mx, 12, reqBytes, 0, mx.Length - 12);

            for (int i = 0; i < reqBytes.Length; i++)
            {
                reqBytes[i] ^= 0xD9; // XOR decryption with 0xD9
            }

            byte[] decompressedBytes;
            using (MemoryStream input = new MemoryStream(reqBytes))
            using (GZipStream gzip = new GZipStream(input, CompressionMode.Decompress))
            using (MemoryStream output = new MemoryStream())
            {
                gzip.CopyTo(output);
                decompressedBytes = output.ToArray();
            }

            string jsonText = Encoding.UTF8.GetString(decompressedBytes);
            string jsonFilePath = Path.Combine(mxDirectory, "mx.json");
            File.WriteAllText(jsonFilePath, jsonText);
            GetRaidList.GetRaidListMain();

        }



    }
}