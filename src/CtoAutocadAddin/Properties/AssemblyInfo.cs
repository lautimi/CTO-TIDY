using System.Reflection;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.Runtime;

[assembly: AssemblyTitle("CtoAutocadAddin")]
[assembly: AssemblyDescription("Add-In AutoCAD Map 3D 2020 para cálculo y despliegue de CTOs (FTTH).")]
[assembly: AssemblyCompany("Koovra")]
[assembly: AssemblyProduct("CtoAutocadAddin")]
[assembly: AssemblyCopyright("© Koovra")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: ComVisible(false)]
[assembly: Guid("a1b2c3d4-0001-4000-8000-000000000001")]

[assembly: ExtensionApplication(typeof(Koovra.Cto.AutocadAddin.AddinApplication))]
[assembly: CommandClass(typeof(Koovra.Cto.AutocadAddin.Commands.AsociarPostesCommand))]
[assembly: CommandClass(typeof(Koovra.Cto.AutocadAddin.Commands.PanelCommand))]
[assembly: CommandClass(typeof(Koovra.Cto.AutocadAddin.Commands.LeerComentariosCommand))]
[assembly: CommandClass(typeof(Koovra.Cto.AutocadAddin.Commands.SeleccionarPostesCommand))]
[assembly: CommandClass(typeof(Koovra.Cto.AutocadAddin.Commands.CalcularCtosCommand))]
[assembly: CommandClass(typeof(Koovra.Cto.AutocadAddin.Commands.DesplegarCtosCommand))]
[assembly: CommandClass(typeof(Koovra.Cto.AutocadAddin.Commands.RunAllCommand))]
[assembly: CommandClass(typeof(Koovra.Cto.AutocadAddin.Commands.ConfigCommand))]
[assembly: CommandClass(typeof(Koovra.Cto.AutocadAddin.Commands.InspeccionarPosteCommand))]
