# VtuberDataCollecter
利用YoutubeAPI收集資料並建立可視化模型


## 使用技術 (Tech Stack)
- 開發語言：C# (.NET 8)
- 資料庫：MySQL
- 視覺化：預計用Vue.js + ECharts


## 安裝與使用方式 (Installation & Usage)
1. **Clone 專案**：
   ```sh
   git clone https://github.com/XZ1-1/VtuberDataCollecter.git


## 設定 API Key
- 本專案使用 YouTube API，需要設定 API Key：
> dotnet user-secrets set "YouTubeApiKey" "your-api-key-here"


## Database Schema
![Database Schema](ERD1.1.png)



## 開發進度
 - [x] 研究YouTube API(試抓)
 - [x] 設計ERD
 - [ ] API資料處理
 - [ ] 設定排程每日抓取資料進資料庫
 - [ ] 研究ASP.NET Core API
 - [ ] 建ASP.NET Core API
 - [ ] 研究Vue.js + ECharts
 - [ ] 串接Vue.js
 - [ ] 部屬網站
