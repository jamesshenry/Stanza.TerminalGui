using System;
using System.Collections.Specialized;
using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Stanza.TerminalGui.Demo;

[StanzaView<ResultsViewModel>]
public partial class ResultsDialog : Dialog
{
    private readonly GraphView _graph;

    protected override bool OnAccepting(CommandEventArgs args)
    {
        if (base.OnAccepting(args))
        {
            return true;
        }
        return false;
    }

    public ResultsDialog(ResultsViewModel viewModel)
    {
        Height = Dim.Percent(90);
        Width = Dim.Percent(50);
        ViewModel = viewModel;
        _graph = new GraphView
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(), // leave room for buttons
            MarginLeft = 3,          // Space for Y-axis labels (e.g. "120.00")
            MarginBottom = 2         // Space for X-axis labels
        };

        _graph.CellSize = new PointF(2.0f, 5.0f);

        //_graph.MarginLeft = 3;
        //_graph.MarginBottom = 2;

        // X Axis (Time)
        _graph.AxisX.Text = "Time (s)";
        _graph.AxisX.Increment = 5f;      // Tick every 5 seconds
        _graph.AxisX.ShowLabelsEvery = 2; // Label every 10 seconds (5 * 2)
        _graph.AxisX.LabelGetter = (v) => $"{v.Value:0}s";

        // Y Axis (WPM)
        _graph.AxisY.Text = "WPM";
        _graph.AxisY.Increment = 5f;     // Tick every 10 WPM
        _graph.AxisY.ShowLabelsEvery = 2; // Label every tick (10, 20, 30...)
        Add(_graph);
        AddButton(new() { Text = "_Cancel" });
        AddButton(new() { Text = "_Ok" });
    }

    partial void OnApplyBindings(BindingContext context)
    {
        if (ViewModel == null)
            return;

        ViewModel
            .Snapshots.OnCollectionChanged(
                (s, e) =>
                {
                    var data = ViewModel.WpmSeriesData.ToList();
                    if (!data.Any()) return;

                    // 1. Calculate Bounds
                    float minWpm = data.Min(p => p.Y);
                    float maxWpm = data.Max(p => p.Y);

                    // 2. Add a 10% buffer so the line isn't cramped
                    float buffer = (maxWpm - minWpm) * 0.1f;
                    if (buffer < 5) buffer = 5; // Minimum buffer for visibility

                    // 3. Shift the Y-Axis 
                    // ScrollOffset.Y = the WPM value at the bottom of the graph
                    _graph.ScrollOffset = new PointF(_graph.ScrollOffset.X, minWpm - buffer);

                    // 4. Adjust the "Ruler" (Ticks)
                    // We ensure the Y axis line only draws from our min point
                    _graph.AxisY.Minimum = minWpm - buffer;

                    // 5. (Optional) Auto-Scale Height
                    // If you want the graph to always fill the vertical space:
                    float graphHeightInCells = _graph.Viewport.Height - _graph.MarginBottom;
                    if (graphHeightInCells > 0)
                    {
                        float totalRange = (maxWpm + buffer) - (minWpm - buffer);
                        _graph.CellSize = new PointF(_graph.CellSize.X, totalRange / graphHeightInCells);
                    }

                    // Standard redraw logic
                    _graph.Series.Clear();
                    _graph.Series.Add(new ScatterSeries { Points = data });

                    _graph.Annotations.Clear();
                    var path = new PathAnnotation { BeforeSeries = true };
                    path.Points.AddRange(data);
                    _graph.Annotations.Add(path);

                    _graph.SetNeedsDraw();
                }
            )
            .AddTo(context);
    }
}
