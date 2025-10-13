using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using ProfitProphet.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace ProfitProphet.Services
{
    /// <summary>
    /// Felelős a chart (PlotModel) felépítéséért és interaktív viselkedéséért:
    /// - gapmentes X-tengely (CategoryAxis)
    /// - automatikus Y-skálázás (aktuális nézet alapján)
    /// - zoom/pan támogatás
    /// - lazy load (régi adatok betöltése, ha visszagörgetünk)
    /// </summary>
    public class ChartBuilder
    {
        private CategoryAxis _xAxis;
        private LinearAxis _yAxis;
        private CandleStickSeries _series;

        private DateTime _earliestLoaded;
        private bool _isLoadingOlder;

        private List<CandleData> _candles;  // referencia a teljes sorozatra

        private Func<DateTime, DateTime, Task<List<CandleData>>> _lazyLoader;

        public PlotModel Model { get; private set; }

        public void ConfigureLazyLoader(Func<DateTime, DateTime, Task<List<CandleData>>> loader)
        {
            _lazyLoader = loader;
        }

        //public PlotModel BuildInteractiveChart(List<CandleData> candles, string symbol, string interval)
        //{
        //    if (candles == null || candles.Count == 0)
        //        throw new ArgumentException("A candle lista üres.");

        //    candles = candles.OrderBy(c => c.Timestamp).ToList();
        //    _earliestLoaded = candles.First().Timestamp;
        //    _isLoadingOlder = false;

        //    Model = new PlotModel
        //    {
        //        Title = $"{symbol} ({interval})",
        //        TextColor = OxyColors.White,
        //        Background = OxyColor.FromRgb(22, 27, 34),
        //        PlotAreaBackground = OxyColor.FromRgb(24, 28, 34),
        //        PlotAreaBorderThickness = new OxyThickness(0),
        //        PlotAreaBorderColor = OxyColor.FromRgb(40, 40, 40)
        //    };

        //    //_xAxis = new CategoryAxis
        //    //{
        //    //    Position = AxisPosition.Bottom,
        //    //    TextColor = OxyColors.White,
        //    //    MajorGridlineStyle = LineStyle.Solid,
        //    //    MinorGridlineStyle = LineStyle.Dot,
        //    //    GapWidth = 0,
        //    //    IntervalLength = 80,
        //    //    IsZoomEnabled = true,
        //    //    IsPanEnabled = true
        //    //};

        //    //// Optimalizált címkézés: max 12 címke, csak az évszám az év elején, egyébként csak a hónap rövidítve
        //    //int candleCount = candles.Count;
        //    //int maxLabels = 12; // ennyi címke fér el (kb. havonta 1, ha 1y adatod van)
        //    //int step = Math.Max(1, candleCount / maxLabels);

        //    //for (int i = 0; i < candleCount; i++)
        //    //{
        //    //    if (i % step == 0)
        //    //    {
        //    //        var date = candles[i].Timestamp;
        //    //        // csak az év egyszer, a hónap rövid formában
        //    //        string label = (i == 0 || date.Month == 1) ? date.ToString("yyyy MMM") : date.ToString("MM.dd");
        //    //        _xAxis.Labels.Add(label);
        //    //    }
        //    //    else
        //    //    {
        //    //        _xAxis.Labels.Add(string.Empty);
        //    //    }
        //    //}


        //    //Model.Axes.Add(_xAxis);

        //    //foreach (var c in candles)
        //    //    _xAxis.Labels.Add(c.Timestamp.ToString("yyyy-MM-dd"));
        //    //Model.Axes.Add(_xAxis);





        //    _yAxis = new LinearAxis
        //    {
        //        Position = AxisPosition.Left,
        //        TextColor = OxyColors.White,
        //        MajorGridlineStyle = LineStyle.Solid,
        //        MinorGridlineStyle = LineStyle.Dot,
        //        AxislineColor = OxyColors.Gray,
        //        Title = "Price",
        //        IsZoomEnabled = true,
        //        IsPanEnabled = true
        //    };
        //    Model.Axes.Add(_yAxis);

        //    _series = new CandleStickSeries
        //    {
        //        IncreasingColor = OxyColor.FromRgb(34, 197, 94),
        //        DecreasingColor = OxyColor.FromRgb(239, 68, 68),
        //        CandleWidth = CalculateCandleWidth(interval),
        //        TrackerFormatString = "{Category}\nO: {4:0.###}\nH: {1:0.###}\nL: {2:0.###}\nC: {3:0.###}",
        //        YAxisKey = _yAxis.Key
        //    };

        //    for (int i = 0; i < candles.Count; i++)
        //    {
        //        var c = candles[i];
        //        _series.Items.Add(new HighLowItem(i, c.High, c.Low, c.Open, c.Close));
        //    }

        //    Model.Series.Add(_series);

        //    // Események
        //    _xAxis.AxisChanged += async (_, __) =>
        //    {
        //        await MaybeLazyLoadOlderAsync();
        //        AutoFitYToVisible();
        //        Model.InvalidatePlot(false);
        //    };

        //    Model.ResetAllAxes();
        //    AutoFitYToVisible();
        //    Model.InvalidatePlot(true);

        //    return Model;
        //}

        private void AutoFitYToVisible()
        {
            if (_series?.Items == null || _series.Items.Count == 0)
                return;

            int start = Math.Max(0, (int)Math.Floor(_xAxis.ActualMinimum));
            int end = Math.Min(_series.Items.Count - 1, (int)Math.Ceiling(_xAxis.ActualMaximum));

            if (start > end) { start = 0; end = _series.Items.Count - 1; }

            double min = double.MaxValue;
            double max = double.MinValue;

            for (int i = start; i <= end; i++)
            {
                var item = _series.Items[i];
                if (item.Low < min) min = item.Low;
                if (item.High > max) max = item.High;
            }

            if (max > min && max - min > 0)
            {
                var pad = (max - min) * 0.02;
                _yAxis.Zoom(min - pad, max + pad);
            }
        }

        //private async Task MaybeLazyLoadOlderAsync()
        //{
        //    if (_lazyLoader == null) return;
        //    if (_isLoadingOlder) return;
        //    if (_xAxis.ActualMinimum >= 5) return;

        //    _isLoadingOlder = true;
        //    try
        //    {
        //        var viewMin = _xAxis.ActualMinimum;
        //        var viewMax = _xAxis.ActualMaximum;

        //        var olderEnd = _earliestLoaded;
        //        var olderStart = _earliestLoaded.AddDays(-90);

        //        var olderData = await _lazyLoader(olderStart, olderEnd);
        //        if (olderData == null || olderData.Count == 0)
        //            return;

        //        olderData = olderData.OrderBy(c => c.Timestamp).ToList();
        //        _earliestLoaded = olderData.First().Timestamp;

        //        int shift = olderData.Count;

        //        foreach (var item in _series.Items)
        //            item.X += shift;

        //        for (int i = 0; i < olderData.Count; i++)
        //        {
        //            var c = olderData[i];
        //            _series.Items.Insert(i, new HighLowItem(i, c.High, c.Low, c.Open, c.Close));
        //            _xAxis.Labels.Insert(i, c.Timestamp.ToString("yyyy-MM-dd"));
        //        }

        //        _xAxis.Zoom(viewMin + shift, viewMax + shift);
        //        AutoFitYToVisible();
        //        Model.InvalidatePlot(true);
        //    }
        //    finally
        //    {
        //        _isLoadingOlder = false;
        //    }

        //    //_xAxis.Zoom(viewMin + shift, viewMax + shift);
        //    UpdateXAxisLabels();
        //    AutoFitYToVisible();
        //    Model.InvalidatePlot(true);
        //}

        public static double CalculateCandleWidth(string interval)
        {
            if (string.IsNullOrWhiteSpace(interval)) return 0.5;
            interval = interval.ToLower();
            if (interval.EndsWith("m")) return 0.5;
            if (interval.EndsWith("h")) return 0.7;
            if (interval.EndsWith("d")) return 0.9;
            return 0.6;
        }

        // --- Segédtípus ---
        public class CandleData
        {
            public DateTime Timestamp { get; set; }
            public double Open { get; set; }
            public double High { get; set; }
            public double Low { get; set; }
            public double Close { get; set; }
        }

        public PlotModel BuildInteractiveChart(List<CandleData> candles, string symbol, string interval)
        {
            if (candles == null || candles.Count == 0)
                throw new ArgumentException("A candle lista üres.");

            _candles = candles.OrderBy(c => c.Timestamp).ToList();
            _earliestLoaded = _candles.First().Timestamp;
            _isLoadingOlder = false;

            Model = new PlotModel
            {
                Title = $"{symbol} ({interval})",
                TextColor = OxyColors.White,
                Background = OxyColor.FromRgb(22, 27, 34),
                PlotAreaBackground = OxyColor.FromRgb(24, 28, 34),
                PlotAreaBorderThickness = new OxyThickness(0),
                PlotAreaBorderColor = OxyColor.FromRgb(40, 40, 40)
            };

            _xAxis = new CategoryAxis
            {
                Position = AxisPosition.Bottom,
                TextColor = OxyColors.White,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                GapWidth = 0,
                IntervalLength = 80,
                IsZoomEnabled = true,
                IsPanEnabled = true
            };
            Model.Axes.Add(_xAxis);

            _yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                TextColor = OxyColors.White,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                AxislineColor = OxyColors.Gray,
                Title = "Price",
                IsZoomEnabled = true,
                IsPanEnabled = true
            };
            Model.Axes.Add(_yAxis);

            _series = new CandleStickSeries
            {
                IncreasingColor = OxyColor.FromRgb(34, 197, 94),
                DecreasingColor = OxyColor.FromRgb(239, 68, 68),
                CandleWidth = CalculateCandleWidth(interval),
                TrackerFormatString = "{Category}\nO: {4:0.###}\nH: {1:0.###}\nL: {2:0.###}\nC: {3:0.###}",
                YAxisKey = _yAxis.Key
            };

            // CSAK EGYSZER adjuk hozzá az adatokat
            for (int i = 0; i < _candles.Count; i++)
            {
                var c = _candles[i];
                _series.Items.Add(new HighLowItem(i, c.High, c.Low, c.Open, c.Close));
            }

            Model.Series.Add(_series);

            // Inicial view: utolsó 120 gyertya, + padding jobbra
            Model.ResetAllAxes();
            int total = _candles.Count;
            if (total > 0)
            {
                int visibleCount = Math.Min(120, total);
                int startIndex = total - visibleCount;
                int endIndex = total - 1;

                // Padding a jobb oldalon, hogy az utolsó gyertya ne lógjon ki
                double padding = visibleCount * 0.05;  // 5% padding
                _xAxis.Zoom(startIndex, endIndex + padding);
            }

            UpdateXAxisLabels();
            AutoFitYToVisible();
            Model.InvalidatePlot(true);

            // Zoom/pan közben frissítés
#pragma warning disable CS0618
            _xAxis.AxisChanged += async (_, __) =>
            {
                await MaybeLazyLoadOlderAsync();  // ← Most már async!
                UpdateXAxisLabels();
                AutoFitYToVisible();
                Model.InvalidatePlot(false);
            };
#pragma warning restore CS0618

            return Model;
        }

        private async Task MaybeLazyLoadOlderAsync()
        {
            if (_lazyLoader == null) return;
            if (_isLoadingOlder) return;
            if (_xAxis.ActualMinimum >= 5) return;  // Már van elég adat az elején

            _isLoadingOlder = true;
            try
            {
                var viewMin = _xAxis.ActualMinimum;
                var viewMax = _xAxis.ActualMaximum;

                var olderEnd = _earliestLoaded;
                var olderStart = _earliestLoaded.AddDays(-90);

                var olderData = await _lazyLoader(olderStart, olderEnd);
                if (olderData == null || olderData.Count == 0)
                    return;

                olderData = olderData.OrderBy(c => c.Timestamp).ToList();
                _earliestLoaded = olderData.First().Timestamp;

                int shift = olderData.Count;

                // Eltoljuk a meglévő indexeket
                foreach (var item in _series.Items)
                    item.X += shift;

                // Beszúrjuk az új adatokat az elejére
                for (int i = 0; i < olderData.Count; i++)
                {
                    var c = olderData[i];
                    _series.Items.Insert(i, new HighLowItem(i, c.High, c.Low, c.Open, c.Close));
                    _candles.Insert(i, c);  // ← Szinkronban tartjuk
                }

                // Nézet követése az új adatok után
                _xAxis.Zoom(viewMin + shift, viewMax + shift);
            }
            finally
            {
                _isLoadingOlder = false;
            }
        }

        private void UpdateXAxisLabels()
        {
            if (_xAxis == null || _candles == null) return;
            int n = _candles.Count;
            if (n == 0) return;

            // Címkék előkészítése
            if (_xAxis.Labels.Count != n)
            {
                _xAxis.Labels.Clear();
                for (int i = 0; i < n; i++) _xAxis.Labels.Add(string.Empty);
            }

            double amin = _xAxis.ActualMinimum;
            double amax = _xAxis.ActualMaximum;
            if (double.IsNaN(amin) || double.IsNaN(amax) || amax <= amin)
                return;

            int start = Math.Max(0, (int)Math.Floor(amin));
            int end = Math.Min(n - 1, (int)Math.Ceiling(amax));
            int visible = Math.Max(1, end - start + 1);

            // Todas a tisztítás
            for (int i = 0; i < n; i++)
                _xAxis.Labels[i] = string.Empty;

            // Szoros zoom: napi szint
            if (visible <= 20)
            {
                for (int i = start; i <= end; i++)
                {
                    var dt = _candles[i].Timestamp;

                    // Nagyon szoros zoom (6-8 gyertya): év + hónap + nap
                    if (visible <= 8)
                    {
                        _xAxis.Labels[i] = dt.ToString("yyyy-MM-dd");
                    }
                    // Közepesen szoros zoom: hónap + nap
                    else
                    {
                        _xAxis.Labels[i] = dt.ToString("MM-dd");
                        if (i == start || dt.Day == 1)
                            _xAxis.Labels[i] = dt.ToString("MMM dd");
                    }
                }
                return;
            }

            // Közepesen sűrű: 12 címke
            int desired = Math.Min(12, visible);
            int step = (int)Math.Ceiling((double)visible / desired);

            for (int i = start; i <= end; i += step)
            {
                var dt = _candles[i].Timestamp;
                if (visible > 180)
                    _xAxis.Labels[i] = (dt.Month == 1) ? dt.ToString("yyyy") : string.Empty;
                else
                    _xAxis.Labels[i] = dt.ToString("MMM");
            }
        }
    }
}
