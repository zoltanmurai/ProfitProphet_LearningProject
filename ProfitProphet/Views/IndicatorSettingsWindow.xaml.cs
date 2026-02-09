using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ProfitProphet.DTOs;
using ProfitProphet.Indicators.Abstractions;
using ProfitProphet.ViewModels;

namespace ProfitProphet.Views
{
    public partial class IndicatorSettingsWindow : Window
    {
        private readonly IndicatorConfigDto _config;
        private readonly List<DynamicParamViewModel> _viewModels = new();

        public IndicatorSettingsWindow(IndicatorConfigDto config, IIndicator indicatorDefinition)
        {
            InitializeComponent();
            _config = config;

            TitleText.Text = $"{indicatorDefinition.DisplayName} Beállítások";

            // Végigmegyünk az indikátor által kért paramétereken (pl. "MaPeriod")
            foreach (var paramDef in indicatorDefinition.Params)
            {
                string currentValue;

                // --- JAVÍTÁS: OKOS KERESÉS (Case-Insensitive) ---
                // Megkeressük, van-e ilyen kulcs a mentett adatok között, 
                // függetlenül a kis/nagybetűtől (pl. megtalálja a "maPeriod"-ot a "MaPeriod" helyett)

                var existingKey = _config.Parameters.Keys
                    .FirstOrDefault(k => k.Equals(paramDef.Name, StringComparison.OrdinalIgnoreCase));

                if (existingKey != null && _config.Parameters.TryGetValue(existingKey, out var savedVal))
                {
                    // Ha megvan (akár kisbetűvel), használjuk azt
                    currentValue = savedVal;
                }
                else
                {
                    // Ha nincs, akkor az alapértelmezett érték
                    currentValue = paramDef.DefaultValue?.ToString() ?? "";
                }
                // ------------------------------------------------

                _viewModels.Add(new DynamicParamViewModel(paramDef, currentValue));
            }

            ParamsList.ItemsSource = _viewModels;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            foreach (var vm in _viewModels)
            {
                // Mentéskor már a helyes, új (Nagybetűs) formátumban mentjük el.
                // Így legközelebb már nem lesz gond.
                _config.Parameters[vm.Definition.Name] = vm.GetSerializedValue();
            }

            DialogResult = true;
            Close();
        }
    }
}