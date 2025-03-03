using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VtuberDataCollecter
{
    //DbContext就資料庫
    public class YoutubeDB : DbContext
    {
        //DbSet就資料表
        public DbSet<Video> Videos { get; set; }  // 影片表
        public DbSet<Channel> Channels { get; set; }  // 頻道表

        private string GetConnectionStrings()
        {
            try
            {
                IConfigurationRoot config = new ConfigurationBuilder()
                                      .AddUserSecrets<Program>()  // 告訴 .NET 讀取 User Secrets
                                      .Build();

                string connectionString = config.GetConnectionString("MySQL") ?? "";  // 取得 MySQL 連線字串
                return connectionString;
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return "";
            }
        }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            var connectionString = GetConnectionStrings();
            Console.WriteLine($"connectionString:{connectionString}");
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
                .EnableDetailedErrors()
                .EnableSensitiveDataLogging();  //上線時要刪掉這行
        }
        //UNSIGNED,VARCHAR的資料屬性在C#沒，就用這個改
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //Video
            //modelBuilder.Entity<Video>()
            //    .Property(c => c.LiveMaxConcurrentViewers)
            //    .HasColumnType("INT UNSIGNED");
            modelBuilder.Entity<Video>()
                .Property(c => c.Duration)
                .HasColumnType("INT UNSIGNED");
            modelBuilder.Entity<Video>()
                .Property(c => c.CommentCount)
                .HasColumnType("INT UNSIGNED");
            modelBuilder.Entity<Video>()
                .Property(c => c.ViewCount)
                .HasColumnType("BIGINT UNSIGNED");
            modelBuilder.Entity<Video>()
                .Property(c => c.LikeCount)
                .HasColumnType("BIGINT UNSIGNED");
            modelBuilder.Entity<Video>()
                .Property(v => v.PublishedAt)
                .HasColumnType("DATETIME");

            //Channel
            modelBuilder.Entity<Channel>()
                .Property(c => c.ChannelSubscribers)
                .HasColumnType("BIGINT UNSIGNED");  // 設定為 UNSIGNED
            modelBuilder.Entity<Channel>()
                .Property(c => c.ChannelTotalViews)
                .HasColumnType("BIGINT UNSIGNED");  // 設定為 UNSIGNED
            modelBuilder.Entity<Channel>()
                .Property(c => c.ChannelTotalVideos)
                .HasColumnType("INT UNSIGNED");  // 設定為 UNSIGNED

            //Video_Tags
            modelBuilder.Entity<Video_Tag>()
                .Property(c => c.TagId)
                .HasColumnType("INT UNSIGNED");
            modelBuilder.Entity<Video_Tag>()
                .HasKey(vt => new { vt.VideoId, vt.TagId });  // Composite Key

            //Tags
            modelBuilder.Entity<Tag>()
                .Property(c => c.TagId)
                .HasColumnType("INT UNSIGNED");
        }
    }

    //宣告table的Column
    public class Video
    {
        [Key]
        public string? VideoId { get; set; }
        public string? ChannelId { get; set; }  //foreign key
        public Channel? Channel { get; set; }    //可以存該video的channel訊息
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? ThumbnailUrl { get; set; }
        //public uint LiveMaxConcurrentViewers { get; set; }
        public uint Duration { get; set; }
        public uint CommentCount { get; set; }
        public ulong ViewCount { get; set; }
        public ulong LikeCount { get; set; }
        public DateTime PublishedAt { get; set; }

        //1 Video有多個Video_Tag
        public List<Video_Tag> Video_Tags { get; set; } = new List<Video_Tag>();
    }
    public class Channel
    {
        //string=varchar(255)
        [Key]
        public string? ChannelId { get; set; }
        public string? ChannelName { get; set; }
        public ulong ChannelSubscribers { get; set; }
        public ulong ChannelTotalViews { get; set; }
        public uint ChannelTotalVideos { get; set; }

        //1 Channel有多個Videos
        public List<Video> Videos { get; set; } = new List<Video>();
    }
    public class Video_Tag
    {
        public string? VideoId { get; set; }      //foreign key
        public Video? Video { get; set; }    //可以存該video訊息
        public uint? TagId { get; set; }       //foreign key
        public Tag? Tag { get; set; }    //可以存該Tag訊息
    }
    public class Tag
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]   //AUTO_INCREMENT 自動遞增
        public uint? TagId { get; set; }
        public string? TagName { get; set; }

        //1 Tag有多個Video_Tag(1個tag可能被多個video使用)
        public List<Video_Tag> Video_Tags { get; set; } = new List<Video_Tag>();
    }

}
