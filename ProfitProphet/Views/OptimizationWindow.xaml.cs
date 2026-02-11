using ProfitProphet.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ProfitProphet.Views
{
    /// <summary>
    /// Interaction logic for OptimizationWindow.xaml
    /// </summary>
    public partial class OptimizationWindow : Window
    {
        public OptimizationWindow()
        {
            InitializeComponent();
            this.ApplyDarkTitleBar();
        }
        // Akkor hívódik meg, amikor az ablak Context-je (a ViewModel) megváltozik
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.Property.Name == "DataContext" && DataContext is OptimizationViewModel vm)
            {
                // Feliratkozunk a bezárás eseményre (MVVM barát módon)
                vm.OnRequestClose += (success) =>
                {
                    this.DialogResult = success;
                    this.Close();
                };
            }
        }
    }
}
