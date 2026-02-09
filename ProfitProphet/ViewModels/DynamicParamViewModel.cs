using ProfitProphet.Indicators.Abstractions;
using System;

namespace ProfitProphet.ViewModels
{
    public class DynamicParamViewModel
    {
        public IndicatorParamDef Definition { get; }

        // Ez köti a TextBox-ot (Számok/Szöveg)
        public string StringValue { get; set; } = "";

        // Ez köti a CheckBox-ot (Logikai)
        public bool BoolValue { get; set; }

        // Segédtulajdonságok a XAML láthatósághoz
        public bool IsBoolean => Definition.Type == typeof(bool);
        public bool IsText => !IsBoolean; // Minden ami nem bool, az egyelőre szövegmező (int, double, string)

        public DynamicParamViewModel(IndicatorParamDef def, string currentValue)
        {
            Definition = def;

            if (IsBoolean)
            {
                bool.TryParse(currentValue, out var b);
                BoolValue = b;
            }
            else
            {
                StringValue = currentValue;
            }
        }

        // Visszaadja az értéket stringként (a Dictionary-be mentéshez)
        public string GetSerializedValue()
        {
            return IsBoolean ? BoolValue.ToString() : StringValue;
        }
    }
}