using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using VtuberDataCollecter;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using Google.Apis.YouTube.v3.Data;
using System.Xml;

/*分析影片受歡迎要素*/

class Program
{
    static List<string> channelIds = new List<string>
    {
        "UCL_qhgtOy0dy1Agp8vkySQg", // Mori Calliope
        "UCoSrY_IQQVpmIRZ9Xf-y93g", // Gawr Gura
        "UCyl1z3jo3XHR1riLFKG5UAg", // Watson Amelia
        "UCMwGHR0BTZuLsmjY_NT5Pwg", // Ninomae Ina'nis
        "UCHsx4Hqa-1ORjQTh9TYDhww", // Takanashi Kiara
        "UC8rcEBzJSleTkf_-agPM20g", // IRyS
        "UCmbs8T6MWqUHP1tIQvSgKrg", // Ouro Kronii
        "UC3n5uGu18FoCy23ggWWp8tA", // Nanashi Mumei
        "UCO_aKKYxn4tvrqPjcTzZ6EQ", // Ceres Fauna (Graduated)
        "UCsUj0dszADCGbF3gNrQEuSQ"  // Tsukumo Sana (Graduated)
    };

    static async Task Main()
    {
        #region 連到SQLiteDB
        /*連到SQLiteDB
        //using var connection = new SqliteConnection("Data Source=YouTubeData.db");
        //connection.Open();
        //CreateDatabase(connection);

        //EN組10個頻道
        foreach (var channelId in channelIds)
        {
            await FetchAndStoreVideosAsync(channelId, connection);
        }

        Console.WriteLine("資料抓取完成！");
        */
        #endregion

        //Youtube API撈資料

        //EN組10個頻道
        
        foreach (var channelId in channelIds)
        {
            await FetchAndStoreVideosAsync(channelId);
        }


        //MySQL
        using (var db = new YoutubeDB())
        {
            try
            {
                db.Database.EnsureCreated();  // 確保資料庫存在，不存在就建立
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        using (var db = new YoutubeDB())
        {
            try
            {
                //db.Videos.Update(new Video { VideoId = "abc123", Title = "My Video", Description = "description", Duration = 3600, PublishedAt = DateTime.UtcNow });
                //db.SaveChanges();

                //var video = db.Videos.FirstOrDefault(v => v.VideoId == "abc123");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }


    }

    static string GetAPI()
    {
        IConfigurationRoot config = new ConfigurationBuilder()
                                          .AddUserSecrets<Program>()  // 告訴 .NET 讀取 User Secrets
                                          .Build();

        string apiKey = config["YouTubeApiKey"] ?? "";  // 取得 API Key
        return apiKey;
    }

    /// <summary>MySQL</summary>
    static async Task FetchAndStoreVideosAsync(string channelId)
    {
        try
        {
            //打開Youtube服務，給API Key
            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = GetAPI()
            });

            // Channel
            //<請求>搜尋ChannelId的snippet(搜尋某頻道的影片基本資訊)
            var request =  youtubeService.Search.List("snippet");
            request.ChannelId = channelId;  //指定特定頻道
            request.MaxResults = 5; //最多5個
            request.Order = SearchResource.ListRequest.OrderEnum.Date;  //從最新的開始搜尋
            request.Type = "video"; //僅搜尋影片，不會有頻道、播放清單等等其他資料
            var searchResponse = await request.ExecuteAsync();


            if (searchResponse.Items.Count == 0)
            {
                Console.WriteLine($"找不到頻道 {channelId} 的影片");
                return;
            }
            string channelName = searchResponse.Items[0].Snippet.ChannelTitle;
            var channelRequest = youtubeService.Channels.List("statistics");
            channelRequest.Id = channelId;
            var channelResponse = await channelRequest.ExecuteAsync();

            ulong channelSubscribers = 0, channelTotalViews = 0, channelTotalVideos = 0;
            if (channelResponse.Items.Count > 0)
            {
                var channelStats = channelResponse.Items[0].Statistics;
                channelSubscribers = channelStats.SubscriberCount.GetValueOrDefault(0);
                channelTotalViews = channelStats.ViewCount.GetValueOrDefault(0);
                channelTotalVideos = channelStats.VideoCount.GetValueOrDefault(0);
            }
            Console.WriteLine($"channelName:{channelName}");
            Console.WriteLine($"channelSubscribers:{channelSubscribers}");
            Console.WriteLine($"channelTotalViews:{channelTotalViews}");
            Console.WriteLine($"channelTotalVideos:{channelTotalVideos}");

            //搜了5部最新的影片之後
            foreach (var searchResult in searchResponse.Items)
            {
                //video
                string videoId = searchResult.Id.VideoId;

                //<請求>列出影片基本資訊、內容、統計數據、直播資訊
                var videoRequest = youtubeService.Videos.List("snippet,contentDetails,statistics,liveStreamingDetails");
                videoRequest.Id = videoId;  //設定影片ID，有5部
                var videoResponse = await videoRequest.ExecuteAsync();

                if (videoResponse.Items.Count == 0)
                    continue;

                var video = videoResponse.Items[0];
                string title = video.Snippet.Title;
                string description = video.Snippet.Description;
                string thumbnailUrl = video.Snippet.Thumbnails.Maxres?.Url ?? 
                                      video.Snippet.Thumbnails.High?.Url ?? 
                                      video.Snippet.Thumbnails.Medium?.Url ?? 
                                      video.Snippet.Thumbnails.Default__.Url;
                DateTime publishedAt = DateTime.Parse(searchResult.Snippet.PublishedAtRaw);//str"2024-03-02T15:30:00Z" to DateTime
                //!!!Video_tag 要另外處理
                string tags = video.Snippet.Tags != null ? string.Join(",", video.Snippet.Tags) : "";
                //ulong liveMaxConcurrentViewers = video.LiveStreamingDetails?.ConcurrentViewers ?? 0;
                uint duration = ParseYouTubeDurationToInt(video.ContentDetails.Duration);
                ulong commentCount = video.Statistics.CommentCount.GetValueOrDefault(0);
                ulong viewCount = video.Statistics.ViewCount.GetValueOrDefault(0);
                ulong likeCount = video.Statistics.LikeCount.GetValueOrDefault(0);
                // 取得 SuperChat 總收入
                //decimal superChatIncome = await FetchSuperChatIncomeAsync(youtubeService, videoId);

                Console.WriteLine($"videoId:{videoId}");
                Console.WriteLine($"title:{title}");
                Console.WriteLine($"description:{description}");
                Console.WriteLine($"thumbnailUrl:{thumbnailUrl}");
                Console.WriteLine($"publishedAt:{publishedAt}");
                Console.WriteLine($"tags:{tags}");
                //Console.WriteLine($"liveMaxConcurrentViewers:{liveMaxConcurrentViewers}");
                Console.WriteLine($"duration:{duration}");
                Console.WriteLine($"commentCount:{commentCount}");
                Console.WriteLine($"viewCount:{viewCount}");
                Console.WriteLine($"likeCount:{likeCount}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FetchAndStoreVideosAsync:{ex.Message}");
        }
    }

    /// <summary>SQLite</summary>
    static void CreateDatabase(SqliteConnection connection)
    {
        try
        {
            var command = connection.CreateCommand();
            #region data
            /*
             * VideoId                  影片ID    PRIMARY KEY
             * Title                    標題
             * Description              敘述
             * Tags                     標籤
             * ThumbnailUrl             縮圖網址
             * LiveMaxConcurrentViewers 直播最大同時觀看人數
             * Duration                 影片長度
             * CommentCount             影片留言數
             * ViewCount                影片觀看數
             * LikeCount                影片喜歡數
             * //SuperChatIncome          超級感謝收入
             * PublishedAt              影片發布時間(ISO 8601，ex.2021-12-01T15:30:00Z)
             * ChannelId                頻道ID
             * ChannelName              頻道名稱
             * ChannelSubscribers       頻道訂閱數
             * ChannelTotalViews        頻道總觀看數
             * ChannelTotalVideos       頻道影片數量
             */
            #endregion
            //SuperChatIncome INTEGER,
            command.CommandText = @"
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS channel (
                ChannelId TEXT PRIMARY KEY,
                ChannelName TEXT,
                ChannelSubscribers INTEGER,
                ChannelTotalViews INTEGER,
                ChannelTotalVideos INTEGER
            );

            CREATE TABLE IF NOT EXISTS video (
                VideoId TEXT PRIMARY KEY,
                ChannelId TEXT,
                Title TEXT,
                Description TEXT,
                ThumbnailUrl TEXT,
                LiveMaxConcurrentViewers INTEGER,
                Duration INTEGER,  
                CommentCount INTEGER,
                ViewCount INTEGER,
                LikeCount INTEGER,
                PublishedAt TEXT,  
                FOREIGN KEY (ChannelId) REFERENCES channel(ChannelId) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS tags (
                TagId INTEGER PRIMARY KEY AUTOINCREMENT,
                TagName TEXT UNIQUE
            );

            CREATE TABLE IF NOT EXISTS video_tags (
                VideoId TEXT NOT NULL,
                TagId INTEGER NOT NULL,
                PRIMARY KEY (VideoId, TagId),
                FOREIGN KEY (VideoId) REFERENCES video(VideoId) ON DELETE CASCADE,
                FOREIGN KEY (TagId) REFERENCES tags(TagId) ON DELETE CASCADE
            );
            ";
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CreateDatabase:{ex.Message}");
        }
    }

    /// <summary>SQLite</summary>
    static async Task FetchAndStoreVideosAsync(string channelId, SqliteConnection connection)
    {
        try
        {
            //打開Youtube服務，給API Key
            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = GetAPI()
            });

            //<請求>搜尋ChannelId的snippet(搜尋某頻道的影片基本資訊)
            //搜尋snippet(影片基本資訊)
            var request = youtubeService.Search.List("snippet");
            //指定特定頻道
            request.ChannelId = channelId;
            //最多5個
            request.MaxResults = 5;
            //從最新的開始搜尋
            request.Order = SearchResource.ListRequest.OrderEnum.Date;
            //僅搜尋影片，不會有頻道、播放清單等等其他資料
            request.Type = "video";

            var searchResponse = await request.ExecuteAsync();

            //搜了5部最新的影片之後
            foreach (var searchResult in searchResponse.Items)
            {
                //影片資訊:videoId,title,description,thumbnailUrl,publishedAt
                string videoId = searchResult.Id.VideoId;
                string title = searchResult.Snippet.Title;
                string description = searchResult.Snippet.Description;
                string thumbnailUrl = searchResult.Snippet.Thumbnails.Default__.Url;
                string publishedAt = searchResult.Snippet.PublishedAtRaw;

                //<請求>列出影片基本資訊、內容、統計數據、直播資訊
                var videoRequest = youtubeService.Videos.List("snippet,contentDetails,statistics,liveStreamingDetails");
                //設定影片ID，有5部
                videoRequest.Id = videoId;
                //執行請求，等到回傳後才繼續執行
                var videoResponse = await videoRequest.ExecuteAsync();

                if (videoResponse.Items.Count == 0)
                    continue;

                //影片資訊:viewCount,likeCount,commentCount
                var video = videoResponse.Items[0];
                string tags = video.Snippet.Tags != null ? string.Join(",", video.Snippet.Tags) : "";
                long viewCount = (long)video.Statistics.ViewCount.GetValueOrDefault(0);
                long likeCount = (long)video.Statistics.LikeCount.GetValueOrDefault(0);
                long commentCount = (long)video.Statistics.CommentCount.GetValueOrDefault(0);
                ulong liveMaxConcurrentViewers = video.LiveStreamingDetails?.ConcurrentViewers ?? 0;
                string duration = ParseYouTubeDurationToStr(video.ContentDetails.Duration);
                // 取得 SuperChat 總收入
                //decimal superChatIncome = await FetchSuperChatIncomeAsync(youtubeService, videoId);

                // 取得頻道相關資訊
                string channelName = searchResult.Snippet.ChannelTitle;

                var channelRequest = youtubeService.Channels.List("statistics");
                channelRequest.Id = channelId;
                var channelResponse = await channelRequest.ExecuteAsync();

                long channelSubscribers = 0, channelTotalViews = 0, channelTotalVideos = 0;
                if (channelResponse.Items.Count > 0)
                {
                    var channelStats = channelResponse.Items[0].Statistics;
                    channelSubscribers = (long)channelStats.SubscriberCount.GetValueOrDefault(0);
                    channelTotalViews = (long)channelStats.ViewCount.GetValueOrDefault(0);
                    channelTotalVideos = (long)channelStats.VideoCount.GetValueOrDefault(0);
                }

                StoreVideoData(connection, videoId, title, description, tags, thumbnailUrl, liveMaxConcurrentViewers, duration,
                   commentCount, viewCount, likeCount, publishedAt, channelId, channelName,
                   channelSubscribers, channelTotalViews, channelTotalVideos);
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"FetchAndStoreVideosAsync:{ex.Message}");
        }
    }
    /// <summary>解析影片時長 (ISO 8601 -> HH:mm:ss)</summary>
    /// <param name="duration"></param>
    /// <returns>HH:mm:ss</returns>
    static string ParseYouTubeDurationToStr(string duration)
    {
        try
        {
            var match = Regex.Match(duration, @"PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?");
            int hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
            int minutes = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            int seconds = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

            return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{ex.Message}");
            return "";
        }
    }
    public static uint ParseYouTubeDurationToInt(string duration)
    {
        return (uint)XmlConvert.ToTimeSpan(duration).TotalSeconds;
    }

    static async Task<decimal> FetchSuperChatIncomeAsync(YouTubeService youtubeService, string videoId)
    {
        try
        {
            var liveChatRequest = youtubeService.LiveBroadcasts.List("snippet");
            liveChatRequest.Id = videoId;
            var liveChatResponse = await liveChatRequest.ExecuteAsync();

            if (liveChatResponse.Items.Count == 0 || liveChatResponse.Items[0].Snippet.LiveChatId == null)
                return 0;

            string liveChatId = liveChatResponse.Items[0].Snippet.LiveChatId;
            var chatMessagesRequest = youtubeService.LiveChatMessages.List(liveChatId, "snippet,authorDetails");
            chatMessagesRequest.MaxResults = 200;

            var chatMessagesResponse = await chatMessagesRequest.ExecuteAsync();

            //精準算錢
            decimal totalSuperChatIncome = 0;
            foreach (var message in chatMessagesResponse.Items)
            {
                if (message.Snippet.SuperChatDetails != null)
                {
                    //SuperChatDetails.AmountMicros 1 dollar = 1,000,000 micros
                    totalSuperChatIncome += (decimal)(message.Snippet.SuperChatDetails.AmountMicros / 1_000_000m); // 轉換金額
                }
            }

            return totalSuperChatIncome;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{ex.Message}");
            return 0; // 如果 API 失敗，預設為 0
        }
    }

    /// <summary>SQLite</summary>
    static void StoreVideoData(SqliteConnection connection, string videoId, string title, string description,
                               string tags, string thumbnailUrl, ulong liveMaxConcurrentViewers, string duration,
                               long commentCount, long viewCount, long likeCount,
                               string publishedAt, string channelId, string channelName, long channelSubscribers,
                               long channelTotalViews, long channelTotalVideos)
    {
        try
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
            INSERT OR REPLACE INTO VideoData 
            (VideoId, Title, Description, Tags, ThumbnailUrl, LiveMaxConcurrentViewers, Duration, CommentCount, 
             ViewCount, LikeCount, PublishedAt, ChannelId, ChannelName, ChannelSubscribers, 
             ChannelTotalViews, ChannelTotalVideos) 
            VALUES 
            ($videoId, $title, $description, $tags, $thumbnailUrl, $liveMaxConcurrentViewers, $duration, $commentCount, 
             $viewCount, $likeCount, $publishedAt, $channelId, $channelName, $channelSubscribers, 
             $channelTotalViews, $channelTotalVideos);";
            //Console.WriteLine(command.CommandText);

            command.Parameters.AddWithValue("$videoId", videoId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$title", title ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$description", description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$tags", tags ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$thumbnailUrl", thumbnailUrl ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$liveMaxConcurrentViewers", liveMaxConcurrentViewers);
            command.Parameters.AddWithValue("$duration", duration ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$commentCount", commentCount);
            command.Parameters.AddWithValue("$viewCount", viewCount);
            command.Parameters.AddWithValue("$likeCount", likeCount);
            //command.Parameters.AddWithValue("$superChatIncome", (long)superChatIncome);
            command.Parameters.AddWithValue("$publishedAt", publishedAt ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$channelId", channelId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$channelName", channelName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$channelSubscribers", channelSubscribers);
            command.Parameters.AddWithValue("$channelTotalViews", channelTotalViews);
            command.Parameters.AddWithValue("$channelTotalVideos", channelTotalVideos);

            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{ex.Message}");
        }
    }
}
