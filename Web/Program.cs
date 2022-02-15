using Services;
using Services.Extensions;
using Services.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddScoped<LogUserActivity>();
builder.Services.AddIdentityServices(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddCors();
builder.Services.AddSignalR();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo() {Title = "WebAPIv5", Version = "v1"});
    c.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Scheme = "bearer"
    });
    c.OperationFilter<AuthorizationHeaderParameterOperationFilter>();
});


// Configure the HTTP request pipeline.

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "WebAPIv5 v1"));
}
else
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseMiddleware<ExceptionMiddleware>();

app.UseHttpsRedirection();

if (builder.Environment.IsDevelopment())
{
    app.UseCors(
        policy =>
            policy
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .WithOrigins(
                    builder.Configuration
                        .GetSection(nameof(ClientProperties))
                        .Get<ClientProperties>().BaseClientUrl
                )
    );
}

app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

// Endpoints
app.MapControllers();
app.MapHub<PresenceHub>("hubs/presence");
app.MapHub<MessageHub>("hubs/messages");
app.MapFallbackToController("Index", "Fallback");


using var scope = app.Services.CreateScope();

var services = scope.ServiceProvider;

try
{
    var context = services.GetRequiredService<DataContext>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var roleManager = services.GetRequiredService<RoleManager<AppRole>>();
    await context.Database.MigrateAsync();
    await SeedData.SeedUsers(userManager, roleManager);
}
catch (Exception exception)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    logger.LogError(exception, "An error occurred during migration");
    // Console.WriteLine($"Fail: An error occurred during migration, {new StackTrace(exception, true)}");
}

await app.RunAsync();