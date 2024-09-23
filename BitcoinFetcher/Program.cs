using StackExchange.Redis;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // Sử dụng tên container "my-redis" thay vì localhost
        string redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "my-redis";

        try
        {
            // Kết nối tới Redis bằng tên container "my-redis"
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect($"{redisHost}:6379");
            if (redis.IsConnected)
            {
                Console.WriteLine("[INFO] Kết nối Redis thành công!");
            }
            else
            {
                Console.WriteLine("[ERROR] Kết nối Redis thất bại!");
                return; // Thoát chương trình nếu kết nối thất bại
            }

            IDatabase db = redis.GetDatabase();

            while (true)
            {
                // Lấy giá Bitcoin từ API thực tế
                var bitcoinPrice = await GetBitcoinPrice();
                var timestamp = DateTime.Now;

                // Đẩy vào Redis Queue
                string message = $"{timestamp.ToString("o")}|{bitcoinPrice}";
                db.ListRightPush("bitcoin_prices", message);

                Console.WriteLine($"[INFO] {message}");

                await Task.Delay(10000); // Chờ 10 giây
            }
        }
        catch (RedisConnectionException ex)
        {
            Console.WriteLine($"[ERROR] Không thể kết nối Redis: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Lỗi không xác định: {ex.Message}");
        }
    }

    static async Task<decimal> GetBitcoinPrice()
    {
        using HttpClient client = new HttpClient();
        string response = await client.GetStringAsync("https://api.coindesk.com/v1/bpi/currentprice/BTC.json");

        // Parse response JSON
        try
        {
            using JsonDocument doc = JsonDocument.Parse(response);
            decimal bitcoinPrice = doc.RootElement
                .GetProperty("bpi")
                .GetProperty("USD")
                .GetProperty("rate_float")
                .GetDecimal();

            return bitcoinPrice;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[ERROR] Lỗi khi phân tích JSON: {ex.Message}");
            return 0; // Trả về giá trị mặc định khi có lỗi
        }
    }
}
