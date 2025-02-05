using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace mxdat
{
    class Updatalist
    {
        static readonly string rootDirectory = AppDomain.CurrentDomain.BaseDirectory;
        static readonly string jsonDirectory = Path.Combine(rootDirectory, "Updata"); // Updata 資料夾
        static readonly List<string> sourceDirectories = new List<string> 
        { 
            Path.Combine(rootDirectory, "extracted_excels") 
        }; // 所有來源目錄

        // 讀取 ip.txt 內容作為 serverUrl
        static readonly string ipFilePath = Path.Combine(rootDirectory, "ip.txt");
        static readonly string tokenFilePath = Path.Combine(rootDirectory, "token.txt");
        static readonly string serverUrl = LoadServerUrl();

        static readonly string token = Loadtoken();
        static readonly List<string> fileNames = new List<string>
        {
            "EliminateRaidSeasonManageExcelTable.json",
            "RaidSeasonManageExcelTable.json"
        };

        public static void UpdatalistMain(string[] args)
        {
            EnsureDirectoryExists(jsonDirectory);

            // 立即執行工作
            Job(args);
        }

        static void EnsureDirectoryExists(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Console.WriteLine($"Created directory: {directory}");
            }
        }

        /// <summary>
        /// 從 ip.txt 讀取 serverUrl，若不存在則回傳空字串或預設 URL
        /// </summary>
        /// <returns>讀取到的 serverUrl</returns>
        static string LoadServerUrl()
        {
            if (File.Exists(ipFilePath))
            {
                string url = File.ReadAllText(ipFilePath, Encoding.UTF8).Trim();
                Console.WriteLine($"Loaded serverUrl from ip.txt: {url}");
                return url;
            }
            else
            {
                Console.WriteLine("ip.txt not found. Please create ip.txt with the server URL.");
                // 可依需求指定預設值
                return "";
            }
        }
        static string Loadtoken()
        {
            if (File.Exists(ipFilePath))
            {
                string url = File.ReadAllText(tokenFilePath, Encoding.UTF8).Trim();
                Console.WriteLine($"Loaded serverUrl from token.txt: {url}");
                return url;
            }
            else
            {
                Console.WriteLine("token.txt not found. Please create ip.txt with the server URL.");
                // 可依需求指定預設值
                return "";
            }
        }

        static void ProcessAndCopyFilesToUpdata()
        {
            EnsureDirectoryExists(jsonDirectory);

            Parallel.ForEach(sourceDirectories, sourceDirectory =>
            {
                foreach (var fileName in fileNames)
                {
                    string sourceFilePath = Path.Combine(sourceDirectory, fileName);
                    if (File.Exists(sourceFilePath))
                    {
                        try
                        {
                            using (var fileStream = File.OpenRead(sourceFilePath))
                            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                            using (var jsonReader = new JsonTextReader(streamReader))
                            {
                                var jsonObject = JToken.ReadFrom(jsonReader);

                                // 若 JSON 中包含 DataList 屬性，則取其內容
                                if (jsonObject.Type == JTokenType.Object && jsonObject["DataList"] != null)
                                {
                                    jsonObject = jsonObject["DataList"];
                                }

                                // 若存在 protocol 欄位則移除
                                if (jsonObject.Type == JTokenType.Object && ((JObject)jsonObject).ContainsKey("protocol"))
                                {
                                    ((JObject)jsonObject).Remove("protocol");
                                }

                                // 加入 protocol 欄位，內容為檔案名稱
                                var jsonWithProtocol = new JObject
                                {
                                    ["Data"] = jsonObject,
                                    ["protocol"] = fileName
                                };

                                // 儲存最終 JSON 至目的資料夾
                                string destFilePath = Path.Combine(jsonDirectory, fileName);
                                File.WriteAllText(destFilePath, jsonWithProtocol.ToString(Formatting.Indented), Encoding.UTF8);
                                Console.WriteLine($"Processed and copied {sourceFilePath} to {destFilePath}");
                            }
                        }
                        catch (JsonReaderException)
                        {
                            Console.WriteLine($"File {sourceFilePath} is not a valid JSON, skipping.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing {sourceFilePath}: {ex.Message}");
                        }
                    }
                }
            });
        }

        static void UploadFiles()
        {
            var jsonFiles = Directory.GetFiles(jsonDirectory, "*.json");

            Parallel.ForEach(jsonFiles, filePath =>
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"File {filePath} does not exist, skipping.");
                    return;
                }

                try
                {
                    using (var fileStream = File.OpenRead(filePath))
                    using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                    using (var jsonReader = new JsonTextReader(streamReader))
                    {
                        var jsonObject = JToken.ReadFrom(jsonReader);

                        // 使用 RestSharp 發送 POST 請求到 server
                        var client = new RestClient(serverUrl);
                        var request = new RestRequest(Method.POST);
                        request.AddHeader("Content-Type", "application/json");
                        request.AddHeader("Token", token);
                        request.AddHeader("User-Agent", "PostmanRuntime/7.39.0");
                        request.AddHeader("Connection", "keep-alive");
                        request.AddHeader("Accept-Encoding", "gzip, deflate, br");
                        request.AddJsonBody(jsonObject.ToString());

                        try
                        {
                            IRestResponse response = client.Execute(request);
                            Console.WriteLine($"Sent {Path.GetFileName(filePath)} to server, response status code: {response.StatusCode}");
                            Console.WriteLine($"Response content: {response.Content}");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Failed to send {Path.GetFileName(filePath)} to server: {e.Message}");
                        }
                    }
                }
                catch (JsonReaderException)
                {
                    Console.WriteLine($"File {filePath} is not a valid JSON, skipping.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {filePath}: {ex.Message}");
                }
            });
        }

        static void Job(string[] args)
        {
            ProcessAndCopyFilesToUpdata();
            UploadFiles();
            Getlist.GetlistMain(args);
        }
    }
}
