using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stanza.TerminalGui;
using Stanza.TerminalGui.Demo;
using Terminal.Gui;
using Terminal.Gui.App;

// 1. Setup DI

var builder = Host.CreateApplicationBuilder();
builder.Configuration.Sources.Clear();

// builder.Services.AddLogging(builder =>
// {
//     builder.AddDebug(); // Writes to IDE debug output
//     builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace); // Log all messages
// });
builder.Services.AddTransient<ProfileViewModel>();
builder.Services.AddTransient<ProfileView>();
builder.Services.AddTransient<ResultsViewModel>();
builder.Services.AddTransient<ResultsDialog>();
builder.Services.AddSingleton<IApplication>(_ => Application.Create().Init());

var host = builder.Build();
host.UseStanzaLogging();

// 2. Setup Stanza Logging to use MEL

using var app = host.Services.GetRequiredService<IApplication>();

// 4. Resolve the View (DI injects the ViewModel)
var mainView = host.Services.GetRequiredService<ResultsDialog>();
var viewModel = host.Services.GetRequiredService<ResultsViewModel>();
mainView.ViewModel = viewModel;
viewModel.Initialize();

app.Run(mainView);
app.Dispose();
