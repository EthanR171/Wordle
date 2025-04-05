using WordServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Register gRPC service
builder.Services.AddGrpc();

var app = builder.Build();

// Map your gRPC endpoint
app.MapGrpcService<WordService>();

// Default route for REST
app.MapGet("/", () => "This is the WordServer gRPC service. Use a gRPC client to connect.");

app.Run();
