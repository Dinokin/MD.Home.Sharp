using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MD.Home.Sharp.Cache;
using MD.Home.Sharp.Extensions;
using MD.Home.Sharp.Others.Cache;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace MD.Home.Sharp.Controllers
{
    [Route("")]
    [ResponseCache(Duration = 1209600)]
    public sealed class MainController : Controller
    {
        private readonly CacheManager _cacheManager;
        private readonly CacheStats _cacheStats;
        private readonly JsonSerializerOptions _serializerOptions;

        public MainController(CacheManager cacheManager, CacheStats cacheStats, JsonSerializerOptions serializerOptions)
        {
            _cacheManager = cacheManager;
            _cacheStats = cacheStats;
            _serializerOptions = serializerOptions;
        }
        
        [HttpGet("{action}")]
        [ResponseCache(NoStore = true)]
        public IActionResult Stats() => new JsonResult(_cacheStats.Snapshot, _serializerOptions);

        [HttpGet("data/{chapterId:length(32)}/{fileName}")]
        public async Task<IActionResult> FetchNormalImage(string chapterId, string fileName) => await FetchImage(false, chapterId, fileName);

        [HttpGet("data-saver/{chapterId:length(32)}/{fileName}")]
        public async Task<IActionResult> FetchDataSaverImage(string chapterId, string fileName) => await FetchImage(true, chapterId, fileName);
        
        [SuppressMessage("ReSharper", "UnusedParameter.Global")]
        [HttpGet("{token}/data/{chapterId:length(32)}/{fileName}")]
        public async Task<IActionResult> FetchTokenizedNormalImage(string token, string chapterId, string fileName) => await FetchImage(false, chapterId, fileName);

        [SuppressMessage("ReSharper", "UnusedParameter.Global")]
        [HttpGet("{token}/data-saver/{chapterId:length(32)}/{fileName}")]
        public async Task<IActionResult> FetchTokenizedDataSaverImage(string token, string chapterId, string fileName) => await FetchImage(true, chapterId, fileName); 
        
        private async Task<IActionResult> FetchImage(bool dataSaver, string chapterId, string fileName)
        {
            var url = $"/{(dataSaver ? "data-saver" : "data")}/{chapterId}/{fileName}";
            
            var cacheEntry = _cacheManager.GetCacheEntry(url);

            return cacheEntry == null ? await HandleCacheMiss(url) : HandleCacheHit(url, cacheEntry);
        }

        private async Task<IActionResult> HandleCacheMiss(string url)
        {
            Log.Logger.Debug($"Request for {url} missed cache");

            HttpResponseMessage response;

            try
            {
                response = await Program.HttpClient.GetAsync(Program.MangaDexClient.RemoteSettings.ImageServer + url);
            }
            catch
            {
                Log.Logger.Error($"Upstream query for {url} failed without status");
                
                return StatusCode(500);
            }

            if (!response.IsSuccessStatusCode)
            {
                Log.Logger.Warning($"Upstream query for {url} errored with status {(int) response.StatusCode}");

                return StatusCode((int) response.StatusCode);
            }

            var content = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType;
            var contentLength = response.Content.Headers.ContentLength;
            var lastModified = response.Content.Headers.LastModified;

            if (content.Length == 0)
            {
                Log.Logger.Warning($"Upstream query for {url} returned empty content");

                return StatusCode(500);
            }
            
            if (!contentType.IsImageMimeType())
            {
                Log.Logger.Warning($"Upstream query for {url} returned bad mimetype {contentType}");

                return StatusCode(500);
            }

            Log.Logger.Debug($"Upstream query for {url} succeeded");

            var cacheEntry = _cacheManager.InsertCacheEntry(url, contentType!, lastModified.GetValueOrDefault(DateTimeOffset.UtcNow), content);
            
            if (contentLength != null)
                Response.Headers.Add("Content-Length", contentLength.ToString());
            else
                Response.Headers.Add("Transfer-Encoding", "chunked");

            Response.Headers.Add("X-Cache", "MISS");

            return ReturnFile(cacheEntry);
        }
        
        private IActionResult HandleCacheHit(string url, CacheEntry cacheEntry)
        {
            Log.Logger.Debug($"Request for {url} hit cache");
            
            Response.Headers.Add("X-Cache", "HIT");
            Response.Headers.Add("Content-Length", cacheEntry.Content.LongLength.ToString());
            
            return ReturnFile(cacheEntry);
        }

        private IActionResult ReturnFile(CacheEntry cacheEntry)
        {
            Response.Headers.Add("Last-Modified", cacheEntry.LastModified.ToString(CultureInfo.InvariantCulture));

            return File(cacheEntry.Content, cacheEntry.ContentType);
        }
    }
}