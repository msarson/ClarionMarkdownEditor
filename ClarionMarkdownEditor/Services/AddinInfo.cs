using System;
using System.IO;
using System.Reflection;
using System.Xml;

namespace ClarionMarkdownEditor.Services
{
    /// <summary>
    /// Version and install location of the running addin.
    ///
    /// The version is read from the &lt;Identity version="..."/&gt; of the .addin manifest beside the
    /// DLL, because that is the version the IDE, Addin Finder and the ClarionAssistant installer all
    /// compare. The build stamps it from $(Version), so it also matches the assembly. Falls back to
    /// the assembly's informational version if the manifest can't be read.
    /// </summary>
    public static class AddinInfo
    {
        private const string ManifestFileName = "ClarionMarkdownEditor.addin";

        private static readonly Lazy<string> _version = new Lazy<string>(ReadVersion);

        public static string InstallFolder => Path.GetDirectoryName(typeof(AddinInfo).Assembly.Location);

        public static string Version => _version.Value;

        private static string ReadVersion()
        {
            try
            {
                var manifestPath = Path.Combine(InstallFolder, ManifestFileName);
                if (File.Exists(manifestPath))
                {
                    var doc = new XmlDocument();
                    doc.Load(manifestPath);
                    var version = doc.SelectSingleNode("/AddIn/Manifest/Identity/@version")?.Value;
                    if (!string.IsNullOrEmpty(version)) return version;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddinInfo: could not read manifest version: {ex.Message}");
            }

            var assembly = typeof(AddinInfo).Assembly;
            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return !string.IsNullOrEmpty(informational) ? informational : assembly.GetName().Version.ToString(3);
        }
    }
}
