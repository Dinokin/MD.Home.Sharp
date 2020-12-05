using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MD.Home.Sharp.Cache;
using MD.Home.Sharp.Extensions;
using MD.Home.Sharp.Serialization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace MD.Home.Sharp.Controllers
{
    [Route("")]
    [ResponseCache(Duration = 1209600)]
    public sealed class MainController : Controller
    {
        private readonly CacheManager _cacheManager;

        public MainController(CacheManager cacheManager) => _cacheManager = cacheManager;

        [HttpGet("statistics")]
        public IActionResult CacheStatistics()
        {
            return new JsonResult(new
            {
                Cache.CacheStatistics.StartTime,
                Cache.CacheStatistics.HitCount,
                Cache.CacheStatistics.AverageHitTtfb,
                Cache.CacheStatistics.MissCount,
                Cache.CacheStatistics.AverageMissTtfb
            }, new JsonSerializerOptions {PropertyNamingPolicy = new SnakeCaseNamingPolicy(), WriteIndented = true});
        }

        [HttpGet("data/{chapterId:guid}/{name}")]
        public async Task<IActionResult> FetchNormalImage(Guid chapterId, string name) => await FetchImage(false, chapterId, name);

        [HttpGet("data-saver/{chapterId:guid}/{name}")]
        public async Task<IActionResult> FetchDataSaverImage(Guid chapterId, string name) => await FetchImage(true, chapterId, name);
        
        [HttpGet("{token}/data/{chapterId:guid}/{name}")]
        [SuppressMessage("ReSharper", "UnusedParameter.Global")]
        public async Task<IActionResult> FetchTokenizedNormalImage(string token, Guid chapterId, string name) => await FetchImage(false, chapterId, name);

        [HttpGet("{token}/data-saver/{chapterId:guid}/{name}")]
        [SuppressMessage("ReSharper", "UnusedParameter.Global")]
        public async Task<IActionResult> FetchTokenizedDataSaverImage(string token, Guid chapterId, string name) => await FetchImage(true, chapterId, name); 
        
        private async Task<IActionResult> FetchImage(bool dataSaver, Guid chapterId, string name)
        {
            var url = $"/{(dataSaver ? "data-saver" : "data")}/{chapterId:N}/{name}";
            
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