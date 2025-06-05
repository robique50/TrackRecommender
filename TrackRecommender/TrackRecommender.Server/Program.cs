using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;
using TrackRecommender.Server.Data;
using TrackRecommender.Server.Mappers.Implementations;
using TrackRecommender.Server.Mappers.Interfaces;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Models.DTOs;
using TrackRecommender.Server.Repositories.Implementations;
using TrackRecommender.Server.Repositories.Interfaces;
using TrackRecommender.Server.Services;
using TrackRecommender.Server.Services.Implementations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        o => o.UseNetTopologySuite()
    )
);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
});

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

const int httpClientTimeoutSeconds = 720;
builder.Services.AddHttpClient("OverpassAPI", client =>
{
    client.Timeout = TimeSpan.FromSeconds(httpClientTimeoutSeconds);
});
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ITrailRepository, TrailRepository>();
builder.Services.AddScoped<IRegionRepository, RegionRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();

builder.Services.AddScoped<TrailImportService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<WeatherService>();
builder.Services.AddScoped<RegionImportService>();
builder.Services.AddScoped<RegionService>();

builder.Services.AddScoped<IMapper<User, UserProfileDto>, UserMapper>();
builder.Services.AddScoped<UserPreferencesMapper>(provider =>
    new UserPreferencesMapper(provider.GetRequiredService<IRegionRepository>()));
builder.Services.AddScoped<IMapper<UserPreferences, UserPreferencesDto>>(provider =>
    provider.GetRequiredService<UserPreferencesMapper>());
builder.Services.AddScoped<IMapper<Trail, TrailDto>>(provider =>
    new TrailMapper(provider.GetRequiredService<ILogger<TrailMapper>>())); 
builder.Services.AddScoped<ReviewMapper>();

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey))
{
    throw new InvalidOperationException("JWT Key is missing in configuration. Add it to appsettings.json.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "TrackRecommender",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "TrackRecommenderUsers",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("https://localhost:54271")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TrackRecommender API",
        Version = "v1",
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,  
        Scheme = "bearer",               
        BearerFormat = "JWT"             
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TrackRecommender API v1");
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
    });
}

app.UseHttpsRedirection();
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Remove("Strict-Transport-Security");
        await next();
    });
}

app.UseCors("AllowAngular");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapFallbackToFile("/index.html");

app.Run();