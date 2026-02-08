using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using ProfitProphet.Entities;
using ProfitProphet.Models.Backtesting;
using ProfitProphet.Models.Charting;
using ProfitProphet.Services.Charting;
using ProfitProphet.Services.Indicators;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ProfitProphet.Services
{
    public class ChartBuilder
    {
        private CategoryAxis _xAxis;
        private LinearAxis _mainYAxis; // Main price axis
        private CandleStickSeries _series;
        private readonly IIndicatorRegistry _indicatorRegistry;
        private readonly IChartSettingsService _chartSettings;

        private DateTime _earliestLoaded;
        private bool _isLoadingOlder;

        private List<CandleData> _candles = new();
        public IReadOnlyList<CandleData> Candles => _candles;

        public bool ShowGapMarkers { get; set; } = true;

        private Func<DateTime, DateTime, Task<List<CandleData>>> _lazyLoader;

        public PlotModel Model { get; private set; }

        public ChartBuilder()
            : this(new ProfitProphet.Services.Indicators.IndicatorRegistry(),
                   new ProfitProphet.Services.Charting.ChartSettingsService())
        {
        }

        public ChartBuilder(IIndicatorRegistry indicatorRegistry, IChartSettingsService chartSettings)
        {
            _indicatorRegistry = indicatorRegistry ?? throw new ArgumentNullException(nameof(indicatorRegistry));
            _chartSettings = chartSettings ?? throw new ArgumentNullException(nameof(chartSettings));
        }

        public void ConfigureLazyLoader(Func<DateTime, DateTime, Task<List<CandleData>>> loader)
        {
            _lazyLoader = loader;
        }

        public class CandleData
        {
            public DateTime Timestamp { get; set; }
            public double Open { get; set; }
            public double High { get; set; }
            public double Low { get; set; }
            public double Close { get; set; }
            public double Volume { get; set; }
            public bool HasGapBefore { get; set; }
        }

        // ==================================================================================
        // Main Build Logic with Dynamic Panels
        // ==================================================================================
        public PlotModel BuildInteractiveChart(List<CandleData> candles, string symbol, string interval)
        {
            if (candles == null || candles.Count == 0)
                return new PlotModel { Title = $"{symbol} - No Data", TextColor = OxyColors.White };

            // 1. Sort and initialize data
            _candles = candles.OrderBy(c => c.Timestamp).ToList();
            _earliestLoaded = _candles.First().Timestamp;
            _isLoadingOlder = false;

            // 2. Initialize Model
            Model = new PlotModel
            {
                Title = $"{symbol} ({interval})",
                TextColor = OxyColors.White,
                Background = OxyColor.FromRgb(22, 27, 34),
                PlotAreaBackground = OxyColor.FromRgb(24, 28, 34),
                PlotAreaBorderThickness = new OxyThickness(0),
                PlotAreaBorderColor = OxyColor.FromRgb(40, 40, 40)
            };

            // 3. Analyze Settings to determine layout
            var settings = _chartSettings.GetForSymbol(symbol);

            // Filter indicators that need separate panels
            var subPanelIndicators = settings.Indicators
                .Where(i => i.IsVisible && !IsOverlay(i.IndicatorId, i.Pane))
                .ToList();

            int subPanelCount = subPanelIndicators.Count;

            // 4. Calculate Layout Geometry
            // Allocation: Each sub-panel gets 20% height. Main chart gets the rest.
            double panelHeight = 0.20;
            double spacing = 0.02;
            double mainHeight = 1.0 - (subPanelCount * (panelHeight + spacing));

            // Ensure main chart has at least 40% height
            if (mainHeight < 0.4)
            {
                double availableForSubs = 0.6;
                // If we have many panels, they get smaller
                panelHeight = (availableForSubs / subPanelCount) - spacing;
                mainHeight = 0.4;
            }
            // If no subs, main chart takes full height
            if (subPanelCount == 0) mainHeight = 1.0;

            // 5. Setup X-Axis (Shared)
            _xAxis = new CategoryAxis
            {
                Position = AxisPosition.Bottom,
                TextColor = OxyColors.White,
                MajorGridlineStyle = LineStyle.None,
                MinorGridlineStyle = LineStyle.None,
                GapWidth = 0.1,
                IntervalLength = 80,
                Angle = -60,
                IsZoomEnabled = true,
                IsPanEnabled = true,
                Key = "X-Axis"
            };
            Model.Axes.Add(_xAxis);

            // 6. Setup Main Y-Axis (Price)
            _mainYAxis = new LinearAxis
            {
                Position = AxisPosition.Right,
                TextColor = OxyColors.White,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                AxislineColor = OxyColors.Gray,
                Title = "Price",
                IsZoomEnabled = true,
                IsPanEnabled = true,
                Key = "MainY",
                StartPosition = 1.0 - mainHeight, // Starts from top down
                EndPosition = 1.0
            };
            Model.Axes.Add(_mainYAxis);

            // 7. Add Candle Data
            _series = new CandleStickSeries
            {
                IncreasingColor = OxyColor.FromRgb(34, 197, 94),
                DecreasingColor = OxyColor.FromRgb(239, 68, 68),
                CandleWidth = CalculateCandleWidth(interval),
                TrackerFormatString = "{Category}\nO: {4:0.###}\nH: {1:0.###}\nL: {2:0.###}\nC: {3:0.###}",
                YAxisKey = _mainYAxis.Key,
                XAxisKey = _xAxis.Key
            };

            for (int i = 0; i < _candles.Count; i++)
            {
                var c = _candles[i];
                _series.Items.Add(new HighLowItem(i, c.High, c.Low, c.Open, c.Close));
            }
            Model.Series.Add(_series);

            if (ShowGapMarkers && IsDailyInterval(interval) && _candles.Count > 1)
            {
                AddGapMarkers();
            }

            // 8. Create Axes for Sub-Panels and Store Map
            // We map the IndicatorInstance (object reference) to its Axis Key
            var indicatorToAxisMap = new Dictionary<IndicatorInstance, LinearAxis>();

            for (int i = 0; i < subPanelCount; i++)
            {
                var indInst = subPanelIndicators[i];

                // Calculate position from bottom up
                double start = i * (panelHeight + spacing);
                double end = start + panelHeight;

                var subAxis = new LinearAxis
                {
                    Key = $"Axis_Sub_{i}_{indInst.IndicatorId}", // Unique Key for this instance
                    Position = AxisPosition.Right,
                    TextColor = OxyColors.LightGray,
                    StartPosition = start,
                    EndPosition = end,
                    MajorGridlineStyle = LineStyle.Solid,
                    MinorGridlineStyle = LineStyle.None,
                    Title = indInst.IndicatorId.ToUpper(),
                    TitleFontSize = 10,
                    IsZoomEnabled = false,
                    IsPanEnabled = false
                };
                //if (indInst.IndicatorId.Equals("cmf", StringComparison.OrdinalIgnoreCase))
                //{
                //    subAxis.Minimum = -1;
                //    subAxis.Maximum = 1;
                //    subAxis.MajorStep = 0.5;
                //    subAxis.MinorStep = 0.1;
                //}

                // Add axis to model
                Model.Axes.Add(subAxis);

                // Map it
                indicatorToAxisMap[indInst] = subAxis;
            }

            // 9. Initial View
            Model.ResetAllAxes();
            if (_candles.Count > 0)
            {
                int visibleCount = Math.Min(120, _candles.Count);
                int startIndex = _candles.Count - visibleCount;
                int endIndex = _candles.Count - 1;
                double padding = visibleCount * 0.05;
                _xAxis.Zoom(startIndex, endIndex + padding);
            }
            UpdateXAxisLabels();
            AutoFitYToVisible();
            Model.InvalidatePlot(true);

            // 10. Events
#pragma warning disable CS0618
            _xAxis.AxisChanged += async (_, __) =>
            {
                if (_candles == null || _candles.Count == 0) return;
                await MaybeLazyLoadOlderAsync();
                UpdateXAxisLabels();
                AutoFitYToVisible();
                Model.InvalidatePlot(false);
            };
            _xAxis.AxisChanged += (_, __) => UpdateXAxisTitle();
#pragma warning restore CS0618

            // 11. Compute and Render Indicators
            //var ohlc = _candles.Select(c => new OhlcPoint
            //{
            //    Open = c.Open,
            //    High = c.High,
            //    Low = c.Low,
            //    Close = c.Close
            //}).ToList();
            var ohlc = _candles.Select(c => new OhlcPoint
            {
                Open = c.Open,
                High = c.High,
                Low = c.Low,
                Close = c.Close,
                Volume = c.Volume
            }).ToList();

            if (ohlc.Count > 0)
            {
                var last = ohlc.Last();
                System.Diagnostics.Debug.WriteLine($"[ADAT ELLENŐRZÉS] Utolsó gyertya: {last.Close} | Volume: {last.Volume}");

                if (last.Volume == 0)
                    System.Diagnostics.Debug.WriteLine("[HIBA] A Volume értéke 0! Valahol elveszik az adat.");
                else
                    System.Diagnostics.Debug.WriteLine("[OK] A Volume adat megérkezett a ChartBuilder-be.");
            }

            foreach (var inst in settings.Indicators.Where(i => i.IsVisible))
            {
                var ind = _indicatorRegistry.Get(inst.IndicatorId);
                if (ind == null) continue;

                var result = ind.Compute(ohlc, inst.Params);

                // Determine Axis
                LinearAxis targetAxis = _mainYAxis;

                // If it is in the map, it means it's a sub-panel indicator
                if (indicatorToAxisMap.ContainsKey(inst))
                {
                    targetAxis = indicatorToAxisMap[inst];
                }

                System.Diagnostics.Debug.WriteLine($"[MODEL ELLENŐRZÉS] Model: {Model} | _xAxis: {_xAxis}");
                ind.Render(Model, result, _xAxis, targetAxis);
            }

            return Model;
        }

        // nyilak kirajzolása
        //public void ShowTradeMarkers(List<TradeRecord> trades)
        //{
        //    if (Model == null || _xAxis == null || _candles == null || _candles.Count == 0) return;

        //    // 1. Előző nyilak törlése 
        //    var oldAnnotations = Model.Annotations.Where(a => a.Tag is string t && t == "TradeMarker").ToList();
        //    foreach (var ann in oldAnnotations)
        //    {
        //        Model.Annotations.Remove(ann);
        //    }

        //    double avgPrice = _candles.Average(c => c.Close);
        //    double offset = avgPrice * 0.005; // 0.5% távolság

        //    // 2. Új nyilak kirajzolása
        //    foreach (var trade in trades)
        //    {
        //        // VÉTEL JEL (Zöld nyíl felfelé)
        //        double xIndex = Axis.ToDouble(trade.EntryDate);

        //        // Mivel CategoryAxis-t használunk, a dátumot vissza kell keresni
        //        //var candleIndex = _candles.FindIndex(c => c.Timestamp == trade.EntryDate);
        //        var buyCandleIndex = _candles.FindIndex(c => c.Timestamp.Date == trade.EntryDate.Date);

        //        if (buyCandleIndex >= 0)
        //        {
        //            var candle = _candles[buyCandleIndex];

        //            var buyArrow = new PointAnnotation
        //            {
        //                X = buyCandleIndex,
        //                // A gyertya ALJA alá tesszük az offsettel
        //                Y = candle.Low - offset,
        //                Shape = MarkerType.Triangle, // Felfelé mutató háromszög
        //                Fill = OxyColors.LimeGreen,
        //                Stroke = OxyColors.Black,
        //                StrokeThickness = 1,
        //                Size = 8, // Kicsit kisebb, elegánsabb méret
        //                Text = "B",
        //                TextColor = OxyColors.White,
        //                TextVerticalAlignment = VerticalAlignment.Top, // Szöveg a pont alatt
        //                ToolTip = $"Vétel: {trade.EntryDate:yyyy-MM-dd}\nÁr: {trade.EntryPrice}",
        //                Tag = "TradeMarker",
        //                Layer = AnnotationLayer.AboveSeries
        //            };
        //            Model.Annotations.Add(buyArrow);
        //        }

        //        var sellCandleIndex = _candles.FindIndex(c => c.Timestamp.Date == trade.ExitDate.Date);

        //        if (sellCandleIndex >= 0)
        //        {
        //            var candle = _candles[sellCandleIndex];

        //            var sellMarker = new PointAnnotation
        //            {
        //                X = sellCandleIndex,
        //                Y = candle.High + offset, // A gyertya TETEJE fölé
        //                Shape = MarkerType.Cross, // <--- Marad a Cross
        //                Fill = OxyColors.Red,
        //                Stroke = OxyColors.Red, // A keresztnek a Stroke adja a színét
        //                StrokeThickness = 3,    // Legyen vastagabb, hogy jól látsszon
        //                Size = 10,
        //                // Text = "S", // A keresztnél zavaró lehet a szöveg, de kiveheted a kommentet ha kell
        //                // TextColor = OxyColors.White,
        //                // TextVerticalAlignment = VerticalAlignment.Bottom,
        //                ToolTip = $"Eladás: {trade.ExitDate:yyyy-MM-dd}\nÁr: {trade.ExitPrice}\nProfit: {trade.Profit:C2}",
        //                Tag = "TradeMarker",
        //                Layer = AnnotationLayer.AboveSeries
        //            };
        //            Model.Annotations.Add(sellMarker);
        //        }
        //    }

        //    // 3. Frissítés
        //    Model.InvalidatePlot(true);
        //}

        public void ShowTradeMarkers(List<TradeRecord> trades)
        {
            if (Model == null || _xAxis == null || _candles == null || _candles.Count == 0) return;

            // 1. Előző nyilak törlése (marad a régi logika)
            var oldAnnotations = Model.Annotations.Where(a => a.Tag is string t && t == "TradeMarker").ToList();
            foreach (var ann in oldAnnotations)
            {
                Model.Annotations.Remove(ann);
            }

            // Offset számítása (marad)
            double avgPrice = _candles.Average(c => c.Close);
            double offset = avgPrice * 0.015; // 0.5% távolság

            // 2. Új nyilak kirajzolása Webdings betűtípussal
            foreach (var trade in trades)
            {
                // --- VÉTEL (BUY) ---
                // Keressük meg a gyertya indexét
                var buyCandleIndex = _candles.FindIndex(c => c.Timestamp.Date == trade.EntryDate.Date);

                if (buyCandleIndex >= 0)
                {
                    var candle = _candles[buyCandleIndex];

                    // PointAnnotation HELYETT TextAnnotation
                    var buyArrow = new OxyPlot.Annotations.TextAnnotation
                    {
                        // Hova tegye: X = Index, Y = Low alatt
                        TextPosition = new DataPoint(buyCandleIndex, candle.Low - offset),

                        //Text = "5",                 // Webdings "5" = Felfelé nyíl
                        Text = "▲",
                        //Font = "Webdings",    // Betűtípus
                        FontSize = 24,              // Méret

                        Stroke = OxyColors.Transparent,
                        TextColor = OxyColors.LimeGreen, // Zöld szín
                        FontWeight = OxyPlot.FontWeights.Bold,

                        // IGAZÍTÁS: A pont legyen a szöveg TETEJE (tehát a nyíl a pont alatt lóg)
                        TextVerticalAlignment = OxyPlot.VerticalAlignment.Top,
                        TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,

                        ToolTip = $"Vétel: {trade.EntryDate:yyyy-MM-dd}\nÁr: {trade.EntryPrice}",
                        Tag = "TradeMarker",
                        Layer = AnnotationLayer.AboveSeries
                    };
                    Model.Annotations.Add(buyArrow);
                }

                // --- ELADÁS (SELL) ---
                var sellCandleIndex = _candles.FindIndex(c => c.Timestamp.Date == trade.ExitDate.Date);

                if (sellCandleIndex >= 0)
                {
                    var candle = _candles[sellCandleIndex];

                    // PointAnnotation HELYETT TextAnnotation
                    var sellArrow = new OxyPlot.Annotations.TextAnnotation
                    {
                        // Hova tegye: X = Index, Y = High fölött
                        TextPosition = new DataPoint(sellCandleIndex, candle.High + offset),

                        //Text = "6",                 // Webdings "6" = Lefelé nyíl
                        Text = "▼",
                        //Font = "Webdings",
                        FontSize = 24,

                        Stroke = OxyColors.Transparent,
                        TextColor = OxyColors.Red,   // Piros szín
                        FontWeight = OxyPlot.FontWeights.Bold,

                        // IGAZÍTÁS: A pont legyen a szöveg ALJA (tehát a nyíl a pont fölött ül)
                        TextVerticalAlignment = OxyPlot.VerticalAlignment.Bottom,
                        TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,

                        ToolTip = $"Eladás: {trade.ExitDate:yyyy-MM-dd}\nÁr: {trade.ExitPrice}\nProfit: {trade.Profit:C2}",
                        Tag = "TradeMarker",
                        Layer = AnnotationLayer.AboveSeries
                    };
                    Model.Annotations.Add(sellArrow);
                }
            }

            // 3. Frissítés
            Model.InvalidatePlot(true);
        }

        private void AddGapMarkers()
        {
            for (int i = 0; i < _candles.Count; i++)
            {
                var c = _candles[i];
                if (!c.HasGapBefore) continue;

                var gapLine = new LineAnnotation
                {
                    Type = LineAnnotationType.Vertical,
                    X = i - 0.5,
                    Color = OxyColor.FromArgb(60, 200, 200, 255),
                    StrokeThickness = 0.5,
                    LineStyle = LineStyle.Dash,
                    Layer = AnnotationLayer.BelowSeries,
                    XAxisKey = _xAxis.Key,
                    YAxisKey = _mainYAxis.Key
                };
                Model.Annotations.Add(gapLine);
            }
        }

        private bool IsOverlay(string indicatorId, string paneSetting)
        {
            if (!string.IsNullOrEmpty(paneSetting))
            {
                if (paneSetting.ToLower() == "main") return true;
                if (paneSetting.ToLower() == "sub") return false;
            }

            var id = indicatorId.ToLower();
            if (id.Contains("stoch") || id.Contains("rsi") || id.Contains("cmf") || id.Contains("macd"))
                return false;

            return true;
        }

        // ==================================================================================
        // Helpers
        // ==================================================================================

        private void AutoFitYToVisible()
        {
            // Exit if resources are missing
            if (_xAxis == null || Model == null) return;

            // 1. Determine visible X-axis range (Indices)
            double startX = _xAxis.ActualMinimum;
            double endX = _xAxis.ActualMaximum;

            if (double.IsNaN(startX) || double.IsNaN(endX)) return;

            int startIdx = (int)Math.Floor(startX);
            int endIdx = (int)Math.Ceiling(endX);

            // 2. Iterate through ALL Y-axes in the model (Main + Sub panels)
            foreach (var axis in Model.Axes.OfType<LinearAxis>())
            {
                // Process only vertical axes (Left or Right)
                if (axis.Position != AxisPosition.Left && axis.Position != AxisPosition.Right) continue;

                double min = double.MaxValue;
                double max = double.MinValue;
                bool hasDataInView = false;

                // 3. Find series linked to this specific axis
                // FIX: Cast to XYAxisSeries to access YAxisKey
                var linkedSeries = Model.Series
                    .OfType<XYAxisSeries>()
                    .Where(s => s.YAxisKey == axis.Key);

                foreach (var s in linkedSeries)
                {
                    // A) CandleStickSeries (Main Chart)
                    if (s is CandleStickSeries cs)
                    {
                        // Optimize loop: check only visible items
                        int safeStart = Math.Max(0, startIdx);
                        int safeEnd = Math.Min(cs.Items.Count - 1, endIdx);

                        for (int i = safeStart; i <= safeEnd; i++)
                        {
                            var item = cs.Items[i];
                            if (item.Low < min) min = item.Low;
                            if (item.High > max) max = item.High;
                            hasDataInView = true;
                        }
                    }
                    // B) LineSeries (Indicators: CMF, Stoch, SMA)
                    else if (s is LineSeries ls)
                    {
                        // Filter points within the visible X range
                        var visiblePoints = ls.Points
                            .Where(p => p.X >= startIdx && p.X <= endIdx);

                        foreach (var p in visiblePoints)
                        {
                            if (double.IsNaN(p.Y)) continue;
                            if (p.Y < min) min = p.Y;
                            if (p.Y > max) max = p.Y;
                            hasDataInView = true;
                        }
                    }
                }

                // 4. Update axis range if valid data found
                if (hasDataInView && max > min)
                {
                    // Apply 5% padding for better visualization
                    double range = max - min;
                    double padding = range * 0.05;

                    // Avoid zero range issues
                    if (range == 0) padding = 1.0;

                    axis.Zoom(min - padding, max + padding);
                }
            }
        }

        public static double CalculateCandleWidth(string interval)
        {
            if (string.IsNullOrWhiteSpace(interval)) return 0.5;
            interval = interval.ToLower();
            if (interval.EndsWith("m")) return 0.5;
            if (interval.EndsWith("h")) return 0.7;
            if (interval.EndsWith("d")) return 0.9;
            return 0.6;
        }

        private static bool IsDailyInterval(string interval)
        {
            if (string.IsNullOrWhiteSpace(interval)) return false;
            interval = interval.Trim();
            return interval.Equals("1d", StringComparison.OrdinalIgnoreCase)
                || interval.Equals("d1", StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateXAxisTitle()
        {
            if (_candles == null || _candles.Count == 0 || _xAxis == null) return;

            double amin = _xAxis.ActualMinimum;
            double amax = _xAxis.ActualMaximum;

            if (double.IsNaN(amin) || double.IsNaN(amax)) return;

            int start = Math.Max(0, (int)Math.Floor(amin));
            int end = Math.Min(_candles.Count - 1, (int)Math.Ceiling(amax));

            if (start < 0 || end < 0 || start >= _candles.Count || end >= _candles.Count) return;

            var startYear = _candles[start].Timestamp.Year;
            var endYear = _candles[end].Timestamp.Year;

            string title = startYear == endYear
                ? startYear.ToString()
                : $"{startYear} – {endYear}";

            _xAxis.Title = title;
            Model?.InvalidatePlot(false);
        }

        private async Task MaybeLazyLoadOlderAsync()
        {
            if (_lazyLoader == null) return;
            if (_isLoadingOlder) return;
            if (_xAxis.ActualMinimum >= 5) return;

            _isLoadingOlder = true;
            try
            {
                var viewMin = _xAxis.ActualMinimum;
                var viewMax = _xAxis.ActualMaximum;

                var olderEnd = _earliestLoaded;
                var olderStart = _earliestLoaded.AddDays(-90);

                var olderData = await _lazyLoader(olderStart, olderEnd);
                if (olderData == null || olderData.Count == 0) return;

                olderData = olderData.OrderBy(c => c.Timestamp).ToList();
                _earliestLoaded = olderData.First().Timestamp;

                int shift = olderData.Count;

                foreach (var item in _series.Items) item.X += shift;

                for (int i = 0; i < olderData.Count; i++)
                {
                    var c = olderData[i];
                    _series.Items.Insert(i, new HighLowItem(i, c.High, c.Low, c.Open, c.Close));
                    _candles.Insert(i, c);
                }

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

            if (_xAxis.Labels.Count != n)
            {
                _xAxis.Labels.Clear();
                for (int i = 0; i < n; i++) _xAxis.Labels.Add(string.Empty);
            }

            double amin = _xAxis.ActualMinimum;
            double amax = _xAxis.ActualMaximum;
            if (double.IsNaN(amin) || double.IsNaN(amax) || amax <= amin) return;

            int start = Math.Max(0, (int)Math.Floor(amin));
            int end = Math.Min(n - 1, (int)Math.Ceiling(amax));
            int visible = Math.Max(1, end - start + 1);

            for (int i = 0; i < n; i++) _xAxis.Labels[i] = string.Empty;

            if (visible <= 40)
            {
                for (int i = start; i <= end; i++)
                {
                    var dt = _candles[i].Timestamp;
                    if (visible <= 30)
                        _xAxis.Labels[i] = dt.ToString("yyyy-MMM-dd");
                    else
                    {
                        _xAxis.Labels[i] = dt.ToString("MMM-dd");
                        if (i == start || dt.Day == 1)
                            _xAxis.Labels[i] = dt.ToString("MMM dd");
                    }
                }
                return;
            }

            int desired = Math.Min(16, visible);
            int step = (int)Math.Ceiling((double)visible / desired);

            for (int i = start; i <= end; i += step)
            {
                var dt = _candles[i].Timestamp;
                if (visible > 120)
                    _xAxis.Labels[i] = (dt.Month == 1) ? dt.ToString("yyyy MMM") : string.Empty;
                else
                    _xAxis.Labels[i] = dt.ToString("yyyy MMM");
            }
        }

        public void AddIndicatorToSymbol(string symbol, string indicatorId, Action<IndicatorParams>? configure = null)
        {
            var st = _chartSettings.GetForSymbol(symbol);
            string defaultPane = IsOverlay(indicatorId, null) ? "main" : "sub";
            var inst = new IndicatorInstance
            {
                IndicatorId = indicatorId,
                IsVisible = true,
                Pane = defaultPane,
                Params = new IndicatorParams()
            };
            configure?.Invoke(inst.Params);
            st.Indicators.Add(inst);
            _chartSettings.Save(st);
        }

        public void ClearIndicatorsForSymbol(string symbol)
        {
            var st = _chartSettings.GetForSymbol(symbol);
            st.Indicators.Clear();
            _chartSettings.Save(st);
        }
    }
}