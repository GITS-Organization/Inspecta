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
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders(Inspecta.Api.PageRequest.TotalHeader);
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
// The pool is capped explicitly (Npgsql's default is 100 per instance, more than a small Postgres plan allows) unless the connection string says otherwise.
var connectionBuilder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
if (connectionString.IndexOf("Pool Size", StringComparison.OrdinalIgnoreCase) < 0 && connectionString.IndexOf("MaxPoolSize", StringComparison.OrdinalIgnoreCase) < 0)
    connectionBuilder.MaxPoolSize = builder.Configuration.GetValue("Database:MaxPoolSize", 20);
builder.Services.AddDbContext<InspectaDbContext>(options => options.UseNpgsql(connectionBuilder.ConnectionString));
builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<ICoordinatorEmailConfigRepository, EfCoordinatorEmailConfigRepository>();
builder.Services.AddScoped<ICompanyRepository, EfCompanyRepository>();
builder.Services.AddScoped<ICompanyGate, CompanyGate>();
builder.Services.AddScoped<IEmailSyncRepository, EfEmailSyncRepository>();
builder.Services.AddScoped<IQuotationDraftRepository, EfQuotationDraftRepository>();
builder.Services.AddSingleton<ILoginAttemptTracker, LoginAttemptTracker>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<ICoordinatorEmailConfigService, CoordinatorEmailConfigService>();
builder.Services.AddScoped<IQuotationDraftService, QuotationDraftService>();
builder.Services.AddScoped<IEmailParserService, EmailParserService>();
builder.Services.AddScoped<IRequestRepository, EfRequestRepository>();
builder.Services.AddScoped<IClientMailRuleRepository, EfClientMailRuleRepository>();
builder.Services.AddScoped<ICompanyProfileRepository, EfCompanyProfileRepository>();
builder.Services.AddScoped<IDashboardRepository, EfDashboardRepository>();
builder.Services.AddScoped<IClientReplyRepository, EfClientReplyRepository>();
builder.Services.AddScoped<IClientReplyService, ClientReplyService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<CompanyProfileService>();
builder.Services.AddScoped<ICompanyProfileService>(provider => provider.GetRequiredService<CompanyProfileService>());
builder.Services.AddScoped<ICompanyBrandingProvider>(provider => provider.GetRequiredService<CompanyProfileService>());
builder.Services.AddScoped<IClientMailRuleService, ClientMailRuleService>();
builder.Services.AddScoped<IEmailBacklogService, EmailBacklogService>();
builder.Services.AddScoped<IOutboundEmailRepository, EfOutboundEmailRepository>();
builder.Services.AddSingleton(new QuoteDeliveryOptions(builder.Configuration.GetValue("QuoteDelivery:Enabled", true)));
builder.Services.AddScoped<IQuoteDeliveryService, QuoteDeliveryService>();
builder.Services.AddSingleton<IEmailSender, MailKitSmtpEmailSender>();
builder.Services.AddSingleton<IEmailBranding, Inspecta.Infrastructure.Services.EmailBrandingProvider>();
builder.Services.AddPdfGeneration(builder.Configuration);
builder.Services.AddScoped<IInspectionRequestService, InspectionRequestService>();
builder.Services.AddScoped<IRequestTimelineService, RequestTimelineService>();
builder.Services.AddScoped<IRequestTransferService, RequestTransferService>();
builder.Services.AddScoped<IReviewerService, ReviewerService>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<IClientRepository, EfClientRepository>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IEquipmentRepository, EfEquipmentRepository>();
builder.Services.AddScoped<IEquipmentCatalog, EfEquipmentCatalog>();
builder.Services.AddScoped<IMachineSuggestionService, MachineSuggestionService>();
builder.Services.AddScoped<IRequestEquipmentRegistrar>(services => new RequestEquipmentRegistrar(
    services.GetRequiredService<IEquipmentCatalog>(), builder.Configuration.GetValue("Requests:AutoRegisterEquipment", true)));
builder.Services.AddScoped<IEquipmentService, EquipmentService>();
builder.Services.AddScoped<IQuoteRepository, EfQuoteRepository>();
builder.Services.AddScoped<IInvoiceRepository, EfInvoiceRepository>();
builder.Services.AddScoped<IInvoiceDeliveryService, InvoiceDeliveryService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IExecutiveTargetsRepository, EfExecutiveTargetsRepository>();
builder.Services.AddScoped<IExecutiveDataSource, EfExecutiveDataSource>();
builder.Services.AddScoped<IExecutiveService, ExecutiveService>();
builder.Services.AddScoped<IQuoteService, QuoteService>();
builder.Services.AddScoped<ICertificateRepository, EfCertificateRepository>();
builder.Services.AddScoped<ICertificateService, CertificateService>();
builder.Services.AddScoped<ICertificateFileRepository, EfCertificateFileRepository>();
builder.Services.AddScoped<ICertificateDeliveryRepository, EfCertificateDeliveryRepository>();
builder.Services.AddSingleton(new Inspecta.Application.Certificates.CertificateOptions(
    builder.Configuration["Certificates:PublicBaseUrl"] is { Length: > 0 } publicBaseUrl ? publicBaseUrl : "https://api.gitsplatform.com"));
builder.Services.AddSingleton<IQrCodeRenderer, QrCodeRenderer>();
builder.Services.AddScoped<ITransactionRunner, EfTransactionRunner>();
builder.Services.AddScoped<IMachineInspectionService, MachineInspectionService>();
builder.Services.AddScoped<IDirectCertificationService, DirectCertificationService>();
builder.Services.AddScoped<IJobMachineService, JobMachineService>();
builder.Services.AddScoped<Inspecta.Application.Services.ClientCertificateService>();
builder.Services.AddSingleton(new Inspecta.Application.Services.CertificateDeliveryOptions(builder.Configuration.GetValue("CertificateDelivery:Enabled", false)));
builder.Services.AddScoped<Inspecta.Application.Services.CertificateDeliveryService>();
// Behind the host's proxy the caller's address is the one the proxy appended to X-Forwarded-For (the last hop): the middleware takes exactly
// that one (ForwardLimit 1), so a client cannot choose its own address by sending the header itself. The proxy's address is not fixed, so the
// trusted lists are cleared; the app is only reachable through the proxy.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
// JSON lists can be large; the PDFs and zips are already compressed and are left alone.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ["application/json", "application/problem+json", "text/html"];
});
// Limits per caller address. The public certificate page is open to everyone; sign-in and token refresh are limited so that a password cannot be guessed quickly.
builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    static System.Threading.RateLimiting.RateLimitPartition<string> PerAddress(HttpContext context, int permits) =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
    limiter.AddPolicy(Inspecta.Api.Controllers.VerificationController.RateLimitPolicy, context => PerAddress(context, 60));
    limiter.AddPolicy(Inspecta.Api.Controllers.AuthController.LoginRateLimitPolicy, context => PerAddress(context, 10));
    limiter.AddPolicy(Inspecta.Api.Controllers.AuthController.RefreshRateLimitPolicy, context => PerAddress(context, 60));
    limiter.AddPolicy(Inspecta.Api.Controllers.MicrosoftMailboxController.CallbackRateLimitPolicy, context => PerAddress(context, 30));
});
builder.Services.AddScoped<IPasswordService, IdentityPasswordService>();
builder.Services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();
builder.Services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
// The Microsoft sign-in for Outlook mailboxes: off (not configured) until the MicrosoftOAuth settings are set.
builder.Services.AddSingleton(builder.Configuration.GetSection("MicrosoftOAuth").Get<Inspecta.Application.Services.MicrosoftOAuthOptions>()
    ?? new Inspecta.Application.Services.MicrosoftOAuthOptions());
builder.Services.AddHttpClient<IMicrosoftOAuthClient, Inspecta.Infrastructure.Mail.MicrosoftOAuthClient>(client =>
    client.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddSingleton<Inspecta.Application.Services.MailboxTokenCache>();
builder.Services.AddScoped<IMailboxAuthenticator, Inspecta.Application.Services.MailboxAuthenticator>();
builder.Services.AddScoped<IMicrosoftMailboxConnectionService, Inspecta.Application.Services.MicrosoftMailboxConnectionService>();
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
                    user.ManagerId
                })
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
            if (currentUser is null || !currentUser.IsActive)
            {
                context.Fail("Account is disabled or no longer exists.");
                return;
            }

            // Everyone but the platform owner works inside a company that must be switched on with a valid subscription.
            var companyId = currentUser.ManagerId ?? (currentUser.Role == Inspecta.Domain.Enums.UserRole.TechnicalManager ? currentUser.Id : (Guid?)null);
            if (currentUser.Role != Inspecta.Domain.Enums.UserRole.Admin)
            {
                if (companyId is not Guid ownCompanyId)
                {
                    context.Fail("The account does not belong to a company.");
                    return;
                }

                var company = await dbContext.Companies.AsNoTracking()
                    .Where(item => item.Id == ownCompanyId && item.IsActive)
                    .Select(item => new { item.SubscriptionStatus, item.SubscriptionEndDate })
                    .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
                if (company is null ||
                    company.SubscriptionStatus != Inspecta.Domain.Enums.SubscriptionStatus.Active ||
                    company.SubscriptionEndDate is { } endDate && endDate <= DateTimeOffset.UtcNow)
                {
                    context.Fail("The company subscription is suspended or expired.");
                    return;
                }
            }

            var tokenRole = context.Principal?.FindFirst("role")?.Value;
            var currentRole = Inspecta.Application.Authorization.RoleNames.Of(currentUser.Role);
            if (!string.Equals(tokenRole, currentRole, StringComparison.Ordinal))
            {
                context.Fail("Account role has changed. Sign in again.");
                return;
            }

            var expectedTenantId = currentUser.Role switch
            {
                Inspecta.Domain.Enums.UserRole.Admin => (Guid?)null,
                _ => companyId
            };
            var tokenTenantId = context.Principal?.FindFirst("tenant_id")?.Value;
            var tokenManagerId = context.Principal?.FindFirst("manager_id")?.Value;
            // A technical manager signed in before companies existed holds a token without this claim: it is accepted until it expires.
            var expectedManagerId = currentUser.Role == Inspecta.Domain.Enums.UserRole.TechnicalManager && tokenManagerId is null ? null : currentUser.ManagerId?.ToString();
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
    // PostgreSQL gets the migrations; a test host that swaps in another provider just creates the schema.
    if (dbContext.Database.IsNpgsql()) await dbContext.Database.MigrateAsync();
    else await dbContext.Database.EnsureCreatedAsync();
    await new BootstrapAdminSeeder(dbContext, scope.ServiceProvider.GetRequiredService<IConfiguration>())
        .SeedAsync();
}

if (app.Configuration.GetValue("Swagger:Enabled", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None));
}

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.Use(async (context, next) =>
{
    // Nothing of the API is meant to be framed, sniffed or to leak the address of the page that linked to it.
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.UseResponseCompression();
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

/// <summary>Lets the HTTP-level tests start the whole API in memory.</summary>
public partial class Program;
