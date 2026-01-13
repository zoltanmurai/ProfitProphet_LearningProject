using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ProfitProphet.Models.Strategies
{
    public enum TradeAmountType
    {
        AllCash,        // Minden elérhető pénzből (All-in)
        FixedCash,      // Fix dollár összeg (pl. 1000 USD / kötés)
        FixedShareCount, // Fix darabszám (pl. 10 db / kötés)
        PercentageOfEquity // A tőke %-a (pl. 10%)
    }

    public class StrategyProfile : INotifyPropertyChanged
    {
        public int Id { get; set; }

        private string _name;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Symbol { get; set; }

        // --- (NetBroker & Logika) ---

        // Lot méret típus
        public TradeAmountType AmountType { get; set; } = TradeAmountType.AllCash;

        // A méret értéke (pl. 1000 USD vagy 10 db)
        public double TradeAmount { get; set; } = 2000;

        // Jutalék % (0.45%)
        public double CommissionPercent { get; set; } = 0.45;

        // Minimum jutalék (7 USD)
        public double MinCommission { get; set; } = 7.0;

        // Rávásárlás engedélyezése (Pyramiding)
        public bool AllowPyramiding { get; set; } = false;

        // Csak pluszban adhat el?
        public bool OnlySellInProfit { get; set; } = false;

        // -------------------------------------------

        public List<StrategyGroup> EntryGroups { get; set; } = new List<StrategyGroup>();
        public List<StrategyGroup> ExitGroups { get; set; } = new List<StrategyGroup>();

        public double BestScore { get; set; }
        public double WinRate { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
