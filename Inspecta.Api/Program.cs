using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Inspecta.Api.Middleware;
using Inspecta.Api.OpenApi;
using Inspecta.Api.BackgroundServices;
using Inspecta.Api.Authorization;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Infrastructure.Data;
using Inspecta.Infrastructure.Mail;
using Inspecta.Infrastructure.Pdf;
using Inspecta.Infrastructure.Repositories;
using Inspecta.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AuthExceptionHandler>();
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower)));
builder.Services.AddEndpointsApiExplorer();
var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var csvOrigins = builder.Configuration["Cors:AllowedOriginsCsv"];
var allowedOrigins = configuredOrigins
    .Concat(string.IsNullOrWhiteSpace(csvOrigins)
        ? []
        : csvOrigins.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    if (allowedOrigins.Length > 0)
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
}));
builder.Services.AddSwaggerGen(options =>
{
    options.MapType<DateOnly>(() => new Microsoft.OpenApi.Models.OpenApiSchema { Type = "string", Format = "date", Example = new Microsoft.OpenApi.Any.OpenApiString("2026-11-05") });
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Inspecta API",
        Version = "v1",
        Description = "JWT-secured API for Inspecta inspection workflows, technical managers, and user management."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a valid access token. The Bearer prefix is added automatically."
    });
    options.OperationFilter<AuthorizeOperationFilter>();

    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

    // After the XML comments, so the workflow titles and tag order are the last word.
    options.OperationFilter<WorkflowOperationFilter>();
    options.DocumentFilter<WorkflowDocumentFilter>();
});
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentTenant>();

var connectionString = builder.Configuration.GetConnectionString("Inspecta")
    ?? throw new InvalidOperationException("Set the ConnectionStrings:Inspecta PostgreSQL connection string.");
builder.Services.AddDbContext<InspectaDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<ICoordinatorEmailConfigRepository, EfCoordinatorEmailConfigRepository>();
builder.Services.AddScoped<IEmailSyncRepository, EfEmailSyncRepository>();
builder.Services.AddScoped<IQuotationDraftRepository, EfQuotationDraftRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<ICoordinatorEmailConfigService, CoordinatorEmailConfigService>();
builder.Services.AddScoped<IQuotationDraftService, QuotationDraftService>();
builder.Services.AddScoped<IEmailParserService, EmailParserService>();
builder.Services.AddScoped<IRequestRepository, EfRequestRepository>();
builder.Services.AddScoped<IClientMailRuleRepository, EfClientMailRuleRepository>();
builder.Services.AddScoped<IClientMailRuleService, ClientMailRuleService>();
builder.Services.AddScoped<IEmailBacklogService, EmailBacklogService>();
builder.Services.AddScoped<IOutboundEmailRepository, EfOutboundEmailRepository>();
builder.Services.AddSingleton(new QuoteDeliveryOptions(builder.Configuration.GetValue("QuoteDelivery:Enabled", true)));
builder.Services.AddScoped<IQuoteDeliveryService, QuoteDeliveryService>();
builder.Services.AddSingleton<IEmailSender, MailKitSmtpEmailSender>();
builder.Services.AddPdfGeneration(builder.Configuration);
builder.Services.AddScoped<IInspectionRequestService, InspectionRequestService>();
builder.Services.AddScoped<IClientRepository, EfClientRepository>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IEquipmentRepository, EfEquipmentRepository>();
builder.Services.AddScoped<IEquipmentCatalog, EfEquipmentCatalog>();
builder.Services.AddScoped<IRequestEquipmentRegistrar, RequestEquipmentRegistrar>();
builder.Services.AddScoped<IEquipmentService, EquipmentService>();
builder.Services.AddScoped<IQuoteRepository, EfQuoteRepository>();
builder.Services.AddScoped<IQuoteService, QuoteService>();
builder.Services.AddScoped<ICertificateRepository, EfCertificateRepository>();
builder.Services.AddScoped<ICertificateService, CertificateService>();
builder.Services.AddScoped<ICertificateFileRepository, EfCertificateFileRepository>();
builder.Services.AddScoped<ICertificateDeliveryRepository, EfCertificateDeliveryRepository>();
builder.Services.AddSingleton(new Inspecta.Application.Certificates.CertificateOptions(
    builder.Configuration["Certificates:PublicBaseUrl"] is { Length: > 0 } publicBaseUrl ? publicBaseUrl : "https://api.gitsplatform.com"));
builder.Services.AddSingleton<IQrCodeRenderer, QrCodeRenderer>();
builder.Services.AddScoped<IMachineInspectionService, MachineInspectionService>();
builder.Services.AddScoped<Inspecta.Application.Services.ClientCertificateService>();
builder.Services.AddSingleton(new Inspecta.Application.Services.CertificateDeliveryOptions(builder.Configuration.GetValue("CertificateDelivery:Enabled", false)));
builder.Services.AddScoped<Inspecta.Application.Services.CertificateDeliveryService>();
// The public page behind a certificate's QR code is open to everyone, so it is limited per address
// (the first X-Forwarded-For address when behind the host's proxy).
builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiter.AddPolicy(Inspecta.Api.Controllers.VerificationController.RateLimitPolicy, context =>
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim();
        var key = forwarded.Length > 0 ? forwarded : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(key, _ =>
            new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});
builder.Services.AddScoped<IPasswordService, IdentityPasswordService>();
builder.Services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();
builder.Services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
builder.Services.AddHostedService<EmailSyncBackgroundService>();
builder.Services.AddHostedService<MaybeCleanupBackgroundService>();

var signingKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
{
    throw new InvalidOperationException("Set Jwt:SigningKey to a secret containing at least 32 UTF-8 bytes.");
}

var issuer = builder.Configuration["Jwt:Issuer"] ?? "Inspecta.Api";
var audience = builder.Configuration["Jwt:Audience"] ?? "Inspecta.Client";
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
                .Select(user => new
                {
                    user.Id,
                    user.IsActive,
                    user.Role,
                    user.ManagerId,
                    user.SubscriptionStatus,
                    user.SubscriptionEndDate
                })
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
            if (currentUser is null || !currentUser.IsActive)
            {
                context.Fail("Account is disabled or no longer exists.");
                return;
            }

            if (currentUser.Role == Inspecta.Domain.Enums.UserRole.TechnicalManager)
            {
                if (currentUser.SubscriptionStatus != Inspecta.Domain.Enums.SubscriptionStatus.Active ||
                    currentUser.SubscriptionEndDate is { } managerEndDate && managerEndDate <= DateTimeOffset.UtcNow)
                {
                    context.Fail("The technical manager subscription is suspended or expired.");
                    return;
                }
            }
            else if (currentUser.Role != Inspecta.Domain.Enums.UserRole.Admin)
            {
                if (currentUser.ManagerId is not Guid managerId)
                {
                    context.Fail("The account is not assigned to a technical manager.");
                    return;
                }

                var manager = await dbContext.Users.AsNoTracking()
                    .Where(user => user.Id == managerId &&
                        user.IsActive &&
                        user.Role == Inspecta.Domain.Enums.UserRole.TechnicalManager)
                    .Select(user => new { user.SubscriptionStatus, user.SubscriptionEndDate })
                    .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
                if (manager is null ||
                    manager.SubscriptionStatus != Inspecta.Domain.Enums.SubscriptionStatus.Active ||
                    manager.SubscriptionEndDate is { } teamEndDate && teamEndDate <= DateTimeOffset.UtcNow)
                {
                    context.Fail("The technical manager subscription is suspended or expired.");
                    return;
                }
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
                return;
            }

            var expectedTenantId = currentUser.Role switch
            {
                Inspecta.Domain.Enums.UserRole.Admin => (Guid?)null,
                Inspecta.Domain.Enums.UserRole.TechnicalManager => currentUser.Id,
                _ => currentUser.ManagerId
            };
            var tokenTenantId = context.Principal?.FindFirst("tenant_id")?.Value;
            var expectedManagerId = currentUser.ManagerId?.ToString();
            var tokenManagerId = context.Principal?.FindFirst("manager_id")?.Value;
            var tokenUserId = context.Principal?.FindFirst("user_id")?.Value;
            if (!string.Equals(tokenUserId, currentUser.Id.ToString(), StringComparison.Ordinal) ||
                !string.Equals(tokenTenantId, expectedTenantId?.ToString(), StringComparison.Ordinal) ||
                !string.Equals(tokenManagerId, expectedManagerId, StringComparison.Ordinal))
            {
                context.Fail("Account tenant assignment has changed. Sign in again.");
            }
        }
    };
});
builder.Services.AddInspectaAuthorization();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<InspectaDbContext>();
    await dbContext.Database.MigrateAsync();
    await new BootstrapAdminSeeder(dbContext, scope.ServiceProvider.GetRequiredService<IConfiguration>())
        .SeedAsync();
}

if (app.Configuration.GetValue("Swagger:Enabled", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None));
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

app.Run();
