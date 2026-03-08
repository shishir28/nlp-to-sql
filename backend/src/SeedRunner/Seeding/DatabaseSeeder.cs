using Bogus;
using Dapper;
using MySqlConnector;

namespace SeedRunner.Seeding;

public class DatabaseSeeder
{
    private readonly string _connectionString;
    private readonly SeedOptions _options;
    private readonly Randomizer _randomizer;

    public DatabaseSeeder(string connectionString, SeedOptions options)
    {
        _connectionString = connectionString;
        _options = options;
        _randomizer = new Randomizer(12345); // Fixed seed for reproducibility
    }

    public async Task RunAsync()
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        Console.WriteLine("🗑️  Cleaning existing data...");
        await CleanDatabase(connection);

        Console.WriteLine("� Seeding reference data...");
        await SeedReferenceData(connection);

        Console.WriteLine("👥 Seeding customers...");
        var customerIds = await SeedCustomers(connection);

        Console.WriteLine("🏘️  Seeding properties, owners, tenants, vendors...");
        foreach (var customerId in customerIds)
        {
            await SeedCustomerData(connection, customerId);
        }

        Console.WriteLine("✨ Seeding complete!");
    }

    private static async Task SeedReferenceData(MySqlConnection connection)
    {
        // Seed Australian States
        var states = new[]
        {
            ("NSW", "New South Wales"),
            ("VIC", "Victoria"),
            ("QLD", "Queensland"),
            ("WA", "Western Australia"),
            ("SA", "South Australia"),
            ("TAS", "Tasmania"),
            ("ACT", "Australian Capital Territory"),
            ("NT", "Northern Territory")
        };

        foreach (var (code, name) in states)
        {
            await connection.ExecuteAsync(@"
                INSERT IGNORE INTO RefStatesAU (StateCode, StateName) 
                VALUES (@StateCode, @StateName)",
                new { StateCode = code, StateName = name });
        }

        // Seed Status Codes
        var statusCodes = new[]
        {
            ("Tenancy", "ACTIVE", "Active", 1),
            ("Tenancy", "NOTICE", "Notice Given", 2),
            ("Tenancy", "ENDED", "Ended", 3),
            ("MaintenanceJob", "OPEN", "Open", 1),
            ("MaintenanceJob", "ASSIGNED", "Assigned to Vendor", 2),
            ("MaintenanceJob", "IN_PROGRESS", "In Progress", 3),
            ("MaintenanceJob", "COMPLETED", "Completed", 4),
            ("MaintenanceJob", "CLOSED", "Closed", 5),
            ("Inspection", "PENDING", "Pending", 1),
            ("Inspection", "COMPLETED", "Completed", 2),
            ("Inspection", "FAILED", "Failed", 3)
        };

        foreach (var (domain, code, label, sortOrder) in statusCodes)
        {
            await connection.ExecuteAsync(@"
                INSERT IGNORE INTO RefStatusCodes (Domain, Code, Label, SortOrder) 
                VALUES (@Domain, @Code, @Label, @SortOrder)",
                new { Domain = domain, Code = code, Label = label, SortOrder = sortOrder });
        }
    }

    private static async Task CleanDatabase(MySqlConnection connection)
    {
        await connection.ExecuteAsync("SET FOREIGN_KEY_CHECKS = 0");
        await connection.ExecuteAsync("TRUNCATE TABLE OwnerStatementLines");
        await connection.ExecuteAsync("TRUNCATE TABLE OwnerStatements");
        await connection.ExecuteAsync("TRUNCATE TABLE RentLedgerEntries");
        await connection.ExecuteAsync("TRUNCATE TABLE Inspections");
        await connection.ExecuteAsync("TRUNCATE TABLE MaintenanceJobs");
        await connection.ExecuteAsync("TRUNCATE TABLE Tenancies");
        await connection.ExecuteAsync("TRUNCATE TABLE PropertyOwners");
        await connection.ExecuteAsync("TRUNCATE TABLE Properties");
        await connection.ExecuteAsync("TRUNCATE TABLE Tenants");
        await connection.ExecuteAsync("TRUNCATE TABLE Vendors");
        await connection.ExecuteAsync("TRUNCATE TABLE Owners");
        await connection.ExecuteAsync("TRUNCATE TABLE Customers");
        await connection.ExecuteAsync("SET FOREIGN_KEY_CHECKS = 1");
    }

    private async Task<List<int>> SeedCustomers(MySqlConnection connection)
    {
        var faker = new Faker("en_AU");
        var customerIds = new List<int>();

        var customerFaker = new Faker<Customer>()
            .RuleFor(c => c.Name, f => f.Company.CompanyName())
            .RuleFor(c => c.Abn, f => GenerateAbn(f))
            .RuleFor(c => c.Email, f => f.Internet.Email())
            .RuleFor(c => c.CreatedAtUtc, f => f.Date.Past(3).ToUniversalTime())
            .RuleFor(c => c.IsActive, f => true);

        var customers = customerFaker.Generate(_options.Customers);

        foreach (var customer in customers)
        {
            var id = await connection.ExecuteScalarAsync<int>(@"
                INSERT INTO Customers (Name, Abn, Email, CreatedAtUtc, IsActive)
                VALUES (@Name, @Abn, @Email, @CreatedAtUtc, @IsActive);
                SELECT LAST_INSERT_ID();", customer);
            customerIds.Add(id);
        }

        Console.WriteLine($"  ✅ Seeded {customers.Count} customers");
        return customerIds;
    }

    private async Task SeedCustomerData(MySqlConnection connection, int customerId)
    {
        var faker = new Faker("en_AU");

        // Seed Owners
        var ownerIds = await SeedOwners(connection, customerId, faker);

        // Seed Vendors
        var vendorIds = await SeedVendors(connection, customerId, faker);

        // Seed Tenants
        var tenantIds = await SeedTenants(connection, customerId, faker);

        // Seed Properties
        var propertyIds = await SeedProperties(connection, customerId, faker);

        // Link Properties to Owners
        await SeedPropertyOwners(connection, customerId, propertyIds, ownerIds, faker);

        // Seed Tenancies
        var tenancyIds = await SeedTenancies(connection, customerId, propertyIds, tenantIds, faker);

        // Seed Rent Ledger Entries (with arrears patterns)
        await SeedRentLedgerEntries(connection, customerId, tenancyIds, faker);

        // Seed Maintenance Jobs
        await SeedMaintenanceJobs(connection, customerId, propertyIds, vendorIds, faker);

        // Seed Inspections
        await SeedInspections(connection, customerId, propertyIds, tenancyIds, faker);

        // Seed Owner Statements
        await SeedOwnerStatements(connection, customerId, ownerIds, faker);
    }

    private static async Task<List<int>> SeedOwners(MySqlConnection connection, int customerId, Faker faker)
    {
        var ownerIds = new List<int>();
        var ownerCount = faker.Random.Int(8, 15);

        for (int i = 0; i < ownerCount; i++)
        {
            var id = await connection.ExecuteScalarAsync<int>(@"
                INSERT INTO Owners (CustomerId, FullName, Email, Phone, Abn, CreatedAtUtc)
                VALUES (@CustomerId, @FullName, @Email, @Phone, @Abn, @CreatedAtUtc);
                SELECT LAST_INSERT_ID();",
                new
                {
                    CustomerId = customerId,
                    FullName = faker.Name.FullName(),
                    Email = faker.Internet.Email(),
                    Phone = faker.Phone.PhoneNumber("04## ### ###"),
                    Abn = GenerateAbn(faker),
                    CreatedAtUtc = faker.Date.Past(2).ToUniversalTime()
                });
            ownerIds.Add(id);
        }

        return ownerIds;
    }

    private static async Task<List<int>> SeedVendors(MySqlConnection connection, int customerId, Faker faker)
    {
        var vendorIds = new List<int>();
        var vendorCount = faker.Random.Int(5, 10);
        var vendorTypes = new[] { "Plumbing", "Electrical", "Painting", "Carpentry", "HVAC", "Landscaping", "Cleaning" };

        for (int i = 0; i < vendorCount; i++)
        {
            var category = faker.PickRandom(vendorTypes);
            var id = await connection.ExecuteScalarAsync<int>(@"
                INSERT INTO Vendors (CustomerId, VendorName, Category, Email, Phone, Abn, IsActive, CreatedAtUtc)
                VALUES (@CustomerId, @VendorName, @Category, @Email, @Phone, @Abn, @IsActive, @CreatedAtUtc);
                SELECT LAST_INSERT_ID();",
                new
                {
                    CustomerId = customerId,
                    VendorName = faker.Company.CompanyName() + " " + category,
                    Category = category,
                    Email = faker.Internet.Email(),
                    Phone = faker.Phone.PhoneNumber("04## ### ###"),
                    Abn = GenerateAbn(faker),
                    IsActive = true,
                    CreatedAtUtc = faker.Date.Past(2).ToUniversalTime()
                });
            vendorIds.Add(id);
        }

        return vendorIds;
    }

    private static async Task<List<int>> SeedTenants(MySqlConnection connection, int customerId, Faker faker)
    {
        var tenantIds = new List<int>();
        var tenantCount = faker.Random.Int(30, 50);

        for (int i = 0; i < tenantCount; i++)
        {
            var id = await connection.ExecuteScalarAsync<int>(@"
                INSERT INTO Tenants (CustomerId, FullName, Email, Phone, EmergencyContact, CreatedAtUtc)
                VALUES (@CustomerId, @FullName, @Email, @Phone, @EmergencyContact, @CreatedAtUtc);
                SELECT LAST_INSERT_ID();",
                new
                {
                    CustomerId = customerId,
                    FullName = faker.Name.FullName(),
                    Email = faker.Internet.Email(),
                    Phone = faker.Phone.PhoneNumber("04## ### ###"),
                    EmergencyContact = $"{faker.Name.FullName()} - {faker.Phone.PhoneNumber("04## ### ###")}",
                    CreatedAtUtc = faker.Date.Past(2).ToUniversalTime()
                });
            tenantIds.Add(id);
        }

        return tenantIds;
    }

    private async Task<List<int>> SeedProperties(MySqlConnection connection, int customerId, Faker faker)
    {
        var propertyIds = new List<int>();
        var states = new[] { "NSW", "VIC", "QLD", "WA", "SA", "TAS", "ACT", "NT" };
        var propertyTypes = new[] { "House", "Apartment", "Townhouse", "Unit" };

        for (int i = 0; i < _options.PropertiesPerCustomer; i++)
        {
            var state = faker.PickRandom(states);
            var id = await connection.ExecuteScalarAsync<int>(@"
                INSERT INTO Properties (CustomerId, AddressLine1, AddressLine2, Suburb, StateCode, Postcode, PropertyType, Bedrooms, Bathrooms, Parking, IsActive, CreatedAtUtc)
                VALUES (@CustomerId, @AddressLine1, @AddressLine2, @Suburb, @StateCode, @Postcode, @PropertyType, @Bedrooms, @Bathrooms, @Parking, @IsActive, @CreatedAtUtc);
                SELECT LAST_INSERT_ID();",
                new
                {
                    CustomerId = customerId,
                    AddressLine1 = faker.Address.StreetAddress(),
                    AddressLine2 = faker.Random.Bool(0.3f) ? faker.Address.SecondaryAddress() : null,
                    Suburb = faker.Address.City(),
                    StateCode = state,
                    Postcode = faker.Random.Int(2000, 7999).ToString(),
                    PropertyType = faker.PickRandom(propertyTypes),
                    Bedrooms = faker.Random.Int(1, 5),
                    Bathrooms = faker.Random.Int(1, 3),
                    Parking = faker.Random.Int(0, 2),
                    IsActive = true,
                    CreatedAtUtc = faker.Date.Past(3).ToUniversalTime()
                });
            propertyIds.Add(id);
        }

        Console.WriteLine($"    ✅ Seeded {propertyIds.Count} properties for customer {customerId}");
        return propertyIds;
    }

    private static async Task SeedPropertyOwners(MySqlConnection connection, int customerId, List<int> propertyIds, List<int> ownerIds, Faker faker)
    {
        // Each property has 1-2 owners
        foreach (var propertyId in propertyIds)
        {
            var ownerCount = faker.Random.Int(1, 2);
            var selectedOwners = faker.PickRandom(ownerIds, ownerCount);

            foreach (var ownerId in selectedOwners)
            {
                await connection.ExecuteAsync(@"
                    INSERT INTO PropertyOwners (CustomerId, PropertyId, OwnerId, OwnershipPct, StartDate, EndDate)
                    VALUES (@CustomerId, @PropertyId, @OwnerId, @OwnershipPct, @StartDate, @EndDate)",
                    new
                    {
                        CustomerId = customerId,
                        PropertyId = propertyId,
                        OwnerId = ownerId,
                        OwnershipPct = ownerCount == 1 ? 100.00m : 50.00m,
                        StartDate = faker.Date.Past(2),
                        EndDate = (DateTime?)null
                    });
            }
        }
    }

    private async Task<List<int>> SeedTenancies(MySqlConnection connection, int customerId, List<int> propertyIds, List<int> tenantIds, Faker faker)
    {
        var tenancyIds = new List<int>();
        var statuses = new[] { "ACTIVE", "ACTIVE", "ACTIVE", "ENDED", "ENDED" }; // 60% active
        var frequencies = new[] { "Weekly", "Weekly", "Fortnightly", "Monthly" };
        // Lease end months spread across next 6 months for lease renewal queries
        var upcomingLeaseEndMonths = new[] { 1, 2, 2, 3, 3, 4, 5, 6 };

        for (int i = 0; i < _options.TenanciesPerCustomer; i++)
        {
            var propertyId = faker.PickRandom(propertyIds);
            var tenantId = faker.PickRandom(tenantIds);
            var frequency = faker.PickRandom(frequencies);

            DateTime startDate;
            DateTime leaseEndDate;
            string status;

            if (i < 8)
            {
                // Guarantee 8 ACTIVE tenancies with lease end dates in the next 1–6 months
                status = "ACTIVE";
                startDate = faker.Date.Past(2);
                leaseEndDate = DateTime.UtcNow.AddMonths(upcomingLeaseEndMonths[i]).Date;
            }
            else
            {
                status = faker.PickRandom(statuses);
                startDate = faker.Date.Past(1);
                var leaseDuration = faker.Random.Int(6, 12);
                leaseEndDate = startDate.AddMonths(leaseDuration);
            }

            var endDate = status == "ENDED" ? leaseEndDate.AddDays(faker.Random.Int(-30, 30)) : (DateTime?)null;

            var rentPerWeek = faker.Random.Int(300, 800);
            var bondWeeks = 4;

            var id = await connection.ExecuteScalarAsync<int>(@"
                INSERT INTO Tenancies (CustomerId, PropertyId, TenantId, StartDate, EndDate, LeaseEndDate, 
                    RentAmount, RentFrequency, BondAmount, StatusCode, CreatedAtUtc)
                VALUES (@CustomerId, @PropertyId, @TenantId, @StartDate, @EndDate, @LeaseEndDate, 
                    @RentAmount, @RentFrequency, @BondAmount, @StatusCode, @CreatedAtUtc);
                SELECT LAST_INSERT_ID();",
                new
                {
                    CustomerId = customerId,
                    PropertyId = propertyId,
                    TenantId = tenantId,
                    StartDate = startDate,
                    EndDate = endDate,
                    LeaseEndDate = leaseEndDate,
                    RentAmount = (decimal)rentPerWeek,
                    RentFrequency = frequency,
                    BondAmount = (decimal)(rentPerWeek * bondWeeks),
                    StatusCode = status,
                    CreatedAtUtc = startDate.ToUniversalTime()
                });
            tenancyIds.Add(id);
        }

        Console.WriteLine($"    ✅ Seeded {tenancyIds.Count} tenancies for customer {customerId}");
        return tenancyIds;
    }

    private static async Task SeedRentLedgerEntries(MySqlConnection connection, int customerId, List<int> tenancyIds, Faker faker)
    {
        var count = 0;
        // Guarantee at least 5 tenancies (or 1/3 of all) have arrears so arrears queries always return data
        var guaranteedArrearsCount = Math.Max(5, tenancyIds.Count / 3);

        for (int t = 0; t < tenancyIds.Count; t++)
        {
            var tenancyId = tenancyIds[t];
            // First N tenancies guaranteed arrears; rest have 20% random chance
            var hasArrears = t < guaranteedArrearsCount || faker.Random.Bool(0.2f);
            var entries = faker.Random.Int(3, 12);

            for (int i = 0; i < entries; i++)
            {
                var dueDate = DateTime.UtcNow.AddDays(-i * 7);
                var rentAmount = faker.Random.Int(300, 600);
                var isPaid = !hasArrears || i >= 3;

                // Charge entry
                await connection.ExecuteAsync(@"
                    INSERT INTO RentLedgerEntries (CustomerId, TenancyId, EntryType, TransactionDate, DueDate, 
                        PaidDate, Amount, BalanceDelta, Description, Reference, CreatedAtUtc)
                    VALUES (@CustomerId, @TenancyId, @EntryType, @TransactionDate, @DueDate, 
                        @PaidDate, @Amount, @BalanceDelta, @Description, @Reference, @CreatedAtUtc)",
                    new
                    {
                        CustomerId = customerId,
                        TenancyId = tenancyId,
                        EntryType = "Charge",
                        TransactionDate = dueDate,
                        DueDate = dueDate,
                        PaidDate = (DateTime?)null,
                        Amount = (decimal)rentAmount,
                        BalanceDelta = (decimal)rentAmount,  // Charge increases balance
                        Description = "Weekly rent",
                        Reference = $"RENT-{dueDate:yyyyMMdd}",
                        CreatedAtUtc = dueDate
                    });
                count++;

                // Receipt entry if paid
                if (isPaid)
                {
                    var paidDate = dueDate.AddDays(faker.Random.Int(0, 5));
                    await connection.ExecuteAsync(@"
                        INSERT INTO RentLedgerEntries (CustomerId, TenancyId, EntryType, TransactionDate, DueDate, 
                            PaidDate, Amount, BalanceDelta, Description, Reference, CreatedAtUtc)
                        VALUES (@CustomerId, @TenancyId, @EntryType, @TransactionDate, @DueDate, 
                            @PaidDate, @Amount, @BalanceDelta, @Description, @Reference, @CreatedAtUtc)",
                        new
                        {
                            CustomerId = customerId,
                            TenancyId = tenancyId,
                            EntryType = "Receipt",
                            TransactionDate = paidDate,
                            DueDate = dueDate,
                            PaidDate = paidDate,
                            Amount = (decimal)rentAmount,
                            BalanceDelta = (decimal)(-rentAmount),  // Receipt decreases balance
                            Description = "Rent payment",
                            Reference = $"PAY-{paidDate:yyyyMMdd}",
                            CreatedAtUtc = paidDate
                        });
                    count++;
                }
            }
        }
        Console.WriteLine($"    ✅ Seeded {count} rent ledger entries for customer {customerId}");
    }

    private async Task<List<int>> SeedMaintenanceJobs(MySqlConnection connection, int customerId, List<int> propertyIds, List<int> vendorIds, Faker faker)
    {
        var jobIds = new List<int>();
        var statuses = new[] { "OPEN", "OPEN", "IN_PROGRESS", "COMPLETED", "COMPLETED", "COMPLETED" };
        var priorities = new[] { "LOW", "LOW", "MEDIUM", "MEDIUM", "HIGH" };
        var categories = new[]
        {
            "Plumbing", "Windows", "HVAC", "Plumbing", "Carpeting", "Painting",
            "Fencing", "Locksmith", "Plumbing", "Electrical", "Roofing", "Landscaping"
        };
        var summaries = new[]
        {
            "Leaking tap", "Broken window", "Faulty air conditioner", "Blocked drain",
            "Damaged carpet", "Painting required", "Fence repair", "Door lock replacement",
            "Hot water system", "Electrical fault", "Roof leak", "Garden maintenance"
        };

        for (int i = 0; i < _options.MaintenanceJobsPerCustomer; i++)
        {
            var propertyId = faker.PickRandom(propertyIds);
            var vendorId = vendorIds.Count > 0 ? faker.PickRandom(vendorIds) : (int?)null;
            var status = faker.PickRandom(statuses);
            var categoryIndex = faker.Random.Int(0, categories.Length - 1);
            var openedDate = faker.Date.Past(90).ToUniversalTime();
            var closedDate = status == "COMPLETED" ? openedDate.AddDays(faker.Random.Int(1, 14)) : (DateTime?)null;

            var id = await connection.ExecuteScalarAsync<int>(@"
                INSERT INTO MaintenanceJobs (CustomerId, PropertyId, TenancyId, VendorId, StatusCode, 
                    Priority, Category, Summary, Description, OpenedAtUtc, ClosedAtUtc, EstimatedCost, ActualCost)
                VALUES (@CustomerId, @PropertyId, @TenancyId, @VendorId, @StatusCode, 
                    @Priority, @Category, @Summary, @Description, @OpenedAtUtc, @ClosedAtUtc, @EstimatedCost, @ActualCost);
                SELECT LAST_INSERT_ID();",
                new
                {
                    CustomerId = customerId,
                    PropertyId = propertyId,
                    TenancyId = (int?)null,  // Optional - not all maintenance is tied to a tenancy
                    VendorId = vendorId,
                    StatusCode = status,
                    Priority = faker.PickRandom(priorities),
                    Category = categories[categoryIndex],
                    Summary = summaries[categoryIndex],
                    Description = faker.Lorem.Sentence(),
                    OpenedAtUtc = openedDate,
                    ClosedAtUtc = closedDate,
                    EstimatedCost = (decimal?)faker.Random.Int(100, 2000),
                    ActualCost = status == "COMPLETED" ? (decimal?)faker.Random.Int(100, 2000) : null
                });
            jobIds.Add(id);
        }

        Console.WriteLine($"    ✅ Seeded {jobIds.Count} maintenance jobs for customer {customerId}");
        return jobIds;
    }

    private static async Task SeedInspections(MySqlConnection connection, int customerId, List<int> propertyIds, List<int> tenancyIds, Faker faker)
    {
        var count = 0;
        var types = new[] { "Routine", "Entry", "Exit", "Compliance" };
        // Weighted so completed past inspections produce realistic mix of PASS/FAIL/PENDING
        var completedStatuses = new[] { "PASS", "PASS", "PASS", "FAIL", "FAIL", "PENDING" };

        // --- Guaranteed block: 6 FAIL + 6 PASS compliance inspections with past dates ---
        var failNotes = new[]
        {
            "Smoke alarm not functioning", "Pool fence non-compliant", "Electrical hazard detected",
            "Gas leak identified", "Mould infestation found", "Emergency exit blocked"
        };
        for (int i = 0; i < 6 && i < propertyIds.Count; i++)
        {
            var scheduledDate = DateTime.UtcNow.AddDays(-faker.Random.Int(7, 180));
            var completedDate = scheduledDate.AddDays(faker.Random.Int(0, 3));
            await connection.ExecuteAsync(@"
                INSERT INTO Inspections (CustomerId, PropertyId, TenancyId, InspectionType, ScheduledDate,
                    CompletedDate, ComplianceStatus, Notes, InspectorName, CreatedAtUtc)
                VALUES (@CustomerId, @PropertyId, @TenancyId, @InspectionType, @ScheduledDate,
                    @CompletedDate, @ComplianceStatus, @Notes, @InspectorName, @CreatedAtUtc)",
                new
                {
                    CustomerId = customerId,
                    PropertyId = propertyIds[i],
                    TenancyId = faker.PickRandom(tenancyIds),
                    InspectionType = "Compliance",
                    ScheduledDate = scheduledDate,
                    CompletedDate = (DateTime?)completedDate,
                    ComplianceStatus = "FAIL",
                    Notes = failNotes[i],
                    InspectorName = faker.Name.FullName(),
                    CreatedAtUtc = scheduledDate.ToUniversalTime()
                });
            count++;
        }
        for (int i = 0; i < 6 && i < propertyIds.Count; i++)
        {
            var idx = Math.Min(i + 6, propertyIds.Count - 1);
            var scheduledDate = DateTime.UtcNow.AddDays(-faker.Random.Int(7, 120));
            var completedDate = scheduledDate.AddDays(faker.Random.Int(0, 2));
            await connection.ExecuteAsync(@"
                INSERT INTO Inspections (CustomerId, PropertyId, TenancyId, InspectionType, ScheduledDate,
                    CompletedDate, ComplianceStatus, Notes, InspectorName, CreatedAtUtc)
                VALUES (@CustomerId, @PropertyId, @TenancyId, @InspectionType, @ScheduledDate,
                    @CompletedDate, @ComplianceStatus, @Notes, @InspectorName, @CreatedAtUtc)",
                new
                {
                    CustomerId = customerId,
                    PropertyId = propertyIds[idx],
                    TenancyId = faker.PickRandom(tenancyIds),
                    InspectionType = faker.PickRandom(new[] { "Routine", "Compliance" }),
                    ScheduledDate = scheduledDate,
                    CompletedDate = (DateTime?)completedDate,
                    ComplianceStatus = "PASS",
                    Notes = faker.Lorem.Sentence(),
                    InspectorName = faker.Name.FullName(),
                    CreatedAtUtc = scheduledDate.ToUniversalTime()
                });
            count++;
        }

        // --- Random block: mix of past (completed) and future (pending) ---
        foreach (var propertyId in propertyIds.Take(faker.Random.Int(8, 15)))
        {
            for (int i = 0; i < faker.Random.Int(1, 3); i++)
            {
                var tenancyId = faker.PickRandom(tenancyIds);
                // 50% past, 50% future — fixes the "always PENDING" bug
                var usePastDate = faker.Random.Bool(0.5f);
                var scheduledDate = usePastDate ? faker.Date.Past(180) : faker.Date.Future(90);
                var isCompleted = scheduledDate < DateTime.UtcNow;
                var complianceStatus = isCompleted ? faker.PickRandom(completedStatuses) : "PENDING";
                var completedDate = isCompleted ? scheduledDate.AddDays(faker.Random.Int(0, 2)) : (DateTime?)null;

                await connection.ExecuteAsync(@"
                    INSERT INTO Inspections (CustomerId, PropertyId, TenancyId, InspectionType, ScheduledDate,
                        CompletedDate, ComplianceStatus, Notes, InspectorName, CreatedAtUtc)
                    VALUES (@CustomerId, @PropertyId, @TenancyId, @InspectionType, @ScheduledDate,
                        @CompletedDate, @ComplianceStatus, @Notes, @InspectorName, @CreatedAtUtc)",
                    new
                    {
                        CustomerId = customerId,
                        PropertyId = propertyId,
                        TenancyId = tenancyId,
                        InspectionType = faker.PickRandom(types),
                        ScheduledDate = scheduledDate,
                        CompletedDate = completedDate,
                        ComplianceStatus = complianceStatus,
                        Notes = isCompleted ? faker.Lorem.Sentence() : null,
                        InspectorName = isCompleted ? faker.Name.FullName() : null,
                        CreatedAtUtc = faker.Date.Past(30).ToUniversalTime()
                    });
                count++;
            }
        }
        Console.WriteLine($"    ✅ Seeded {count} inspections for customer {customerId} (guaranteed 6 FAIL + 6 PASS)");
    }

    private static async Task SeedOwnerStatements(MySqlConnection connection, int customerId, List<int> ownerIds, Faker faker)
    {
        var count = 0;
        var months = 6;

        foreach (var ownerId in ownerIds)
        {
            for (int i = 0; i < months; i++)
            {
                var periodEnd = DateTime.UtcNow.AddMonths(-i).Date;
                var periodStart = periodEnd.AddMonths(-1);
                var grossIncome = faker.Random.Decimal(2000, 6000);
                var expenses = faker.Random.Decimal(200, 1000);
                var managementFees = grossIncome * 0.08m; // 8% management fee
                var netPayout = grossIncome - expenses - managementFees;

                await connection.ExecuteAsync(@"
                    INSERT INTO OwnerStatements (CustomerId, OwnerId, PeriodStart, PeriodEnd, 
                        GrossIncome, Expenses, ManagementFees, NetPayout)
                    VALUES (@CustomerId, @OwnerId, @PeriodStart, @PeriodEnd, 
                        @GrossIncome, @Expenses, @ManagementFees, @NetPayout)",
                    new
                    {
                        CustomerId = customerId,
                        OwnerId = ownerId,
                        PeriodStart = periodStart,
                        PeriodEnd = periodEnd,
                        GrossIncome = grossIncome,
                        Expenses = expenses,
                        ManagementFees = managementFees,
                        NetPayout = netPayout
                    });
                count++;
            }
        }
        Console.WriteLine($"    ✅ Seeded {count} owner statements for customer {customerId}");
    }

    private static string GenerateAbn(Faker faker)
    {
        return $"{faker.Random.Int(10, 99)} {faker.Random.Int(100, 999)} {faker.Random.Int(100, 999)} {faker.Random.Int(100, 999)}";
    }

    // Entity models for Bogus
    private class Customer
    {
        public string Name { get; set; } = string.Empty;
        public string? Abn { get; set; }
        public string? Email { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public bool IsActive { get; set; }
    }
}
