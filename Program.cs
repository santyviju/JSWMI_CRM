using JSWMI_CRM.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages(options =>
{
    // Razor Pages options
});

// Business Services
builder.Services.AddSingleton<ISupabaseService, SupabaseService>();
builder.Services.AddSingleton<ISapB1Service, SapB1Service>();
builder.Services.AddScoped<ICuttingPlanImportService, CuttingPlanImportService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
