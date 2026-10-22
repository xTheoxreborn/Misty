using System.Windows.Data;
using System.Windows.Markup;

namespace Misty.Services
{
    /// <summary>Usage XAML : Content="{loc:Tr Cle}"</summary>
    [MarkupExtensionReturnType(typeof(string))]
    public class TrExtension : MarkupExtension
    {
        public TrExtension() { }
        public TrExtension(string cle) => Cle = cle;

        [ConstructorArgument("cle")]
        public string Cle { get; set; } = "";

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var liaison = new Binding($"[{Cle}]") { Source = Loc.Instance, Mode = BindingMode.OneWay };
            return liaison.ProvideValue(serviceProvider);
        }
    }
}