using StackExchange.Redis;
using System;
using System.Linq;
using System.Timers;
using System.Windows;

namespace WPFDisplay
{
    public partial class MainWindow : Window
    {
        private ConnectionMultiplexer redis;
        private System.Timers.Timer timer;
        private DateTime startTime;
        private decimal startPrice;
        private decimal minPrice = decimal.MaxValue;
        private decimal maxPrice = decimal.MinValue;
        private string minTime;
        private string maxTime;

        public MainWindow()
        {
            InitializeComponent();

            // Đảm bảo tên container Redis là my-redis trong Docker Compose
            string redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";

            try
            {
                // Kết nối tới Redis
                redis = ConnectionMultiplexer.Connect($"{redisHost}:6379");

                // Kiểm tra kết nối bằng cách gọi Ping tới Redis
                var db = redis.GetDatabase();
                var result = db.Ping();  // Lệnh Ping để kiểm tra kết nối

                if (redis.IsConnected)
                {
                    // Hiển thị trạng thái kết nối thành công
                    RedisConnectionStatusLabel.Text = $"Redis Status: Connected! Ping: {result.TotalMilliseconds} ms";
                    Console.WriteLine("[INFO] Kết nối Redis thành công!");
                }
                else
                {
                    // Nếu Redis không phản hồi, hiển thị lỗi kết nối
                    RedisConnectionStatusLabel.Text = "Redis Status: Failed to connect!";
                    Console.WriteLine("[ERROR] Kết nối Redis thất bại!");
                    return;
                }
            }
            catch (RedisConnectionException ex)
            {
                // Bắt lỗi khi không thể kết nối tới Redis (do Redis chưa bật)
                RedisConnectionStatusLabel.Text = $"Redis Status: Redis chưa được bật! - {ex.Message}";
                Console.WriteLine($"[ERROR] Redis chưa được bật hoặc không thể kết nối: {ex.Message}");
                return;
            }
            catch (Exception ex)
            {
                // Bắt lỗi chung khi có lỗi khác
                RedisConnectionStatusLabel.Text = $"Redis Status: Error - {ex.Message}";
                Console.WriteLine($"[ERROR] Lỗi khi kết nối Redis: {ex.Message}");
                return;
            }

            startTime = DateTime.Now;
            LoadInitialPrice();
            SetupTimer();
        }

        private void SetupTimer()
        {
            timer = new System.Timers.Timer(1000);  // 1 giây cập nhật một lần
            timer.Elapsed += OnTimerTick;
            timer.Start();
        }

        private void LoadInitialPrice()
        {
            var db = redis.GetDatabase();
            var latestPrice = db.ListRange("bitcoin_prices", -1, -1).FirstOrDefault();
            if (!latestPrice.IsNullOrEmpty)
            {
                var data = latestPrice.ToString().Split('|');
                if (data.Length == 2)
                {
                    startPrice = decimal.Parse(data[1]);
                    StartTimeLabel.Text = $"Start Time: {startTime.ToString("yyyy-MM-dd HH:mm:ss")}";
                    StartPriceLabel.Text = $"Start Price: {startPrice}";
                }
            }
        }

        private void OnTimerTick(object sender, ElapsedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var db = redis.GetDatabase();
                var priceList = db.ListRange("bitcoin_prices", -50, -1);

                // Cập nhật thời gian hiện tại liên tục bằng DateTime.Now
                string currentFormattedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                CurrentPriceLabel.Text = $"Current Time: {currentFormattedTime}";

                if (priceList.Length > 0)
                {
                    var latest = priceList.Last().ToString().Split('|');
                    if (latest.Length == 2)
                    {
                        var priceTime = DateTime.Parse(latest[0]).ToString("yyyy-MM-dd HH:mm:ss");
                        if (decimal.TryParse(latest[1], out var currentPrice))
                        {
                            // Cập nhật giá trị hiện tại với giá mới từ Redis
                            CurrentPriceLabel.Text = $"Current Time: {currentFormattedTime} - Price: {currentPrice}";

                            // Cập nhật giá thấp nhất và cao nhất
                            if (currentPrice < minPrice)
                            {
                                minPrice = currentPrice;
                                minTime = priceTime;
                            }

                            if (currentPrice > maxPrice)
                            {
                                maxPrice = currentPrice;
                                maxTime = priceTime;
                            }

                            MinPriceLabel.Text = $"Min Time: {minTime} - Min Price: {minPrice}";
                            MaxPriceLabel.Text = $"Max Time: {maxTime} - Max Price: {maxPrice}";

                            // Cập nhật danh sách 50 giá trị gần nhất với số thứ tự
                            PriceListBox.Items.Clear();
                            int counter = 1;
                            foreach (var price in priceList)
                            {
                                var priceData = price.ToString().Split('|');
                                if (priceData.Length == 2)
                                {
                                    string formattedPriceTime = DateTime.Parse(priceData[0]).ToString("yyyy-MM-dd HH:mm:ss");
                                    PriceListBox.Items.Add($"{counter}. {formattedPriceTime} - Price: {priceData[1]}");
                                    counter++;
                                }
                            }
                        }
                    }
                }
            });
        }
    }
}

