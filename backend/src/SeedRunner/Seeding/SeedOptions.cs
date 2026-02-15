namespace SeedRunner.Seeding;

public class SeedOptions
{
    public int Customers { get; set; } = 10;
    public int PropertiesPerCustomer { get; set; } = 20;
    public int TenanciesPerCustomer { get; set; } = 25;
    public int MaintenanceJobsPerCustomer { get; set; } = 400;
}
