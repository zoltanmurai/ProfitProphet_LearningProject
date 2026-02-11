using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ProfitProphet.Views
{
    public enum ThemeSelection
    {
        System, // Kövesse a Windowst
        Light,  // Kényszerített Világos
        Dark    // Kényszerített Sötét
    }
    public static class WindowStyleHelper
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // Kezdetben nem égetjük be a Dark-ot, hanem majd a Detect állítja be
        public static ThemeMode CurrentTheme { get; private set; }
        //public static ThemeSelection CurrentSelection { get; private set; }
        public static ThemeSelection CurrentSelection { get; private set; } = ThemeSelection.System;

        public enum ThemeMode
        {
            Light,
            Dark
        }

        /// <summary>
        /// Ez a metódus megmondja, épp milyen módban van a Windows
        /// </summary>
        public static ThemeMode DetectSystemTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        var val = key.GetValue("AppsUseLightTheme");
                        if (val is int i && i == 1)
                        {
                            return ThemeMode.Light;
                        }
                    }
                }
            }
            catch
            {
                // Ha hiba van (pl. régi Windows), maradjon a biztonságos Light mód prezentációhoz
                return ThemeMode.Light;
            }

            return ThemeMode.Dark;
        }

        public static void ApplyUserSelection(ThemeSelection selection)
        {
            CurrentSelection = selection;
            ThemeMode modeToApply;

            switch (selection)
            {
                case ThemeSelection.Light:
                    modeToApply = ThemeMode.Light;
                    break;
                case ThemeSelection.Dark:
                    modeToApply = ThemeMode.Dark;
                    break;
                case ThemeSelection.System:
                default:
                    modeToApply = DetectSystemTheme(); // Megnézi a Registry-t
                    break;
            }

            SetApplicationTheme(modeToApply);
        }

        /// <summary>
        /// Figyeli a Windows téma váltását futás közben
        /// </summary>
        public static void StartListeningToSystemChanges()
        {
            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category == UserPreferenceCategory.General)
                {
                    var newTheme = DetectSystemTheme();
                    if (newTheme != CurrentTheme)
                    {
                        // Dispatcher kell, mert a SystemEvents más szálon jöhet
                        Application.Current.Dispatcher.Invoke(() => SetApplicationTheme(newTheme));
                    }
                }
            };
        }

        public static void ApplyDarkTitleBar(this Window window)
        {
            if (window == null) return;

            if (!window.IsLoaded)
            {
                window.SourceInitialized += (s, e) => SetTitleBarTheme(window, CurrentTheme);
            }
            else
            {
                SetTitleBarTheme(window, CurrentTheme);
            }
        }

        public static void SetApplicationTheme(ThemeMode theme)
        {
            CurrentTheme = theme;

            foreach (Window window in Application.Current.Windows)
            {
                SetTitleBarTheme(window, theme);
                ApplyThemeToWindow(window, theme);
            }

            ApplyApplicationResources(theme);
        }

        private static void SetTitleBarTheme(Window window, ThemeMode theme)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            if (Environment.OSVersion.Version.Build >= 19041)
            {
                int useImmersiveDarkMode = theme == ThemeMode.Dark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useImmersiveDarkMode, sizeof(int));
            }
        }

        private static void ApplyThemeToWindow(Window window, ThemeMode theme)
        {
            if (theme == ThemeMode.Dark)
            {
                window.Background = new SolidColorBrush(Color.FromRgb(14, 17, 23)); // PrimaryBackground Dark
            }
            else
            {
                window.Background = new SolidColorBrush(Colors.White); // PrimaryBackground Light
            }
            window.InvalidateVisual();
        }

        private static void ApplyApplicationResources(ThemeMode theme)
        {
            var resources = Application.Current.Resources;

            if (theme == ThemeMode.Dark)
            {
                // DARK MODE SZÍNEK
                resources["PrimaryBackground"] = new SolidColorBrush(Color.FromRgb(14, 17, 23));
                resources["SecondaryBackground"] = new SolidColorBrush(Color.FromRgb(22, 27, 34));
                resources["TertiaryBackground"] = new SolidColorBrush(Color.FromRgb(31, 41, 55));

                resources["PrimaryText"] = new SolidColorBrush(Colors.White);
                resources["SecondaryText"] = new SolidColorBrush(Color.FromRgb(165, 177, 194));

                resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(44, 47, 51));
                resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(37, 99, 235));

                resources["ButtonHoverBackground"] = new SolidColorBrush(Color.FromRgb(31, 41, 55));
                resources["ListBoxSelectedBackground"] = new SolidColorBrush(Color.FromRgb(37, 99, 235));

                resources["TextBoxBackground"] = new SolidColorBrush(Color.FromRgb(45, 45, 48));
                resources["TextBoxForeground"] = new SolidColorBrush(Colors.White);

                resources["ConsoleForeground"] = new SolidColorBrush(Color.FromRgb(255, 215, 0)); // #FFD700

                resources["StrategyBuyBrush"] = new SolidColorBrush(Colors.LimeGreen);
                resources["StrategySellBrush"] = new SolidColorBrush(Colors.Red); // Vagy #FF5555

                resources["StrategyOrBrush"] = new SolidColorBrush(Color.FromRgb(255, 215, 0)); // #FFD700
            }
            else
            {
                // LIGHT MODE SZÍNEK (PREZENTÁCIÓHOZ EZ KELL)
                resources["PrimaryBackground"] = new SolidColorBrush(Colors.White);
                resources["SecondaryBackground"] = new SolidColorBrush(Color.FromRgb(243, 244, 246));
                resources["TertiaryBackground"] = new SolidColorBrush(Color.FromRgb(229, 231, 235));

                resources["PrimaryText"] = new SolidColorBrush(Color.FromRgb(17, 24, 39));
                resources["SecondaryText"] = new SolidColorBrush(Color.FromRgb(107, 114, 128));

                resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(209, 213, 219));
                resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(37, 99, 235));

                resources["ButtonHoverBackground"] = new SolidColorBrush(Color.FromRgb(243, 244, 246));
                resources["ListBoxSelectedBackground"] = new SolidColorBrush(Color.FromRgb(219, 234, 254));

                resources["TextBoxBackground"] = new SolidColorBrush(Colors.White);
                resources["TextBoxForeground"] = new SolidColorBrush(Color.FromRgb(17, 24, 39));

                resources["ConsoleForeground"] = new SolidColorBrush(Color.FromRgb(184, 134, 11)); // DarkGoldenrod

                resources["StrategyBuyBrush"] = new SolidColorBrush(Color.FromRgb(0, 128, 0)); // Green
                resources["StrategySellBrush"] = new SolidColorBrush(Color.FromRgb(220, 20, 60)); // Crimson

                resources["StrategyOrBrush"] = new SolidColorBrush(Color.FromRgb(184, 134, 11)); // DarkGoldenrod
            }
        }

        public static void ToggleTheme()
        {
            var newTheme = CurrentTheme == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
            SetApplicationTheme(newTheme);
        }
    }
}