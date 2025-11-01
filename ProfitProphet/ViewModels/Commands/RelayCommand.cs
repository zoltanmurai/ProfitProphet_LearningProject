using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProfitProphet.ViewModels.Commands
{
    public sealed class RelayCommand : ICommand
    {
        private readonly Func<object?, bool>? _can;
        private readonly Action<object?> _exec;

        public RelayCommand(Action<object?> exec, Func<object?, bool>? can = null)
        {
            _exec = exec ?? throw new ArgumentNullException(nameof(exec));
            _can = can;
        }

        public bool CanExecute(object? p) => _can?.Invoke(p) ?? true;
        public void Execute(object? p) => _exec(p);

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }
}


