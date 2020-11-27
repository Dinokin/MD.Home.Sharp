﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using MD.Home.Sharp.Extensions;
using MD.Home.Sharp.Others;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.WebUtilities;
using Serilog;
using Sodium;

namespace MD.Home.Sharp.Filters
{
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    internal class TokenValidator : IActionFilter
    {
        private readonly ILogger _logger;
        private readonly JsonSerializerOptions _serializerOptions;

        public TokenValidator(ILogger logger, JsonSerializerOptions serializerOptions)
        {
            _logger = logger;
            _serializerOptions = serializerOptions;
        }

        [SuppressMessage("ReSharper", "InvertIf")]
        [SuppressMessage("ReSharper", "RedundantJumpStatement")]
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var path = context.HttpContext.Request.Path.Value.RemoveToken();

            if (!context.ActionArguments.TryGetValue("token", out var token))
                return;

            var tokenBytes = WebEncoders.Base64UrlDecode((string) token);

            switch (tokenBytes.Length)
            {
                case 0 when !Program.MangaDexClient.RemoteSettings.ForceTokens:
                    return;
                case < 24:
                    _logger.Information($"Request for {path} rejected for invalid token");

                    context.Result = new StatusCodeResult(403);
                
                    return;
            }

            Token? serializedToken;
            
            try
            {
                tokenBytes = SecretBox.Open(tokenBytes[24..], tokenBytes[..24], Program.MangaDexClient.RemoteSettings.TokenKey);
                serializedToken = JsonSerializer.Deserialize<Token>(Encoding.UTF8.GetString(tokenBytes), _serializerOptions);
            }
            catch
            {
                _logger.Information($"Request for {path} rejected for invalid token");

                context.Result = new StatusCodeResult(403);
                
                return;
            }

            if (serializedToken == null)
            {
                _logger.Information($"Request for {path} rejected for invalid token");

                context.Result = new StatusCodeResult(403);
                
                return;
            }

            if (DateTimeOffset.UtcNow > serializedToken.ExpirationDate)
            {
                _logger.Information($"Request for {path} rejected for expired token");

                context.Result = new StatusCodeResult(410);
                
                return;
            }
            
            if (context.ActionArguments.TryGetValue("chapterId", out var chapterId) && serializedToken.Hash != ((Guid) chapterId).ToString("N"))
            {
                _logger.Information($"Request for {path} rejected for inapplicable token");

                context.Result = new StatusCodeResult(410);
                
                return;
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}