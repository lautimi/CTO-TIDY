using Autodesk.AutoCAD.Runtime;

namespace Koovra.Cto.AutocadAddin
{
    public class AddinApplication : IExtensionApplication
    {
        public void Initialize()
        {
            System.AppDomain.CurrentDomain.AssemblyResolve += ResolveCore;
        }

        public void Terminate()
        {
        }

        private static System.Reflection.Assembly ResolveCore(object sender, System.ResolveEventArgs args)
        {
            if (!args.Name.StartsWith("CtoAutocadAddin.Core", System.StringComparison.OrdinalIgnoreCase))
                return null;
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            using (var stream = asm.GetManifestResourceStream("CtoAutocadAddin.Core.dll"))
            {
                if (stream == null) return null;
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                return System.Reflection.Assembly.Load(bytes);
            }
        }
    }
}
