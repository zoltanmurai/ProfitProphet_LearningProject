using OxyPlot;
using OxyPlot.Wpf;
using OxyPlot.Commands;
using System;
using System.Threading.Tasks;

namespace ProfitProphet.Services
{
    public class ChartController
    {
        private readonly PlotController _controller;
        private Func<Task>? _lazyLoader;

        public ChartController()
        {
            _controller = new PlotController();
            ConfigureDefaultBindings();
        }

        public PlotController GetController() => _controller;

        public void ConfigureLazyLoader(Func<Task> lazyLoadHandler)
        {
            _lazyLoader = lazyLoadHandler;
        }

        private void ConfigureDefaultBindings()
        {
            _controller.UnbindMouseWheel();
            _controller.BindMouseWheel(OxyMouseButton.None, PlotCommands.ZoomWheel);

            _controller.BindMouseDown(OxyMouseButton.Left, PlotCommands.PanAt);
            _controller.BindMouseDown(OxyMouseButton.Right, PlotCommands.ZoomRectangle);

            // Ctrl + bal klikk → Auto-fit
            _controller.BindMouseDown(OxyMouseButton.Left, OxyModifierKeys.Control,
                new DelegatePlotCommand<OxyMouseDownEventArgs>((view, args) =>
                {
                    view.Model?.ResetAllAxes();
                    view.InvalidatePlot(false);
                    args.Handled = true;
                }));

            // Középső gomb → Lazy load
            _controller.BindMouseDown(OxyMouseButton.Middle,
                new DelegatePlotCommand<OxyMouseDownEventArgs>((view, args) =>
                {
                    if (_lazyLoader != null)
                        _ = _lazyLoader.Invoke();
                    args.Handled = true;
                }));
        }
    }
}
