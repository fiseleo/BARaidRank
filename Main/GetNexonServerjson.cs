using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RestSharp;

namespace mxdat
{
    // 自訂的 JsonConverter，用來處理字串屬性可接受數字與字串
    public class JsonStringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // 如果原始 JSON 的 token 是字串，直接讀取
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }
            // 如果是數字，將數字轉成字串
            else if (reader.TokenType == JsonTokenType.Number)
            {
                // 這裡可以依需求使用 GetInt32、GetInt64 或 GetDouble 等方法
                // 例如：使用 GetRawText 取得原始數值字串表示
                return reader.GetDouble().ToString();
            }
            else if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }
            else
            {
                throw new JsonException($"Unexpected token type: {reader.TokenType}");
            }
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }

    public class GetNexonServerjson
    {
        // 在 RequestBody 類別的屬性上加上自訂轉換器
        public class RequestBody
        {
            [JsonConverter(typeof(JsonStringConverter))]
            public string market_game_id { get; set; }
            
            [JsonConverter(typeof(JsonStringConverter))]
            public string language { get; set; }
            
            [JsonConverter(typeof(JsonStringConverter))]
            public string advertising_id { get; set; }
            
            [JsonConverter(typeof(JsonStringConverter))]
            public string market_code { get; set; }
            
            [JsonConverter(typeof(JsonStringConverter))]
            public string sdk_version { get; set; }
            
            [JsonConverter(typeof(JsonStringConverter))]
            public string country { get; set; }
            
            [JsonConverter(typeof(JsonStringConverter))]
            public string curr_build_version { get; set; }
            
            [JsonConverter(typeof(JsonStringConverter))]
            public string curr_build_number { get; set; }
            
            [JsonConverter(typeof(JsonStringConverter))]
            public string curr_patch_version { get; set; }
        }

        public static string GetNexonServerjsonMain(string[] args)
        {
            var client = new RestClient("https://api-pub.nexon.com/patch/v1.1/version-check");
            var request = new RestRequest();
            request.Method = Method.POST;

            // 刪除舊檔案
            if (File.Exists("resource.json"))
            {
                File.Delete("resource.json");
            }
            if (File.Exists("Excel.zip"))
            {
                File.Delete("Excel.zip");
            }

            request.AddHeader("Connection", "keep-alive");
            request.AddHeader("User-Agent", "Dalvik/2.1.0 (Linux; U; Android 12; SM-A226B Build/V417IR)");
            request.AddHeader("Host", "api-pub.nexon.com");
            request.AddHeader("Accept-Encoding", "gzip");
            request.AddHeader("Content-Type", "application/json; charset=utf-8");

            string rootDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string filePath = Path.Combine(rootDirectory, "body.json");

            if (!File.Exists(filePath))
            {
                Console.WriteLine("body.json not found");
                return "";
            }

            var jsonContent = File.ReadAllText(filePath, Encoding.UTF8);

            // 使用 System.Text.Json 反序列化時，若 RequestBody 中有屬性數字與字串不符，
            // 透過自訂 JsonStringConverter 便可處理數字轉字串的需求
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            RequestBody body;
            try
            {
                body = JsonSerializer.Deserialize<RequestBody>(jsonContent, options);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON deserialization error: {ex.Message}");
                return "";
            }

            request.AddJsonBody(body);

            var response = client.Execute(request);
            Console.WriteLine(response.Content);
            string resourcejsonPath = Path.Combine(rootDirectory, "resource.json");
            File.WriteAllText(resourcejsonPath, response.Content, Encoding.UTF8);

            // 假設 GetExcelzip.GetExcelzipMain 在其他地方已實作
            GetExcelzip.GetExcelzipMain(args);

            return response.Content;
        }
    }
}
