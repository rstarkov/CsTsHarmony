namespace CsTsHarmony.TestServer;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddControllers();
        var app = builder.Build();
        app.MapControllers();

        var b = new ApiBuilder();
        b.AddControllers(app.Services);
        b.DiscoverTypes();
        b.ApplyTypes();
        var c = new ApiCodeGenerator(b.Api);
        c.Imports.Add("import { ApiServiceBase, ApiServiceOptions } from './base';");
        c.Output(@"..\..\Tests\TestClient\api\api.ts");

        app.Run();
    }
}
