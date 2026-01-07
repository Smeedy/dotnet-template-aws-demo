using Api.Authorization;
using Api.Routes.Weather;
using Api.Swagger;
using Application;
using FluentValidation;
using Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Enable or disable swagger from appsettings
var enableSwagger = builder.Configuration.GetValue<bool>("Swagger:Enabled");

// Add other layers
builder.AddApplication();
builder.AddInfrastructure();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.AddCustomSwaggerGenOptions());

// Add JWT-token authentication + our custom authorization policies
builder.Services.AddAuthentication().AddJwtBearer();
builder.Services.AddAuthorization(options => { options.AddAuthorizationPolicies(); });

// FluentValidation register all validators present in this assembly
builder.Services.AddValidatorsFromAssemblyContaining<Api.Program>();

// Add Logging
builder.Host.UseSerilog((context, services, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
);

// Add ProblemDetails for error handling of all non-problem error responses
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddInfrastructureHealthChecks();

var app = builder.Build();

// Produce a ProblemDetails payload for exceptions
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (enableSwagger) {
    app.UseSwagger(); // <-- belangrijk: dit expose't de JSON endpoints
    app.UseSwaggerUI(options => {
        options.RoutePrefix = "swagger";
        options.AddCustomSwaggerUIOptions(app.Environment.IsDevelopment());
    });

    //app.MapSwagger().RequireAuthorization(AuthorizationPolicy.Admin);
}

app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

// Redirect to swagger
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.MapHealthChecks("/healthz");

app.MapWeatherUserGroup()
   .MapWeatherAdminGroup();

app.Run();

// To make it visible for E2E-tests:
namespace Api
{
    public partial class Program
    {
    }
}