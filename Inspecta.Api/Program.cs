using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Inspecta.Api.Middleware;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Infrastructure.Data;
using Inspecta.Infrastructure.Repositories;
using Inspecta.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AuthExceptionHandler>();
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower)));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("Inspecta") ?? "Data Source=inspecta.db";
builder.Services.AddDbContext<InspectaDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IRequestRepository, EfRequestRepository>();
builder.Services.AddScoped<IInspectionRequestService, InspectionRequestService>();
builder.Services.AddScoped<IClientRepository, EfClientRepository>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IEquipmentRepository, EfEquipmentRepository>();
builder.Services.AddScoped<IEquipmentService, EquipmentService>();
builder.Services.AddScoped<IQuoteRepository, EfQuoteRepository>();
builder.Services.AddScoped<IQuoteService, QuoteService>();
builder.Services.AddScoped<ICertificateRepository, EfCertificateRepository>();
builder.Services.AddScoped<ICertificateService, CertificateService>();
builder.Services.AddScoped<ITimesheetRepository, EfTimesheetRepository>();
builder.Services.AddScoped<ITimesheetService, TimesheetService>();
builder.Services.AddScoped<IPasswordService, IdentityPasswordService>();
builder.Services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();

var signingKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
{
    throw new InvalidOperationException("Set Jwt:SigningKey to a secret containing at least 32 UTF-8 bytes.");
}

var issuer = builder.Configuration["Jwt:Issuer"] ?? "Inspecta.Api";
var audience = builder.Configuration["Jwt:Audience"] ?? "Inspecta.Flutter";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        RoleClaimType = "role",
        NameClaimType = JwtRegisteredClaimNames.Sub
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var subject = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (!Guid.TryParse(subject, out var userId))
            {
                context.Fail("Invalid subject claim.");
                return;
            }

            var dbContext = context.HttpContext.RequestServices.GetRequiredService<InspectaDbContext>();
            var currentUser = await dbContext.Users.AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new { user.IsActive, user.Role })
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
            if (currentUser is null || !currentUser.IsActive)
            {
                context.Fail("Account is disabled or no longer exists.");
                return;
            }

            var tokenRole = context.Principal?.FindFirst("role")?.Value;
            var currentRole = currentUser.Role switch
            {
                Inspecta.Domain.Enums.UserRole.Coordinator => "coordinator",
                Inspecta.Domain.Enums.UserRole.Inspector => "inspector",
                Inspecta.Domain.Enums.UserRole.TechnicalManager => "technical_manager",
                Inspecta.Domain.Enums.UserRole.Admin => "admin",
                _ => string.Empty
            };
            if (!string.Equals(tokenRole, currentRole, StringComparison.Ordinal))
            {
                context.Fail("Account role has changed. Sign in again.");
            }
        }
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<InspectaDbContext>();
    await dbContext.Database.MigrateAsync();
    await new BootstrapAdminSeeder(dbContext, scope.ServiceProvider.GetRequiredService<IConfiguration>())
        .SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
