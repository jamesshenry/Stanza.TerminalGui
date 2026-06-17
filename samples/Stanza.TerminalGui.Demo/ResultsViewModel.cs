using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq.Expressions;
using CommunityToolkit.Mvvm.ComponentModel;
using Terminal.Gui.Views;
using Timer = System.Timers.Timer;

namespace Stanza.TerminalGui.Demo;

public partial class ResultsViewModel : ObservableObject
{
    private Timer _refreshTimer = new Timer();
    private Stopwatch _watch = new Stopwatch();
    int correctCount;
    int inCorrectCount;
    int extraCount;
    public bool Result => true;

    public event EventHandler? RequestClose;

    [ObservableProperty]
    public partial ObservableCollection<TestSnapshot> Snapshots { get; set; } = new();
    public IEnumerable<PointF> WpmSeriesData =>
        Snapshots.Select(s => new PointF((float)s.ElapsedTime.TotalSeconds, s.WPM));

    public void Initialize()
    {
        _watch.Reset();
        _refreshTimer = new Timer(TimeSpan.FromSeconds(1));
        _refreshTimer.AutoReset = true;
        _refreshTimer.Elapsed += OnRefreshTimerElapsed;
        _refreshTimer.Start();
        _watch.Start();
    }

    private void OnRefreshTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        int charCounter = Random.Shared.Next(1, 3);

        if (charCounter == 1)
        {
            correctCount++;
        }
        else if (charCounter == 2)
        {
            inCorrectCount++;
        }
        else if (charCounter == 3)
        {
            extraCount++;
        }
        var snap = new TestSnapshot()
        {
            WPM = (float)((Random.Shared.NextDouble() * 0.1) + Random.Shared.Next(34, 46)),
            Accuracy = Random.Shared.Next(80, 90),
            ElapsedTime = _watch.Elapsed,
            Correct = correctCount,
            Incorrect = inCorrectCount,
            Extra = extraCount,
        };

        Snapshots.Add(snap);
    }
}

public readonly record struct TestSnapshot(
    float WPM,
    float Accuracy,
    int Correct,
    int Incorrect,
    int Extra,
    TimeSpan ElapsedTime
) { }
