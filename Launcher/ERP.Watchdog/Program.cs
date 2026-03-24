using ERP.Watchdog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<WatchdogWorker>();

// Register as a Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "SKYRA_Watchdog";
});

var host = builder.Build();
host.Run();
