using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClarionMarkdownEditor.Services
{
    public class FetchResult
    {
        public string ResolvedUrl { get; set; }
        public string Content { get; set; }
        public string ETag { get; set; }
        public string LastModified { get; set; }
        public string ContentType { get; set; }
        public bool NotModified { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int StatusCode { get; set; }
    }

    public class FetchOptions
    {
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
        public string IfNoneMatch { get; set; }
        public string IfModifiedSince { get; set; }
    }

    /// <summary>
    /// Fetches Markdown content over HTTP(S). Handles fallback URLs (e.g. main → master),
    /// conditional GETs (ETag / If-Modified-Since), and never throws on transport errors —
    /// callers inspect <see cref="FetchResult.Success"/> / <see cref="FetchResult.ErrorMessage"/>.
    /// </summary>
    public static class MarkdownFetcher
    {
        private static readonly Lazy<HttpClient> _client = new Lazy<HttpClient>(CreateClient);

        public static async Task<FetchResult> FetchAsync(
            UrlNormalizer.NormalizedUrl normalized,
            FetchOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (normalized == null) throw new ArgumentNullException(nameof(normalized));
            options = options ?? new FetchOptions();

            var urls = new List<string> { normalized.PrimaryUrl };
            if (normalized.FallbackUrls != null) urls.AddRange(normalized.FallbackUrls);

            FetchResult last = null;
            foreach (var url in urls)
            {
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(options.Timeout);
                    last = await FetchSingleAsync(url, options, cts.Token).ConfigureAwait(false);
                }

                if (last.Success || last.NotModified) return last;

                // Roll on past 404s to try the next fallback; bail on anything else.
                if (last.StatusCode != 404) return last;
            }
            return last;
        }

        private static async Task<FetchResult> FetchSingleAsync(string url, FetchOptions options, CancellationToken ct)
        {
            var result = new FetchResult { ResolvedUrl = url };
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrEmpty(options.IfNoneMatch))
                    req.Headers.TryAddWithoutValidation("If-None-Match", options.IfNoneMatch);
                if (!string.IsNullOrEmpty(options.IfModifiedSince))
                    req.Headers.TryAddWithoutValidation("If-Modified-Since", options.IfModifiedSince);

                using (var resp = await _client.Value
                    .SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false))
                {
                    result.StatusCode = (int)resp.StatusCode;

                    if (resp.StatusCode == HttpStatusCode.NotModified)
                    {
                        result.NotModified = true;
                        return result;
                    }

                    if (!resp.IsSuccessStatusCode)
                    {
                        result.ErrorMessage = $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}";
                        return result;
                    }

                    result.ETag = resp.Headers.ETag?.Tag;
                    result.LastModified = resp.Content.Headers.LastModified?.ToString("R");
                    result.ContentType = resp.Content.Headers.ContentType?.MediaType;

                    // raw.githubusercontent.com serves files as application/octet-stream
                    // with no charset hint. Read bytes, decode as UTF-8, strip BOM.
                    var bytes = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    result.Content = StripBom(Encoding.UTF8.GetString(bytes));
                    result.Success = true;
                    return result;
                }
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                result.ErrorMessage = "Request timed out";
                return result;
            }
            catch (OperationCanceledException)
            {
                result.ErrorMessage = "Cancelled";
                return result;
            }
            catch (HttpRequestException ex)
            {
                result.ErrorMessage = ex.InnerException?.Message ?? ex.Message;
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private static string StripBom(string s)
            => !string.IsNullOrEmpty(s) && s[0] == '﻿' ? s.Substring(1) : s;

        private static HttpClient CreateClient()
        {
            // .NET 4.8 defaults to system TLS, but older OS configurations may
            // still need TLS 1.2 nudged on for raw.githubusercontent.com.
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; }
            catch { }

            var handler = new HttpClientHandler { AllowAutoRedirect = true };
            var client = new HttpClient(handler)
            {
                // Per-request timeout is managed via linked CTS in FetchAsync.
                Timeout = Timeout.InfiniteTimeSpan
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "ClarionMarkdownEditor/1.0.2 (+https://github.com/msarson/ClarionMarkdownEditor)");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/markdown");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/plain;q=0.9");
            client.DefaultRequestHeaders.Accept.ParseAdd("*/*;q=0.1");
            return client;
        }
    }
}
