using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using MD.Home.Sharp.Cache;
using MD.Home.Sharp.Extensions;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace MD.Home.Sharp.Controllers
{
    [Route("")]
    [ResponseCache(Duration = 1209600)]
    public class MainController : Controller
    {
        private readonly CacheManager _cacheManager;
        private readonly ILogger _logger;

        public MainController(CacheManager cacheManager, ILogger logger)
        {
            _cacheManager = cacheManager;
            _logger = logger;
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
            
            var entry = _cacheManager.GetEntry(url.GetMd5HashAsGuid());

            return entry == null ? await HandleCacheMiss(url) : HandleCacheHit(url, entry);
        }

        private async Task<IActionResult> HandleCacheMiss(string url)
        {
            _logger.Information($"Request for {url} missed cache");

            HttpResponseMessage response;

            try
            {
                response = await Program.HttpClient.GetAsync(Program.MangaDexClient.RemoteSettings.ImageServer + url);
            }
            catch
            {
                _logger.Error($"Upstream query for {url} failed without status");
                
                return StatusCode(500);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.Information($"Upstream query for {url} errored with status {(int) response.StatusCode}");

                return StatusCode((int) response.StatusCode);
            }

            var content = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType;
            var contentLength = response.Content.Headers.ContentLength;
            var lastModified = response.Content.Headers.LastModified;

            if (content.Length == 0)
            {
                _logger.Warning($"Upstream query for {url} returned empty content");

                return StatusCode(500);
            }
            
            if (!contentType.IsImageMimeType())
            {
                _logger.Warning($"Upstream query for {url} returned bad mimetype {contentType}");

                return StatusCode(500);
            }

            _logger.Information($"Upstream query for {url} succeeded");

            var entry = new CacheEntry
            {
                Id = url.GetMd5HashAsGuid(),
                ContentType = contentType!,
                LastModified = lastModified.GetValueOrDefault(DateTimeOffset.UtcNow).UtcDateTime,
                LastAccessed = DateTime.UtcNow,
                Size = Convert.ToUInt64(content.LongLength),
                Content = content
            };

            _cacheManager.InsertEntry(entry);

            if (contentLength != null)
                Response.Headers.Add("Content-Length", entry.Size.ToString());
            else
                Response.Headers.Add("Transfer-Encoding", "chunked");

            Response.Headers.Add("X-Cache", "MISS");

            return ReturnFile(entry);
        }
        
        private IActionResult HandleCacheHit(string url, CacheEntry cacheEntry)
        {
            _logger.Information($"Request for {url} hit cache");
            
            Response.Headers.Add("X-Cache", "HIT");
            Response.Headers.Add("Content-Length", cacheEntry.Size.ToString());

            return ReturnFile(cacheEntry);
        }

        private IActionResult ReturnFile(CacheEntry cacheEntry)
        {
            Response.Headers.Add("Last-Modified", cacheEntry.LastModified.ToString(CultureInfo.InvariantCulture));

            return File(cacheEntry.Content, cacheEntry.ContentType);
        }
    }
}