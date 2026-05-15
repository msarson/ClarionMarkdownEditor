using System;
using System.Collections.Generic;

namespace ClarionMarkdownEditor.Services
{
    /// <summary>
    /// Converts user-supplied URLs into canonical raw URLs that point at a
    /// fetchable Markdown document. Pure — no I/O. The caller is responsible
    /// for attempting <see cref="NormalizedUrl.FallbackUrls"/> in order if
    /// the primary fetch fails.
    /// </summary>
    public static class UrlNormalizer
    {
        public class NormalizedUrl
        {
            public string PrimaryUrl { get; set; }
            public List<string> FallbackUrls { get; set; } = new List<string>();

            // Directory part of PrimaryUrl (ending in "/"), used to resolve
            // relative images and links in the fetched Markdown.
            public string BaseUrl { get; set; }
        }

        public static NormalizedUrl Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("URL is empty", nameof(input));

            var trimmed = input.Trim();

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("Only http(s) URLs are supported", nameof(input));
            }

            var host = uri.Host;
            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                host = host.Substring(4);

            if (host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
                return NormalizeGitHub(uri);

            return new NormalizedUrl
            {
                PrimaryUrl = uri.ToString(),
                BaseUrl = DeriveBaseUrl(uri.ToString())
            };
        }

        private static NormalizedUrl NormalizeGitHub(Uri uri)
        {
            var segs = uri.AbsolutePath.Trim('/').Split('/');

            if (segs.Length < 2 || string.IsNullOrEmpty(segs[0]) || string.IsNullOrEmpty(segs[1]))
                throw new ArgumentException("github.com URL is missing owner or repo", nameof(uri));

            var owner = segs[0];
            var repo = segs[1];
            if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                repo = repo.Substring(0, repo.Length - 4);

            // github.com/owner/repo/blob/<branch>/<path...>  →  raw URL
            if (segs.Length >= 5 && segs[2].Equals("blob", StringComparison.OrdinalIgnoreCase))
            {
                var branch = segs[3];
                var path = string.Join("/", segs, 4, segs.Length - 4);
                var raw = BuildRawUrl(owner, repo, branch, path);
                return new NormalizedUrl
                {
                    PrimaryUrl = raw,
                    BaseUrl = DeriveBaseUrl(raw)
                };
            }

            // github.com/owner/repo/tree/<branch>[/subdir...]  →  README.md in that dir
            if (segs.Length >= 4 && segs[2].Equals("tree", StringComparison.OrdinalIgnoreCase))
            {
                var branch = segs[3];
                var subdir = segs.Length > 4 ? string.Join("/", segs, 4, segs.Length - 4) + "/" : "";
                var raw = BuildRawUrl(owner, repo, branch, subdir + "README.md");
                return new NormalizedUrl
                {
                    PrimaryUrl = raw,
                    BaseUrl = DeriveBaseUrl(raw)
                };
            }

            // Bare repo URL — probe main, then master.
            var rawMain = BuildRawUrl(owner, repo, "main", "README.md");
            var rawMaster = BuildRawUrl(owner, repo, "master", "README.md");
            return new NormalizedUrl
            {
                PrimaryUrl = rawMain,
                FallbackUrls = new List<string> { rawMaster },
                BaseUrl = DeriveBaseUrl(rawMain)
            };
        }

        private static string BuildRawUrl(string owner, string repo, string branch, string path)
            => $"https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{path}";

        private static string DeriveBaseUrl(string url)
        {
            int q = url.IndexOf('?');
            if (q >= 0) url = url.Substring(0, q);
            int h = url.IndexOf('#');
            if (h >= 0) url = url.Substring(0, h);
            int slash = url.LastIndexOf('/');
            // 8 covers "https://" so we never truncate inside the scheme.
            return slash > 8 ? url.Substring(0, slash + 1) : url;
        }
    }
}
