using Carter;
using FluentValidation;
using Hakathon.Server.Behaviors;
using Hakathon.Server.Database;
using Hakathon.Server.Middlewares;
using Hakathon.Server.Options;
using Hakathon.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Hakathon.Server.Extensions;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddPresentation(IConfiguration configuration)
        {
            services.AddCors();
            services.AddValidatorsFromAssembly(typeof(Program).Assembly);
            services.AddMediatR(config =>
            {
                config.RegisterServicesFromAssembly(typeof(Program).Assembly);
                config.AddOpenBehavior(typeof(ValidationBehavior<,>));
            });
            services.AddCarter();
            services.AddControllers().AddNewtonsoftJson();
            services.AddTransient<ExceptionHandlingMiddleware>();
            services.Configure<GemmaOption>(opt =>
            {
                opt.API_Key = configuration["API_Key"]!;
                opt.AI_Model = configuration["AI_Model"]!;
            });
            services.AddHttpClient<GemmaService>((provider, httpClient) =>
            {
                var options = provider.GetRequiredService<IOptions<GemmaOption>>().Value;

                httpClient.BaseAddress = new Uri($"https://generativelanguage.googleapis.com/v1beta/models/{options.AI_Model}:generateContent");
            });
            
            return services;
        }
        private IServiceCollection AddCors()
        {
            services.AddCors(options =>
            {
                options.AddPolicy("CustomCORS", builder =>
                {
                    builder.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
            });
            return services;
        }
        public IServiceCollection AddPersistence(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            services.AddDbContext<DatabaseContext>();

            return services;
        }
        public IServiceCollection AddAuth(IConfiguration configuration)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    ClockSkew = TimeSpan.Zero,
                    IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!)),
                };
            });
            services.AddAuthorization(conf =>
            {
                conf.DefaultPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build();

            });
            return services;
        }
    }
}
