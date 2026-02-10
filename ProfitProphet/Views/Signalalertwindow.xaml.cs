using System;
using System.Windows;
using System.Windows.Threading;

namespace ProfitProphet.Views
{
    public partial class SignalAlertWindow : Window
    {
        private DispatcherTimer _autoCloseTimer;

        public SignalAlertWindow(string symbol, string signalType)
        {
            InitializeComponent();

            // Szövegek beállítása
            SymbolText.Text = symbol;
            SignalText.Text = $"{signalType.ToUpper()} signal detected!";

            // Auto-close timer beállítása (10 másodperc után automatikusan bezáródik)
            _autoCloseTimer = new DispatcherTimer();
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(10);
            _autoCloseTimer.Tick += AutoCloseTimer_Tick;
            _autoCloseTimer.Start();
        }

        private void AutoCloseTimer_Tick(object sender, EventArgs e)
        {
            _autoCloseTimer.Stop();
            Close();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _autoCloseTimer?.Stop();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _autoCloseTimer?.Stop();
            base.OnClosed(e);
        }
    }
}