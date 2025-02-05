using Newtonsoft.Json.Linq;
using RestSharp;
using System.Text;
using Crypto;             // 假設此命名空間內有 TableService.CreatePassword 方法
using Ionic.Zip;          // DotNetZip 命名空間

namespace mxdat
{
    public class GetExcelzip
    {
        public static void GetExcelzipMain(string[] args)
        {
            try
            {
                // 註冊 CodePagesEncodingProvider，支援 IBM437 等編碼
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                string rootDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string resourceJsonFilePath = Path.Combine(rootDirectory, "resource.json");
                string excelZipPath = Path.Combine(rootDirectory, "Excel.zip");
                string targetDirectoryPath = Path.Combine(rootDirectory, "extracted");

                // 驗證 resource.json 是否存在
                if (!File.Exists(resourceJsonFilePath))
                {
                    Console.WriteLine("Error: resource.json file does not exist");
                    return;
                }

                // 若目標資料夾不存在則建立之
                if (!Directory.Exists(targetDirectoryPath))
                {
                    Directory.CreateDirectory(targetDirectoryPath);
                }
                else
                {
                    Console.WriteLine($"Directory already exists: {targetDirectoryPath}");
                }

                // 讀取 resource.json，並解析出 resource_path
                string jsonContent = File.ReadAllText(resourceJsonFilePath);
                var jsonObject = JObject.Parse(jsonContent);
                string? resourcePath = jsonObject["patch"]?.Value<string>("resource_path");

                if (string.IsNullOrEmpty(resourcePath))
                {
                    Console.WriteLine("Error: resource_path is missing or empty in resource.json");
                    return;
                }

                if (resourcePath.LastIndexOf("/") == -1)
                {
                    Console.WriteLine("Error: Invalid resource_path format");
                    return;
                }

                // 組合下載 Excel.zip 的 URL
                string baseUrl = resourcePath.Substring(0, resourcePath.LastIndexOf("/") + 1);
                string excelZipUrl = $"{baseUrl}Preload/TableBundles/Excel.zip";

                // 使用 RestSharp 下載 Excel.zip
                var client = new RestClient(excelZipUrl);
                var request = new RestRequest(Method.GET);
                IRestResponse response = client.Execute(request);

                if (response.IsSuccessful && response.RawBytes != null && response.RawBytes.Length > 0)
                {
                    byte[] fileBytes = response.RawBytes;
                    File.WriteAllBytes(excelZipPath, fileBytes);
                    Console.WriteLine($"Excel.zip downloaded successfully, size: {fileBytes.Length} bytes");
                }
                else
                {
                    Console.WriteLine("Failed to download Excel.zip or received empty content.");
                    return;
                }

                // 解壓 Excel.zip，使用 DotNetZip 並帶入密碼
                if (File.Exists(excelZipPath))
                {
                    // 假設 TableService.CreatePassword 依檔名產生 byte[]，並以 Base64 字串作為密碼
                    string password = Convert.ToBase64String(TableService.CreatePassword(Path.GetFileName(excelZipPath)));

                    if (string.IsNullOrEmpty(password))
                    {
                        Console.WriteLine("Error: Zip password is empty");
                        return;
                    }

                    Console.WriteLine($"Using ZIP Password: {password}");

                    try
                    {
                        using (ZipFile zip = ZipFile.Read(excelZipPath))
                        {
                            zip.Password = password;
                            // 解壓所有檔案至 targetDirectoryPath，若有同名檔案則覆蓋
                            zip.ExtractAll(targetDirectoryPath, ExtractExistingFileAction.OverwriteSilently);
                        }
                        Console.WriteLine("Excel files extracted successfully using DotNetZip.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error extracting Excel.zip: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("Error: Excel.zip file does not exist.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An exception occurred: {ex.Message}");
            }

            // 呼叫後續的 pythonScipt 處理流程
            Decryptbytes.DecryptbytesMain(args);
        }
    }
}
