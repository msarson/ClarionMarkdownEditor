using System.Drawing;
using System.Reflection;
using ICSharpCode.Core;

namespace ClarionMarkdownEditor
{
    public class RegisterIconsCommand : AbstractCommand
    {
        public override void Run()
        {
            using (var stream = Assembly.GetExecutingAssembly()
                       .GetManifestResourceStream("ClarionMarkdownEditor.Resources.MarkdownEditorIcon.png"))
            {
                if (stream != null)
                    ResourceService.RegisterNeutralImages(
                        new EmbeddedIconManager("ClarionMarkdownEditor.MarkdownEditorIcon", new Bitmap(stream)));
            }
        }
    }

    internal sealed class EmbeddedIconManager : System.Resources.ResourceManager
    {
        private readonly string _key;
        private readonly Bitmap _bitmap;
        public EmbeddedIconManager(string key, Bitmap bitmap)
            : base(key, Assembly.GetExecutingAssembly()) { _key = key; _bitmap = bitmap; }
        public override object GetObject(string name) => name == _key ? _bitmap : null;
        public override object GetObject(string name, System.Globalization.CultureInfo culture) => GetObject(name);
    }
}
