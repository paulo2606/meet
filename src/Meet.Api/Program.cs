using System.Text;
using System.Threading.RateLimiting;
using Meet.Api.Data;
using Meet.Api.Hubs;
using Meet.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("connection string nao configurada");

builder.Services.AddDbContext<MeetDbContext>(options => options.UseNpgsql(connectionString));

var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.Configure<TokenOptions>(jwtSection);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMeetingService, MeetingService>();
builder.Services.AddSignalR();

const string CorsPolicyName = "web";

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        policy.WithOrigins("http://localhost:3000")
            .AllowCredentials()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var loginPermitLimit = builder.Configuration.GetValue("RateLimiting:LoginPermitLimit", 5);
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.PermitLimit = loginPermitLimit;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(Path.Combine(webRoot, "avatars"));
Directory.CreateDirectory(Path.Combine(webRoot, "uploads"));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRateLimiter();
app.UseCors(CorsPolicyName);
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<MeetingHub>("/hubs/meeting");

app.Run();

public partial class Program;
