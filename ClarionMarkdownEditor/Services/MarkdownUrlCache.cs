using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClarionMarkdownEditor.Services
{
    public class CachedEntry
    {
        public string Url { get; set; }
        public string ETag { get; set; }
        public string LastModified { get; set; }
        public DateTime StoredUtc { get; set; }
        public string Content { get; set; }
    }

    /// <summary>
    /// On-disk cache for fetched Markdown URLs. One entry = a tiny key=value
    /// metadata file plus a sibling body file containing the raw Markdown.
    /// Orchestrates conditional GETs (ETag / If-Modified-Since) against
    /// <see cref="MarkdownFetcher"/> and falls back to cached content when
    /// the network is unavailable.
    /// </summary>
    public static class MarkdownUrlCache
    {
        private static readonly string _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClarionMarkdownEditor", "cache");

        public static CachedEntry TryLoad(string url)
        {
            try
            {
                var key = HashKey(url);
                var metaPath = Path.Combine(_cacheDir, key + ".meta");
                var bodyPath = Path.Combine(_cacheDir, key + ".body");
                if (!File.Exists(metaPath) || !File.Exists(bodyPath)) return null;

                var entry = new CachedEntry { Content = File.ReadAllText(bodyPath, Encoding.UTF8) };
                foreach (var line in File.ReadAllLines(metaPath, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    var k = line.Substring(0, eq);
                    var v = line.Substring(eq + 1);
                    switch (k)
                    {
                        case "url":           entry.Url = v; break;
                        case "etag":          entry.ETag = v; break;
                        case "last_modified": entry.LastModified = v; break;
                        case "stored":
                            if (DateTime.TryParse(v, null,
                                    System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                                entry.StoredUtc = dt;
                            break;
                    }
                }
                return entry;
            }
            catch
            {
                return null;
            }
        }

        public static void Save(string url, FetchResult result)
        {
            if (result == null || !result.Success || result.Content == null) return;

            try
            {
                if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);

                var key = HashKey(url);
                var metaPath = Path.Combine(_cacheDir, key + ".meta");
                var bodyPath = Path.Combine(_cacheDir, key + ".body");

                File.WriteAllText(bodyPath, result.Content, new UTF8Encoding(false));

                var sb = new StringBuilder();
                sb.AppendLine("url=" + url);
                if (!string.IsNullOrEmpty(result.ETag))         sb.AppendLine("etag=" + result.ETag);
                if (!string.IsNullOrEmpty(result.LastModified)) sb.AppendLine("last_modified=" + result.LastModified);
                sb.AppendLine("stored=" + DateTime.UtcNow.ToString("o"));
                File.WriteAllText(metaPath, sb.ToString(), new UTF8Encoding(false));
            }
            catch
            {
                // Cache is best-effort. Swallow.
            }
        }

        /// <summary>
        /// Fetches the resolved URL via the cache: cache miss → live fetch;
        /// cache hit → conditional GET with the stored ETag / Last-Modified;
        /// transport failure → serve cached body with <see cref="FetchResult.IsStale"/>
        /// set, so the UI can surface a "stale" badge.
        /// </summary>
        public static async Task<FetchResult> GetOrFetchAsync(
            UrlNormalizer.NormalizedUrl normalized,
            FetchOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (normalized == null) throw new ArgumentNullException(nameof(normalized));
            options = options ?? new FetchOptions();

            var existing = TryLoad(normalized.PrimaryUrl);
            if (existing != null)
            {
                options.IfNoneMatch = existing.ETag;
                options.IfModifiedSince = existing.LastModified;
            }

            var result = await MarkdownFetcher.FetchAsync(normalized, options, cancellationToken)
                                              .ConfigureAwait(false);

            if (result.NotModified && existing != null)
            {
                result.Content = existing.Content;
                result.Success = true;
                return result;
            }

            if (result.Success)
            {
                Save(result.ResolvedUrl ?? normalized.PrimaryUrl, result);
                return result;
            }

            // Live fetch failed — fall back to cached body if we have one.
            if (existing != null)
            {
                result.Content = existing.Content;
                result.Success = true;
                result.IsStale = true;
            }
            return result;
        }

        public static void Clear()
        {
            try
            {
                if (Directory.Exists(_cacheDir))
                    Directory.Delete(_cacheDir, recursive: true);
            }
            catch { }
        }

        private static string HashKey(string url)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(url ?? ""));
                var sb = new StringBuilder(16);
                for (int i = 0; i < 8; i++) sb.AppendFormat("{0:x2}", bytes[i]);
                return sb.ToString();
            }
        }
    }
}
