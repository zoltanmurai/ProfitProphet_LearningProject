using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Models.Strategies
{
    public class StrategyGroup : INotifyPropertyChanged
    {
        private string _name;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        // A csoporton belüli szabályok (ÉS kapcsolat)
        public ObservableCollection<StrategyRule> Rules { get; set; }

        public StrategyGroup()
        {
            Name = "Új Csoport";
            Rules = new ObservableCollection<StrategyRule>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
