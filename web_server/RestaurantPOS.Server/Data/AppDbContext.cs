using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RestaurantPOS.Shared.Enums;

namespace RestaurantPOS.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<TableEntity> Tables => Set<TableEntity>();
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
    public DbSet<ProductEntity> Products => Set<ProductEntity>();
    public DbSet<ProductOptionGroupEntity> ProductOptionGroups => Set<ProductOptionGroupEntity>();
    public DbSet<ProductOptionItemEntity> ProductOptionItems => Set<ProductOptionItemEntity>();
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    public DbSet<OrderItemEntity> OrderItems => Set<OrderItemEntity>();
    public DbSet<OrderItemOptionEntity> OrderItemOptions => Set<OrderItemOptionEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<IngredientEntity> Ingredients => Set<IngredientEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OrderEntity>()
            .HasIndex(o => o.OrderNumber)
            .IsUnique();

        modelBuilder.Entity<OrderEntity>()
            .HasIndex(o => o.ClientRequestId)
            .IsUnique();

        modelBuilder.Entity<UserEntity>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<ProductEntity>()
            .HasIndex(p => p.Code)
            .IsUnique();

        modelBuilder.Entity<IngredientEntity>()
            .HasIndex(i => i.Code)
            .IsUnique();
    }

    public static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password + "RestaurantPOS_Salt_2026"));
        return Convert.ToBase64String(bytes);
    }

    private static readonly object _seedLock = new();

    public void SeedInitialData(string? customAdminUser = null, string? customAdminPassword = null, string? storeName = null, bool isDemoStore = false)
    {
        lock (_seedLock)
        {
            try
            {
                SeedInitialDataInternal(customAdminUser, customAdminPassword, storeName, isDemoStore);
            }
            catch (Exception)
            {
                // Concurrently seeded by another thread, safe to ignore
            }
        }
    }

    private void SeedInitialDataInternal(string? customAdminUser, string? customAdminPassword, string? storeName, bool isDemoStore)
    {
        var adminUsername = string.IsNullOrWhiteSpace(customAdminUser) ? "admin" : customAdminUser.Trim();
        var adminPassword = string.IsNullOrWhiteSpace(customAdminPassword) ? "psoft123" : customAdminPassword.Trim();

        // 1. Always ensure Admin and Cashier accounts exist so store owner can log in
        if (!Users.Any())
        {
            Users.AddRange(
                new UserEntity
                {
                    Username = adminUsername,
                    PasswordHash = HashPassword(adminPassword),
                    FullName = string.IsNullOrWhiteSpace(storeName) ? "ผู้ดูแลระบบ" : $"ผู้ดูแลระบบ {storeName}",
                    Role = UserRole.SuperAdmin,
                    IsActive = true,
                    PermissionsJson = "[\"All\"]"
                },
                new UserEntity
                {
                    Username = "cashier",
                    PasswordHash = HashPassword("psoft123"),
                    FullName = "พนักงานหน้าร้าน",
                    Role = UserRole.Cashier,
                    IsActive = true,
                    PermissionsJson = "[\"ViewPOS\",\"CreateOrder\",\"EditOrder\",\"Payment\"]"
                }
            );
            SaveChanges();
        }
        else
        {
            var adminUser = Users.FirstOrDefault(u => u.Username == adminUsername);
            if (adminUser != null && !string.IsNullOrWhiteSpace(customAdminPassword))
            {
                adminUser.PasswordHash = HashPassword(adminPassword);
                adminUser.IsActive = true;
                SaveChanges();
            }
        }

        // If this is NOT a demo store (e.g. newly registered real store), keep the store 100% clean and empty!
        if (!isDemoStore)
        {
            return;
        }

        // 2. Demo Store Only (DEFAULT): Seed Tables, Categories, Sample Menu, and Ingredients
        if (!Tables.Any())
        {
            var tables = new List<TableEntity>();
            for (int i = 1; i <= 12; i++)
            {
                tables.Add(new TableEntity
                {
                    TableNumber = $"T{i:D2}",
                    Name = $"โต๊ะ {i}",
                    Capacity = i <= 4 ? 2 : (i <= 10 ? 4 : 8),
                    Status = TableStatus.Available
                });
            }
            Tables.AddRange(tables);
            SaveChanges();
        }

        if (!Categories.Any())
        {
            var catMain = new CategoryEntity { Name = "อาหารจานหลัก", SortOrder = 1, IsActive = true };
            var catSnack = new CategoryEntity { Name = "ของทานเล่น", SortOrder = 2, IsActive = true };
            var catDrink = new CategoryEntity { Name = "เครื่องดื่ม", SortOrder = 3, IsActive = true };
            var catDessert = new CategoryEntity { Name = "ของหวาน", SortOrder = 4, IsActive = true };

            Categories.AddRange(catMain, catSnack, catDrink, catDessert);
            SaveChanges();
        }

        if (!Products.Any())
        {
            var catMain = Categories.FirstOrDefault(c => c.Name == "อาหารจานหลัก");
            var catSnack = Categories.FirstOrDefault(c => c.Name == "ของทานเล่น");
            var catDrink = Categories.FirstOrDefault(c => c.Name == "เครื่องดื่ม");
            var catDessert = Categories.FirstOrDefault(c => c.Name == "ของหวาน");

            if (catMain != null && catSnack != null && catDrink != null && catDessert != null)
            {
                var products = new List<ProductEntity>
                {
                    new() { CategoryId = catMain.Id, Code = "M01", Name = "ผัดไทยกุ้งสด", Price = 85.00m, ImageUrl = "/uploads/menu/m01_padthai.jpg", TrackStock = false, KitchenStation = "MainKitchen" },
                    new() { CategoryId = catMain.Id, Code = "M02", Name = "ข้าวผัดกระเพราหมูกรอบ", Price = 75.00m, ImageUrl = "/uploads/menu/m02_krapao.jpg", TrackStock = false, KitchenStation = "MainKitchen" },
                    new() { CategoryId = catMain.Id, Code = "M03", Name = "ต้มยำกุ้งน้ำข้น", Price = 150.00m, ImageUrl = "/uploads/menu/m03_tomyum.jpg", TrackStock = false, KitchenStation = "MainKitchen" },
                    new() { CategoryId = catMain.Id, Code = "M04", Name = "แกงเขียวหวานไก่โรตี", Price = 95.00m, ImageUrl = "/uploads/menu/m04_greencurry.jpg", TrackStock = false, KitchenStation = "MainKitchen" },
                    
                    new() { CategoryId = catSnack.Id, Code = "S01", Name = "ปีกไก่ทอดน้ำปลา", Price = 80.00m, ImageUrl = "/uploads/menu/s01_wings.jpg", TrackStock = false, KitchenStation = "MainKitchen" },
                    new() { CategoryId = catSnack.Id, Code = "S02", Name = "เปาะเปี๊ยะทอด", Price = 60.00m, ImageUrl = "/uploads/menu/s02_springrolls.jpg", TrackStock = false, KitchenStation = "MainKitchen" },
                    new() { CategoryId = catSnack.Id, Code = "S03", Name = "เฟรนช์ฟรายส์", Price = 55.00m, ImageUrl = "/uploads/menu/s03_fries.jpg", TrackStock = false, KitchenStation = "MainKitchen" },

                    new() { CategoryId = catDrink.Id, Code = "D01", Name = "ชาไทยเย็น", Price = 45.00m, ImageUrl = "/uploads/menu/d01_thaitea.jpg", TrackStock = false, KitchenStation = "Bar" },
                    new() { CategoryId = catDrink.Id, Code = "D02", Name = "กาแฟโบราณ", Price = 45.00m, ImageUrl = "/uploads/menu/d02_thaicoffee.jpg", TrackStock = false, KitchenStation = "Bar" },
                    new() { CategoryId = catDrink.Id, Code = "D03", Name = "น้ำมะนาวโซดา", Price = 50.00m, ImageUrl = "/uploads/menu/d03_limesoda.jpg", TrackStock = false, KitchenStation = "Bar" },
                    new() { CategoryId = catDrink.Id, Code = "D04", Name = "น้ำดื่มบริสุทธิ์", Price = 15.00m, ImageUrl = "/uploads/menu/d04_water.jpg", TrackStock = false, KitchenStation = "Bar" },

                    new() { CategoryId = catDessert.Id, Code = "DS01", Name = "ข้าวเหนียวมะม่วง", Price = 89.00m, ImageUrl = "/uploads/menu/ds01_mangorice.jpg", TrackStock = false, KitchenStation = "Dessert" },
                    new() { CategoryId = catDessert.Id, Code = "DS02", Name = "บัวลอยไข่หวาน", Price = 50.00m, ImageUrl = "/uploads/menu/ds02_bualoy.jpg", TrackStock = false, KitchenStation = "Dessert" }
                };

                Products.AddRange(products);
                SaveChanges();

                // Add options to Tea
                var tea = products.First(p => p.Code == "D01");
                var sweetGroup = new ProductOptionGroupEntity
                {
                    ProductId = tea.Id,
                    Name = "ระดับความหวาน",
                    IsRequired = true,
                    AllowMultiple = false,
                    Options = new List<ProductOptionItemEntity>
                    {
                        new() { Name = "หวาน 100% (ปกติ)", ExtraPrice = 0 },
                        new() { Name = "หวานน้อย 50%", ExtraPrice = 0 },
                        new() { Name = "ไม่หวาน 0%", ExtraPrice = 0 }
                    }
                };
                ProductOptionGroups.Add(sweetGroup);
                SaveChanges();
            }
        }

        // Ensure default images are assigned to existing products
        var defaultImages = new Dictionary<string, string>
        {
            { "M01", "/uploads/menu/m01_padthai.jpg" },
            { "M02", "/uploads/menu/m02_krapao.jpg" },
            { "M03", "/uploads/menu/m03_tomyum.jpg" },
            { "M04", "/uploads/menu/m04_greencurry.jpg" },
            { "S01", "/uploads/menu/s01_wings.jpg" },
            { "S02", "/uploads/menu/s02_springrolls.jpg" },
            { "S03", "/uploads/menu/s03_fries.jpg" },
            { "D01", "/uploads/menu/d01_thaitea.jpg" },
            { "D02", "/uploads/menu/d02_thaicoffee.jpg" },
            { "D03", "/uploads/menu/d03_limesoda.jpg" },
            { "D04", "/uploads/menu/d04_water.jpg" },
            { "DS01", "/uploads/menu/ds01_mangorice.jpg" },
            { "DS02", "/uploads/menu/ds02_bualoy.jpg" }
        };

        bool updatedAnyImage = false;
        foreach (var p in Products)
        {
            if (string.IsNullOrEmpty(p.ImageUrl) && defaultImages.TryGetValue(p.Code, out var img))
            {
                p.ImageUrl = img;
                updatedAnyImage = true;
            }
        }
        if (updatedAnyImage)
        {
            SaveChanges();
        }

        // Seed Raw Materials (Ingredients / สต๊อกวัตถุดิบ)
        if (!Ingredients.Any())
        {
            var ingredients = new List<IngredientEntity>
            {
                new() { Code = "ING001", Name = "กุ้งขาวสดแกะเปลือก", Category = "เนื้อสัตว์และอาหารทะเล", Quantity = 12.5m, Unit = "กก.", MinQuantityAlert = 5m, CostPrice = 240.00m, LastRestockedAt = DateTime.UtcNow },
                new() { Code = "ING002", Name = "หมูกรอบปรุงสุก", Category = "เนื้อสัตว์และอาหารทะเล", Quantity = 8.0m, Unit = "กก.", MinQuantityAlert = 3m, CostPrice = 280.00m, LastRestockedAt = DateTime.UtcNow },
                new() { Code = "ING003", Name = "สันในไก่สด", Category = "เนื้อสัตว์และอาหารทะเล", Quantity = 15.0m, Unit = "กก.", MinQuantityAlert = 5m, CostPrice = 95.00m, LastRestockedAt = DateTime.UtcNow },
                new() { Code = "ING004", Name = "ปีกไก่กลาง", Category = "เนื้อสัตว์และอาหารทะเล", Quantity = 10.0m, Unit = "กก.", MinQuantityAlert = 4m, CostPrice = 110.00m, LastRestockedAt = DateTime.UtcNow },
                new() { Code = "ING005", Name = "ไข่ไก่เบอร์ 2", Category = "ของสดและเบ็ดเตล็ด", Quantity = 150m, Unit = "ฟอง", MinQuantityAlert = 60m, CostPrice = 4.20m, LastRestockedAt = DateTime.UtcNow },
                new() { Code = "ING006", Name = "เส้นจันทน์ผัดไทย", Category = "เส้นและแป้ง", Quantity = 25.0m, Unit = "ห่อ", MinQuantityAlert = 10m, CostPrice = 28.00m, LastRestockedAt = DateTime.UtcNow },
                new() { Code = "ING007", Name = "ข้าวหอมมะลิคัดพิเศษ", Category = "ข้าวและธัญพืช", Quantity = 45.0m, Unit = "กก.", MinQuantityAlert = 20m, CostPrice = 42.00m, LastRestockedAt = DateTime.UtcNow },
                new() { Code = "ING008", Name = "ใบกะเพราป่า", Category = "ผักและสมุนไพร", Quantity = 3.5m, Unit = "กก.", MinQuantityAlert = 2m, CostPrice = 60.00m, LastRestockedAt = DateTime.UtcNow },
                new() { Code = "ING009", Name = "พริกขี้หนูสวนสด", Category = "ผักและสมุนไพร", Quantity = 2.0m, Unit = "กก.", MinQuantityAlert = 1m, CostPrice = 120.00m, LastRestockedAt = DateTime.UtcNow },
                new() { Code = "ING010", Name = "น้ำมันพืชสำหรับทอด", Category = "เครื่องปรุงและน้ำมัน", Quantity = 18.0m, Unit = "ลิตร", MinQuantityAlert = 10m, CostPrice = 52.00m, LastRestockedAt = DateTime.UtcNow },
                new() { Code = "ING011", Name = "น้ำปลาแท้เกรดพรีเมียม", Category = "เครื่องปรุงและน้ำมัน", Quantity = 12.0m, Unit = "ขวด", MinQuantityAlert = 5m, CostPrice = 38.00m, LastRestockedAt = DateTime.UtcNow },
                new() { Code = "ING012", Name = "กะทิสดอบควันเทียน", Category = "ของสดและเบ็ดเตล็ด", Quantity = 1.5m, Unit = "กก.", MinQuantityAlert = 4m, CostPrice = 75.00m, LastRestockedAt = DateTime.UtcNow },
                new() { Code = "ING013", Name = "ผงชาไทยเข้มข้น", Category = "เครื่องดื่มและบาร์", Quantity = 6.0m, Unit = "ถุง", MinQuantityAlert = 3m, CostPrice = 145.00m, LastRestockedAt = DateTime.UtcNow },
                new() { Code = "ING014", Name = "มะม่วงน้ำดอกไม้สุก", Category = "ผลไม้และของหวาน", Quantity = 0.0m, Unit = "กก.", MinQuantityAlert = 5m, CostPrice = 85.00m, LastRestockedAt = DateTime.UtcNow, Notes = "ขาดตลาดชั่วคราว รอส่งของพรุ่งนี้" },
                new() { Code = "ING015", Name = "กล่องใส่อาหารแบบรักษ์โลก", Category = "บรรจุภัณฑ์", Quantity = 200m, Unit = "ใบ", MinQuantityAlert = 50m, CostPrice = 3.50m, LastRestockedAt = DateTime.UtcNow }
            };
            Ingredients.AddRange(ingredients);
            SaveChanges();
        }

        // Set one product as example out of stock with reason for demonstration
        var mangorice = Products.FirstOrDefault(p => p.Code == "DS01");
        if (mangorice != null && string.IsNullOrEmpty(mangorice.OutOfStockReason))
        {
            mangorice.IsAvailable = false;
            mangorice.OutOfStockReason = "มะม่วงน้ำดอกไม้สุกหมดชั่วคราว";
            SaveChanges();
        }
    }
}
