using ProfitProphet.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ProfitProphet.ViewModels.Dialogs
{
    public static class IndicatorSettingsDialog
    {
        public static bool Show(ref IndicatorConfigDto cfg)
        {
            // nagyon egyszerű Window: 2-3 mező max, típustól függően
            var win = new Window
            {
                Title = $"{cfg.Type} beállítások",
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = BuildContent(cfg, out var onOk),
            };
            win.Owner = Application.Current.MainWindow;

            var result = win.ShowDialog() == true;
            if (result)
            {
                onOk(); // mentjük vissza a mezők értékeit a cfg.Parameters-be
                cfg.IsEnabled = true;
            }
            return result;
        }

        private static FrameworkElement BuildContent(IndicatorConfigDto cfg, out Action onOk)
        {
            var grid = new Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var panel = new StackPanel { Orientation = Orientation.Vertical, };
            Grid.SetRow(panel, 0);
            grid.Children.Add(panel);

            // dinamikus mezők
            var boxes = new Dictionary<string, TextBox>();

            void AddRow(string label, string key, string defaultVal)
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock { Text = label, Width = 120, VerticalAlignment = VerticalAlignment.Center });
                var tb = new TextBox { Width = 100, Text = cfg.Parameters.TryGetValue(key, out var v) ? v : defaultVal };
                boxes[key] = tb;
                sp.Children.Add(tb);
                panel.Children.Add(sp);
            }

            switch (cfg.Type)
            {
                case IndicatorType.SMA:
                    AddRow("Periódus", "period", "20");
                    break;
                case IndicatorType.EMA:
                    AddRow("Periódus", "period", "20");
                    break;

                case IndicatorType.Stochastic:
                    AddRow("%K periódus", "kPeriod", "14");
                    AddRow("%D periódus", "dPeriod", "3");
                    AddRow("Kimenet (%D? true/false)", "outputD", cfg.Parameters.GetValueOrDefault("outputD", "false"));
                    break;

                case IndicatorType.CMF:
                    AddRow("Periódus", "period", "20");
                    AddRow("Signal MA (0=Ki)", "maPeriod", "10");
                    break;
            }

            // OK/Mégse
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80 };
            var cancel = new Button { Content = "Mégse", IsCancel = true, MinWidth = 80 };
            buttons.Children.Add(ok); buttons.Children.Add(cancel);
            Grid.SetRow(buttons, 1);
            grid.Children.Add(buttons);

            onOk = () =>
            {
                foreach (var kv in boxes)
                    cfg.Parameters[kv.Key] = kv.Value.Text?.Trim() ?? "";
            };

            ok.Click += (_, __) => { var w = Window.GetWindow(grid); w.DialogResult = true; w.Close(); };
            cancel.Click += (_, __) => { var w = Window.GetWindow(grid); w.DialogResult = false; w.Close(); };

            return grid;
        }
    }
}
