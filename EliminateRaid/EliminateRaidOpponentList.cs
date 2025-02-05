using mxdat.NetworkProtocol;
using RestSharp;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Text;
using Microsoft.Data.Sqlite; // 請先安裝 Microsoft.Data.Sqlite NuGet 套件

namespace mxdat
{
    public class EliminateRaidOpponentList
    {
        public static bool shouldContinue = false; // 新旗標變數
        public static int savedRankValue = 1;         // 暫存暫停前的 rank 值
        public static int rankValue = 15;
        public static bool isfinishloop = false;

        public static void EliminateRaidOpponentListMain(string[] args, DateTime seasonEndData, DateTime settlementEndDate)
        {
            Console.OutputEncoding = Encoding.UTF8;
            CheckAndPauseAt3AM();

            if (shouldContinue)
            {
                shouldContinue = false;
                Console.WriteLine($"Returning from EliminateRaidOpponentListjson, continuing to execute EliminateRaidOpponentList with rankValue {savedRankValue}");
                ExecuteMainLogic(args, seasonEndData, settlementEndDate, savedRankValue); // 從暫存的 rank 值繼續執行
            }
            else if (isfinishloop)
            {
                isfinishloop = false;
                Console.WriteLine($"Returning from EliminateRaidOpponentListjson, continuing to execute EliminateRaidOpponentList with rankValue {savedRankValue}");
                ExecuteMainLogic(args, seasonEndData, settlementEndDate, savedRankValue);
            }
            else
            {
                ExecuteMainLogic(args, seasonEndData, settlementEndDate, 1);
            }
        }

        private static void ExecuteMainLogic(string[] args, DateTime seasonEndData, DateTime settlementEndDate, int rankValue)
        {
            string rootDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string mxdatjson = Path.Combine(rootDirectory, "mxdat.json");

            PacketCryptManager instance = new PacketCryptManager();

            // 以下為從 mxdat.json 中取出參數的小方法（以區域函式方式實作）
            static string ExtractMxToken(string mxdatjson)
            {
                string jsonData = File.ReadAllText(mxdatjson);
                JObject jsonObject = JObject.Parse(jsonData);
                return jsonObject["SessionKey"]["MxToken"].ToString();
            }

            static string ExtractAccountId(string mxdatjson)
            {
                string jsonData = File.ReadAllText(mxdatjson);
                JObject jsonObject = JObject.Parse(jsonData);
                return jsonObject["AccountId"].ToString();
            }

            static string ExtractAccountServerId(string mxdatjson)
            {
                string jsonData = File.ReadAllText(mxdatjson);
                JObject jsonObject = JObject.Parse(jsonData);
                return jsonObject["SessionKey"]["AccountServerId"].ToString();
            }

            string mxToken = ExtractMxToken(mxdatjson);
            long hash = 193286413221927;
            // 注意：這邊原程式有可能交換了 AccountId 與 AccountServerId 的使用，這裡按照原始程式碼順序處理
            string accountServerId = ExtractAccountId(mxdatjson);
            string accountId = ExtractAccountServerId(mxdatjson);

            string baseJson = "{{\"Protocol\": 45002, " +
                              "\"Rank\": {0}, " +
                              "\"Score\": null, " +
                              "\"IsUpper\": false, " +
                              "\"IsFirstRequest\": true, " +
                              "\"SearchType\": 1, " +
                              "\"ClientUpTime\": 25, " +
                              "\"Resendable\": true, " +
                              "\"Hash\": {1}, " +
                              "\"IsTest\": false, " +
                              "\"SessionKey\":{{" +
                              "\"AccountServerId\": {3}, " +
                              "\"MxToken\": \"{2}\"}}, " +
                              "\"AccountId\": \"{4}\"}}";

            while (true)
            {
                // 正常迴圈邏輯：產生 JSON 請求字串
                string json = string.Format(baseJson, rankValue, hash, mxToken, accountServerId, accountId);
                Console.WriteLine($"査排名{rankValue}中...");

                byte[] mx = instance.RequestToBinary(Protocol.EliminateRaid_OpponentList, json);
                string filePath = "mx.dat";
                File.WriteAllBytes(filePath, mx);

                var client = new RestClient("https://nxm-tw-bagl.nexon.com:5000/api/gateway");
                client.Timeout = -1;
                var request = new RestRequest(Method.POST);
                request.AddHeader("mx", "1");
                request.AddFile("mx", filePath);

                IRestResponse response = null;
                try
                {
                    response = client.Execute(request);
                    if (response.StatusCode != HttpStatusCode.OK || string.IsNullOrWhiteSpace(response.Content))
                    {
                        Console.WriteLine("Response is empty or request failed, retrying...");
                        Thread.Sleep(2000);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}, retrying...");
                    Thread.Sleep(2000);
                    continue;
                }

                if (!response.Content.Contains("OpponentUserDBs"))
                {
                    Console.WriteLine(response.Content);
                    Console.WriteLine("No player information detected");
                    shouldContinue = true; // 設定旗標
                    isfinishloop = false;
                    ExecuteMainLogic(args, seasonEndData, settlementEndDate, 1);
                    return;
                }

                // ★ 將 response JSON 存入 SQLite 資料庫
                InsertJsonIntoDatabase(response.Content, rankValue);

                // ★ 為了上傳，先建立一個暫存檔，供 UploadJsonToServer 方法使用，上傳後立即刪除暫存檔
                string tempFilePath = Path.GetTempFileName();
                File.WriteAllText(tempFilePath, response.Content, Encoding.UTF8);
                UploadJsonToServer(tempFilePath);
                File.Delete(tempFilePath);

                rankValue =  rankValue + 30;
                hash++;
                Thread.Sleep(900);
                CheckAndPauseAt3AM();
            }
        }

        /// <summary>
        /// 將收到的 JSON 內容進一步解析，並將各個關鍵欄位（例如外層的 protocol、
        /// 內層 packet 中的 Protocol、ServerTimeTicks、ServerNotification，以及 OpponentUserDBs 的數量）
        /// 與原始 JSON 一併存入 SQLite 資料庫中。
        /// </summary>
        /// <param name="json">完整的回應 JSON 字串</param>
        /// <param name="rankValue">目前的 Rank 值</param>
        private static void InsertJsonIntoDatabase(string json, int rankValue)
        {
            // 資料庫檔案路徑（針對消除對手清單）
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "eliminate_raid_opponent_list.db");
            string connectionString = $"Data Source={dbPath}";

            // 解析外層 JSON
            JObject outer;
            try
            {
                outer = JObject.Parse(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解析外層 JSON 失敗：{ex.Message}");
                return;
            }

            // 從外層取得 protocol 與 packet 字串（packet 為內層的 JSON 字串）
            string outerProtocol = outer["protocol"]?.ToString() ?? "";
            string packetStr = outer["packet"]?.ToString() ?? "";

            // 解析內層 packet JSON
            JObject packet;
            try
            {
                packet = JObject.Parse(packetStr);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解析內層 packet JSON 失敗：{ex.Message}");
                return;
            }

            // 取得內層的各項欄位
            string innerProtocol = packet["Protocol"]?.ToString() ?? "";
            string serverTimeTicks = packet["ServerTimeTicks"]?.ToString() ?? "";
            string serverNotification = packet["ServerNotification"]?.ToString() ?? "";

            // 取得 OpponentUserDBs 陣列及其筆數（若有此欄位）
            int opponentCount = 0;
            JArray opponentArray = packet["OpponentUserDBs"] as JArray;
            if (opponentArray != null)
            {
                opponentCount = opponentArray.Count;
            }

            // 開啟 SQLite 資料庫連線
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                // 建立主資料表 OpponentList（存放外層及內層概要資料）
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                    @"
            CREATE TABLE IF NOT EXISTS OpponentList (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OuterProtocol TEXT,
                InnerProtocol TEXT,
                Rank INTEGER,
                ServerTimeTicks TEXT,
                ServerNotification TEXT,
                OpponentCount INTEGER,
                JsonData TEXT,
                InsertedAt TEXT
            );
            ";
                    command.ExecuteNonQuery();
                }

                // 插入主記錄並取得自動產生的 Id
                long mainRecordId = -1;
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                    @"
            INSERT INTO OpponentList 
                (OuterProtocol, InnerProtocol, Rank, ServerTimeTicks, ServerNotification, OpponentCount, JsonData, InsertedAt)
            VALUES 
                ($outerProtocol, $innerProtocol, $rank, $serverTimeTicks, $serverNotification, $opponentCount, $jsonData, $insertedAt);
            SELECT last_insert_rowid();
            ";
                    command.Parameters.AddWithValue("$outerProtocol", outerProtocol);
                    command.Parameters.AddWithValue("$innerProtocol", innerProtocol);
                    command.Parameters.AddWithValue("$rank", rankValue);
                    command.Parameters.AddWithValue("$serverTimeTicks", serverTimeTicks);
                    command.Parameters.AddWithValue("$serverNotification", serverNotification);
                    command.Parameters.AddWithValue("$opponentCount", opponentCount);
                    command.Parameters.AddWithValue("$jsonData", json);
                    command.Parameters.AddWithValue("$insertedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                    mainRecordId = (long)command.ExecuteScalar();
                }

                // 建立對手詳細資料的資料表 OpponentDetail
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                    @"
            CREATE TABLE IF NOT EXISTS OpponentDetail (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MainRecordId INTEGER,
                OpponentIndex INTEGER,
                AccountId INTEGER,
                RepresentCharacterUniqueId INTEGER,
                Level INTEGER,
                Nickname TEXT,
                Tier INTEGER,
                Rank INTEGER,
                BestRankingPoint REAL,
                BestRankingPointDetail REAL,
                EmblemUniqueId INTEGER,
                InsertedAt TEXT,
                FOREIGN KEY(MainRecordId) REFERENCES OpponentList(Id)
            );
            ";
                    command.ExecuteNonQuery();
                }

                // 如果有 OpponentUserDBs 陣列，逐筆解析每筆對手資料並存入 OpponentDetail
                if (opponentArray != null)
                {
                    int opponentIndex = 1;
                    foreach (var opponentToken in opponentArray)
                    {
                        JObject opponent = opponentToken as JObject;
                        if (opponent == null)
                            continue;

                        // 解析對手基本欄位
                        int oppAccountId = opponent["AccountId"]?.Value<int>() ?? 0;
                        int oppRepresentUniqueId = opponent["RepresentCharacterUniqueId"]?.Value<int>() ?? 0;
                        int oppLevel = opponent["Level"]?.Value<int>() ?? 0;
                        string oppNickname = opponent["Nickname"]?.ToString() ?? "";
                        int oppTier = opponent["Tier"]?.Value<int>() ?? 0;
                        int oppRank = opponent["Rank"]?.Value<int>() ?? 0;
                        double oppBestRankingPoint = opponent["BestRankingPoint"]?.Value<double>() ?? 0;
                        double oppBestRankingPointDetail = opponent["BestRankingPointDetail"]?.Value<double>() ?? 0;
                        int oppEmblemUniqueId = 0;
                        if (opponent["AccountAttachmentDB"] is JObject attachment)
                        {
                            oppEmblemUniqueId = attachment["EmblemUniqueId"]?.Value<int>() ?? 0;
                        }

                        // 插入對手詳細資料
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText =
                            @"
                    INSERT INTO OpponentDetail 
                        (MainRecordId, OpponentIndex, AccountId, RepresentCharacterUniqueId, Level, Nickname, Tier, Rank, BestRankingPoint, BestRankingPointDetail, EmblemUniqueId, InsertedAt)
                    VALUES 
                        ($mainRecordId, $opponentIndex, $oppAccountId, $oppRepresentUniqueId, $oppLevel, $oppNickname, $oppTier, $oppRank, $oppBestRankingPoint, $oppBestRankingPointDetail, $oppEmblemUniqueId, $insertedAt);
                    ";
                            command.Parameters.AddWithValue("$mainRecordId", mainRecordId);
                            command.Parameters.AddWithValue("$opponentIndex", opponentIndex);
                            command.Parameters.AddWithValue("$oppAccountId", oppAccountId);
                            command.Parameters.AddWithValue("$oppRepresentUniqueId", oppRepresentUniqueId);
                            command.Parameters.AddWithValue("$oppLevel", oppLevel);
                            command.Parameters.AddWithValue("$oppNickname", oppNickname);
                            command.Parameters.AddWithValue("$oppTier", oppTier);
                            command.Parameters.AddWithValue("$oppRank", oppRank);
                            command.Parameters.AddWithValue("$oppBestRankingPoint", oppBestRankingPoint);
                            command.Parameters.AddWithValue("$oppBestRankingPointDetail", oppBestRankingPointDetail);
                            command.Parameters.AddWithValue("$oppEmblemUniqueId", oppEmblemUniqueId);
                            command.Parameters.AddWithValue("$insertedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            command.ExecuteNonQuery();
                        }
                        opponentIndex++;
                    }
                }

                connection.Close();
            }

            Console.WriteLine($"Inserted detailed JSON for rank {rankValue} into SQLite database, including opponent details.");
        }



        /// <summary>
        /// 上傳 JSON 至伺服器（保留原有上傳邏輯，但 serverUrl 從 ip.txt 讀取）
        /// </summary>
        /// <param name="filePath">包含 JSON 內容的檔案路徑</param>
        private static void UploadJsonToServer(string filePath)
        {
            // 從 ip.txt 讀取 serverUrl
            string ipFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ip.txt");
            string serverUrl = "";
            if (File.Exists(ipFilePath))
            {
                serverUrl = File.ReadAllText(ipFilePath, Encoding.UTF8).Trim();
                Console.WriteLine($"Loaded server URL from ip.txt: {serverUrl}");
            }
            else
            {
                Console.WriteLine("ip.txt not found. Using default server URL.");
                Environment.Exit(1);
            }
            string tokenFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "token.txt");

            string token = "";
            if (File.Exists(tokenFilePath))
            {
                token = File.ReadAllText(tokenFilePath, Encoding.UTF8).Trim();
                Console.WriteLine($"Loaded token from token.txt: {token}");
            }
            else
            {
                Console.WriteLine("token.txt not found. Using default token.");
                Environment.Exit(1);
            }

            try
            {
                string jsonData = File.ReadAllText(filePath, Encoding.UTF8);
                var client = new RestClient(serverUrl);
                var request = new RestRequest(Method.POST);
                request.AddParameter("application/json", jsonData, ParameterType.RequestBody);
                request.AddHeader("Token", token);
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("User-Agent", "PostmanRuntime/7.39.0");
                request.AddHeader("Connection", "keep-alive");
                request.AddHeader("Accept-Encoding", "gzip, deflate, br");

                IRestResponse response = client.Execute(request);
                Console.WriteLine($"Uploaded {Path.GetFileName(filePath)} to server, response status code: {response.StatusCode}");
                Console.WriteLine($"Response content: {response.Content}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to upload {Path.GetFileName(filePath)} to server: {ex.Message}");
            }
        }

        private static void CheckAndPauseAt3AM()
        {
            DateTime now = DateTime.Now;
            DateTime today3AM = now.Date.AddHours(3);
            if (now > today3AM)
            {
                today3AM = today3AM.AddDays(1);
            }

            TimeSpan timeTo3AM = today3AM - now;
            if (timeTo3AM.TotalMinutes <= 15)
            {
                Console.WriteLine("Approaching 3 AM, pausing the program for 60 minutes...");
                Thread.Sleep(TimeSpan.FromMinutes(60));
                ExecuteDecryptmxdat();
            }
        }

        private static void ExecuteDecryptmxdat()
        {
            Console.WriteLine("Running Decryptmxdat...");
            string[] emptyArgs = new string[0];
            Decryptmxdat.DecryptMain(emptyArgs);
        }
    }
}
