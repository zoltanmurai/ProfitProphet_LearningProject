using OxyPlot;
using OxyPlot.Wpf;
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
            _controller.BindMouseWheel((OxyModifierKeys)OxyMouseButton.None, OxyPlot.PlotCommands.ZoomWheel);

            _controller.BindMouseDown(OxyMouseButton.Left, OxyPlot.PlotCommands.PanAt);
            _controller.BindMouseDown(OxyMouseButton.Right, OxyPlot.PlotCommands.ZoomRectangle);

            // Ctrl + bal klikk → Auto-fit
            _controller.BindMouseDown(OxyMouseButton.Left, OxyModifierKeys.Control,
                new DelegatePlotCommand<OxyMouseDownEventArgs>((view, controller, args) =>
                {
                    view.ActualModel?.ResetAllAxes();
                    view.InvalidatePlot(false);
                    args.Handled = true;
                }));

            // Középső gomb → Lazy load
            _controller.BindMouseDown(OxyMouseButton.Middle,
                new DelegatePlotCommand<OxyMouseDownEventArgs>((view, controller, args) =>
                {
                    if (_lazyLoader != null)
                        _ = _lazyLoader.Invoke();
                    args.Handled = true;
                }));
        }
    }
}
