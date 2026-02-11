using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ProfitProphet.Views
{
    public static class WindowStyleHelper
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // ===== AKTUÁLIS TÉMA TÁROLÁSA =====
        public static ThemeMode CurrentTheme { get; private set; } = ThemeMode.Dark;

        public enum ThemeMode
        {
            Light,
            Dark
        }

        /// <summary>
        /// Dark title bar alkalmazása bármely Window-ra
        /// </summary>
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

        /// <summary>
        /// Téma váltás az EGÉSZ alkalmazásra
        /// </summary>
        public static void SetApplicationTheme(ThemeMode theme)
        {
            CurrentTheme = theme;

            // 1. Frissítjük az összes nyitott ablakot
            foreach (Window window in Application.Current.Windows)
            {
                SetTitleBarTheme(window, theme);
                ApplyThemeToWindow(window, theme);
            }

            // 2. Frissítjük az Application szintű erőforrásokat
            ApplyApplicationResources(theme);
        }

        /// <summary>
        /// Title bar színének beállítása
        /// </summary>
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

        /// <summary>
        /// Ablak háttérszínek és stílusok frissítése
        /// </summary>
        private static void ApplyThemeToWindow(Window window, ThemeMode theme)
        {
            if (theme == ThemeMode.Dark)
            {
                // Dark mode színek
                window.Background = new SolidColorBrush(Color.FromRgb(14, 17, 23));
            }
            else
            {
                // Light mode színek
                window.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            }

            window.InvalidateVisual();
        }

        /// <summary>
        /// Application szintű Resource Dictionary frissítése
        /// </summary>
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

                // TEXTBOX/COMBOBOX SPECIFIKUS - DARK
                resources["TextBoxBackground"] = new SolidColorBrush(Color.FromRgb(45, 45, 48));
                resources["TextBoxForeground"] = new SolidColorBrush(Colors.White);
            }
            else
            {
                // LIGHT MODE SZÍNEK
                resources["PrimaryBackground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                resources["SecondaryBackground"] = new SolidColorBrush(Color.FromRgb(243, 244, 246));
                resources["TertiaryBackground"] = new SolidColorBrush(Color.FromRgb(229, 231, 235));

                resources["PrimaryText"] = new SolidColorBrush(Color.FromRgb(17, 24, 39));
                resources["SecondaryText"] = new SolidColorBrush(Color.FromRgb(107, 114, 128));

                resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(209, 213, 219));
                resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(37, 99, 235));

                resources["ButtonHoverBackground"] = new SolidColorBrush(Color.FromRgb(243, 244, 246));
                resources["ListBoxSelectedBackground"] = new SolidColorBrush(Color.FromRgb(219, 234, 254));

                // TEXTBOX/COMBOBOX SPECIFIKUS - LIGHT (FONTOS!)
                resources["TextBoxBackground"] = new SolidColorBrush(Colors.White);
                resources["TextBoxForeground"] = new SolidColorBrush(Color.FromRgb(17, 24, 39)); // SÖTÉT SZÖVEG!
            }
        }


        /// <summary>
        /// Toggle (váltás) a két téma között
        /// </summary>
        public static void ToggleTheme()
        {
            var newTheme = CurrentTheme == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
            SetApplicationTheme(newTheme);
        }
    }
}
