using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MD.Home.Server.Cache;
using MD.Home.Server.Extensions;
using MD.Home.Server.Others;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Sodium;

namespace MD.Home.Server.Controllers
{
    [Route("")]
    public class MainController : Controller
    {
        private readonly CacheManager _cacheManager;
        private readonly MangaDexClient _mangaDexClient;
        private readonly ILogger _logger;

        public MainController(CacheManager cacheManager, MangaDexClient mangaDexClient, ILogger logger)
        {
            _cacheManager = cacheManager;
            _mangaDexClient = mangaDexClient;
            _logger = logger;
        }

        [HttpGet("data/{chapterId:guid}/{name}")]
        [ResponseCache(Duration = 1209600)]
        public async Task<IActionResult> FetchNormalImage(Guid chapterId, string name) => await FetchImage(chapterId, name, false, null);

        [HttpGet("data-saver/{chapterId:guid}/{name}")]
        [ResponseCache(Duration = 1209600)]
        public async Task<IActionResult> FetchDataSaverImage(Guid chapterId, string name) => await FetchImage(chapterId, name, true, null);
        
        [HttpGet("{token}/data/{chapterId:guid}/{name}")]
        [ResponseCache(Duration = 1209600)]
        public async Task<IActionResult> FetchTokenizedNormalImage(string token, Guid chapterId, string name) => await FetchImage(chapterId, name, false, token);

        [HttpGet("{token}/data-saver/{chapterId:guid}/{name}")]
        [ResponseCache(Duration = 1209600)]
        public async Task<IActionResult> FetchTokenizedDataSaverImage(string token, Guid chapterId, string name) => await FetchImage(chapterId, name, true, token); 
        
        private async Task<IActionResult> FetchImage(Guid chapterId, string name, bool dataSaver, string? token)
        {
            var url = $"/{(dataSaver ? "data-saver" : "data")}/{chapterId:N}/{name}";
            
            if (!IsValidReferrer())
            {
                _logger.Information($"Request for {url} rejected due to non-allowed referrer ${string.Join(',', Request.Headers["Referer"])}");

                return StatusCode(403);
            }

            if (token != null || _mangaDexClient.RemoteSettings.ForceTokens)
            {
                if (token == null)
                {
                    _logger.Information($"Request for {url} rejected for invalid token");

                    return StatusCode(403);
                }
                
                var decodedToken = token.DecodeToken();

                if (decodedToken.Length < 24)
                {
                    _logger.Information($"Request for {url} rejected for invalid token");

                    return StatusCode(403);
                }

                var nonce = new byte[24];
                Array.Copy(decodedToken, nonce, 24);

                var cipherText = new byte[decodedToken.Length - 24];
                Array.Copy(decodedToken, 24, cipherText, 0, decodedToken.Length - 24);

                Token? serializedToken;

                try
                {
                    decodedToken = SecretBox.Open(cipherText, nonce, _mangaDexClient.RemoteSettings.DecodedToken);
                    serializedToken = JsonSerializer.Deserialize<Token>(Encoding.UTF8.GetString(decodedToken), _mangaDexClient.JsonSerializerOptions);
                }
                catch
                {
                    _logger.Information($"Request for {url} rejected for invalid token");
                    
                    return StatusCode(403);
                }

                if (serializedToken == null)
                {
                    _logger.Information($"Request for {url} rejected for invalid token");

                    return StatusCode(403);
                }

                if (DateTime.UtcNow > serializedToken.ExpirationDate.UtcDateTime)
                {
                    _logger.Information($"Request for {url} rejected for expired token");

                    return StatusCode(410);
                }

                if (serializedToken.Hash != chapterId.ToString("N"))
                {
                    _logger.Information($"Request for {url} rejected for inapplicable token");

                    return StatusCode(403);
                }
            }
            
            var entry = _cacheManager.GetEntry(url.GetHashAsGuid());

            return entry == null ? await HandleCacheMiss(url) : HandleCacheHit(url, entry);
        }

        private async Task<IActionResult> HandleCacheMiss(string url)
        {
            _logger.Information($"Request for {url} missed cache");

            HttpResponseMessage response;

            try
            {
                response = await _mangaDexClient.HttpClient.GetAsync(_mangaDexClient.RemoteSettings.ImageServer + url);
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

            var contentType = response.Content.Headers.ContentType?.MediaType;
            var contentLength = response.Content.Headers.ContentLength;
            var lastModified = response.Content.Headers.LastModified;

            if (!contentType.IsImageMimeType())
            {
                _logger.Warning($"Upstream query for {url} returned bad mimetype {contentType}");

                return StatusCode(500);
            }
            
            _logger.Information($"Upstream query for {url} succeeded");

            var entry = new CacheEntry
            {
                Id = url.GetHashAsGuid(),
                ContentType = contentType!,
                LastModified = lastModified?.DateTime ?? DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow,
                Size = Convert.ToUInt64(contentLength),
                Content = await response.Content.ReadAsByteArrayAsync()
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
            Response.Headers.Add("X-Content-Type-Options", "nosniff");
            Response.Headers.Add("Last-Modified", cacheEntry.LastModified.ToString(CultureInfo.InvariantCulture));

            return File(cacheEntry.Content, cacheEntry.ContentType);
        }

        private bool IsValidReferrer()
        {
            string[] allowedReferrers = {"https://mangadex.org", "https://mangadex.network", string.Empty};

            return !Request.Headers.TryGetValue("Referer", out var referer) || referer.Any(str => allowedReferrers.Any(str.Contains));
        }
    }
}