using Microsoft.Extensions.Configuration;
using MySqlConnector;
using SeedRunner.Seeding;

Console.WriteLine("🌱 Property Analytics Database Seeder");
Console.WriteLine("=====================================\n");

// Load configuration (env vars override appsettings.json for Docker)
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("MySql")
    ?? throw new InvalidOperationException("MySql connection string not found");

// Test connection
Console.WriteLine("Testing database connection...");
await using (var testConnection = new MySqlConnection(connectionString))
{
    await testConnection.OpenAsync();
    Console.WriteLine("✅ Connected to MySQL\n");
}

// Run seeder
var options = new SeedOptions
{
    Customers = configuration.GetValue<int>("SeedOptions:Customers", 10),
    PropertiesPerCustomer = configuration.GetValue<int>("SeedOptions:PropertiesPerCustomer", 20),
    TenanciesPerCustomer = configuration.GetValue<int>("SeedOptions:TenanciesPerCustomer", 25),
    MaintenanceJobsPerCustomer = configuration.GetValue<int>("SeedOptions:MaintenanceJobsPerCustomer", 400)
};

Console.WriteLine($"Seed Configuration:");
Console.WriteLine($"  Customers: {options.Customers}");
Console.WriteLine($"  Properties per customer: {options.PropertiesPerCustomer}");
Console.WriteLine($"  Tenancies per customer: {options.TenanciesPerCustomer}");
Console.WriteLine($"  Maintenance jobs per customer: {options.MaintenanceJobsPerCustomer}\n");

var seeder = new DatabaseSeeder(connectionString, options);
await seeder.RunAsync();

Console.WriteLine("\n✅ Seeding complete!");
